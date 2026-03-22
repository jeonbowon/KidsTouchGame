using System;
using System.Collections;
using UnityEngine;

#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
using System.Collections.Generic;
#endif

public class AdManager : MonoBehaviour
{
    public static AdManager I { get; private set; }

    private bool _isEditor;
    private bool _isDevBuild;
    private bool _isTestContext;

    // ✅ 광고 제거 구매 시 true
    private bool _adsDisabled;

#if GOOGLE_MOBILE_ADS
    private volatile bool _pendingInitComplete;
#endif

    [Header("AdMob IDs (Android)")]
    [SerializeField] private string androidAppId = "";
    [SerializeField] private string rewardedUnitIdAndroid = "";
    [SerializeField] private string interstitialUnitIdAndroid = "";

    [Header("Simulate (when no SDK or in editor)")]
    [SerializeField] private bool simulateInEditor = true;
    [SerializeField] private bool simulateInDeviceIfNoSdk = false;
    [SerializeField] private float simulateDelay = 1.0f;

    [Header("Dev/Test Safety (Recommended)")]
    [SerializeField] private bool forceGoogleTestAdUnitsInDevBuild = true;

    private const string TEST_REWARDED_ANDROID = "ca-app-pub-3940256099942544/5224354917";
    private const string TEST_INTERSTITIAL_ANDROID = "ca-app-pub-3940256099942544/1033173712";

#if GOOGLE_MOBILE_ADS
    [Header("Test Devices (Editor/DevBuild ONLY)")]
    [SerializeField] private string[] testDeviceIds = new string[0];

    private RewardedAd _rewarded;
    private InterstitialAd _interstitial;

    private bool _rewardedReady;
    private bool _interstitialReady;

    private bool _rewardedLoading;
    private bool _interstitialLoading;

    private bool _rewardedLoadingPublic;
    private bool _interstitialLoadingPublic;

    private bool _rewardedLoadFailed;
    private bool _interstitialLoadFailed;

    private string _lastRewardedLoadError = "";
    private string _lastInterstitialLoadError = "";

    private Action<bool> _pendingRewardedDone;
    private bool _pendingRewardedOpened;
    private bool _pendingRewardedEarned;
    private bool _pendingRewardedClosed;

    private Action<bool> _pendingInterstitialDone;
    private bool _pendingInterstitialOpened;
    private bool _pendingInterstitialClosed;
#endif

    public bool IsRewardedReady => _rewardedReady;
    public bool IsInterstitialReady => _interstitialReady;

    public bool IsRewardedLoading => _rewardedLoadingPublic;
    public bool RewardedLoadFailed => _rewardedLoadFailed;
    public string LastRewardedLoadError => _lastRewardedLoadError;

    public bool IsInterstitialLoading => _interstitialLoadingPublic;
    public bool InterstitialLoadFailed => _interstitialLoadFailed;
    public string LastInterstitialLoadError => _lastInterstitialLoadError;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        _isEditor = Application.isEditor;
        _isDevBuild = Debug.isDebugBuild;
        _isTestContext = (_isEditor || _isDevBuild);

        // ✅ IAP 광고 제거가 이미 저장되어 있으면 즉시 반영
        _adsDisabled = IAPManager.HasNoAds();

        if (!_adsDisabled)
            Init();
    }

    public void DisableAllAds()
    {
        if (_adsDisabled) return;

        _adsDisabled = true;

#if GOOGLE_MOBILE_ADS
        try { _rewarded?.Destroy(); } catch { }
        try { _interstitial?.Destroy(); } catch { }
        _rewarded = null;
        _interstitial = null;

        _rewardedReady = false;
        _interstitialReady = false;

        _rewardedLoading = false;
        _interstitialLoading = false;

        _rewardedLoadingPublic = false;
        _interstitialLoadingPublic = false;

        _rewardedLoadFailed = false;
        _interstitialLoadFailed = false;

        _lastRewardedLoadError = "DisabledByPurchase";
        _lastInterstitialLoadError = "DisabledByPurchase";
#endif
    }

    private void Update()
    {
#if GOOGLE_MOBILE_ADS
        if (_adsDisabled) return;

        if (_pendingInitComplete)
        {
            _pendingInitComplete = false;
            LoadRewarded();
            LoadInterstitial();
        }
#endif
    }

    private bool IsAdMobSdkPresent()
    {
#if GOOGLE_MOBILE_ADS
        return true;
#else
        return false;
#endif
    }

    public void Init()
    {
        if (_adsDisabled) return;

#if GOOGLE_MOBILE_ADS
        ConfigureTestDevices_EditorOrDevBuildOnly();

        MobileAds.Initialize(_ =>
        {
            _pendingInitComplete = true;
        });
#else
        Debug.LogWarning("[ADS] AdMob SDK가 없습니다. 시뮬레이션/실패만 가능합니다.");
        _rewardedReady = false;
        _interstitialReady = false;

        _rewardedLoadingPublic = false;
        _rewardedLoadFailed = true;
        _lastRewardedLoadError = "No SDK";

        _interstitialLoadingPublic = false;
        _interstitialLoadFailed = true;
        _lastInterstitialLoadError = "No SDK";
#endif
    }

    private void ConfigureTestDevices_EditorOrDevBuildOnly()
    {
#if GOOGLE_MOBILE_ADS
        if (!_isEditor && !_isDevBuild) return;

        if (testDeviceIds == null || testDeviceIds.Length == 0) return;

        var list = new List<string>();
        foreach (var id in testDeviceIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
                list.Add(id.Trim());
        }

        if (list.Count == 0) return;

        var config = new RequestConfiguration
        {
            TestDeviceIds = list
        };

        MobileAds.SetRequestConfiguration(config);
#endif
    }

    public void RequestRewardedReload()
    {
        if (_adsDisabled) return;
        LoadRewarded();
    }

    public void RequestInterstitialReload()
    {
        if (_adsDisabled) return;
        LoadInterstitial();
    }

    public void LoadRewarded()
    {
        if (_adsDisabled) return;

#if GOOGLE_MOBILE_ADS
        _rewardedReady = false;
        _rewardedLoading = true;

        _rewardedLoadingPublic = true;
        _rewardedLoadFailed = false;
        _lastRewardedLoadError = "";

        string unitId = GetRewardedUnitId();
        if (string.IsNullOrEmpty(unitId))
        {
            _rewardedLoading = false;
            _rewardedLoadingPublic = false;
            _rewardedLoadFailed = true;
            _lastRewardedLoadError = "Empty Rewarded UnitId";
            return;
        }

        var request = new AdRequest();
        RewardedAd.Load(unitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            _rewardedLoading = false;
            _rewardedLoadingPublic = false;

            if (_adsDisabled) { try { ad?.Destroy(); } catch { } return; }

            if (error != null || ad == null)
            {
                _rewardedReady = false;
                _rewardedLoadFailed = true;
                _lastRewardedLoadError = (error != null) ? error.ToString() : "ad==null";
                return;
            }

            try { _rewarded?.Destroy(); } catch { }

            _rewarded = ad;
            _rewardedReady = true;

            _rewardedLoadFailed = false;
            _lastRewardedLoadError = "";

            _rewarded.OnAdFullScreenContentOpened += () => { _pendingRewardedOpened = true; };
            _rewarded.OnAdFullScreenContentClosed += () =>
            {
                _pendingRewardedClosed = true;

                if (_pendingRewardedDone != null)
                {
                    bool ok = _pendingRewardedOpened && _pendingRewardedEarned && _pendingRewardedClosed;
                    _pendingRewardedDone.Invoke(ok);

                    _pendingRewardedDone = null;
                    _pendingRewardedOpened = false;
                    _pendingRewardedEarned = false;
                    _pendingRewardedClosed = false;
                }

                _rewardedReady = false;
                try { _rewarded?.Destroy(); } catch { }
                _rewarded = null;

                LoadRewarded();
            };
        });
#endif
    }

    public void ShowRewarded(Action<bool> onDone, string reason = "Rewarded")
    {
        if (_adsDisabled)
        {
            onDone?.Invoke(true);
            return;
        }

#if UNITY_EDITOR
        if (simulateInEditor || !IsAdMobSdkPresent())
        {
            StartCoroutine(Co_Simulate(onDone, true, reason));
            return;
        }
#endif

#if GOOGLE_MOBILE_ADS
        if (_rewarded == null || !_rewardedReady)
        {
            onDone?.Invoke(false);
            RequestRewardedReload();
            return;
        }

        _pendingRewardedDone = onDone;
        _pendingRewardedOpened = false;
        _pendingRewardedEarned = false;
        _pendingRewardedClosed = false;

        try
        {
            _rewarded.Show((Reward reward) => { _pendingRewardedEarned = true; });
        }
        catch
        {
            _pendingRewardedDone?.Invoke(false);
            _pendingRewardedDone = null;
            _rewardedReady = false;
            try { _rewarded?.Destroy(); } catch { }
            _rewarded = null;
            LoadRewarded();
        }
#else
        if (simulateInDeviceIfNoSdk) StartCoroutine(Co_Simulate(onDone, true, reason));
        else onDone?.Invoke(false);
#endif
    }

    public void LoadInterstitial()
    {
        if (_adsDisabled) return;

#if GOOGLE_MOBILE_ADS
        _interstitialReady = false;
        _interstitialLoading = true;

        _interstitialLoadingPublic = true;
        _interstitialLoadFailed = false;
        _lastInterstitialLoadError = "";

        string unitId = GetInterstitialUnitId();
        if (string.IsNullOrEmpty(unitId))
        {
            _interstitialLoading = false;
            _interstitialLoadingPublic = false;
            _interstitialLoadFailed = true;
            _lastInterstitialLoadError = "Empty Interstitial UnitId";
            return;
        }

        var request = new AdRequest();
        InterstitialAd.Load(unitId, request, (InterstitialAd ad, LoadAdError error) =>
        {
            _interstitialLoading = false;
            _interstitialLoadingPublic = false;

            if (_adsDisabled) { try { ad?.Destroy(); } catch { } return; }

            if (error != null || ad == null)
            {
                _interstitialReady = false;
                _interstitialLoadFailed = true;
                _lastInterstitialLoadError = (error != null) ? error.ToString() : "ad==null";
                return;
            }

            try { _interstitial?.Destroy(); } catch { }

            _interstitial = ad;
            _interstitialReady = true;

            _interstitialLoadFailed = false;
            _lastInterstitialLoadError = "";

            _interstitial.OnAdFullScreenContentOpened += () => { _pendingInterstitialOpened = true; };
            _interstitial.OnAdFullScreenContentClosed += () =>
            {
                _pendingInterstitialClosed = true;

                if (_pendingInterstitialDone != null)
                {
                    bool ok = _pendingInterstitialOpened && _pendingInterstitialClosed;
                    _pendingInterstitialDone.Invoke(ok);

                    _pendingInterstitialDone = null;
                    _pendingInterstitialOpened = false;
                    _pendingInterstitialClosed = false;
                }

                _interstitialReady = false;
                try { _interstitial?.Destroy(); } catch { }
                _interstitial = null;

                LoadInterstitial();
            };
        });
#endif
    }

    public void ShowInterstitial(Action<bool> onDone, string reason = "Interstitial")
    {
        if (_adsDisabled)
        {
            onDone?.Invoke(true);
            return;
        }

#if UNITY_EDITOR
        if (simulateInEditor || !IsAdMobSdkPresent())
        {
            StartCoroutine(Co_Simulate(onDone, true, reason));
            return;
        }
#endif

#if GOOGLE_MOBILE_ADS
        if (_interstitial == null || !_interstitialReady)
        {
            onDone?.Invoke(false);
            RequestInterstitialReload();
            return;
        }

        _pendingInterstitialDone = onDone;
        _pendingInterstitialOpened = false;
        _pendingInterstitialClosed = false;

        try
        {
            _interstitial.Show();
        }
        catch
        {
            _pendingInterstitialDone?.Invoke(false);
            _pendingInterstitialDone = null;
            _interstitialReady = false;
            try { _interstitial?.Destroy(); } catch { }
            _interstitial = null;
            LoadInterstitial();
        }
#else
        if (simulateInDeviceIfNoSdk) StartCoroutine(Co_Simulate(onDone, true, reason));
        else onDone?.Invoke(false);
#endif
    }

    private IEnumerator Co_Simulate(Action<bool> onDone, bool success, string reason)
    {
        yield return new WaitForSecondsRealtime(simulateDelay);
        onDone?.Invoke(success);
    }

    private string GetRewardedUnitId()
    {
#if UNITY_ANDROID
        if (forceGoogleTestAdUnitsInDevBuild && _isTestContext) return TEST_REWARDED_ANDROID;
        return rewardedUnitIdAndroid;
#else
        return rewardedUnitIdAndroid;
#endif
    }

    private string GetInterstitialUnitId()
    {
#if UNITY_ANDROID
        if (forceGoogleTestAdUnitsInDevBuild && _isTestContext) return TEST_INTERSTITIAL_ANDROID;
        return interstitialUnitIdAndroid;
#else
        return interstitialUnitIdAndroid;
#endif
    }
}