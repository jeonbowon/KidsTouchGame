using UnityEngine;

/// <summary>
/// 화면 안에서 랜덤한 지점을 계속 찍으면서
/// 부드럽게 이동하는 보너스 적 전용 이동 스크립트.
/// EnemyGalaga.useGalagaMove 는 반드시 false 로 설정하고,
/// EnemyRandomMover 는 꺼두어야 함.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyBonusMover : MonoBehaviour
{
    [Header("이동 속도(난이도 조절용)")]
    [Tooltip("한 번 목표로 움직일 때 최소 속도")]
    public float moveSpeedMin = 1.5f;

    [Tooltip("한 번 목표로 움직일 때 최대 속도")]
    public float moveSpeedMax = 2.5f;

    [Header("목표 지점 변경 주기(초)")]
    [Tooltip("다음 목표 지점을 고를 최소 시간")]
    public float changeTargetTimeMin = 1.5f;

    [Tooltip("다음 목표 지점을 고를 최대 시간")]
    public float changeTargetTimeMax = 3.0f;

    [Header("카메라 안쪽 여유(클램핑 포함)")]
    [Tooltip("화면 가장자리에서 얼마나 안쪽까지만 움직일지")]
    public float cameraPadding = 0.5f;

    [Header("수명 설정")]
    [Tooltip("이 시간이 지나면 자동으로 사라짐(초). 0 이하이면 무제한")]
    public float lifeTime = 15f;

    private Camera cam;
    private Vector2 targetPos;
    private float nextChangeTime;
    private float currentSpeed;

    // 카메라 월드 좌표 경계
    private float minX, maxX, minY, maxY;
    private bool hasBounds = false;

    private float spawnTime;

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

    /// <summary>
    /// 카메라의 월드 좌표 경계를 계산해 둔다.
    /// </summary>
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

    /// <summary>
    /// 다음 목표 지점과 이동 속도를 랜덤으로 선택.
    /// </summary>
    private void PickNewTarget()
    {
        if (!hasBounds)
            CacheCameraBounds();

        if (!hasBounds) return;

        float x = Random.Range(minX + cameraPadding, maxX - cameraPadding);
        float y = Random.Range(minY + cameraPadding, maxY - cameraPadding);

        targetPos = new Vector2(x, y);

        // 난이도에 따라 이동 속도 랜덤 선택
        currentSpeed = Random.Range(moveSpeedMin, moveSpeedMax);

        // 다음 목표 변경 시간 설정
        float t = Random.Range(changeTargetTimeMin, changeTargetTimeMax);
        nextChangeTime = Time.time + t;
    }
}
