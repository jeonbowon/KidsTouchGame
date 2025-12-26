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
    // ✅ (핵심 추가) DevBuild/Editor에서는 "구글 공식 테스트 유닛ID"로 강제 전환 옵션
    //
    // 왜 필요하냐?
    // - 대표님 로그는 Code=3 / No fill 이 반복됩니다.
    // - TestDevice 등록만으론 "항상 광고가 뜨는 것"을 보장하지 못합니다.
    // - 그래서 DevBuild에서는 "무조건 나오는 공식 테스트 유닛ID"로 파이프라인을 확정합니다.
    //
    // 안전장치:
    // - Release 빌드(Debug.isDebugBuild=false)에서는 자동으로 실유닛ID로만 동작합니다.
    [Header("Dev/Test Safety (Recommended)")]
    [Tooltip("Editor 또는 Development Build에서는 구글 공식 테스트 유닛ID로 강제합니다(가장 확실). Release 빌드는 자동으로 실유닛ID 사용.")]
    [SerializeField] private bool forceGoogleTestAdUnitsInDevBuild = true;

    // ✅ 구글 공식 테스트 유닛ID (Android)
    // - Rewarded : ca-app-pub-3940256099942544/5224354917
    // - Interstitial : ca-app-pub-3940256099942544/1033173712
    // (배너 등도 있지만 현재 프로젝트는 Rewarded/Interstitial만)
    private const string TEST_REWARDED_ANDROID = "ca-app-pub-3940256099942544/5224354917";
    private const string TEST_INTERSTITIAL_ANDROID = "ca-app-pub-3940256099942544/1033173712";

    // ─────────────────────────────────────────────────────────────
    // ✅ (추가) 테스트 기기 등록 옵션
    // - 이건 "테스트 광고로 표시"를 유도하는 장치 (유닛ID는 그대로)
    // - 하지만 대표님처럼 No fill 이면 이것만으로는 광고가 항상 뜬다고 보장 못함
    [Header("Test Devices (Editor/DevBuild ONLY)")]
    [Tooltip("테스트 기기 ID(AdMob이 출력해주는 Test Device Id). 여러 대면 Size 늘려서 추가하세요.")]
    [SerializeField] private string[] testDeviceIds = new string[0];

    public bool IsRewardedReady => _rewardedReady;
    public bool IsInterstitialReady => _interstitialReady;

    public bool IsRewardedLoading => _rewardedLoadingPublic;
    public bool RewardedLoadFailed => _rewardedLoadFailed;
    public string LastRewardedLoadError => _lastRewardedLoadError;

    public bool IsInterstitialLoading => _interstitialLoadingPublic;
    public bool InterstitialLoadFailed => _interstitialLoadFailed;
    public string LastInterstitialLoadError => _lastInterstitialLoadError;

    private bool _rewardedReady = false;
    private bool _interstitialReady = false;

    private bool _rewardedLoadingPublic = false;
    private bool _rewardedLoadFailed = false;
    private string _lastRewardedLoadError = "";

    private bool _interstitialLoadingPublic = false;
    private bool _interstitialLoadFailed = false;
    private string _lastInterstitialLoadError = "";

#if GOOGLE_MOBILE_ADS
    private RewardedAd _rewarded;
    private InterstitialAd _interstitial;

    private Action<bool> _pendingRewardedDone;
    private bool _pendingRewardedOpened;
    private bool _pendingRewardedEarned;
    private bool _pendingRewardedClosed;

    private Action<bool> _pendingInterstitialDone;
    private bool _pendingInterstitialOpened;
    private bool _pendingInterstitialClosed;

    private bool _rewardedLoading = false;
    private bool _interstitialLoading = false;
#endif

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log($"[ADS] AdManager Awake / SDK={(IsAdMobSdkPresent() ? "YES" : "NO")} / editor={Application.isEditor} devBuild={Debug.isDebugBuild}");
        Init();
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
            Debug.Log("[ADS] MobileAds.Initialize 완료");
            LoadRewarded();
            LoadInterstitial();
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
        bool isEditor = Application.isEditor;
        bool isDevBuild = Debug.isDebugBuild;

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

        // ✅ 대표님이 Builder()로 하다가 컴파일 에러가 났던 이유:
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

                _pendingRewardedDone?.Invoke(false);
                _pendingRewardedDone = null;

                _pendingRewardedOpened = false;
                _pendingRewardedEarned = false;
                _pendingRewardedClosed = false;

                _rewardedReady = false;
                try { _rewarded?.Destroy(); } catch { }
                _rewarded = null;

                LoadRewarded();
            };
        });
#else
        _rewardedReady = false;
        _rewardedLoadingPublic = false;
        _rewardedLoadFailed = true;
        _lastRewardedLoadError = "No SDK";
        Debug.Log("[ADS] LoadRewarded (No SDK) -> Ready=false");
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
        if (_pendingRewardedDone != null)
        {
            Debug.LogWarning($"[ADS] Rewarded already showing/pending. reject (reason={reason})");
            onDone?.Invoke(false);
            return;
        }

        if (_rewarded == null || !_rewardedReady)
        {
            Debug.LogWarning($"[ADS] Rewarded NOT READY (reason={reason})");
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
            _rewarded.Show(reward =>
            {
                Debug.Log($"[ADS] Reward Earned! type={reward.Type} amount={reward.Amount} (reason={reason})");
                _pendingRewardedEarned = true;
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ADS] Rewarded Show Exception: {e.Message}");
            _pendingRewardedDone?.Invoke(false);
            _pendingRewardedDone = null;
            _pendingRewardedOpened = false;
            _pendingRewardedEarned = false;
            _pendingRewardedClosed = false;

            _rewardedReady = false;
            try { _rewarded?.Destroy(); } catch { }
            _rewarded = null;

            LoadRewarded();
        }
#else
        if (simulateInDeviceIfNoSdk)
        {
            Debug.Log($"[ADS] Device Simulate Rewarded -> {simulateDelay}s 후 성공 처리 (reason={reason})");
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

                _pendingInterstitialDone?.Invoke(false);
                _pendingInterstitialDone = null;

                _pendingInterstitialOpened = false;
                _pendingInterstitialClosed = false;

                _interstitialReady = false;
                try { _interstitial?.Destroy(); } catch { }
                _interstitial = null;

                LoadInterstitial();
            };
        });
#else
        _interstitialReady = false;
        _interstitialLoadingPublic = false;
        _interstitialLoadFailed = true;
        _lastInterstitialLoadError = "No SDK";
        Debug.Log("[ADS] LoadInterstitial (No SDK) -> Ready=false");
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
        if (_pendingInterstitialDone != null)
        {
            Debug.LogWarning($"[ADS] Interstitial already showing/pending. reject (reason={reason})");
            onDone?.Invoke(false);
            return;
        }

        if (_interstitial == null || !_interstitialReady)
        {
            Debug.LogWarning($"[ADS] Interstitial NOT READY (reason={reason})");
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
            Debug.LogWarning($"[ADS] Interstitial Show Exception: {e.Message}");
            _pendingInterstitialDone?.Invoke(false);
            _pendingInterstitialDone = null;
            _pendingInterstitialOpened = false;
            _pendingInterstitialClosed = false;

            _interstitialReady = false;
            try { _interstitial?.Destroy(); } catch { }
            _interstitial = null;

            LoadInterstitial();
        }
#else
        if (simulateInDeviceIfNoSdk)
        {
            Debug.Log($"[ADS] Device Simulate Interstitial -> {simulateDelay}s 후 성공 처리 (reason={reason})");
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
        bool isTestContext = (Application.isEditor || Debug.isDebugBuild);

        // ✅ DevBuild/Editor이면 무조건 "공식 테스트 유닛" 사용(가장 확실)
        if (forceGoogleTestAdUnitsInDevBuild && isTestContext)
        {
            return TEST_REWARDED_ANDROID;
        }

        // ✅ Release(또는 강제 OFF)면 대표님 실유닛 사용
        return rewardedUnitIdAndroid;
#else
        return rewardedUnitIdAndroid;
#endif
    }

    private string GetInterstitialUnitId()
    {
#if UNITY_ANDROID
        bool isTestContext = (Application.isEditor || Debug.isDebugBuild);

        if (forceGoogleTestAdUnitsInDevBuild && isTestContext)
        {
            return TEST_INTERSTITIAL_ANDROID;
        }

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
