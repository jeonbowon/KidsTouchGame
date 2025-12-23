using System;
using System.Collections;
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

    [Header("Score / Stage Clear")]
    [SerializeField] private int scoreToClearStage = 100;

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

    // Continue는 이번 런에서 1번만
    private bool bonusContinueUsed = false;

    // GameOver 처리 중 중복 방지
    private bool isHandlingGameOverFlow = false;

    private enum GameOverChoice { None, Continue, Menu }
    private GameOverChoice _choice = GameOverChoice.None;

    // 완전정지 보장용
    private float _defaultFixedDeltaTime = 0.02f;

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
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private bool IsStageScene(string sceneName) => sceneName.StartsWith("Stage");

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines();
        ForcePause(false);

        EnsureAdManagerExists();

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
            Time.fixedDeltaTime = 0f;          // 물리까지 확실히 멈춤
            AudioListener.pause = true;        // 오디오도 멈춤(선택이지만 “완전정지” 체감에 좋음)
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

        bonusContinueUsed = false;
        isHandlingGameOverFlow = false;
        _choice = GameOverChoice.None;

        UpdateLivesUI();
        UpdateStageUI();
        UpdateScoreUI();
        if (messageText != null) messageText.text = "";

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

        bonusContinueUsed = false;
        isHandlingGameOverFlow = false;
        _choice = GameOverChoice.None;

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

        KillAllEnemiesAndBullets_Fallback();

        yield return ShowMessageFor("STAGE CLEAR", 1.5f);

        if (CurrentStage < maxStage)
        {
            CurrentStage++;
            UpdateStageUI();

            score = 0;
            UpdateScoreUI();

            string nextSceneName = $"Stage{CurrentStage}";
            Debug.Log($"[GameManager] 다음 스테이지 로드: {nextSceneName}");
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            yield return ShowMessageFor("ALL STAGES CLEAR!", 1.8f);

            ForcePause(false);
            Debug.Log("[GameManager] 모든 스테이지 클리어 → 메인메뉴 로드");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public void OnPlayerDied()
    {
        if (isGameOver) return;
        if (isRespawning) return;

        int before = Lives;
        Lives--;
        if (Lives < 0) Lives = 0;

        Debug.Log($"[GameManager] OnPlayerDied() Lives {before} -> {Lives}");

        UpdateLivesUI();

        if (Lives <= 0)
        {
            Debug.Log("[GameManager] Lives 0 → GameOver");
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
        if (playerPrefab == null)
        {
            Debug.LogError("[GameManager] Player 프리팹이 비어있습니다.");
            return;
        }
        if (playerSpawnPoint == null)
        {
            Debug.LogError("[GameManager] Player SpawnPoint가 비어있습니다.");
            return;
        }

        var go = Instantiate(playerPrefab, playerSpawnPoint.position, Quaternion.identity);
        Debug.Log($"[GameManager] Player 스폰: {go.name} @ {playerSpawnPoint.position}");

        var ph = go.GetComponent<PlayerHealth>();
        if (ph != null && respawnInvincibleTime > 0f)
        {
            ph.BeginInvincibility(respawnInvincibleTime);
        }
    }

    IEnumerator Co_GameOver()
    {
        if (isGameOver) yield break;

        isGameOver = true;
        isStageRunning = false;
        isStageClearing = false;
        isRespawning = false;

        Debug.Log($"[GameManager] Co_GameOver 진입 (bonusContinueUsed={bonusContinueUsed})");

        KillAllPlayers();
        EnsureGameOverPanel();

        // ✅ 완전 정지
        ForcePause(true);

        // 첫 GameOver: 선택 UI + Continue 재시도(실패해도 메뉴로 튕기지 않음)
        if (!bonusContinueUsed)
        {
            if (!isHandlingGameOverFlow)
                StartCoroutine(Co_FirstGameOverChoiceFlow());
            yield break;
        }

        // 두번째 GameOver: 선택 없이 전면광고 시도 후 메뉴
        if (!isHandlingGameOverFlow)
            StartCoroutine(Co_SecondGameOverAdsThenMenuFlow());

        yield break;
    }

    // ─────────────────────────────────────────────────────────────
    // 첫 GameOver: Continue/Menu 선택
    private IEnumerator Co_FirstGameOverChoiceFlow()
    {
        isHandlingGameOverFlow = true;

        while (true)
        {
            _choice = GameOverChoice.None;

            if (gameOverPanelInstance == null)
            {
                Debug.LogWarning("[GameManager] GameOverPanel 없음 → 강제 메뉴");
                ForcePause(false);
                SceneManager.LoadScene(mainMenuSceneName);
                isHandlingGameOverFlow = false;
                yield break;
            }

            gameOverPanelInstance.Show("GAME OVER\nCONTINUE or MENU?", showButtons: true);
            gameOverPanelInstance.SetButtonsInteractable(true);

            while (_choice == GameOverChoice.None)
                yield return null;

            // MENU
            if (_choice == GameOverChoice.Menu)
            {
                yield return Co_MenuWithInterstitialWaitThenGoMenu();
                isHandlingGameOverFlow = false;
                yield break;
            }

            // CONTINUE (실패하면 패널 유지 + 안내 + 버튼 다시 활성화, 게임 재개 금지)
            yield return Co_TryContinueWithRewardedWait();
            // 성공하면 코루틴 내부에서 isHandlingGameOverFlow=false 후 종료
        }
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
            Debug.LogWarning("[ADS] AdManager 없음 → Continue 불가(재시도 UI)");
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
            Debug.LogWarning("[ADS] Rewarded NOT READY (timeout) → 재시도(게임 재개 금지)");
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
        }, "FirstContinue");

        while (!done) yield return null;

        if (success)
        {
            bonusContinueUsed = true;

            Lives = Mathf.Max(1, continueLives);
            UpdateLivesUI();

            // ✅ 성공한 경우에만 재개
            isGameOver = false;
            isStageRunning = true;
            isRespawning = false;

            if (gameOverPanelInstance != null)
                gameOverPanelInstance.Hide();

            ForcePause(false);

            yield return new WaitForSecondsRealtime(0.05f);
            SpawnPlayer();

            Debug.Log("[GAME] Continue 성공 → 부활 후 재개");
            isHandlingGameOverFlow = false;
            yield break;
        }

        // ❌ 실패: 절대 재개하면 안 됨
        Debug.LogWarning("[ADS] Rewarded 실패/미제공 → 패널 유지 + 재시도 (게임 재개 금지)");
        if (gameOverPanelInstance != null)
        {
            gameOverPanelInstance.SetInfo("AD FAILED.\nPlease try again.");
            gameOverPanelInstance.SetButtonsInteractable(true);
        }
        yield return new WaitForSecondsRealtime(0.2f);
        yield break;
    }

    // MENU: Interstitial 준비될 때까지 기다렸다가 보여주고, 아니면 스킵하고 메뉴
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
            t += Time.unscaledDeltaTime;

            if (gameOverPanelInstance != null && t >= nextTick)
            {
                float remain = Mathf.Max(0f, interstitialWaitTimeout - t);
                gameOverPanelInstance.SetInfo($"LOADING AD...\n{remain:0.0}s");
                nextTick = t + waitTick;
            }

            yield return null;
        }

        bool showOk = false;

        if (AdManager.I != null && AdManager.I.IsInterstitialReady)
        {
            if (gameOverPanelInstance != null)
                gameOverPanelInstance.SetInfo("SHOWING AD...\n(Interstitial)");

            bool done = false;
            AdManager.I.ShowInterstitial(ok =>
            {
                showOk = ok;
                done = true;
            }, "FirstMenuToMenu_Interstitial");

            while (!done) yield return null;
        }
        else
        {
            Debug.LogWarning("[ADS] Interstitial NOT READY → 광고 스킵하고 메뉴 이동");
        }

        ForcePause(false);
        DestroyGameOverPanelInstance();

        Debug.Log($"[GAME] Menu 이동 (interstitialShown={showOk})");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // 두번째 GameOver: 선택 없이 광고 시도 후 메뉴
    private IEnumerator Co_SecondGameOverAdsThenMenuFlow()
    {
        isHandlingGameOverFlow = true;

        if (gameOverPanelInstance != null)
            gameOverPanelInstance.Show("GAME OVER", showButtons: false);

        if (AdManager.I != null)
            AdManager.I.RequestInterstitialReload();

        float t = 0f;
        while (AdManager.I != null && !AdManager.I.IsInterstitialReady && t < interstitialWaitTimeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (AdManager.I != null && AdManager.I.IsInterstitialReady)
        {
            bool done = false;
            AdManager.I.ShowInterstitial(_ => done = true, "SecondDeathToMenu_Interstitial");
            while (!done) yield return null;
        }

        ForcePause(false);
        DestroyGameOverPanelInstance();
        SceneManager.LoadScene(mainMenuSceneName);

        isHandlingGameOverFlow = false;
    }

    // ─────────────────────────────────────────────────────────────
    // GameOverPanel 버튼 콜백
    private void OnPanelContinue()
    {
        if (!isGameOver) return;
        if (bonusContinueUsed) return;
        Debug.Log("[UI] Continue 버튼 클릭");
        _choice = GameOverChoice.Continue;
    }

    private void OnPanelMenu()
    {
        if (!isGameOver) return;
        Debug.Log("[UI] Menu 버튼 클릭");
        _choice = GameOverChoice.Menu;
    }

    private void EnsureGameOverPanel()
    {
        if (!IsStageScene(SceneManager.GetActiveScene().name))
            return;

        if (gameOverPanelInstance != null)
            return;

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
}
