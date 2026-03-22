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

    [Header("Popup")]
    [SerializeField] private StoreConfirmPopup confirmPopup;

    [Header("Modal Blocker (UI Lock)")]
    [SerializeField] private GameObject modalBlocker;     // ✅ ModalBlocker 연결 (Raycast Target ON인 Image가 있어야 함)

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

        // 혹시 이미 결제 진행 중 상태로 들어온 경우(드물지만) 잠금 반영
        ApplyIapUiLock(IAPManager.IsPurchaseInProgress);
    }

    private void OnDisable()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnCoinsGranted -= OnIapCoinsGranted;
            IAPManager.Instance.OnRemoveAdsPurchased -= OnIapRemoveAdsPurchased;
        }
    }

    private void Update()
    {
        // ✅ “결제 진행중”은 시스템 창이 떠있는 동안도 유지되어야 합니다.
        // IAPManager에서 플래그를 잘 관리하면, 이 한 줄로 UI 잠금이 자동으로 따라갑니다.
        ApplyIapUiLock(IAPManager.IsPurchaseInProgress);
    }

    private void ApplyIapUiLock(bool locked)
    {
        if (modalBlocker != null && modalBlocker.activeSelf != locked)
            modalBlocker.SetActive(locked);
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
    // IAP 버튼 (현금 결제) : “결제 진행 중 재진입” 확실히 차단
    // ─────────────────────────────────────────────────────────────
    public void OnClickBuyRemoveAds()
    {
        // ✅ 결제 진행 중이면 무조건 무시
        if (IAPManager.IsPurchaseInProgress)
            return;

        if (IAPManager.Instance == null)
        {
            Debug.LogWarning("[STORE] IAPManager가 없습니다.");
            return;
        }

        if (confirmPopup != null)
        {
            confirmPopup.ShowConfirm(
                title: "구매 확인",
                message: "광고 제거 상품을 구매하시겠습니까?\n\n결제창이 떠 있는 동안에는 다른 버튼을 누를 수 없습니다.",
                confirmLabel: "구매",
                cancelLabel: "취소",
                onConfirm: () =>
                {
                    // 결제 시작 전에 먼저 잠금
                    ApplyIapUiLock(true);
                    IAPManager.Instance.BuyRemoveAds();
                },
                onCancel: () =>
                {
                    // 취소하면 잠금 해제
                    ApplyIapUiLock(false);
                }
            );
        }
        else
        {
            ApplyIapUiLock(true);
            IAPManager.Instance.BuyRemoveAds();
        }
    }

    public void OnClickBuyCoin10000()
    {
        if (IAPManager.IsPurchaseInProgress)
            return;

        if (IAPManager.Instance == null)
        {
            Debug.LogWarning("[STORE] IAPManager가 없습니다.");
            return;
        }

        if (confirmPopup != null)
        {
            confirmPopup.ShowConfirm(
                title: "구매 확인",
                message: "코인 10,000을 구매하시겠습니까?\n\n결제창이 떠 있는 동안에는 다른 버튼을 누를 수 없습니다.",
                confirmLabel: "구매",
                cancelLabel: "취소",
                onConfirm: () =>
                {
                    ApplyIapUiLock(true);
                    IAPManager.Instance.BuyCoin10000();
                },
                onCancel: () =>
                {
                    ApplyIapUiLock(false);
                }
            );
        }
        else
        {
            ApplyIapUiLock(true);
            IAPManager.Instance.BuyCoin10000();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 탭 전환 (결제중엔 전환도 막는게 안전)
    // ─────────────────────────────────────────────────────────────
    public void OnClickTabIap()
    {
        if (IAPManager.IsPurchaseInProgress)
            return;

        if (scrollViewRoot != null) scrollViewRoot.SetActive(false);
        if (iapRoot != null) iapRoot.SetActive(true);
        RefreshCoinsUI();
    }

    public void SetCategory(int catValue) => SetCategory((CosmeticCategory)catValue);

    public void SetCategory(CosmeticCategory cat)
    {
        if (IAPManager.IsPurchaseInProgress)
            return;

        EnsureShopListVisible(forceRebuild: false);

        if (category != cat)
            category = cat;

        RefreshAll();
    }

    private void EnsureShopListVisible(bool forceRebuild)
    {
        if (iapRoot != null) iapRoot.SetActive(false);
        if (scrollViewRoot != null) scrollViewRoot.SetActive(true);
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
        if (IAPManager.IsPurchaseInProgress)
            return;

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
        if (IAPManager.IsPurchaseInProgress)
            return;

        SetCategory(CosmeticCategory.ShipSkin);
    }

    public void OnClickTabWeapon()
    {
        if (IAPManager.IsPurchaseInProgress)
            return;

        SetCategory(CosmeticCategory.Weapon);
    }
}
