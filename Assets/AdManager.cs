using System;
using System.Collections;
using UnityEngine;

#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
using System.Collections.Generic; // ✅ 테스트 기기 ID 리스트
#endif

public class AdManager : MonoBehaviour
{
    public static AdManager I { get; private set; }

    // ✅ Unity API(Debug.isDebugBuild 등)는 "메인 스레드"에서만 안전합니다.
    // MobileAds.Initialize 콜백은 메인 스레드가 아닐 수 있어, 값을 Awake에서 캐시해 둡니다.
    private bool _isEditor;
    private bool _isDevBuild;
    private bool _isTestContext;

#if GOOGLE_MOBILE_ADS
    private volatile bool _pendingInitComplete; // Initialize 콜백에서만 true로 세팅 (스레드 세이프 용도)
#endif

    [Header("AdMob IDs (Android)")]
    [Tooltip("ca-app-pub-xxxxxxxxxxxxxxxx~yyyyyyyyyy (App ID)")]
    [SerializeField] private string androidAppId = "";

    [Tooltip("Rewarded Unit Id (Continue용)  ca-app-pub-xxx/yyy")]
    [SerializeField] private string rewardedUnitIdAndroid = "";

    [Tooltip("Interstitial Unit Id (Menu 이동용)  ca-app-pub-xxx/yyy")]
    [SerializeField] private string interstitialUnitIdAndroid = "";

    [Header("Simulate (when no SDK or in editor)")]
    [SerializeField] private bool simulateInEditor = true;
    [SerializeField] private bool simulateInDeviceIfNoSdk = false;
    [SerializeField] private float simulateDelay = 1.0f;

    // ─────────────────────────────────────────────────────────────
    // ✅ (핵심) DevBuild/Editor에서는 "구글 공식 테스트 유닛ID"로 강제 전환 옵션
    [Header("Dev/Test Safety (Recommended)")]
    [Tooltip("Editor 또는 Development Build에서는 구글 공식 테스트 유닛ID로 강제합니다(가장 확실). Release 빌드는 자동으로 실유닛ID 사용.")]
    [SerializeField] private bool forceGoogleTestAdUnitsInDevBuild = true;

    // ✅ 구글 공식 테스트 유닛ID (Android)
    private const string TEST_REWARDED_ANDROID = "ca-app-pub-3940256099942544/5224354917";
    private const string TEST_INTERSTITIAL_ANDROID = "ca-app-pub-3940256099942544/1033173712";

#if GOOGLE_MOBILE_ADS
    [Header("Test Devices (Editor/DevBuild ONLY)")]
    [Tooltip("테스트 기기 ID(AdMob이 출력해주는 Test Device Id). 여러 대면 Size 늘려서 추가하세요.")]
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

        // ✅ 메인 스레드에서 캐시
        _isEditor = Application.isEditor;
        _isDevBuild = Debug.isDebugBuild;
        _isTestContext = (_isEditor || _isDevBuild);

        Debug.Log($"[ADS] AdManager Awake / SDK={(IsAdMobSdkPresent() ? "YES" : "NO")} / editor={_isEditor} devBuild={_isDevBuild}");
        Init();
    }

    private void Update()
    {
#if GOOGLE_MOBILE_ADS
        // ✅ Initialize 콜백이 메인 스레드가 아니어도,
        // 실제 로드(LoadRewarded/LoadInterstitial)는 여기서 메인 스레드로 실행됩니다.
        if (_pendingInitComplete)
        {
            _pendingInitComplete = false;
            Debug.Log("[ADS] MobileAds.Initialize 완료 (main thread) -> LoadRewarded/LoadInterstitial");
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
#if GOOGLE_MOBILE_ADS
        Debug.Log($"[ADS] Init AdMob. appIdAndroid={(string.IsNullOrEmpty(androidAppId) ? "(empty)" : "set")}");

        // ✅ 테스트 기기 등록은 Initialize 이전에(요청 설정이므로)
        ConfigureTestDevices_EditorOrDevBuildOnly();

        MobileAds.Initialize(_ =>
        {
            // ⚠ 콜백 스레드는 메인 스레드가 아닐 수 있습니다.
            // 여기서는 플래그만 세팅하고, Update()에서 메인 스레드로 로드를 수행합니다.
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
        bool isEditor = _isEditor;
        bool isDevBuild = _isDevBuild;

        // ✅ Release 빌드에서는 테스트 기기 등록 자체를 적용하지 않음
        if (!isEditor && !isDevBuild)
        {
            Debug.Log("[ADS] TestDevice config SKIP (Release build)");
            return;
        }

        if (testDeviceIds == null || testDeviceIds.Length == 0)
        {
            Debug.Log($"[ADS] TestDevice config SKIP (no ids) / editor={isEditor} devBuild={isDevBuild}");
            return;
        }

        var list = new List<string>();
        foreach (var id in testDeviceIds)
        {
            if (!string.IsNullOrWhiteSpace(id))
                list.Add(id.Trim());
        }

        if (list.Count == 0)
        {
            Debug.Log($"[ADS] TestDevice config SKIP (ids invalid) / editor={isEditor} devBuild={isDevBuild}");
            return;
        }

        // Unity 플러그인 버전에 따라 RequestConfiguration.Builder 타입이 없을 수 있습니다.
        // 아래처럼 "TestDeviceIds 프로퍼티"로 세팅하는 방식이 호환성이 좋습니다.
        var config = new RequestConfiguration
        {
            TestDeviceIds = list
        };

        MobileAds.SetRequestConfiguration(config);
        Debug.Log($"[ADS] Test devices REGISTERED / count={list.Count} / editor={isEditor} devBuild={isDevBuild}");
#endif
    }

    public void RequestRewardedReload()
    {
#if GOOGLE_MOBILE_ADS
        if (_rewardedReady && _rewarded != null)
        {
            Debug.Log("[ADS] RequestRewardedReload() -> already READY, keep it (no reset)");
            return;
        }

        if (_rewardedLoading)
        {
            Debug.Log("[ADS] RequestRewardedReload() -> already LOADING, skip");
            return;
        }
#endif
        Debug.Log("[ADS] RequestRewardedReload() -> ensure load");
        LoadRewarded();
    }

    public void RequestInterstitialReload()
    {
#if GOOGLE_MOBILE_ADS
        if (_interstitialReady && _interstitial != null)
        {
            Debug.Log("[ADS] RequestInterstitialReload() -> already READY, keep it (no reset)");
            return;
        }

        if (_interstitialLoading)
        {
            Debug.Log("[ADS] RequestInterstitialReload() -> already LOADING, skip");
            return;
        }
#endif
        Debug.Log("[ADS] RequestInterstitialReload() -> ensure load");
        LoadInterstitial();
    }

    // ─────────────────────────────────────────────────────────────
    // Rewarded
    public void LoadRewarded()
    {
#if GOOGLE_MOBILE_ADS
        _rewardedReady = false;
        _rewardedLoading = true;

        _rewardedLoadingPublic = true;
        _rewardedLoadFailed = false;
        _lastRewardedLoadError = "";

        string unitId = GetRewardedUnitId();
        if (string.IsNullOrEmpty(unitId))
        {
            Debug.LogWarning("[ADS] Rewarded UnitId가 비어있습니다.");
            _rewardedLoading = false;

            _rewardedLoadingPublic = false;
            _rewardedLoadFailed = true;
            _lastRewardedLoadError = "Empty Rewarded UnitId";
            return;
        }

        Debug.Log($"[ADS] Rewarded Load 요청 unitId={Mask(unitId)}");

        var request = new AdRequest();
        RewardedAd.Load(unitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            _rewardedLoading = false;
            _rewardedLoadingPublic = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning($"[ADS] Rewarded FAILED to load: {error}");
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

            Debug.Log("[ADS] Rewarded LOADED");

            _rewarded.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("[ADS] Rewarded OPENED");
                _pendingRewardedOpened = true;
            };

            _rewarded.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[ADS] Rewarded CLOSED");
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

            _rewarded.OnAdFullScreenContentFailed += (AdError err) =>
            {
                Debug.LogWarning($"[ADS] Rewarded FAILED to show: {err}");
            };
        });
#endif
    }

    public void ShowRewarded(Action<bool> onDone, string reason = "Rewarded")
    {
        Debug.Log($"[ADS] ShowRewarded 요청 reason={reason} / ready={IsRewardedReady}");

#if UNITY_EDITOR
        if (simulateInEditor || !IsAdMobSdkPresent())
        {
            Debug.Log($"[ADS] Editor Simulate Rewarded -> {simulateDelay}s 후 성공 처리 (reason={reason})");
            StartCoroutine(Co_Simulate(onDone, true, reason));
            return;
        }
#endif

#if GOOGLE_MOBILE_ADS
        if (_rewarded == null || !_rewardedReady)
        {
            Debug.LogWarning($"[ADS] Rewarded NOT READY -> FAIL (reason={reason}) / lastErr={_lastRewardedLoadError}");
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
            _rewarded.Show((Reward reward) =>
            {
                Debug.Log($"[ADS] Rewarded EARNED type={reward.Type} amount={reward.Amount}");
                _pendingRewardedEarned = true;
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ADS] Rewarded Show Exception: {e}");
            _pendingRewardedDone?.Invoke(false);
            _pendingRewardedDone = null;
            _rewardedReady = false;
            try { _rewarded?.Destroy(); } catch { }
            _rewarded = null;

            LoadRewarded();
        }
#else
        if (simulateInDeviceIfNoSdk)
        {
            Debug.Log($"[ADS] No SDK -> Simulate Rewarded {simulateDelay}s (reason={reason})");
            StartCoroutine(Co_Simulate(onDone, true, reason));
        }
        else
        {
            Debug.LogWarning($"[ADS] No SDK -> Rewarded FAIL (reason={reason})");
            onDone?.Invoke(false);
        }
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // Interstitial
    public void LoadInterstitial()
    {
#if GOOGLE_MOBILE_ADS
        _interstitialReady = false;
        _interstitialLoading = true;

        _interstitialLoadingPublic = true;
        _interstitialLoadFailed = false;
        _lastInterstitialLoadError = "";

        string unitId = GetInterstitialUnitId();
        if (string.IsNullOrEmpty(unitId))
        {
            Debug.LogWarning("[ADS] Interstitial UnitId가 비어있습니다.");
            _interstitialLoading = false;

            _interstitialLoadingPublic = false;
            _interstitialLoadFailed = true;
            _lastInterstitialLoadError = "Empty Interstitial UnitId";
            return;
        }

        Debug.Log($"[ADS] Interstitial Load 요청 unitId={Mask(unitId)}");

        var request = new AdRequest();
        InterstitialAd.Load(unitId, request, (InterstitialAd ad, LoadAdError error) =>
        {
            _interstitialLoading = false;
            _interstitialLoadingPublic = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning($"[ADS] Interstitial FAILED to load: {error}");
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

            Debug.Log("[ADS] Interstitial LOADED");

            _interstitial.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("[ADS] Interstitial OPENED");
                _pendingInterstitialOpened = true;
            };

            _interstitial.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[ADS] Interstitial CLOSED");
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

            _interstitial.OnAdFullScreenContentFailed += (AdError err) =>
            {
                Debug.LogWarning($"[ADS] Interstitial FAILED to show: {err}");
            };
        });
#endif
    }

    public void ShowInterstitial(Action<bool> onDone, string reason = "Interstitial")
    {
        Debug.Log($"[ADS] ShowInterstitial 요청 reason={reason} / ready={IsInterstitialReady}");

#if UNITY_EDITOR
        if (simulateInEditor || !IsAdMobSdkPresent())
        {
            Debug.Log($"[ADS] Editor Simulate Interstitial -> {simulateDelay}s 후 성공 처리 (reason={reason})");
            StartCoroutine(Co_Simulate(onDone, true, reason));
            return;
        }
#endif

#if GOOGLE_MOBILE_ADS
        if (_interstitial == null || !_interstitialReady)
        {
            Debug.LogWarning($"[ADS] Interstitial NOT READY -> FAIL (reason={reason}) / lastErr={_lastInterstitialLoadError}");
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
        catch (Exception e)
        {
            Debug.LogWarning($"[ADS] Interstitial Show Exception: {e}");
            _pendingInterstitialDone?.Invoke(false);
            _pendingInterstitialDone = null;

            _interstitialReady = false;
            try { _interstitial?.Destroy(); } catch { }
            _interstitial = null;

            LoadInterstitial();
        }
#else
        if (simulateInDeviceIfNoSdk)
        {
            Debug.Log($"[ADS] No SDK -> Simulate Interstitial {simulateDelay}s (reason={reason})");
            StartCoroutine(Co_Simulate(onDone, true, reason));
        }
        else
        {
            Debug.LogWarning($"[ADS] No SDK -> Interstitial FAIL (reason={reason})");
            onDone?.Invoke(false);
        }
#endif
    }

    private IEnumerator Co_Simulate(Action<bool> onDone, bool success, string reason)
    {
        yield return new WaitForSecondsRealtime(simulateDelay);
        Debug.Log($"[ADS] Simulate DONE success={success} (reason={reason})");
        onDone?.Invoke(success);
    }

    // ─────────────────────────────────────────────────────────────
    // ✅ (핵심 수정) DevBuild/Editor에서는 테스트 유닛ID로 강제 전환
    private string GetRewardedUnitId()
    {
#if UNITY_ANDROID
        bool isTestContext = _isTestContext;

        if (forceGoogleTestAdUnitsInDevBuild && isTestContext)
            return TEST_REWARDED_ANDROID;

        return rewardedUnitIdAndroid;
#else
        return rewardedUnitIdAndroid;
#endif
    }

    private string GetInterstitialUnitId()
    {
#if UNITY_ANDROID
        bool isTestContext = _isTestContext;

        if (forceGoogleTestAdUnitsInDevBuild && isTestContext)
            return TEST_INTERSTITIAL_ANDROID;

        return interstitialUnitIdAndroid;
#else
        return interstitialUnitIdAndroid;
#endif
    }

    private string Mask(string s)
    {
        if (string.IsNullOrEmpty(s)) return "(empty)";
        if (s.Length <= 8) return "****";
        return s.Substring(0, 6) + "..." + s.Substring(s.Length - 4);
    }
}
