using UnityEngine;

public class EnemyRandomMover : MonoBehaviour
{
    [Header("기본 이동 속도")]
    public float moveSpeed = 1.5f;

    [Header("수평 랜덤 이동 (스폰 위치 기준 반경)")]
    [Tooltip("스폰된 x 위치를 기준으로 좌우로 이동할 최대 거리")]
    public float horizontalRange = 1.5f;

    [Tooltip("좌우 이동 속도")]
    public float horizontalSpeed = 2f;

    [Header("수직 랜덤 흔들림")]
    public float verticalAmplitude = 0.5f;
    public float verticalFrequency = 1.5f;

    [Header("랜덤 방향 변경 주기")]
    public float dirChangeIntervalMin = 1f;
    public float dirChangeIntervalMax = 3f;

    [Header("총알 발사")]
    public EnemyShooter shooter;
    public float fireIntervalMin = 1.0f;
    public float fireIntervalMax = 2.5f;

    private float _baseY;
    private float _nextDirChangeTime;
    private float _nextFireTime;
    private float _currentDir = 1f;

    // ★ 추가: 각 적 개체마다 자기 기준이 될 x 중심값
    private float _centerX;

    void Start()
    {
        _baseY = transform.position.y;

        // ★ 스폰된 위치를 기준으로 좌우 이동 범위를 잡는다
        _centerX = transform.position.x;

        ScheduleNextDirectionChange();
        ScheduleNextFire();
    }

    void Update()
    {
        MovePattern();
        ShootingPattern();
    }

    void MovePattern()
    {
        Vector3 pos = transform.position;

        // 좌우 이동
        pos.x += _currentDir * horizontalSpeed * Time.deltaTime;

        // ★ 이 적의 "중심 위치" 기준으로만 범위를 제한
        float minX = _centerX - horizontalRange;
        float maxX = _centerX + horizontalRange;

        // 범위를 넘어가면 방향反전 + 클램프
        if (pos.x < minX || pos.x > maxX)
        {
            _currentDir *= -1f;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
        }

        // 수직 웨이브 패턴
        float waveY = Mathf.Sin(Time.time * verticalFrequency) * verticalAmplitude;
        pos.y = _baseY + waveY - moveSpeed * Time.deltaTime; // 천천히 아래로 내려가도록

        transform.position = pos;

        // 일정 시간마다 방향 랜덤 변경
        if (Time.time >= _nextDirChangeTime)
        {
            _currentDir = Random.value > 0.5f ? 1f : -1f;
            ScheduleNextDirectionChange();
        }
    }

    void ShootingPattern()
    {
        if (shooter == null) return;

        if (Time.time >= _nextFireTime)
        {
            shooter.FireAtPlayer();
            ScheduleNextFire();
        }
    }

    void ScheduleNextDirectionChange()
    {
        _nextDirChangeTime = Time.time + Random.Range(dirChangeIntervalMin, dirChangeIntervalMax);
    }

    void ScheduleNextFire()
    {
        _nextFireTime = Time.time + Random.Range(fireIntervalMin, fireIntervalMax);
    }
}
