using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// MonetizationManager
/// - 광고/리워드/전면광고/광고제거(IAP) 상태를 한곳에서 처리합니다.
/// - GameManager는 "요청(try)"만 하고, 실제 AdManager/IAPManager 접근은 여기서만 합니다.
/// </summary>
public class MonetizationManager : MonoBehaviour
{
    public static MonetizationManager I { get; private set; }

    [Header("Auto Create")]
    [SerializeField] private bool autoCreateAdManagerIfMissing = true;
    [SerializeField] private bool autoCreateIapManagerIfMissing = true;

    private bool _subscribedIapEvents;
    private bool _cachedNoAds;

    public bool IsAdsDisabled => _cachedNoAds;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        RefreshNoAdsCache();

        EnsureSubsystems();
        TrySubscribeIapEvents();
    }

    private void Start()
    {
        // IAPManager는 Start에서 InitializePurchasing을 하므로, 한 프레임 뒤에도 한 번 더 구독 시도
        StartCoroutine(Co_LateSubscribe());
    }

    private IEnumerator Co_LateSubscribe()
    {
        yield return null;
        EnsureSubsystems();
        TrySubscribeIapEvents();
        RefreshNoAdsCache();
    }

    private void RefreshNoAdsCache()
    {
        _cachedNoAds = IAPManager.HasNoAds();
    }

    public void EnsureSubsystems()
    {
        if (autoCreateIapManagerIfMissing)
            EnsureIapManager();

        if (autoCreateAdManagerIfMissing)
            EnsureAdManager();

        // NoAds면 AdManager가 있어도 즉시 disable 적용(안전)
        if (IsAdsDisabled)
            DisableAds();
    }

    private void EnsureAdManager()
    {
        if (AdManager.I != null) return;

        var found = FindObjectOfType<AdManager>(true);
        if (found != null) return;

        var go = new GameObject("AdManager (Auto)");
        go.AddComponent<AdManager>();
        DontDestroyOnLoad(go);
    }

    private void EnsureIapManager()
    {
        if (IAPManager.Instance != null) return;

        var found = FindObjectOfType<IAPManager>(true);
        if (found != null) return;

        var go = new GameObject("IAPManager (Auto)");
        go.AddComponent<IAPManager>();
        DontDestroyOnLoad(go);
    }

    private void TrySubscribeIapEvents()
    {
        if (_subscribedIapEvents) return;
        if (IAPManager.Instance == null) return;

        IAPManager.Instance.OnRemoveAdsPurchased -= HandleRemoveAdsPurchased;
        IAPManager.Instance.OnRemoveAdsPurchased += HandleRemoveAdsPurchased;

        _subscribedIapEvents = true;
    }

    private void HandleRemoveAdsPurchased()
    {
        RefreshNoAdsCache();
        DisableAds();
    }

    public void DisableAds()
    {
        RefreshNoAdsCache();

        if (!IsAdsDisabled) return;

        if (AdManager.I != null)
            AdManager.I.DisableAllAds();
    }

    /// <summary>
    /// 스테이지 진입 시 광고 미리 로드(가능할 때만)
    /// </summary>
    public void PreloadStageAds()
    {
        EnsureSubsystems();
        RefreshNoAdsCache();

        if (IsAdsDisabled) return;
        if (AdManager.I == null) return;

        AdManager.I.RequestRewardedReload();
        AdManager.I.RequestInterstitialReload();
    }

    /// <summary>
    /// Rewarded를 "대기 + 표시"까지 수행하는 코루틴.
    /// onStatus: "LOADING... xx.xs", "SHOWING..." 같은 문구를 GameOverPanel 등에 표시할 때 사용.
    /// </summary>
    public IEnumerator Co_ShowRewardedWithWait(
        float waitTimeout,
        float tick,
        Action<string> onStatus,
        Action<bool> onDone,
        string placement = "Rewarded")
    {
        EnsureSubsystems();
        RefreshNoAdsCache();

        if (IsAdsDisabled)
        {
            onDone?.Invoke(true); // 광고제거면 성공으로 간주(게임 흐름 끊지 않음)
            yield break;
        }

        if (AdManager.I == null)
        {
            onStatus?.Invoke("AD NOT AVAILABLE.\nTry again.");
            onDone?.Invoke(false);
            yield break;
        }

        onStatus?.Invoke("LOADING AD...\n(Rewarded)");

        AdManager.I.RequestRewardedReload();

        float t = 0f;
        float nextTick = 0f;

        while (!AdManager.I.IsRewardedReady && t < waitTimeout)
        {
            if (!AdManager.I.IsRewardedLoading && AdManager.I.RewardedLoadFailed)
                break;

            t += Time.unscaledDeltaTime;

            if (t >= nextTick)
            {
                float remain = Mathf.Max(0f, waitTimeout - t);
                onStatus?.Invoke($"LOADING AD...\n{remain:0.0}s");
                nextTick = t + Mathf.Max(0.05f, tick);
            }

            yield return null;
        }

        if (!AdManager.I.IsRewardedReady)
        {
            string err = AdManager.I.LastRewardedLoadError;
            Debug.LogWarning($"[Monetization] Rewarded NOT READY / err={err}");

            onStatus?.Invoke("AD NOT READY.\nPlease try again.");
            onDone?.Invoke(false);
            yield break;
        }

        onStatus?.Invoke("SHOWING AD...\n(Rewarded)");

        bool done = false;
        bool success = false;

        AdManager.I.ShowRewarded(ok =>
        {
            success = ok;
            done = true;
        }, placement);

        while (!done) yield return null;

        onDone?.Invoke(success);
    }

    public IEnumerator Co_ShowInterstitialWithWait(
        float waitTimeout,
        float tick,
        Action<string> onStatus,
        Action<bool> onDone,
        string placement = "Interstitial")
    {
        EnsureSubsystems();
        RefreshNoAdsCache();

        if (IsAdsDisabled)
        {
            onDone?.Invoke(true);
            yield break;
        }

        if (AdManager.I == null)
        {
            onDone?.Invoke(false);
            yield break;
        }

        onStatus?.Invoke("LOADING AD...\n(Interstitial)");

        AdManager.I.RequestInterstitialReload();

        float t = 0f;
        float nextTick = 0f;

        while (!AdManager.I.IsInterstitialReady && t < waitTimeout)
        {
            if (!AdManager.I.IsInterstitialLoading && AdManager.I.InterstitialLoadFailed)
                break;

            t += Time.unscaledDeltaTime;

            if (t >= nextTick)
            {
                float remain = Mathf.Max(0f, waitTimeout - t);
                onStatus?.Invoke($"LOADING AD...\n{remain:0.0}s");
                nextTick = t + Mathf.Max(0.05f, tick);
            }

            yield return null;
        }

        if (!AdManager.I.IsInterstitialReady)
        {
            Debug.LogWarning("[Monetization] Interstitial NOT READY -> skip");
            onDone?.Invoke(false);
            yield break;
        }

        onStatus?.Invoke("SHOWING AD...\n(Interstitial)");

        bool done = false;

        AdManager.I.ShowInterstitial(ok => { done = true; }, placement);
        while (!done) yield return null;

        onDone?.Invoke(true);
    }
}
