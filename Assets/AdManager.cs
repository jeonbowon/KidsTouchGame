using System;
using System.Collections;
using UnityEngine;

#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
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

    public bool IsRewardedReady => _rewardedReady;
    public bool IsInterstitialReady => _interstitialReady;

    private bool _rewardedReady = false;
    private bool _interstitialReady = false;

#if GOOGLE_MOBILE_ADS
    private RewardedAd _rewarded;
    private InterstitialAd _interstitial;

    // pending guard
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

        Debug.Log($"[ADS] AdManager Awake / SDK={(IsAdMobSdkPresent() ? "YES" : "NO")} / editor={Application.isEditor}");
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

        MobileAds.Initialize(_ =>
        {
            Debug.Log("[ADS] MobileAds.Initialize 완료");
            LoadRewarded();
            LoadInterstitial();
        });
#else
        Debug.LogWarning("[ADS] AdMob SDK가 없습니다. 시뮬레이션/실패만 가능합니다.");
        // ❗ SDK 없는데 Ready=true로 속이면, GameManager가 “광고 준비됨”으로 착각합니다.
        _rewardedReady = false;
        _interstitialReady = false;
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // ✅ 핵심 수정:
    // “다시 로드해라”가 아니라 “없으면 로드해라(있으면 유지)”로 동작하게 바꿈.
    //  - ready 인데도 LoadRewarded()를 호출하면 _rewardedReady=false로 리셋되어
    //    Continue 클릭 시 3~6초 대기가 생기는 원인이 됨.
    public void RequestRewardedReload()
    {
#if GOOGLE_MOBILE_ADS
        // 이미 준비되어 있으면 절대 깨지 않는다.
        if (_rewardedReady && _rewarded != null)
        {
            Debug.Log("[ADS] RequestRewardedReload() -> already READY, keep it (no reset)");
            return;
        }

        // 로딩 중이면 중복 로드하지 않는다.
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

        string unitId = GetRewardedUnitId();
        if (string.IsNullOrEmpty(unitId))
        {
            Debug.LogWarning("[ADS] Rewarded UnitId가 비어있습니다.");
            _rewardedLoading = false;
            return;
        }

        Debug.Log($"[ADS] Rewarded Load 요청 unitId={Mask(unitId)}");

        var request = new AdRequest();
        RewardedAd.Load(unitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            _rewardedLoading = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning($"[ADS] Rewarded FAILED to load: {error}");
                _rewardedReady = false;
                return;
            }

            try { _rewarded?.Destroy(); } catch { }

            _rewarded = ad;
            _rewardedReady = true;

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

                // ✅ “진짜 성공” 조건: OPENED && EARNED && CLOSED
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

                LoadRewarded(); // 다음을 위해 재로드
            };

            _rewarded.OnAdFullScreenContentFailed += (AdError err) =>
            {
                Debug.LogWarning($"[ADS] Rewarded FAILED to show: {err}");

                // 실패면 무조건 false
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
            RequestRewardedReload(); // ✅ ensure load (no reset if ready)
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

        string unitId = GetInterstitialUnitId();
        if (string.IsNullOrEmpty(unitId))
        {
            Debug.LogWarning("[ADS] Interstitial UnitId가 비어있습니다.");
            _interstitialLoading = false;
            return;
        }

        Debug.Log($"[ADS] Interstitial Load 요청 unitId={Mask(unitId)}");

        var request = new AdRequest();
        InterstitialAd.Load(unitId, request, (InterstitialAd ad, LoadAdError error) =>
        {
            _interstitialLoading = false;

            if (error != null || ad == null)
            {
                Debug.LogWarning($"[ADS] Interstitial FAILED to load: {error}");
                _interstitialReady = false;
                return;
            }

            try { _interstitial?.Destroy(); } catch { }

            _interstitial = ad;
            _interstitialReady = true;

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

                LoadInterstitial(); // 다음을 위해 재로드
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
            RequestInterstitialReload(); // ✅ ensure load (no reset if ready)
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

    // ─────────────────────────────────────────────────────────────
    private IEnumerator Co_Simulate(Action<bool> onDone, bool result, string reason)
    {
        yield return new WaitForSecondsRealtime(simulateDelay);
        Debug.Log($"[ADS] Simulate Done (reason={reason}) result={result}");
        onDone?.Invoke(result);
    }

    private string GetRewardedUnitId()
    {
#if UNITY_ANDROID
        return rewardedUnitIdAndroid;
#else
        return rewardedUnitIdAndroid;
#endif
    }

    private string GetInterstitialUnitId()
    {
#if UNITY_ANDROID
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
