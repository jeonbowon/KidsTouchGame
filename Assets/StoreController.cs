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

        // ❌ Locked면 아무 것도 못 함 (Stage에서 먼저 Unlocked 되어야 함)
        if (!unlocked && !owned)
        {
            Debug.Log($"[STORE] LOCKED: id={item.id}, unlockOnStageClear={item.unlockOnStageClear}");
            return;
        }

        // 미소유면 BUY(코인 차감) -> Owned
        if (!owned)
        {
            int price = Mathf.Max(0, item.priceCoins);
            if (price > 0 && !CosmeticSaveManager.TrySpendCoins(price))
            {
                Debug.Log($"[STORE] 코인 부족: have={CosmeticSaveManager.GetCoins()}, cost={price}, id={item.id}");
                return;
            }

            CosmeticSaveManager.GrantOwned(item.id);
            owned = true;
        }

        // 소유한 것만 장착
        if (owned)
            CosmeticSaveManager.Equip(item.category, item.id);

        RefreshAll();

        if (previewApplier != null)
            previewApplier.ApplyEquipped();
    }
}
