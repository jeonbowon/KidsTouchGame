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

    // ✅ Rewarded: Show 호출자가 기다리는 콜백 저장 (콜백 누락 방지)
    private Action<bool> _pendingRewardedDone;
    private bool _pendingRewardedEarned;

    // ✅ Interstitial: Show 호출자가 기다리는 콜백 저장 (중복 구독 방지)
    private Action<bool> _pendingInterstitialDone;
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

        Debug.Log($"[ADS] AdManager Awake / GOOGLE_MOBILE_ADS={(IsAdMobSdkPresent() ? "YES" : "NO")} / editor={Application.isEditor}");

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

        MobileAds.Initialize(initStatus =>
        {
            Debug.Log("[ADS] MobileAds.Initialize 완료");
            LoadRewarded();
            LoadInterstitial();
        });
#else
        Debug.LogWarning("[ADS] AdMob SDK( GoogleMobileAds )가 없습니다. 시뮬레이션/실패만 가능합니다.");
        _rewardedReady = true;
        _interstitialReady = true;
#endif
    }

    // ─────────────────────────────────────────────────────────────
    // Rewarded
    public void LoadRewarded()
    {
#if GOOGLE_MOBILE_ADS
        _rewardedReady = false;

        string unitId = GetRewardedUnitId();
        if (string.IsNullOrEmpty(unitId))
        {
            Debug.LogWarning("[ADS] Rewarded UnitId가 비어있습니다. (AdMob 콘솔에서 생성 후 입력 필요)");
            return;
        }

        Debug.Log($"[ADS] Rewarded Load 요청 unitId={Mask(unitId)}");

        var request = new AdRequest();
        RewardedAd.Load(unitId, request, (RewardedAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[ADS] Rewarded FAILED to load: {error}");
                _rewardedReady = false;
                return;
            }

            // 기존 객체 정리
            try { _rewarded?.Destroy(); } catch { }

            _rewarded = ad;
            _rewardedReady = true;

            Debug.Log("[ADS] Rewarded LOADED");

            // ✅ 이벤트 구독은 로드 성공 시 1번만
            _rewarded.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("[ADS] Rewarded SHOWED (FullScreen Opened)");
            };

            _rewarded.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[ADS] Rewarded CLOSED");

                // ✅ 보상 못 받았는데 닫혔으면 콜백 false로 끝내야 게임이 멈추지 않음
                if (_pendingRewardedDone != null)
                {
                    bool ok = _pendingRewardedEarned;
                    _pendingRewardedDone.Invoke(ok);
                    _pendingRewardedDone = null;
                    _pendingRewardedEarned = false;
                }

                _rewardedReady = false;
                try { _rewarded?.Destroy(); } catch { }
                _rewarded = null;

                LoadRewarded(); // 다음을 위해 재로드
            };

            _rewarded.OnAdFullScreenContentFailed += (AdError err) =>
            {
                Debug.LogWarning($"[ADS] Rewarded FAILED to show: {err}");

                _pendingRewardedDone?.Invoke(false);
                _pendingRewardedDone = null;
                _pendingRewardedEarned = false;

                _rewardedReady = false;
                try { _rewarded?.Destroy(); } catch { }
                _rewarded = null;

                LoadRewarded();
            };
        });
#else
        _rewardedReady = true;
        Debug.Log("[ADS] LoadRewarded (No SDK) -> Ready=true");
#endif
    }

    // Rewarded: Continue용
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
            Debug.LogWarning($"[ADS] Rewarded NOT READY (reason={reason})");
            onDone?.Invoke(false);
            LoadRewarded();
            return;
        }

        // ✅ 콜백 저장(닫힘/실패에서도 반드시 끝내기 위함)
        _pendingRewardedDone = onDone;
        _pendingRewardedEarned = false;

        _rewarded.Show(reward =>
        {
            Debug.Log($"[ADS] Reward Earned! type={reward.Type} amount={reward.Amount} (reason={reason})");
            _pendingRewardedEarned = true;

            // ✅ 보상을 받는 순간 true로 끝내고, 나머지는 Closed에서 정리/재로드
            _pendingRewardedDone?.Invoke(true);
            _pendingRewardedDone = null;
        });
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

        string unitId = GetInterstitialUnitId();
        if (string.IsNullOrEmpty(unitId))
        {
            Debug.LogWarning("[ADS] Interstitial UnitId가 비어있습니다. (AdMob 콘솔에서 생성 후 입력 필요)");
            return;
        }

        Debug.Log($"[ADS] Interstitial Load 요청 unitId={Mask(unitId)}");

        var request = new AdRequest();
        InterstitialAd.Load(unitId, request, (InterstitialAd ad, LoadAdError error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[ADS] Interstitial FAILED to load: {error}");
                _interstitialReady = false;
                return;
            }

            // 기존 객체 정리
            try { _interstitial?.Destroy(); } catch { }

            _interstitial = ad;
            _interstitialReady = true;

            Debug.Log("[ADS] Interstitial LOADED");

            // ✅ 이벤트 구독은 로드 성공 시 1번만 (중복 구독 금지)
            _interstitial.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("[ADS] Interstitial SHOWED (FullScreen Opened)");
            };

            _interstitial.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("[ADS] Interstitial CLOSED");

                // ✅ ShowInterstitial이 기다리던 콜백이 있으면 여기서 끝낸다
                _pendingInterstitialDone?.Invoke(true);
                _pendingInterstitialDone = null;

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

                _interstitialReady = false;
                try { _interstitial?.Destroy(); } catch { }
                _interstitial = null;

                LoadInterstitial();
            };
        });
#else
        _interstitialReady = true;
        Debug.Log("[ADS] LoadInterstitial (No SDK) -> Ready=true");
#endif
    }

    // Interstitial: Menu 이동용 (✅ 닫힘/실패 시점에 콜백)
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
            Debug.LogWarning($"[ADS] Interstitial NOT READY (reason={reason})");
            onDone?.Invoke(false);
            LoadInterstitial();
            return;
        }

        // ✅ 콜백은 “저장만” 한다. (구독 추가 금지)
        _pendingInterstitialDone = onDone;

        _interstitial.Show();
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
