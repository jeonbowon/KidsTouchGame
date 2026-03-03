using System;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPManager : MonoBehaviour, IStoreListener
{
    // Play Console 제품 ID(SKU)와 100% 동일해야 합니다.
    public const string PRODUCT_REMOVE_ADS = "remove_ads";   // Non-Consumable
    public const string PRODUCT_COIN_10000 = "coin_10000";   // Consumable

    private const int COIN_PACK_10000 = 10000;

    // PlayerPrefs Key (광고제거만 저장)
    private const string PREF_NO_ADS = "NO_ADS";

    public static IAPManager Instance { get; private set; }

    private static IStoreController _storeController;
    private static IExtensionProvider _storeExtensionProvider;

    public static bool IsInitialized => _storeController != null && _storeExtensionProvider != null;

    // 결제 플로우 진행중(결제창 떠있거나 처리중)
    public static bool IsPurchaseInProgress { get; private set; }

    public event Action OnRemoveAdsPurchased;
    public event Action<int> OnCoinsGranted;

    // UI 잠금/해제에 쓰는 이벤트
    public event Action<string> OnPurchaseFlowStarted;         // productId
    public event Action<string, bool> OnPurchaseFlowFinished;  // productId, success

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializePurchasing();
    }

    public void InitializePurchasing()
    {
        if (IsInitialized) return;

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(PRODUCT_REMOVE_ADS, ProductType.NonConsumable);
        builder.AddProduct(PRODUCT_COIN_10000, ProductType.Consumable);

        UnityPurchasing.Initialize(this, builder);
    }

    public void BuyRemoveAds() => BuyProductID(PRODUCT_REMOVE_ADS);
    public void BuyCoin10000() => BuyProductID(PRODUCT_COIN_10000);

    private void BuyProductID(string productId)
    {
        // 결제중이면 추가 구매 시도 무시
        if (IsPurchaseInProgress)
        {
            Debug.LogWarning($"[IAP] Purchase already in progress. Ignore: {productId}");
            return;
        }

        if (!IsInitialized)
        {
            Debug.LogWarning($"[IAP] Not initialized yet. productId={productId}");
            InitializePurchasing();
            return;
        }

        Product product = _storeController.products.WithID(productId);
        if (product == null)
        {
            Debug.LogError($"[IAP] Product not found: {productId}");
            return;
        }

        if (!product.availableToPurchase)
        {
            Debug.LogWarning($"[IAP] Product not available: {productId}");
            return;
        }

        // 결제 플로우 시작(이 순간부터 UI 잠가야 함)
        IsPurchaseInProgress = true;
        OnPurchaseFlowStarted?.Invoke(productId);

        _storeController.InitiatePurchase(product);
    }

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _storeController = controller;
        _storeExtensionProvider = extensions;

        Debug.Log("[IAP] Initialized OK");

        if (HasNoAds())
        {
            ApplyNoAdsToGame();
            OnRemoveAdsPurchased?.Invoke();
        }
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogError($"[IAP] Initialize failed: {error}");
    }

#if UNITY_2020_2_OR_NEWER
    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogError($"[IAP] Initialize failed: {error}, {message}");
    }
#endif

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
    {
        string id = e.purchasedProduct.definition.id;
        Debug.Log($"[IAP] Purchase Success: {id}");

        // 결제 플로우 종료(성공)
        IsPurchaseInProgress = false;
        OnPurchaseFlowFinished?.Invoke(id, true);

        if (id == PRODUCT_REMOVE_ADS)
        {
            SetNoAds(true);
            ApplyNoAdsToGame();
            OnRemoveAdsPurchased?.Invoke();
            return PurchaseProcessingResult.Complete;
        }

        if (id == PRODUCT_COIN_10000)
        {
            if (EconomyManager.I != null) EconomyManager.I.AddCoins(COIN_PACK_10000);
            else CosmeticSaveManager.AddCoins(COIN_PACK_10000);
            OnCoinsGranted?.Invoke(COIN_PACK_10000);
            return PurchaseProcessingResult.Complete;
        }

        Debug.LogWarning($"[IAP] Unknown product id: {id}");
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning($"[IAP] Purchase Failed: {product.definition.id}, reason={failureReason}");

        // 결제 플로우 종료(실패/취소 포함)
        IsPurchaseInProgress = false;
        OnPurchaseFlowFinished?.Invoke(product.definition.id, false);
    }

    private static void ApplyNoAdsToGame()
    {
        if (MonetizationManager.I != null)
        {
            MonetizationManager.I.DisableAds();
            return;
        }

        if (AdManager.I != null)
            AdManager.I.DisableAllAds();
    }

    public static bool HasNoAds()
    {
        return PlayerPrefs.GetInt(PREF_NO_ADS, 0) == 1;
    }

    private static void SetNoAds(bool value)
    {
        PlayerPrefs.SetInt(PREF_NO_ADS, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}