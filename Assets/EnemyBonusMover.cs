using UnityEngine;

/// <summary>
/// 화면 안에서 랜덤한 지점을 계속 찍으면서 부드럽게 이동하는 이동 스크립트.
/// - 보너스 적/랜덤 이동 적에서 공용으로 사용 가능.
/// - EnemyGalaga.useGalagaMove 는 반드시 false 로 설정.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyBonusMover : MonoBehaviour
{
    [Header("이동 속도(Stage1 기준)")]
    [Tooltip("Stage1 기준: 한 번 목표로 움직일 때 최소 속도")]
    public float moveSpeedMin = 1.5f;

    [Tooltip("Stage1 기준: 한 번 목표로 움직일 때 최대 속도")]
    public float moveSpeedMax = 2.5f;

    [Header("스테이지 스케일링 (이 스크립트 내부에서 직접 제어)")]
    [Tooltip("true면 CurrentStage에 따라 이동 속도가 점점 빨라집니다.")]
    public bool scaleSpeedByStage = true;

    [Tooltip("스테이지가 1 오를 때마다 최소 속도에 더해질 값")]
    public float moveSpeedMinPerStage = 0.12f;

    [Tooltip("스테이지가 1 오를 때마다 최대 속도에 더해질 값")]
    public float moveSpeedMaxPerStage = 0.15f;

    [Tooltip("스테이지 스케일링 적용 시 속도 최소/최대 클램프")]
    public Vector2 moveSpeedClamp = new Vector2(1.0f, 6.0f);

    [Header("목표 지점 변경 주기(초)")]
    public float changeTargetTimeMin = 1.5f;
    public float changeTargetTimeMax = 3.0f;

    [Header("카메라 안쪽 여유(클램프 포함)")]
    public float cameraPadding = 0.5f;

    [Header("수명 설정")]
    [Tooltip("이 시간이 지나면 자동으로 사라짐(초). 0 이하면 무제한")]
    public float lifeTime = 15f;

    private Camera cam;
    private Vector2 targetPos;
    private float nextChangeTime;
    private float currentSpeed;

    private float minX, maxX, minY, maxY;
    private bool hasBounds = false;

    private float spawnTime;

    // 수명 종료/기타 Destroy 경로에서도 스폰 카운트 누수 방지
    private bool _reportedRemoveToGM = false;

    void Start()
    {
        cam = Camera.main;
        spawnTime = Time.time;

        CacheCameraBounds();
        PickNewTarget();
    }

    void Update()
    {
        // 카메라 없으면 시도해 보고, 그래도 없으면 그냥 리턴
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
            CacheCameraBounds();
        }

        // 1) 수명 체크: lifeTime 이 지나면 자동으로 파괴
        if (lifeTime > 0f && Time.time - spawnTime >= lifeTime)
        {
            ReportRemovedToGM();
            Destroy(gameObject);
            return;
        }

        // 2) 카메라 경계 재계산(혹시 해상도/카메라 변경 대비)
        if (!hasBounds)
            CacheCameraBounds();

        float dt = Time.deltaTime;
        Vector2 pos = transform.position;

        // 3) 목표까지의 방향/거리 계산
        Vector2 dir = (targetPos - pos);
        float distToTarget = dir.magnitude;

        // 거의 도착했거나, 시간이 지나서 목표를 바꿀 타이밍이면 새 목표 설정
        if (distToTarget < 0.05f || Time.time >= nextChangeTime)
        {
            PickNewTarget();
            dir = (targetPos - (Vector2)transform.position);
            distToTarget = dir.magnitude;
        }

        // 4) 목표를 향해 이동
        if (dir.sqrMagnitude > 1e-6f)
        {
            Vector2 step = dir.normalized * currentSpeed * dt;

            // 목표 지점을 넘어서지 않도록 거리 클램프
            if (step.magnitude > distToTarget)
                step = dir;

            Vector2 newPos = (Vector2)transform.position + step;

            // 5) 화면 밖으로 나가지 않도록 카메라 경계 안으로 클램핑
            if (hasBounds)
            {
                newPos.x = Mathf.Clamp(newPos.x, minX + cameraPadding, maxX - cameraPadding);
                newPos.y = Mathf.Clamp(newPos.y, minY + cameraPadding, maxY - cameraPadding);
            }

            transform.position = newPos;
        }
    }

    private void CacheCameraBounds()
    {
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                hasBounds = false;
                return;
            }
        }

        Vector3 worldMin = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 worldMax = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        minX = worldMin.x;
        maxX = worldMax.x;
        minY = worldMin.y;
        maxY = worldMax.y;

        hasBounds = true;
    }

    private int GetStage()
    {
        if (GameManager.I == null) return 1;
        return Mathf.Max(1, GameManager.I.CurrentStage);
    }

    private void GetScaledSpeedRange(out float min, out float max)
    {
        int stage = GetStage();

        // SO 우선 (대표님 요구: 난이도 값은 한 곳에서)
        if (GameManager.I != null && GameManager.I.Difficulty != null)
        {
            min = GameManager.I.Difficulty.bonusSpeedMin.Eval(stage);
            max = GameManager.I.Difficulty.bonusSpeedMax.Eval(stage);
            if (max < min) max = min;
            return;
        }

        // fallback(기존 방식)
        min = moveSpeedMin;
        max = moveSpeedMax;

        if (!scaleSpeedByStage) return;

        float addMin = (stage - 1) * moveSpeedMinPerStage;
        float addMax = (stage - 1) * moveSpeedMaxPerStage;

        min = Mathf.Clamp(min + addMin, moveSpeedClamp.x, moveSpeedClamp.y);
        max = Mathf.Clamp(max + addMax, moveSpeedClamp.x, moveSpeedClamp.y);

        if (max < min) max = min;
    }

    private void PickNewTarget()
    {
        if (!hasBounds)
            CacheCameraBounds();

        if (!hasBounds) return;

        float x = Random.Range(minX + cameraPadding, maxX - cameraPadding);
        float y = Random.Range(minY + cameraPadding, maxY - cameraPadding);
        targetPos = new Vector2(x, y);

        GetScaledSpeedRange(out float sMin, out float sMax);
        currentSpeed = Random.Range(sMin, sMax);

        float t = Random.Range(changeTargetTimeMin, changeTargetTimeMax);
        nextChangeTime = Time.time + t;
    }

    private void OnDestroy()
    {
        ReportRemovedToGM();
    }

    private void ReportRemovedToGM()
    {
        if (_reportedRemoveToGM) return;
        _reportedRemoveToGM = true;

        if (GameManager.I != null)
            GameManager.I.OnEnemyRemoved();
    }
}
