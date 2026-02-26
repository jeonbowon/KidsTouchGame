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

    [Header("IAP Tab (Real Money)")]
    [SerializeField] private GameObject scrollViewRoot;   // Panel_Store 안의 'Scroll View'
    [SerializeField] private GameObject iapRoot;          // Panel_IAP

    [Header("Purchase Popup (NEW)")]
    [SerializeField] private StoreConfirmPopup confirmPopup;

    [Header("Category")]
    [SerializeField] private CosmeticCategory category = CosmeticCategory.ShipSkin;

    [Header("Preview Player (Optional)")]
    [SerializeField] private PlayerCosmeticApplier previewApplier;

    private readonly List<StoreItemCard> _spawned = new List<StoreItemCard>();

    private void OnEnable()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnCoinsGranted -= OnIapCoinsGranted;
            IAPManager.Instance.OnCoinsGranted += OnIapCoinsGranted;

            IAPManager.Instance.OnRemoveAdsPurchased -= OnIapRemoveAdsPurchased;
            IAPManager.Instance.OnRemoveAdsPurchased += OnIapRemoveAdsPurchased;
        }

        if (database == null && !string.IsNullOrEmpty(dbResourcePath))
            database = Resources.Load<CosmeticDatabase>(dbResourcePath);

        EnsureShopListVisible(forceRebuild: true);
        RefreshAll();
    }

    private void OnDisable()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnCoinsGranted -= OnIapCoinsGranted;
            IAPManager.Instance.OnRemoveAdsPurchased -= OnIapRemoveAdsPurchased;
        }
    }

    private void OnIapCoinsGranted(int amount)
    {
        RefreshCoinsUI();
        if (confirmPopup != null)
            confirmPopup.ShowMessage($"코인 +{amount:N0} 지급 완료!", "구매 완료");
    }

    private void OnIapRemoveAdsPurchased()
    {
        if (confirmPopup != null)
            confirmPopup.ShowMessage("광고 제거 구매가 적용되었습니다.", "구매 완료");
    }

    // ─────────────────────────────────────────────────────────────
    // IAP 버튼 (현금 결제)  모달 확인 후 결제하도록 수정
    // ─────────────────────────────────────────────────────────────
    public void OnClickBuyRemoveAds()
    {
        if (IAPManager.Instance == null)
        {
            Debug.LogWarning("[STORE] IAPManager가 없습니다.");
            return;
        }

        if (confirmPopup != null)
        {
            confirmPopup.ShowConfirm(
                title: "구매 확인",
                message: "광고 제거 상품을 구매하시겠습니까?\n\n구매 후 즉시 광고가 제거됩니다.",
                confirmLabel: "구매",
                cancelLabel: "취소",
                onConfirm: () => IAPManager.Instance.BuyRemoveAds()
            );
        }
        else
        {
            IAPManager.Instance.BuyRemoveAds();
        }
    }

    public void OnClickBuyCoin10000()
    {
        if (IAPManager.Instance == null)
        {
            Debug.LogWarning("[STORE] IAPManager가 없습니다.");
            return;
        }

        if (confirmPopup != null)
        {
            confirmPopup.ShowConfirm(
                title: "구매 확인",
                message: "코인 10,000을 구매하시겠습니까?\n\n구매 완료 시 코인이 즉시 지급됩니다.",
                confirmLabel: "구매",
                cancelLabel: "취소",
                onConfirm: () => IAPManager.Instance.BuyCoin10000()
            );
        }
        else
        {
            IAPManager.Instance.BuyCoin10000();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 탭 전환
    // ─────────────────────────────────────────────────────────────
    public void OnClickTabIap()
    {
        if (scrollViewRoot != null) scrollViewRoot.SetActive(false);
        if (iapRoot != null) iapRoot.SetActive(true);
        RefreshCoinsUI();
    }

    public void SetCategory(int catValue) => SetCategory((CosmeticCategory)catValue);

    public void SetCategory(CosmeticCategory cat)
    {
        EnsureShopListVisible(forceRebuild: false);

        if (category != cat)
            category = cat;

        RefreshAll();
    }

    private void EnsureShopListVisible(bool forceRebuild)
    {
        if (iapRoot != null) iapRoot.SetActive(false);
        if (scrollViewRoot != null) scrollViewRoot.SetActive(true);

        if (forceRebuild || _spawned.Count == 0)
        {
            // RefreshAll에서 BuildList가 다시 호출됩니다.
        }
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
        if (scrollViewRoot != null && !scrollViewRoot.activeInHierarchy)
            return;

        if (database == null || contentRoot == null || cardPrefab == null) return;

        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        _spawned.Clear();

        List<CosmeticItem> list = database.GetByCategory(category);
        if (list == null) return;

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

        if (!unlocked && !owned)
        {
            if (confirmPopup != null)
                confirmPopup.ShowMessage("아직 잠겨있습니다.\n스테이지를 더 진행해 주세요.", "LOCKED");
            return;
        }

        if (owned)
        {
            CosmeticSaveManager.Equip(item.category, item.id);
            AfterAnyChange();
            return;
        }

        int price = Mathf.Max(0, item.priceCoins);
        int have = CosmeticSaveManager.GetCoins();
        string itemName = string.IsNullOrWhiteSpace(item.displayName) ? item.id : item.displayName;

        if (confirmPopup == null)
        {
            Debug.LogWarning("[STORE] confirmPopup이 연결되지 않았습니다.");
            return;
        }

        confirmPopup.ShowPurchaseConfirm(itemName, price, have, () =>
        {
            if (price > 0 && !CosmeticSaveManager.TrySpendCoins(price))
            {
                confirmPopup.ShowMessage("코인이 부족합니다.", "구매 실패");
                return;
            }

            CosmeticSaveManager.GrantOwned(item.id);
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

    public void OnClickTabShip()
    {
        Debug.Log("[STORE] TabShip Click");
        SetCategory(CosmeticCategory.ShipSkin);
    }

    public void OnClickTabWeapon()
    {
        Debug.Log("[STORE] TabWeapon Click");
        SetCategory(CosmeticCategory.Weapon);
    }
}
