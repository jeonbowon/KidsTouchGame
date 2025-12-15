using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI; // CanvasScaler, GraphicRaycaster

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Gameplay")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float respawnDelay = 1.2f;
    [SerializeField] private float stageMessageTime = 1.2f;

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
    [SerializeField] private int maxStage = 5;

    [Header("Fallback (Optional)")]
    [SerializeField] private string playerPrefabResourcePath = "";

    [Header("GameOver UI (Resources)")]
    [SerializeField] private string gameOverPanelResourcePath = "GameOverPanel";
    private GameOverPanel gameOverPanelInstance;

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
        return Mathf.Clamp(s, enemyBulletSpeedClamp.x, enemyBulletSpeedClamp.y);
    }

    // ✅ Continue(+1) 광고는 이번 런에서 딱 1번만
    private bool bonusContinueUsed = false;

    // ✅ GameOver 처리 중 중복 방지
    private bool isHandlingGameOverFlow = false;

    // ✅ 첫 GameOver에서 선택을 기다리기 위한 변수
    private enum GameOverChoice { None, Continue, Menu }
    private GameOverChoice _choice = GameOverChoice.None;

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

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
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

        Time.timeScale = 1f;

        EnsureAdManagerExists();

        if (IsStageScene(scene.name))
        {
            RebindStageSceneObjects();
            EnsureGameOverPanel();
            StartCoroutine(Co_StartStage(CurrentStage));
        }
        else
        {
            isStageRunning = false;
            KillAllPlayers();
            DestroyGameOverPanelInstance();
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

        Time.timeScale = 1f;

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
    public void OnEnemyKilled()
    {
        if (!isStageRunning || isGameOver) return;
        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
    }

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

            Time.timeScale = 1f;
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
            Debug.Log("[GameManager] Lives 0 → GameOver 코루틴 시작");
            StartCoroutine(Co_GameOver());
            return;
        }

        StartCoroutine(Co_RespawnPlayerWithMessage());
    }

    IEnumerator Co_RespawnPlayerWithMessage()
    {
        isRespawning = true;

        if (messageText != null)
            yield return ShowMessageFor($"LIVES : {Lives}/{maxLives}", 1.1f);
        else
            yield return new WaitForSeconds(1.1f);

        yield return new WaitForSeconds(respawnDelay);

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

        Time.timeScale = 0f;

        // ✅ 1) 첫 GameOver: 선택 UI를 띄우고 기다린다 (자동 광고 금지)
        if (!bonusContinueUsed)
        {
            if (!isHandlingGameOverFlow)
                StartCoroutine(Co_FirstGameOverChoiceFlow());
            yield break;
        }

        // ✅ 2) 두번째 GameOver(보너스 +1까지 사용 후): 선택 없이 광고 후 무조건 Menu
        if (!isHandlingGameOverFlow)
            StartCoroutine(Co_SecondGameOverAdsThenMenuFlow());

        yield break;
    }

    // ─────────────────────────────────────────────────────────────
    // ✅ 첫 GameOver: Continue/Menu 선택
    private IEnumerator Co_FirstGameOverChoiceFlow()
    {
        isHandlingGameOverFlow = true;
        _choice = GameOverChoice.None;

        if (gameOverPanelInstance != null)
        {
            gameOverPanelInstance.Show("GAME OVER\nCONTINUE or MENU?", showButtons: true);
        }
        else
        {
            Debug.LogWarning("[GameManager] GameOverPanel 인스턴스가 없습니다. → 강제 메뉴 이동");
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
            isHandlingGameOverFlow = false;
            yield break;
        }

        Debug.Log("[GAME] First GameOver: 선택 대기 시작");

        // 버튼 클릭을 기다림 (timeScale=0이어도 UI 입력은 됨)
        while (_choice == GameOverChoice.None)
            yield return null;

        Debug.Log($"[GAME] First GameOver: 선택됨 = {_choice}");

        if (_choice == GameOverChoice.Menu)
        {
            Time.timeScale = 1f;
            DestroyGameOverPanelInstance();
            Debug.Log("[GAME] Menu 선택 → MainMenu 로드");
            SceneManager.LoadScene(mainMenuSceneName);
            isHandlingGameOverFlow = false;
            yield break;
        }

        // Continue 선택
        if (gameOverPanelInstance != null)
            gameOverPanelInstance.Show("SHOWING AD...\n(Rewarded)", showButtons: false);

        yield return new WaitForSecondsRealtime(0.2f);

        bool done = false;
        bool success = false;

        // 광고 로그를 확실히 남김
        //Debug.Log($"[ADS] (First Continue) ShowRewarded 요청 / IsReady={(AdManager.I != null ? AdManager.I.IsReady.ToString() : "null")}");
        Debug.Log($"[ADS] (First Continue) ShowRewarded 요청 / RewardedReady={(AdManager.I != null ? AdManager.I.IsRewardedReady.ToString() : "null")}");


        if (AdManager.I != null)
        {
            AdManager.I.ShowRewarded(ok =>
            {
                success = ok;
                done = true;
            }, "FirstContinue");
        }
        else
        {
            Debug.LogWarning("[ADS] AdManager 없음 → 광고 실패");
            done = true;
            success = false;
        }

        while (!done) yield return null;

        Debug.Log($"[ADS] (First Continue) 종료 success={success}");

        if (success)
        {
            bonusContinueUsed = true;

            Lives = 1; // ✅ 반드시 +1로 시작
            UpdateLivesUI();

            Time.timeScale = 1f;

            isGameOver = false;
            isStageRunning = true;
            isRespawning = false;

            if (gameOverPanelInstance != null)
                gameOverPanelInstance.Hide();

            yield return new WaitForSeconds(0.05f);
            SpawnPlayer();

            Debug.Log("[GAME] Continue 성공 → +1 부활 완료 (bonusContinueUsed=true)");
            isHandlingGameOverFlow = false;
            yield break;
        }

        // 광고 실패면: 첫 GameOver라도 멈춰있게 두지 말고 메뉴로 보냄
        Time.timeScale = 1f;
        DestroyGameOverPanelInstance();
        Debug.Log("[GAME] Continue 광고 실패 → MainMenu 로드");
        SceneManager.LoadScene(mainMenuSceneName);
        isHandlingGameOverFlow = false;
    }

    // ─────────────────────────────────────────────────────────────
    // 두번째 GameOver: 선택 없이 광고 시도 후 무조건 메뉴
    private IEnumerator Co_SecondGameOverAdsThenMenuFlow()
    {
        isHandlingGameOverFlow = true;

        if (gameOverPanelInstance != null)
            gameOverPanelInstance.Show("GAME OVER", showButtons: false);

        yield return new WaitForSecondsRealtime(0.2f);

        bool done = false;

        Debug.Log($"[ADS] (SecondDeath) ShowInterstitial 요청 / InterstitialReady={(AdManager.I != null ? AdManager.I.IsInterstitialReady.ToString() : "null")}");

        if (AdManager.I != null)
        {
            AdManager.I.ShowInterstitial(ok =>
            {
                done = true; // ok는 로그로만 봐도 됨
            }, "SecondDeathToMenu_Interstitial");
        }
        else
        {
            Debug.LogWarning("[ADS] AdManager 없음 → 광고 스킵 후 MainMenu 이동");
            done = true;
        }

        while (!done) yield return null;

        Debug.Log("[ADS] (SecondDeath) 종료 → MainMenu 이동");

        Time.timeScale = 1f;
        DestroyGameOverPanelInstance();
        SceneManager.LoadScene(mainMenuSceneName);

        isHandlingGameOverFlow = false;
    }


    // ─────────────────────────────────────────────────────────────
    // GameOverPanel 버튼 콜백
    private void OnPanelContinue()
    {
        if (!isGameOver) return;
        if (bonusContinueUsed) return; // 두번째 이후엔 선택 자체가 없어야 함
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

        // ✅ 패널 버튼 이벤트 연결
        gameOverPanelInstance.OnContinueClicked -= OnPanelContinue;
        gameOverPanelInstance.OnMenuClicked -= OnPanelMenu;
        gameOverPanelInstance.OnContinueClicked += OnPanelContinue;
        gameOverPanelInstance.OnMenuClicked += OnPanelMenu;

        gameOverPanelInstance.Hide();
        Debug.Log($"[GameManager] GameOverPanel 생성 완료 (parent canvas={canvas.name})");
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
