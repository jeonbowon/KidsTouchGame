using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("기본 적 스폰 설정")]
    [Tooltip("일반 적 프리팹 (EnemyGalaga 스크립트가 붙어있는 프리팹)")]
    public GameObject enemyPrefab;

    [Tooltip("Stage1에서의 기본 생성 간격(초)")]
    public float spawnInterval = 2f;

    [Tooltip("스테이지가 1 올라갈 때마다 생성 간격에 더해질 값 (음수면 더 자주 나옴)")]
    public float spawnIntervalPerStage = -0.3f;

    [Tooltip("생성 간격 최소/최대(초)")]
    public Vector2 spawnIntervalClamp = new Vector2(0.4f, 4.0f);

    private float currentSpawnInterval;
    private float timer = 0f;

    [Header("적 동시 존재 수 제한")]
    [Tooltip("한 번에 화면에 존재할 수 있는 적의 최대 수")]
    public int maxAliveEnemies = 25;

    [Header("보너스 적 스폰 설정")]
    [Tooltip("기존 보너스 적 프리팹")]
    public GameObject bonusEnemyPrefab;

    [Tooltip("보너스 적이 선택될 확률 (0~1)")]
    [Range(0f, 1f)]
    public float bonusSpawnChance = 0.1f;

    [Header("자유 이동 적 스폰 설정")]
    [Tooltip("EnemyBonusMover 를 사용하는 느린 자유 이동 적 프리팹")]
    public GameObject freeEnemyPrefab;

    [Tooltip("자유 이동 적이 선택될 확률 (0~1)")]
    [Range(0f, 1f)]
    public float freeEnemySpawnChance = 0.2f;

    [Header("상단 스폰 범위 (기본값, 필요 시 수동 조절)")]
    [Tooltip("상단에서 스폰되는 X 최소/최대 (카메라 자동 설정을 켜면 Start에서 덮어씀)")]
    public float spawnXMin = -3.5f;
    public float spawnXMax = 3.5f;

    [Tooltip("상단에서 스폰되는 Y 위치")]
    public float spawnY = 6.5f;

    [Header("카메라 기준 자동 설정")]
    [Tooltip("카메라 화면 크기를 기준으로 스폰 범위를 자동 설정할지 여부")]
    public bool autoUseCameraBounds = true;

    [Tooltip("좌우 여유(>0이면 화면 안쪽으로 약간 들어오게)")]
    public float cameraMarginX = 0.5f;

    [Tooltip("위쪽 여유(>0이면 화면 안쪽으로 약간 내려오게)")]
    public float cameraMarginY = 0.5f;

    [Header("좌/우 측면 스폰 옵션")]
    [Tooltip("가끔 왼쪽/오른쪽 측면에서도 적이 등장하도록 할지 여부")]
    public bool useSideSpawn = true;

    [Tooltip("한 번 스폰할 때 측면 스폰이 선택될 확률 (0~1)")]
    [Range(0f, 1f)]
    public float sideSpawnChance = 0.35f;

    [Tooltip("측면에서 스폰될 X 위치(절대값). 카메라 자동 설정 시 Start에서 덮어씀")]
    public float sideSpawnX = 7.0f;

    [Tooltip("측면 스폰의 Y 범위 (화면 높이 중 일부 구간에만 나오게)")]
    public float sideSpawnYMin = -3.5f;
    public float sideSpawnYMax = 4.5f;

    void Start()
    {
        if (autoUseCameraBounds)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                // 화면 좌/우/상단 월드 좌표
                Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f));
                Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
                Vector3 mid = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0f));

                // 상단 스폰 범위: 화면 안쪽에서 나오도록 설정
                spawnXMin = topLeft.x + cameraMarginX;
                spawnXMax = topRight.x - cameraMarginX;
                spawnY = topRight.y - cameraMarginY;

                if (useSideSpawn)
                {
                    float rightX = topRight.x - cameraMarginX;
                    sideSpawnX = Mathf.Abs(rightX);

                    sideSpawnYMin = mid.y;
                    sideSpawnYMax = topRight.y - cameraMarginY;
                }
            }
        }

        RefreshSpawnIntervalByStage();
        timer = 0f;
    }

    /// <summary>
    /// GameManager의 CurrentStage를 기준으로 스폰 간격 계산
    /// </summary>
    private void RefreshSpawnIntervalByStage()
    {
        int stage = 1;
        if (GameManager.I != null)
            stage = Mathf.Max(1, GameManager.I.CurrentStage);

        float raw = spawnInterval + (stage - 1) * spawnIntervalPerStage;
        currentSpawnInterval = Mathf.Clamp(raw, spawnIntervalClamp.x, spawnIntervalClamp.y);

        if (currentSpawnInterval <= 0.01f)
            currentSpawnInterval = spawnIntervalClamp.x;
    }

    void Update()
    {
        if (enemyPrefab == null && bonusEnemyPrefab == null && freeEnemyPrefab == null)
            return;

        // 🔹 게임이 실제로 진행 중일 때만 스폰
        if (GameManager.I != null)
        {
            if (!GameManager.I.IsStageRunning || GameManager.I.IsGameOver)
                return;

            // 🔹 동시에 존재하는 적 수 제한
            if (GameManager.I.AliveEnemyCount >= maxAliveEnemies)
                return;
        }

        timer += Time.deltaTime;
        if (timer >= currentSpawnInterval)
        {
            SpawnOne();
            timer = 0f;
        }
    }

    private void SpawnOne()
    {
        // 스폰 위치 결정 (상단 or 측면)
        Vector3 pos;
        bool spawnFromSide = useSideSpawn && (Random.value < sideSpawnChance);

        if (spawnFromSide)
        {
            bool fromLeft = Random.value < 0.5f;
            float x = fromLeft ? -sideSpawnX : sideSpawnX;
            float y = Random.Range(sideSpawnYMin, sideSpawnYMax);
            pos = new Vector3(x, y, 0f);
        }
        else
        {
            float x = Random.Range(spawnXMin, spawnXMax);
            pos = new Vector3(x, spawnY, 0f);
        }

        // 어떤 적을 뿌릴지 선택 (자유 이동 적 → 보너스 적 → 기본 적 순)
        GameObject prefabToSpawn = enemyPrefab;
        float roll = Random.value;

        if (freeEnemyPrefab != null && roll < freeEnemySpawnChance)
        {
            prefabToSpawn = freeEnemyPrefab;
        }
        else if (bonusEnemyPrefab != null && roll < freeEnemySpawnChance + bonusSpawnChance)
        {
            prefabToSpawn = bonusEnemyPrefab;
        }
        else if (prefabToSpawn == null)
        {
            // 기본 적이 비어있으면 다른 것이라도
            if (freeEnemyPrefab != null) prefabToSpawn = freeEnemyPrefab;
            else if (bonusEnemyPrefab != null) prefabToSpawn = bonusEnemyPrefab;
        }

        if (prefabToSpawn == null)
            return;

        GameObject go = Instantiate(prefabToSpawn, pos, Quaternion.identity);

        if (GameManager.I != null)
            GameManager.I.OnEnemySpawned();
    }
}
