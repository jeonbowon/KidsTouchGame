using UnityEngine;

/// <summary>
/// ȭ�� �ȿ��� ������ ������ ��� �����鼭 �ε巴�� �̵��ϴ� �̵� ��ũ��Ʈ.
/// - ���ʽ� ��/���� �̵� ������ �������� ��� ����.
/// - EnemyGalaga.useGalagaMove �� �ݵ�� false �� ����.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyBonusMover : MonoBehaviour
{
    [Header("�̵� �ӵ�(Stage1 ����)")]
    [Tooltip("Stage1 ����: �� �� ��ǥ�� ������ �� �ּ� �ӵ�")]
    public float moveSpeedMin = 1.5f;

    [Tooltip("Stage1 ����: �� �� ��ǥ�� ������ �� �ִ� �ӵ�")]
    public float moveSpeedMax = 2.5f;

    [Header("�������� �����ϸ� (�� ��ũ��Ʈ ���ο��� ���� ����)")]
    [Tooltip("true�� CurrentStage�� ���� �̵� �ӵ��� ���� �������ϴ�.")]
    public bool scaleSpeedByStage = true;

    [Tooltip("���������� 1 ���� ������ �ּ� �ӵ��� ������ ��")]
    public float moveSpeedMinPerStage = 0.12f;

    [Tooltip("���������� 1 ���� ������ �ִ� �ӵ��� ������ ��")]
    public float moveSpeedMaxPerStage = 0.15f;

    [Tooltip("�������� �����ϸ� ���� �� �ӵ� �ּ�/�ִ� Ŭ����")]
    public Vector2 moveSpeedClamp = new Vector2(1.0f, 6.0f);

    [Header("��ǥ ���� ���� �ֱ�(��)")]
    public float changeTargetTimeMin = 1.5f;
    public float changeTargetTimeMax = 3.0f;

    [Header("ī�޶� ���� ����(Ŭ���� ����)")]
    public float cameraPadding = 0.5f;

    [Header("���� ����")]
    [Tooltip("�� �ð��� ������ �ڵ����� �����(��). 0 ���ϸ� ������")]
    public float lifeTime = 15f;

    private Camera cam;
    private Vector2 targetPos;
    private float nextChangeTime;
    private float currentSpeed;

    private float minX, maxX, minY, maxY;
    private bool hasBounds = false;

    private float spawnTime;

    // ���� ����/��Ÿ Destroy ��ο����� ���� ī��Ʈ ���� ����
    private bool _reportedRemoveToGM = false;

    void Start()
    {
        ResetForActivation();
    }

    void OnEnable()
    {
        // 풀에서 재사용될 때 상태 재초기화
        if (!_initialized) return;
        ResetForActivation();
    }

    private bool _initialized = false;

    private void ResetForActivation()
    {
        _initialized = true;

        if (cam == null) cam = Camera.main;
        spawnTime = Time.time;
        _reportedRemoveToGM = false;

        CacheCameraBounds();
        PickNewTarget();
    }

    void Update()
    {
        // ī�޶� ������ �õ��� ����, �׷��� ������ �׳� ����
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null) return;
            CacheCameraBounds();
        }

        // 1) 수명 체크: lifeTime 이상 경과 시 풀에 반환
        if (lifeTime > 0f && Time.time - spawnTime >= lifeTime)
        {
            ReportRemovedToGM();
            ReturnToPool();
            return;
        }

        // 2) ī�޶� ��� ����(Ȥ�� �ػ�/ī�޶� ���� ���)
        if (!hasBounds)
            CacheCameraBounds();

        float dt = Time.deltaTime;
        Vector2 pos = transform.position;

        // 3) ��ǥ������ ����/�Ÿ� ���
        Vector2 dir = (targetPos - pos);
        float distToTarget = dir.magnitude;

        // ���� �����߰ų�, �ð��� ������ ��ǥ�� �ٲ� Ÿ�̹��̸� �� ��ǥ ����
        if (distToTarget < 0.05f || Time.time >= nextChangeTime)
        {
            PickNewTarget();
            dir = (targetPos - (Vector2)transform.position);
            distToTarget = dir.magnitude;
        }

        // 4) ��ǥ�� ���� �̵�
        if (dir.sqrMagnitude > 1e-6f)
        {
            Vector2 step = dir.normalized * currentSpeed * dt;

            // ��ǥ ������ �Ѿ�� �ʵ��� �Ÿ� Ŭ����
            if (step.magnitude > distToTarget)
                step = dir;

            Vector2 newPos = (Vector2)transform.position + step;

            // 5) ȭ�� ������ ������ �ʵ��� ī�޶� ��� ������ Ŭ����
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

        // SO �켱 (��ǥ�� �䱸: ���̵� ���� �� ������)
        if (GameManager.I != null && GameManager.I.Difficulty != null)
        {
            min = GameManager.I.Difficulty.bonusSpeedMin.Eval(stage);
            max = GameManager.I.Difficulty.bonusSpeedMax.Eval(stage);
            if (max < min) max = min;
            return;
        }

        // fallback(���� ���)
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

    private void OnDisable()
    {
        // 풀 반환 시(SetActive false) 카운트 누수 방지
        ReportRemovedToGM();
    }

    private void OnDestroy()
    {
        // 풀 미사용 환경(PoolManager 없을 때 Destroy)에도 누수 없게
        ReportRemovedToGM();
    }

    private void ReturnToPool()
    {
        if (PoolManager.I != null)
            PoolManager.I.Return(gameObject);
        else
            Destroy(gameObject);
    }

    private void ReportRemovedToGM()
    {
        if (_reportedRemoveToGM) return;
        _reportedRemoveToGM = true;

        if (GameManager.I != null)
            GameManager.I.OnEnemyRemoved();
    }
}
