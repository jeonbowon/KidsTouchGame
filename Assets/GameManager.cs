using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Gameplay")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float respawnDelay = 1.2f;
    [SerializeField] private float stageMessageTime = 1.2f;

    [Header("Respawn Invincibility")]
    [SerializeField] private float respawnInvincibleTime = 1.5f;

    [Header("Prefabs/Refs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerSpawnPoint;

    [Header("UI (optional)")]
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text scoreText;

    // 코인 표시용 텍스트
    [SerializeField] private TMP_Text coinText;

    [Header("Score / Stage Clear")]
    [SerializeField] private int scoreToClearStage = 100;

    [Header("Store Reward (Soft Currency)")]
    [Tooltip("스테이지 클리어 시 상점 코인 보상")]
    [SerializeField] private int stageClearStoreCoins = 30;

    [Header("Cosmetics (Stage unlock)")]
    [SerializeField] private string cosmeticDbResourcePath = "Cosmetics/CosmeticDatabase";
    [Tooltip("스테이지 클리어 시 해금된 아이템을 보여줄 메시지/팝업 시간(초)")]
    [SerializeField] private float unlockMessageTime = 1.2f;

    [Tooltip("없으면 Resources에서 자동 로드: UI/CosmeticUnlockPopup")]
    [SerializeField] private string unlockPopupResourcePath = "UI/CosmeticUnlockPopup";
    [SerializeField] private CosmeticUnlockPopup unlockPopupInScene;

    [Header("Unlock SFX (Optional)")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip unlockSfx;

    private CosmeticDatabase _cosmeticDbCache;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private string firstStageSceneName = "Stage1";

    [Header("Stage Flow")]
    [SerializeField] private int maxStage = 10;

    [Header("Fallback (Optional)")]
    [SerializeField] private string playerPrefabResourcePath = "";

    [Header("GameOver UI (Resources)")]
    [SerializeField] private string gameOverPanelResourcePath = "GameOverPanel";
    private GameOverPanel gameOverPanelInstance;

    [Header("Continue Reward")]
    [Tooltip("Continue 성공 시 지급할 생명 수")]
    [SerializeField] private int continueLives = 3;

    [Header("Ad Wait/Retry")]
    [Tooltip("Rewarded 준비될 때까지 기다릴 최대 시간(초)")]
    [SerializeField] private float rewardedWaitTimeout = 8.0f;

    [Tooltip("Interstitial 준비될 때까지 기다릴 최대 시간(초)")]
    [SerializeField] private float interstitialWaitTimeout = 4.0f;

    [Tooltip("대기 UI 갱신 주기(초)")]
    [SerializeField] private float waitTick = 0.5f;

    public int CurrentStage { get; private set; } = 1;
    public int Lives { get; private set; }
    public int Score => score;

    public int AliveEnemyCount => aliveEnemyCount;
    public bool IsStageRunning => isStageRunning;
    public bool IsGameOver => isGameOver;

    private int aliveEnemyCount = 0;

    private bool isGameOver = false;
    private bool isStageRunning = false;
    private bool isRespawning = false;
    private bool isStageClearing = false;

    private int score = 0;

    [Header("Player Bullet (배율)")]
    [SerializeField] private float playerBulletMulStage1 = 1.0f;
    [SerializeField] private float playerBulletMulPerStage = 0.0f;
    [SerializeField] private Vector2 playerBulletMulClamp = new Vector2(0.1f, 3.0f);

    public float BulletSpeedMul
    {
        get
        {
            float mul = playerBulletMulStage1 + (CurrentStage - 1) * playerBulletMulPerStage;
            return Mathf.Clamp(mul, playerBulletMulClamp.x, playerBulletMulClamp.y);
        }
    }

    [Header("Enemy Bullet (절대 속도)")]
    [SerializeField] private float enemyBulletSpeedStage1 = 3.5f;
    [SerializeField] private float enemyBulletSpeedPerStage = 0.6f;
    [SerializeField] private Vector2 enemyBulletSpeedClamp = new Vector2(0.5f, 10f);

    public float GetEnemyBulletSpeed()
    {
        float s = enemyBulletSpeedStage1 + (CurrentStage - 1) * enemyBulletSpeedPerStage;
        float r = Mathf.Clamp(s, enemyBulletSpeedClamp.x, enemyBulletSpeedClamp.y);
        Debug.Log($"[GM] stage={CurrentStage} -> enemyBulletSpeed={r}");
        return r;
    }

    // GameOver 처리 중 중복 방지
    private bool isHandlingGameOverFlow = false;

    private enum GameOverChoice { None, Continue, Menu }
    private GameOverChoice _choice = GameOverChoice.None;

    // ✅ Continue 성공 여부(바깥 while 탈출용)
    private bool _continueSucceededThisFlow = false;

    // 완전정지 보장용
    private float _defaultFixedDeltaTime = 0.02f;

    // Continue 후 Shop 제안 결과
    private enum PostContinueChoice { None, Shop, Skip }
    private PostContinueChoice _postContinueChoice = PostContinueChoice.None;

    // Additive Shop Overlay 로딩/오픈 중: sceneLoaded에서 게임 흐름을 건드리지 않게 막는 플래그
    private bool _overlayShopLoadingOrOpen = false;

    void Awake()
    {
        if (I != null && I != this)
        {
            Debug.LogWarning("[GameManager] 중복 인스턴스 감지 → 자신 파괴");
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        _defaultFixedDeltaTime = Time.fixedDeltaTime;

        ForcePause(false);

        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 1;

        EnsureAdManagerExists();

        // ✅ Cosmetics: DB/Popup 캐시 준비
        EnsureCosmeticDb();
        EnsureUnlockPopup();
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private bool IsStageScene(string sceneName) => sceneName.StartsWith("Stage");

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ✅✅ 핵심 수정:
        // Overlay Shop(Additive) 로딩/오픈 중에는 어떤 sceneLoaded도
        // "게임 흐름(StopAllCoroutines/ForcePause/StartStage)"를 건드리면 안 됩니다.
        // (한 번이라도 StopAllCoroutines가 실행되면 Close 대기 코루틴이 끊겨서 Close가 먹통처럼 보입니다.)
        if (_overlayShopLoadingOrOpen && mode == LoadSceneMode.Additive)
        {
            Debug.Log($"[GameManager] OverlayShop 중 Additive sceneLoaded({scene.name}) → GameManager 씬 처리 강제 스킵");
            return;
        }

        StopAllCoroutines();
        ForcePause(false);

        EnsureAdManagerExists();

        // 씬 로드마다 popup이 파괴될 수 있으니 재확인
        EnsureCosmeticDb();
        EnsureUnlockPopup();

        if (IsStageScene(scene.name))
        {
            RebindStageSceneObjects();
            EnsureGameOverPanel();

            // 스테이지 진입 시 미리 로드
            if (AdManager.I != null)
            {
                AdManager.I.RequestRewardedReload();
                AdManager.I.RequestInterstitialReload();
            }

            StartCoroutine(Co_StartStage(CurrentStage));
        }
        else
        {
            isStageRunning = false;
            KillAllPlayers();
            DestroyGameOverPanelInstance();
        }
    }

    private void ForcePause(bool paused)
    {
        if (paused)
        {
            Time.timeScale = 0f;
            Time.fixedDeltaTime = 0f;
            AudioListener.pause = true;
        }
        else
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDeltaTime;
            AudioListener.pause = false;
        }
    }

    private void EnsureAdManagerExists()
    {
        if (AdManager.I != null) return;

        var found = FindObjectOfType<AdManager>(true);
        if (found != null) return;

        var go = new GameObject("AdManager (Auto)");
        go.AddComponent<AdManager>();
        DontDestroyOnLoad(go);

        Debug.Log("[GameManager] AdManager가 없어 자동 생성했습니다.");
    }

    private void RebindStageSceneObjects()
    {
        var spawnGo = GameObject.FindWithTag("PlayerSpawn");
        playerSpawnPoint = (spawnGo != null) ? spawnGo.transform : null;
        if (playerSpawnPoint == null)
            Debug.LogError("[GameManager] Stage 씬에서 태그 'PlayerSpawn'을 찾지 못했습니다.");

        if (livesText == null) livesText = SafeFindTMP("LivesText");
        if (stageText == null) stageText = SafeFindTMP("StageText");
        if (messageText == null) messageText = SafeFindTMP("MessageText");
        if (scoreText == null) scoreText = SafeFindTMP("ScoreText");
        if (coinText == null) coinText = SafeFindTMP("CoinText");

        if (playerPrefab == null && !string.IsNullOrEmpty(playerPrefabResourcePath))
        {
            playerPrefab = Resources.Load<GameObject>(playerPrefabResourcePath);
            if (playerPrefab != null)
                Debug.Log($"[GameManager] Resources에서 playerPrefab 로드 성공: {playerPrefabResourcePath}");
            else
                Debug.LogWarning($"[GameManager] Resources에서 playerPrefab 로드 실패: {playerPrefabResourcePath}");
        }

        UpdateLivesUI();
        UpdateStageUI();
        UpdateScoreUI();
        UpdateCoinUI();
    }

    private TMP_Text SafeFindTMP(string name)
    {
        var go = GameObject.Find(name);
        return go ? go.GetComponent<TMP_Text>() : null;
    }

    void Start()
    {
        Lives = maxLives;
        score = 0;

        isGameOver = false;
        isStageRunning = false;
        isRespawning = false;
        isStageClearing = false;
        aliveEnemyCount = 0;

        isHandlingGameOverFlow = false;
        _choice = GameOverChoice.None;
        _continueSucceededThisFlow = false;

        UpdateLivesUI();
        UpdateStageUI();
        UpdateScoreUI();
        UpdateCoinUI();
        if (messageText != null) messageText.text = "";

        EnsureCosmeticDb();
        EnsureUnlockPopup();

        if (IsStageScene(SceneManager.GetActiveScene().name))
        {
            RebindStageSceneObjects();
            EnsureGameOverPanel();

            if (AdManager.I != null)
            {
                AdManager.I.RequestRewardedReload();
                AdManager.I.RequestInterstitialReload();
            }

            StartCoroutine(Co_StartStage(CurrentStage));
        }
    }

    public void NewRun()
    {
        Debug.Log("[GameManager] NewRun()");
        isGameOver = false;
        isStageRunning = false;
        isRespawning = false;
        isStageClearing = false;

        DestroyGameOverPanelInstance();
        KillAllPlayers();

        Lives = maxLives;
        CurrentStage = 1;
        aliveEnemyCount = 0;
        score = 0;

        isHandlingGameOverFlow = false;
        _choice = GameOverChoice.None;
        _continueSucceededThisFlow = false;

        UpdateLivesUI();
        UpdateStageUI();
        UpdateScoreUI();

        ForcePause(false);
        SceneManager.LoadScene(firstStageSceneName);
    }

    IEnumerator Co_StartStage(int stage)
    {
        isStageRunning = false;
        isStageClearing = false;

        yield return ShowMessageFor($"STAGE {stage}", stageMessageTime);

        aliveEnemyCount = 0;
        isStageRunning = true;

        yield return null;

        KillAllPlayers();
        isGameOver = false;
        isRespawning = false;
        isHandlingGameOverFlow = false;
        _choice = GameOverChoice.None;
        _continueSucceededThisFlow = false;

        if (gameOverPanelInstance != null)
            gameOverPanelInstance.Hide();

        SpawnPlayer();
    }

    public void OnEnemySpawned() => aliveEnemyCount++;
    public void OnEnemyRemoved() => aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
    public void OnEnemyKilled() => OnEnemyRemoved();

    public void AddScore(int amount)
    {
        if (isGameOver) return;

        score += amount;
        if (score < 0) score = 0;

        UpdateScoreUI();

        if (!isStageClearing && IsStageScene(SceneManager.GetActiveScene().name))
        {
            if (score >= scoreToClearStage)
                StartCoroutine(Co_StageClear());
        }
    }

    IEnumerator Co_StageClear()
    {
        isStageClearing = true;
        isStageRunning = false;

        if (stageClearStoreCoins > 0)
        {
            CosmeticSaveManager.AddCoins(stageClearStoreCoins);
            UpdateCoinUI();
        }

        KillAllEnemiesAndBullets_Fallback();

        yield return ShowMessageFor("STAGE CLEAR", 1.5f);

        var newlyUnlocked = GrantStageUnlocksAndGetNew(CurrentStage);
        if (newlyUnlocked != null && newlyUnlocked.Count > 0)
            yield return Co_PlayUnlockPresentation(newlyUnlocked);

        if (CurrentStage < maxStage)
        {
            CurrentStage++;
            UpdateStageUI();

            score = 0;
            UpdateScoreUI();

            string nextSceneName = $"Stage{CurrentStage}";
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            yield return ShowMessageFor("ALL STAGES CLEAR!", 1.8f);
            ForcePause(false);
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public void OnPlayerDied()
    {
        if (isGameOver) return;
        if (isRespawning) return;

        Lives--;
        if (Lives < 0) Lives = 0;

        UpdateLivesUI();

        if (Lives <= 0)
        {
            StartCoroutine(Co_GameOver());
            return;
        }

        StartCoroutine(Co_RespawnPlayerWithMessage());
    }

    IEnumerator Co_RespawnPlayerWithMessage()
    {
        isRespawning = true;

        if (messageText != null)
            yield return ShowMessageForRealtime($"LIVES : {Lives}/{maxLives}", 1.1f);
        else
            yield return new WaitForSecondsRealtime(1.1f);

        yield return new WaitForSecondsRealtime(respawnDelay);

        if (!isGameOver)
            SpawnPlayer();

        isRespawning = false;
    }

    void SpawnPlayer()
    {
        if (playerPrefab == null || playerSpawnPoint == null) return;

        var go = Instantiate(playerPrefab, playerSpawnPoint.position, Quaternion.identity);

        var applier = go.GetComponent<PlayerCosmeticApplier>();
        if (applier != null) applier.ApplyEquipped();

        var ph = go.GetComponent<PlayerHealth>();
        if (ph != null && respawnInvincibleTime > 0f)
            ph.BeginInvincibility(respawnInvincibleTime);
    }

    IEnumerator Co_GameOver()
    {
        if (isGameOver) yield break;

        isGameOver = true;
        isStageRunning = false;
        isStageClearing = false;
        isRespawning = false;

        KillAllPlayers();
        EnsureGameOverPanel();

        // ✅ 완전 정지
        ForcePause(true);

        if (!isHandlingGameOverFlow)
            StartCoroutine(Co_GameOverChoiceFlow_Unlimited());
    }

    private IEnumerator Co_GameOverChoiceFlow_Unlimited()
    {
        isHandlingGameOverFlow = true;
        _continueSucceededThisFlow = false;

        while (true)
        {
            if (!isGameOver || _continueSucceededThisFlow)
            {
                isHandlingGameOverFlow = false;
                yield break;
            }

            _choice = GameOverChoice.None;

            if (gameOverPanelInstance == null)
            {
                ForcePause(false);
                SceneManager.LoadScene(mainMenuSceneName);
                isHandlingGameOverFlow = false;
                yield break;
            }

            // 기본 라벨
            gameOverPanelInstance.SetButtonLabels("CONTINUE", "MENU");
            gameOverPanelInstance.Show("GAME OVER\nCONTINUE or MENU?", showButtons: true);
            gameOverPanelInstance.SetButtonsInteractable(true);

            while (_choice == GameOverChoice.None)
            {
                if (!isGameOver || _continueSucceededThisFlow)
                {
                    isHandlingGameOverFlow = false;
                    yield break;
                }
                yield return null;
            }

            if (_choice == GameOverChoice.Menu)
            {
                yield return Co_MenuWithInterstitialWaitThenGoMenu();
                isHandlingGameOverFlow = false;
                yield break;
            }

            yield return Co_TryContinueWithRewardedWait();

            if (!isGameOver || _continueSucceededThisFlow)
            {
                isHandlingGameOverFlow = false;
                yield break;
            }
        }
    }

    // ✅ Continue 성공 직후: Shop 제안 + (선택) 오버레이 Shop 열고 닫힘까지 대기
    private IEnumerator Co_PostContinueStep()
    {
        if (gameOverPanelInstance == null)
            yield break;

        _postContinueChoice = PostContinueChoice.None;
        _choice = GameOverChoice.None;

        // 라벨을 SHOP / SKIP 으로 바꿔서 재사용
        gameOverPanelInstance.SetButtonLabels("SHOP", "SKIP");
        gameOverPanelInstance.Show("CONTINUE READY!\nVISIT SHOP?", showButtons: true);
        gameOverPanelInstance.SetButtonsInteractable(true);

        // 버튼은 기존 OnPanelContinue / OnPanelMenu를 그대로 사용
        while (_choice == GameOverChoice.None)
            yield return null;

        if (_choice == GameOverChoice.Continue) _postContinueChoice = PostContinueChoice.Shop;
        else _postContinueChoice = PostContinueChoice.Skip;

        gameOverPanelInstance.Hide();

        if (_postContinueChoice == PostContinueChoice.Shop)
        {
            yield return Co_OpenShopOverlayAndWaitClose();
        }

        yield break;
    }

    // ✅ 핵심: MainMenu 씬을 Additive로 띄워 Shop만 사용하고 닫히면 언로드
    private IEnumerator Co_OpenShopOverlayAndWaitClose()
    {
        bool closed = false;

        _overlayShopLoadingOrOpen = true;

        // 혹시 다른 스크립트가 TimeScale을 건드려도, Shop 열려 있는 동안은 강제로 멈춤 유지
        ForcePause(true);

        // MainMenuController에게 "오버레이 Shop 모드로 시작해라" + "닫힐 때 콜백"
        MainMenuController.RequestOverlayStore(() =>
        {
            closed = true;
        });

        // Additive 로드
        var op = SceneManager.LoadSceneAsync(mainMenuSceneName, LoadSceneMode.Additive);
        while (!op.isDone) yield return null;

        // 로드 직후도 다시 한 번 강제 Pause(안전)
        ForcePause(true);

        // 닫힐 때까지 대기
        while (!closed) yield return null;

        // Additive 씬 언로드
        var unload = SceneManager.UnloadSceneAsync(mainMenuSceneName);
        while (unload != null && !unload.isDone) yield return null;

        _overlayShopLoadingOrOpen = false;

        // 여기서는 계속 Pause 상태 유지 (Continue 흐름에서 재개/스폰을 한다)
        ForcePause(true);

        yield break;
    }

    private IEnumerator Co_TryContinueWithRewardedWait()
    {
        if (gameOverPanelInstance != null)
        {
            gameOverPanelInstance.SetInfo("LOADING AD...\n(Rewarded)");
            gameOverPanelInstance.SetButtonsInteractable(false);
        }

        if (AdManager.I == null)
        {
            if (gameOverPanelInstance != null)
            {
                gameOverPanelInstance.SetInfo("AD NOT AVAILABLE.\nTry again.");
                gameOverPanelInstance.SetButtonsInteractable(true);
            }
            yield return new WaitForSecondsRealtime(0.2f);
            yield break;
        }

        AdManager.I.RequestRewardedReload();

        float t = 0f;
        float nextTick = 0f;

        while (!AdManager.I.IsRewardedReady && t < rewardedWaitTimeout)
        {
            if (!AdManager.I.IsRewardedLoading && AdManager.I.RewardedLoadFailed)
                break;

            t += Time.unscaledDeltaTime;

            if (gameOverPanelInstance != null && t >= nextTick)
            {
                float remain = Mathf.Max(0f, rewardedWaitTimeout - t);
                gameOverPanelInstance.SetInfo($"LOADING AD...\n{remain:0.0}s");
                nextTick = t + waitTick;
            }

            yield return null;
        }

        if (!AdManager.I.IsRewardedReady)
        {
            string err = AdManager.I.LastRewardedLoadError;
            Debug.LogWarning($"[ADS] Rewarded NOT READY → 재시도(게임 재개 금지) / err={err}");

            if (gameOverPanelInstance != null)
            {
                gameOverPanelInstance.SetInfo("AD NOT READY.\nPlease try again.");
                gameOverPanelInstance.SetButtonsInteractable(true);
            }
            yield return new WaitForSecondsRealtime(0.2f);
            yield break;
        }

        if (gameOverPanelInstance != null)
        {
            gameOverPanelInstance.SetInfo("SHOWING AD...\n(Rewarded)");
            gameOverPanelInstance.SetButtonsInteractable(false);
        }

        bool done = false;
        bool success = false;

        AdManager.I.ShowRewarded(ok =>
        {
            success = ok;
            done = true;
        }, "Continue_Unlimited");

        while (!done) yield return null;

        if (success)
        {
            // 아직 스폰/재개는 하지 않는다. (Shop 여부 결정 후에 한다)
            Lives = Mathf.Max(1, continueLives);
            UpdateLivesUI();

            isGameOver = false;
            isStageRunning = true;
            isRespawning = false;

            _continueSucceededThisFlow = true;

            // Shop 제안/오버레이
            yield return Co_PostContinueStep();

            // 이제 진짜로 재개 + 스폰
            ForcePause(false);
            yield return new WaitForSecondsRealtime(0.05f);
            SpawnPlayer();

            Debug.Log("[GAME] Continue 성공 → 부활 후 재개 (UNLIMITED)");
            yield break;
        }

        Debug.LogWarning("[ADS] Rewarded 실패/미제공 → 패널 유지 + 재시도 (게임 재개 금지)");
        if (gameOverPanelInstance != null)
        {
            gameOverPanelInstance.SetInfo("AD FAILED.\nPlease try again.");
            gameOverPanelInstance.SetButtonsInteractable(true);
        }
        yield break;
    }

    private IEnumerator Co_MenuWithInterstitialWaitThenGoMenu()
    {
        if (gameOverPanelInstance != null)
        {
            gameOverPanelInstance.SetInfo("LOADING AD...\n(Interstitial)");
            gameOverPanelInstance.SetButtonsInteractable(false);
        }

        if (AdManager.I != null)
            AdManager.I.RequestInterstitialReload();

        float t = 0f;
        float nextTick = 0f;

        while (AdManager.I != null && !AdManager.I.IsInterstitialReady && t < interstitialWaitTimeout)
        {
            if (!AdManager.I.IsInterstitialLoading && AdManager.I.InterstitialLoadFailed)
                break;

            t += Time.unscaledDeltaTime;

            if (gameOverPanelInstance != null && t >= nextTick)
            {
                float remain = Mathf.Max(0f, interstitialWaitTimeout - t);
                gameOverPanelInstance.SetInfo($"LOADING AD...\n{remain:0.0}s");
                nextTick = t + waitTick;
            }

            yield return null;
        }

        if (AdManager.I != null && AdManager.I.IsInterstitialReady)
        {
            if (gameOverPanelInstance != null)
                gameOverPanelInstance.SetInfo("SHOWING AD...\n(Interstitial)");

            bool done = false;
            AdManager.I.ShowInterstitial(ok => { done = true; }, "MenuToMenu_Interstitial");
            while (!done) yield return null;
        }
        else
        {
            Debug.LogWarning("[ADS] Interstitial NOT READY → 광고 스킵하고 메뉴 이동");
        }

        ForcePause(false);
        DestroyGameOverPanelInstance();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnPanelContinue()
    {
        _choice = GameOverChoice.Continue;
    }

    private void OnPanelMenu()
    {
        _choice = GameOverChoice.Menu;
    }

    private void EnsureGameOverPanel()
    {
        if (!IsStageScene(SceneManager.GetActiveScene().name))
            return;

        if (gameOverPanelInstance != null)
            return;

        var existing = FindObjectsOfType<GameOverPanel>(true);
        if (existing != null && existing.Length > 0)
        {
            gameOverPanelInstance = existing[0];

            if (existing.Length > 1)
            {
                for (int i = 1; i < existing.Length; i++)
                {
                    if (existing[i] != null && existing[i].gameObject != null)
                        Destroy(existing[i].gameObject);
                }
                Debug.LogWarning($"[GameManager] GameOverPanel 중복 감지({existing.Length}) → 1개만 유지하고 제거했습니다.");
            }

            gameOverPanelInstance.OnContinueClicked -= OnPanelContinue;
            gameOverPanelInstance.OnMenuClicked -= OnPanelMenu;
            gameOverPanelInstance.OnContinueClicked += OnPanelContinue;
            gameOverPanelInstance.OnMenuClicked += OnPanelMenu;

            gameOverPanelInstance.Hide();

            Debug.Log("[GameManager] 씬에 존재하는 GameOverPanel을 사용합니다.");
            return;
        }

        var canvas = FindBestCanvasInScene();
        if (canvas == null)
        {
            Debug.LogError("[GameManager] Stage 씬에서 UI Canvas를 찾지 못했습니다.");
            return;
        }

        var prefab = Resources.Load<GameObject>(gameOverPanelResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[GameManager] Resources에서 GameOverPanel 프리팹을 찾지 못했습니다: {gameOverPanelResourcePath}");
            return;
        }

        var go = Instantiate(prefab, canvas.transform);
        go.name = prefab.name;

        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();
        }

        gameOverPanelInstance = go.GetComponent<GameOverPanel>();
        if (gameOverPanelInstance == null)
        {
            Debug.LogError("[GameManager] GameOverPanel 컴포넌트를 찾지 못했습니다.");
            return;
        }

        gameOverPanelInstance.OnContinueClicked -= OnPanelContinue;
        gameOverPanelInstance.OnMenuClicked -= OnPanelMenu;
        gameOverPanelInstance.OnContinueClicked += OnPanelContinue;
        gameOverPanelInstance.OnMenuClicked += OnPanelMenu;

        gameOverPanelInstance.Hide();
        Debug.Log($"[GameManager] GameOverPanel 생성 완료 (parent={canvas.name})");
    }

    private Canvas FindBestCanvasInScene()
    {
        var canvases = FindObjectsOfType<Canvas>(true);
        Canvas best = null;
        int bestScore = -999;

        foreach (var c in canvases)
        {
            if (c == null) continue;

            int score = 0;
            if (c.renderMode != RenderMode.WorldSpace) score += 10;
            if (c.GetComponent<CanvasScaler>() != null) score += 50;
            if (c.GetComponent<GraphicRaycaster>() != null) score += 50;
            score += c.sortingOrder;

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    private void DestroyGameOverPanelInstance()
    {
        if (gameOverPanelInstance == null) return;
        Destroy(gameOverPanelInstance.gameObject);
        gameOverPanelInstance = null;
    }

    IEnumerator ShowMessageFor(string msg, float time)
    {
        if (messageText != null)
        {
            messageText.text = msg;
            yield return new WaitForSeconds(time);
            messageText.text = "";
        }
        else
        {
            Debug.Log(msg);
            yield return new WaitForSeconds(time);
        }
    }

    IEnumerator ShowMessageForRealtime(string msg, float time)
    {
        if (messageText != null)
        {
            messageText.text = msg;
            yield return new WaitForSecondsRealtime(time);
            messageText.text = "";
        }
        else
        {
            Debug.Log(msg);
            yield return new WaitForSecondsRealtime(time);
        }
    }

    void UpdateLivesUI()
    {
        if (livesText != null)
            livesText.text = $"LIVES: {Lives}/{maxLives}";
    }

    void UpdateStageUI()
    {
        if (stageText != null)
            stageText.text = $"STAGE: {CurrentStage}";
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"SCORE: {score} / {scoreToClearStage}";
    }

    // 코인 UI 갱신
    void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = $"COIN: {CosmeticSaveManager.GetCoins()}";
    }

    private void KillAllPlayers()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
            Destroy(p);
    }

    private void KillAllEnemiesAndBullets_Fallback()
    {
        foreach (var e in GameObject.FindGameObjectsWithTag("Enemy"))
            Destroy(e);

        foreach (var b in GameObject.FindGameObjectsWithTag("Bullet"))
            Destroy(b);

        foreach (var eb in GameObject.FindGameObjectsWithTag("EnemyBullet"))
            Destroy(eb);

        try
        {
            foreach (var it in GameObject.FindGameObjectsWithTag("Item"))
                Destroy(it);
        }
        catch (UnityException) { }

        aliveEnemyCount = 0;
    }

    public void ShowSpeedUpMessage(float time = 1.0f)
    {
        StartCoroutine(ShowMessageFor("SPEED UP!", time));
    }

    // ============================================================
    //  Cosmetics Unlock (Stage Clear)
    // ============================================================

    private void EnsureCosmeticDb()
    {
        if (_cosmeticDbCache != null) return;
        if (string.IsNullOrEmpty(cosmeticDbResourcePath)) return;

        _cosmeticDbCache = Resources.Load<CosmeticDatabase>(cosmeticDbResourcePath);
        if (_cosmeticDbCache == null)
            Debug.LogWarning($"[GameManager] CosmeticDatabase 로드 실패: Resources/{cosmeticDbResourcePath}");
    }

    private void EnsureUnlockPopup()
    {
        if (string.IsNullOrEmpty(unlockPopupResourcePath)) return;
        if (!IsStageScene(SceneManager.GetActiveScene().name)) return;

        var canvas = FindBestCanvasInScene();
        if (canvas == null)
        {
            Debug.LogError("[GameManager] UnlockPopup용 Canvas를 찾지 못했습니다.");
            return;
        }

        // 이미 있으면: 현재 Canvas 밑으로 재부착
        if (unlockPopupInScene != null)
        {
            if (unlockPopupInScene.transform.parent != canvas.transform)
                unlockPopupInScene.transform.SetParent(canvas.transform, false);
            return;
        }

        // 없으면: 로드해서 생성
        var prefab = Resources.Load<CosmeticUnlockPopup>(unlockPopupResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[GameManager] UnlockPopup prefab 로드 실패: Resources/{unlockPopupResourcePath}");
            return;
        }

        unlockPopupInScene = Instantiate(prefab, canvas.transform);
        DontDestroyOnLoad(unlockPopupInScene.gameObject);
    }

    private List<CosmeticItem> GrantStageUnlocksAndGetNew(int clearedStage)
    {
        EnsureCosmeticDb();
        var newly = new List<CosmeticItem>();
        if (_cosmeticDbCache == null) return newly;

        var list = _cosmeticDbCache.GetUnlocksForStageClear(clearedStage);
        if (list == null || list.Count == 0) return newly;

        foreach (var it in list)
        {
            if (it == null) continue;
            if (string.IsNullOrEmpty(it.id)) continue;

            // unlockOnStageClear <= 0 은 기본 Unlocked
            if (it.unlockOnStageClear <= 0) continue;

            // 이미 Unlocked/Owned면 새로 해금된 게 아님
            if (CosmeticSaveManager.IsUnlocked(it.id) || CosmeticSaveManager.IsOwned(it.id))
                continue;

            CosmeticSaveManager.GrantUnlocked(it.id);
            newly.Add(it);
        }

        return newly;
    }

    private IEnumerator Co_PlayUnlockPresentation(List<CosmeticItem> newlyUnlocked)
    {
        if (newlyUnlocked == null || newlyUnlocked.Count == 0) yield break;

        EnsureUnlockPopup();

        foreach (var it in newlyUnlocked)
        {
            if (it == null) continue;

            if (uiAudioSource != null && unlockSfx != null)
                uiAudioSource.PlayOneShot(unlockSfx);

            if (unlockPopupInScene != null)
            {
                yield return unlockPopupInScene.Play(it, unlockMessageTime);
            }
            else
            {
                // 팝업 프리팹이 없으면 MessageText로라도 보상 느낌 메시지
                string name = string.IsNullOrWhiteSpace(it.displayName) ? it.id : it.displayName;
                yield return ShowMessageFor($"NEW SKIN UNLOCKED!\n{name}", unlockMessageTime);
            }
        }
    }
}
