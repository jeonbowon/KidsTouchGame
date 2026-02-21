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

        // 시작은 항상 코인 상점 화면
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
    // IAP 버튼 (현금 결제)
    // ─────────────────────────────────────────────────────────────
    public void OnClickBuyRemoveAds()
    {
        if (IAPManager.Instance == null)
        {
            Debug.LogWarning("[STORE] IAPManager가 없습니다.");
            return;
        }
        IAPManager.Instance.BuyRemoveAds();
    }

    public void OnClickBuyCoin10000()
    {
        if (IAPManager.Instance == null)
        {
            Debug.LogWarning("[STORE] IAPManager가 없습니다.");
            return;
        }
        IAPManager.Instance.BuyCoin10000();
    }

    // ─────────────────────────────────────────────────────────────
    // 탭 전환
    // ─────────────────────────────────────────────────────────────
    public void OnClickTabIap()
    {
        // 코인 리스트 숨김, IAP 패널 표시
        if (scrollViewRoot != null) scrollViewRoot.SetActive(false);
        if (iapRoot != null) iapRoot.SetActive(true);

        RefreshCoinsUI();
    }

    // 핵심: IAP에서 돌아올 때 "카테고리가 같아도" 리스트를 반드시 보여줘야 한다.
    public void SetCategory(int catValue) => SetCategory((CosmeticCategory)catValue);

    public void SetCategory(CosmeticCategory cat)
    {
        // 무조건 Shop 리스트 화면으로 복귀
        EnsureShopListVisible(forceRebuild: false);

        // ✅ 여기 수정이 핵심: 카테고리가 같아도 return 하지 말고 RefreshAll을 수행
        if (category != cat)
            category = cat;

        RefreshAll();
    }

    private void EnsureShopListVisible(bool forceRebuild)
    {
        if (iapRoot != null) iapRoot.SetActive(false);
        if (scrollViewRoot != null) scrollViewRoot.SetActive(true);

        // IAP 탭에서 돌아왔는데 리스트가 비어있는 경우가 실제로 자주 생깁니다.
        // (scrollView를 껐다 켜는 타이밍/초기화 타이밍 때문에)
        if (forceRebuild || _spawned.Count == 0)
        {
            // 여기서 바로 리스트를 만들도록 유도
            // (RefreshAll에서 다시 BuildList가 호출되므로 중복 생성 방지는 BuildList 내부에서 처리됨)
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
        // Shop 화면(Scroll View)이 꺼져있으면 리스트 만들지 않음
        if (scrollViewRoot != null && !scrollViewRoot.activeInHierarchy)
            return;

        if (database == null || contentRoot == null || cardPrefab == null) return;

        // 기존 카드 제거
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
