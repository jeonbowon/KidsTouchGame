using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StoreController : MonoBehaviour
{
    [Header("DB")]
    [SerializeField] private CosmeticDatabase database;
    [SerializeField] private string dbResourcePath = "Cosmetics/CosmeticDatabase";

    [Header("UI")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private Transform contentRoot;       // ScrollView Content
    [SerializeField] private StoreItemCard cardPrefab;

    [Header("Purchase Popup (NEW)")]
    [SerializeField] private StoreConfirmPopup confirmPopup;

    [Header("Category")]
    [SerializeField] private CosmeticCategory category = CosmeticCategory.ShipSkin;

    [Header("Preview Player (Optional)")]
    [SerializeField] private PlayerCosmeticApplier previewApplier;

    private readonly List<StoreItemCard> _spawned = new List<StoreItemCard>();

    private void OnEnable()
    {
        if (database == null && !string.IsNullOrEmpty(dbResourcePath))
            database = Resources.Load<CosmeticDatabase>(dbResourcePath);

        RefreshAll();
    }

    public void SetCategory(int catValue)
    {
        SetCategory((CosmeticCategory)catValue);
    }

    public void SetCategory(CosmeticCategory cat)
    {
        if (category == cat) return;
        category = cat;
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshCoinsUI();
        BuildList();
    }

    private void RefreshCoinsUI()
    {
        if (coinsText != null)
            coinsText.text = $"COINS: {CosmeticSaveManager.GetCoins()}";
    }

    private bool IsUnlockedNow(CosmeticItem item)
    {
        if (item == null) return false;
        if (item.unlockOnStageClear <= 0) return true;
        return CosmeticSaveManager.IsUnlocked(item.id) || CosmeticSaveManager.IsOwned(item.id);
    }

    private void BuildList()
    {
        if (database == null || contentRoot == null || cardPrefab == null) return;

        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        _spawned.Clear();

        List<CosmeticItem> list = database.GetByCategory(category);
        if (list == null) return;

        // 정렬: 장착 -> 소유 -> Unlocked -> Locked
        list.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            bool aEq = CosmeticSaveManager.GetEquipped(a.category) == a.id;
            bool bEq = CosmeticSaveManager.GetEquipped(b.category) == b.id;
            if (aEq != bEq) return aEq ? -1 : 1;

            bool aOw = CosmeticSaveManager.IsOwned(a.id);
            bool bOw = CosmeticSaveManager.IsOwned(b.id);
            if (aOw != bOw) return aOw ? -1 : 1;

            bool aUn = IsUnlockedNow(a);
            bool bUn = IsUnlockedNow(b);
            if (aUn != bUn) return aUn ? -1 : 1;

            int price = a.priceCoins.CompareTo(b.priceCoins);
            if (price != 0) return price;

            return string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal);
        });

        foreach (var item in list)
        {
            if (item == null) continue;

            var card = Instantiate(cardPrefab, contentRoot);
            card.Configure(item, OnClickItem);
            _spawned.Add(card);
        }
    }

    private void OnClickItem(CosmeticItem item)
    {
        if (item == null) return;

        bool unlocked = IsUnlockedNow(item);
        bool owned = CosmeticSaveManager.IsOwned(item.id);

        // Locked면 아무것도 못 함
        if (!unlocked && !owned)
        {
            Debug.Log($"[STORE] LOCKED: id={item.id}, unlockOnStageClear={item.unlockOnStageClear}");
            if (confirmPopup != null)
                confirmPopup.ShowMessage("아직 잠겨있습니다.\n스테이지를 더 진행해 주세요.", "LOCKED");
            return;
        }

        // 이미 소유면 -> 장착만
        if (owned)
        {
            CosmeticSaveManager.Equip(item.category, item.id);
            AfterAnyChange();
            return;
        }

        // 여기부터 “구매 확인 팝업” 후에만 구매 확정
        int price = Mathf.Max(0, item.priceCoins);
        int have = CosmeticSaveManager.GetCoins();
        string itemName = string.IsNullOrWhiteSpace(item.displayName) ? item.id : item.displayName;

        // 팝업이 없으면(실수로 연결 안했으면) 최소한 안전하게 구매를 막는다
        if (confirmPopup == null)
        {
            Debug.LogWarning("[STORE] confirmPopup이 연결되지 않았습니다. 구매 확인 팝업이 없어서 구매를 진행하지 않습니다.");
            return;
        }

        confirmPopup.ShowPurchaseConfirm(itemName, price, have, () =>
        {
            // 코인 부족 체크
            if (price > 0 && !CosmeticSaveManager.TrySpendCoins(price))
            {
                Debug.Log($"[STORE] 코인 부족: have={CosmeticSaveManager.GetCoins()}, cost={price}, id={item.id}");
                confirmPopup.ShowMessage("코인이 부족합니다.", "구매 실패");
                return;
            }

            // 구매 확정
            CosmeticSaveManager.GrantOwned(item.id);

            // 구매 후 바로 장착(원래 로직 유지)
            CosmeticSaveManager.Equip(item.category, item.id);

            AfterAnyChange();
        });
    }

    private void AfterAnyChange()
    {
        RefreshAll();

        if (previewApplier != null)
            previewApplier.ApplyEquipped();
    }
}
