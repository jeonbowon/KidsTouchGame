using System;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPManager : MonoBehaviour, IStoreListener
{
    // Play Console "제품 ID" (SKU) 와 100% 동일해야 합니다.
    public const string PRODUCT_REMOVE_ADS = "remove_ads";   // Non-Consumable
    public const string PRODUCT_COIN_10000 = "coin_10000";   // Consumable

    // 보상 값
    private const int COIN_PACK_10000 = 10000;

    // PlayerPrefs 키
    private const string PREF_NO_ADS = "NO_ADS";
    private const string PREF_COINS = "COINS";

    public static IAPManager Instance { get; private set; }

    private static IStoreController _storeController;
    private static IExtensionProvider _storeExtensionProvider;

    public static bool IsInitialized => _storeController != null && _storeExtensionProvider != null;

    // 외부(AdManager/UI)가 구독할 수 있게 이벤트 제공
    public event Action OnRemoveAdsPurchased;
    public event Action<int> OnCoinsGranted;

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

    // -----------------------------
    // 구매 버튼에서 호출할 함수들
    // -----------------------------
    public void BuyRemoveAds()
    {
        BuyProductID(PRODUCT_REMOVE_ADS);
    }

    public void BuyCoin10000()
    {
        BuyProductID(PRODUCT_COIN_10000);
    }

    private void BuyProductID(string productId)
    {
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
            Debug.LogWarning($"[IAP] Product not available to purchase: {productId}");
            return;
        }

        _storeController.InitiatePurchase(product);
    }

    // -----------------------------
    // IStoreListener
    // -----------------------------
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _storeController = controller;
        _storeExtensionProvider = extensions;

        Debug.Log("[IAP] Initialized OK");

        // 이미 광고제거 구매했으면 게임 시작 시점에 적용될 수 있게 이벤트 한번 쏴줌(옵션)
        if (HasNoAds())
            OnRemoveAdsPurchased?.Invoke();
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

        if (id == PRODUCT_REMOVE_ADS)
        {
            SetNoAds(true);
            OnRemoveAdsPurchased?.Invoke();
            return PurchaseProcessingResult.Complete;
        }

        if (id == PRODUCT_COIN_10000)
        {
            AddCoins(COIN_PACK_10000);
            OnCoinsGranted?.Invoke(COIN_PACK_10000);
            return PurchaseProcessingResult.Complete;
        }

        Debug.LogWarning($"[IAP] Unknown product id: {id}");
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning($"[IAP] Purchase Failed: {product.definition.id}, reason={failureReason}");
    }

    // -----------------------------
    // 보상 적용(기본 PlayerPrefs 버전)
    // 대표님 기존 코인/광고 구조가 있으면 여기만 연결하면 됩니다.
    // -----------------------------
    public static bool HasNoAds()
    {
        return PlayerPrefs.GetInt(PREF_NO_ADS, 0) == 1;
    }

    private static void SetNoAds(bool value)
    {
        PlayerPrefs.SetInt(PREF_NO_ADS, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static int GetCoins()
    {
        return PlayerPrefs.GetInt(PREF_COINS, 0);
    }

    private static void AddCoins(int amount)
    {
        int now = GetCoins();
        now += amount;
        PlayerPrefs.SetInt(PREF_COINS, now);
        PlayerPrefs.Save();
    }
}