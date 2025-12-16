using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("기본 적 스폰 설정")]
    public GameObject enemyPrefab;

    public float spawnInterval = 2f;
    public float spawnIntervalPerStage = -0.3f;
    public Vector2 spawnIntervalClamp = new Vector2(0.4f, 4.0f);

    private float currentSpawnInterval;
    private float timer = 0f;

    [Header("적 동시 존재 수 제한")]
    public int maxAliveEnemies = 25;

    [Tooltip("true면 Stage가 올라갈수록 동시 존재 제한이 늘어납니다.")]
    public bool scaleMaxAliveByStage = false;

    [Tooltip("Stage1 기준으로, 스테이지가 1 올라갈 때마다 maxAliveEnemies에 더해질 값")]
    public int maxAlivePerStage = 2;

    [Header("보너스 적 스폰 설정")]
    public GameObject bonusEnemyPrefab;
    [Range(0f, 1f)] public float bonusSpawnChance = 0.1f;

    [Header("자유 이동 적 스폰 설정(기존)")]
    public GameObject freeEnemyPrefab;
    [Range(0f, 1f)] public float freeEnemySpawnChance = 0.2f;

    [Header("NEW: 탱커(2HP) 랜덤 이동 적 스폰 설정")]
    [Tooltip("Stage2부터 등장하는 새 적 프리팹 (EnemyGalaga + EnemyBonusMover 권장)")]
    public GameObject toughRandomEnemyPrefab;

    [Range(0f, 1f)] public float toughSpawnChanceStage2 = 0.06f;
    public float toughSpawnChancePerStage = 0.03f;
    [Range(0f, 1f)] public float toughSpawnChanceClampMax = 0.45f;

    [Header("상단 스폰 범위")]
    public float spawnXMin = -3.5f;
    public float spawnXMax = 3.5f;
    public float spawnY = 6.5f;

    [Header("카메라 기준 자동 설정")]
    public bool autoUseCameraBounds = true;
    public float cameraMarginX = 0.5f;
    public float cameraMarginY = 0.5f;

    [Header("좌/우 측면 스폰 옵션")]
    public bool useSideSpawn = true;
    [Range(0f, 1f)] public float sideSpawnChance = 0.35f;
    public float sideSpawnX = 7.0f;
    public float sideSpawnYMin = -3.5f;
    public float sideSpawnYMax = 4.5f;

    void Start()
    {
        if (autoUseCameraBounds)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, 0f));
                Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
                Vector3 mid = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0f));

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

    private int GetStage()
    {
        if (GameManager.I == null) return 1;
        return Mathf.Max(1, GameManager.I.CurrentStage);
    }

    private void RefreshSpawnIntervalByStage()
    {
        int stage = GetStage();
        float raw = spawnInterval + (stage - 1) * spawnIntervalPerStage;
        currentSpawnInterval = Mathf.Clamp(raw, spawnIntervalClamp.x, spawnIntervalClamp.y);

        if (currentSpawnInterval <= 0.01f)
            currentSpawnInterval = spawnIntervalClamp.x;
    }

    private int GetMaxAliveByStage()
    {
        int m = maxAliveEnemies;
        if (!scaleMaxAliveByStage) return m;

        int stage = GetStage();
        m += (stage - 1) * Mathf.Max(0, maxAlivePerStage);
        return Mathf.Max(1, m);
    }

    private float GetToughChanceByStage()
    {
        int stage = GetStage();
        if (stage < 2) return 0f;

        float c = toughSpawnChanceStage2 + (stage - 2) * toughSpawnChancePerStage;
        return Mathf.Clamp(c, 0f, toughSpawnChanceClampMax);
    }

    void Update()
    {
        if (enemyPrefab == null && bonusEnemyPrefab == null && freeEnemyPrefab == null && toughRandomEnemyPrefab == null)
            return;

        if (GameManager.I != null)
        {
            if (!GameManager.I.IsStageRunning || GameManager.I.IsGameOver)
                return;

            int maxAlive = GetMaxAliveByStage();
            if (GameManager.I.AliveEnemyCount >= maxAlive)
                return;
        }

        timer += Time.deltaTime;
        if (timer >= currentSpawnInterval)
        {
            SpawnOne();
            timer = 0f;

            // Stage가 바뀌었을 수 있으니 간격 갱신
            RefreshSpawnIntervalByStage();
        }
    }

    private void SpawnOne()
    {
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

        int stage = GetStage();
        float roll = Random.value;

        float toughChance = (toughRandomEnemyPrefab != null) ? GetToughChanceByStage() : 0f;

        // 1) Stage2+ 새 탱커(2HP) 랜덤 이동 적
        if (toughRandomEnemyPrefab != null && stage >= 2 && roll < toughChance)
        {
            InstantiateAndCount(toughRandomEnemyPrefab, pos);
            return;
        }

        // 2) 기존 자유 이동 적
        if (freeEnemyPrefab != null && roll < toughChance + freeEnemySpawnChance)
        {
            InstantiateAndCount(freeEnemyPrefab, pos);
            return;
        }

        // 3) 기존 보너스 적
        if (bonusEnemyPrefab != null && roll < toughChance + freeEnemySpawnChance + bonusSpawnChance)
        {
            InstantiateAndCount(bonusEnemyPrefab, pos);
            return;
        }

        // 4) 기본 적
        if (enemyPrefab != null)
        {
            InstantiateAndCount(enemyPrefab, pos);
            return;
        }

        // 기본 적이 비어있으면 다른 것이라도
        if (freeEnemyPrefab != null) InstantiateAndCount(freeEnemyPrefab, pos);
        else if (bonusEnemyPrefab != null) InstantiateAndCount(bonusEnemyPrefab, pos);
        else if (toughRandomEnemyPrefab != null) InstantiateAndCount(toughRandomEnemyPrefab, pos);
    }

    private void InstantiateAndCount(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;

        Instantiate(prefab, pos, Quaternion.identity);

        if (GameManager.I != null)
            GameManager.I.OnEnemySpawned();
    }
}
