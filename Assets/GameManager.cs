using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Gameplay")]
    [SerializeField] private int maxLives = 3;            // 총 목숨
    [SerializeField] private float respawnDelay = 1.2f;   // 사망 후 리스폰 지연
    [SerializeField] private float stageMessageTime = 1.2f;

    [Header("Prefabs/Refs")]
    [SerializeField] private GameObject playerPrefab;     // MainMenu 씬의 GameManager에 연결 추천
    [SerializeField] private Transform playerSpawnPoint;  // Stage 씬 입장 시 태그로 재바인딩
    [SerializeField] private FormationSpawner formationSpawner; // Stage 씬 입장 시 재바인딩

    [Header("UI (optional)")]
    [SerializeField] private TMP_Text livesText;   // Stage 씬 입장 시 재바인딩 가능
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text messageText;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "MainMenu"; // Build Settings 등록 필수
    [SerializeField] private string firstStageSceneName = "Stage1";  // 첫 스테이지 이름

    [Header("Fallback (Optional)")]
    [Tooltip("playerPrefab이 비어있으면 Resources.Load로 시도할 경로 (예: \"Player\")")]
    [SerializeField] private string playerPrefabResourcePath = "";

    public int CurrentStage { get; private set; } = 1;
    public int Lives { get; private set; }

    private int aliveEnemyCount = 0;
    private bool isGameOver = false;
    private bool isStageRunning = false;
    private bool isRespawning = false; // 리스폰 중복 방지

    // ─────────────────────────────────────────────────────────────
    // Player Bullet (배율)
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

    // Enemy Bullet (절대 속도)
    [Header("Enemy Bullet (절대 속도)")]
    [SerializeField] private float enemyBulletSpeedStage1 = 3.5f;
    [SerializeField] private float enemyBulletSpeedPerStage = 0.6f;
    [SerializeField] private Vector2 enemyBulletSpeedClamp = new Vector2(0.5f, 10f);

    public float GetEnemyBulletSpeed()
    {
        float s = enemyBulletSpeedStage1 + (CurrentStage - 1) * enemyBulletSpeedPerStage;
        return Mathf.Clamp(s, enemyBulletSpeedClamp.x, enemyBulletSpeedClamp.y);
    }

    // ─────────────────────────────────────────────────────────────
    // 수명주기 & 씬 전환 제어
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

        // 에디터에서 MainMenu가 아닌 Stage 씬으로 바로 플레이할 수도 있으니
        // 참조는 OnSceneLoaded에서 재바인딩한다.
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private bool IsStageScene(string sceneName) => sceneName.StartsWith("Stage");

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllCoroutines(); // 씬 전환 시 이전 코루틴 정지

        if (IsStageScene(scene.name))
        {
            RebindStageSceneObjects();
            StartCoroutine(Co_StartStage(CurrentStage));
        }
        else
        {
            // 메인메뉴 등
            isStageRunning = false;
            // 혹시 잔존 오브젝트가 DontDestroy 등에 걸려 남았다면 정리
            KillAllPlayers();
        }
    }

    private void RebindStageSceneObjects()
    {
        // FormationSpawner 재바인딩
#if UNITY_2022_2_OR_NEWER
        formationSpawner = FindFirstObjectByType<FormationSpawner>();
#else
        formationSpawner = FindObjectOfType<FormationSpawner>();
#endif
        if (formationSpawner == null)
            Debug.LogWarning("[GameManager] FormationSpawner를 찾지 못했습니다.");

        // 스폰 포인트 재바인딩 (태그 기반)
        var spawnGo = GameObject.FindWithTag("PlayerSpawn");
        playerSpawnPoint = (spawnGo != null) ? spawnGo.transform : null;
        if (playerSpawnPoint == null)
            Debug.LogError("[GameManager] Stage 씬에서 태그 'PlayerSpawn'을 찾지 못했습니다.");

        // UI 재바인딩 (옵션) — 필요 시 이름/태그로 찾아서 채워줌
        if (livesText == null)
            livesText = SafeFindTMP("LivesText");
        if (stageText == null)
            stageText = SafeFindTMP("StageText");
        if (messageText == null)
            messageText = SafeFindTMP("MessageText");

        // Prefab 비어있으면 Resources로 보충 (옵션)
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
    }

    private TMP_Text SafeFindTMP(string name)
    {
        var go = GameObject.Find(name);
        return go ? go.GetComponent<TMP_Text>() : null;
    }

    void Start()
    {
        Lives = maxLives;
        UpdateLivesUI();
        UpdateStageUI();
        if (messageText != null) messageText.text = "";

        // 에디터에서 Stage 씬을 바로 플레이하는 경우 대비
        if (IsStageScene(SceneManager.GetActiveScene().name))
            StartCoroutine(Co_StartStage(CurrentStage));
    }

    // 메인메뉴의 Play 버튼에서 호출
    public void NewRun()
    {
        Debug.Log("[GameManager] NewRun()");
        isGameOver = false;
        isStageRunning = false;
        isRespawning = false;

        KillAllPlayers(); // 혹시 남아있으면 정리

        Lives = maxLives;
        CurrentStage = 1;
        UpdateLivesUI();
        UpdateStageUI();

        Time.timeScale = 1f; // 혹시 변경되어 있으면 정상화

        SceneManager.LoadScene(firstStageSceneName);
    }

    IEnumerator Co_StartStage(int stage)
    {
        isStageRunning = false;

        yield return ShowMessageFor($"STAGE {stage}", stageMessageTime);

        if (formationSpawner != null)
        {
            aliveEnemyCount = formationSpawner.SpawnFormation(stage);
            Debug.Log($"[GameManager] Formation Spawn 완료. aliveEnemyCount={aliveEnemyCount}");
        }
        else
        {
            aliveEnemyCount = 0;
            Debug.LogWarning("[GameManager] formationSpawner가 없어 적 스폰을 건너뜁니다.");
        }

        isStageRunning = true;

        // 🔸 씬 전환 직후 한 프레임 대기(부모 활성 토글/세팅 경합 방지)
        yield return null;

        // 🔸 시작할 때는 기존 Player를 전부 지우고, 새로 스폰(조건 검사 X)
        KillAllPlayers();
        isGameOver = false;
        isRespawning = false;
        SpawnPlayer();    // ← 무조건 호출
    }


    // 플레이어 사망 보고 (플레이어에서 호출)
    public void OnPlayerDied()
    {
        if (isGameOver) return;
        if (isRespawning) return;

        Lives--;
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
            Debug.LogError("[GameManager] Player 프리팹이 비어있습니다. (MainMenu 씬의 GameManager에 연결하거나 Resources 사용)");
            return;
        }
        if (playerSpawnPoint == null)
        {
            Debug.LogError("[GameManager] Player SpawnPoint가 비어있습니다. (Stage 씬에 태그 'PlayerSpawn' 오브젝트 필요)");
            return;
        }

        var go = Instantiate(playerPrefab, playerSpawnPoint.position, Quaternion.identity);
        Debug.Log($"[GameManager] Player 스폰: {go.name} @ {playerSpawnPoint.position}");
    }

    // 적 사망 보고
    public void OnEnemyKilled()
    {
        if (!isStageRunning || isGameOver) return;

        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);

        if (aliveEnemyCount == 0)
            StartCoroutine(Co_StageClear());
    }

    IEnumerator Co_StageClear()
    {
        isStageRunning = false;

        yield return ShowMessageFor($"STAGE {CurrentStage} CLEAR!", stageMessageTime);

        CurrentStage++;
        UpdateStageUI();

        yield return StartCoroutine(Co_StartStage(CurrentStage));
    }

    IEnumerator Co_GameOver()
    {
        isGameOver = true;
        isStageRunning = false;

        // 🔸 먼저 몽땅 정리
        KillAllPlayers();

        if (formationSpawner != null) formationSpawner.KillAllEnemiesAndBullets();
        else KillAllEnemiesAndBullets_Fallback();

        yield return ShowMessageFor("GAME OVER", 1.5f);

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }


    // 메시지 잠깐 띄우는 공용 루틴
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

    // ─────────────────────────────────────────────────────────────
    // 보조 정리/진단 함수
    private void KillAllPlayers()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players) Destroy(p);
    }

    private void KillAllEnemiesAndBullets_Fallback()
    {
        // 스포너가 없어도 안전하게 정리 (프로젝트 태그/레이어에 맞게 조정 가능)
        foreach (var e in GameObject.FindGameObjectsWithTag("Enemy")) Destroy(e);
        foreach (var b in GameObject.FindGameObjectsWithTag("Bullet")) Destroy(b);
        foreach (var eb in GameObject.FindGameObjectsWithTag("EnemyBullet")) Destroy(eb);
    }
}
