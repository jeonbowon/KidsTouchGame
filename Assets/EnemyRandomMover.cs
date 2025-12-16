using UnityEngine;

public class EnemyRandomMover : MonoBehaviour
{
    [Header("Stage (GameManager.CurrentStage 기반)")]
    public float moveSpeedStage1 = 1.5f;
    public float moveSpeedPerStage = 0.2f;
    public Vector2 moveSpeedClamp = new Vector2(0.5f, 8.0f);

    public float horizontalSpeedStage1 = 2.0f;
    public float horizontalSpeedPerStage = 0.15f;
    public Vector2 horizontalSpeedClamp = new Vector2(0.5f, 10.0f);

    [Tooltip("속도 변화가 부드럽게 따라가게(가감속). 0이면 즉시 적용")]
    public float accel = 6.0f;

    [Header("수평 랜덤 이동 (스폰 위치 기준 반경)")]
    public float horizontalRange = 1.5f;

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
    private float _centerX;

    // 현재 적용 중인 속도(가감속)
    private float _downNow;
    private float _horiNow;

    void Start()
    {
        _baseY = transform.position.y;
        _centerX = transform.position.x;

        _downNow = GetTargetDownSpeed();
        _horiNow = GetTargetHorizontalSpeed();

        ScheduleNextDirectionChange();
        ScheduleNextFire();
    }

    void Update()
    {
        // 목표 속도(스테이지 반영)
        float targetDown = GetTargetDownSpeed();
        float targetHori = GetTargetHorizontalSpeed();

        // 가감속(부드럽게 따라가기)
        if (accel <= 0f)
        {
            _downNow = targetDown;
            _horiNow = targetHori;
        }
        else
        {
            float t = 1f - Mathf.Exp(-accel * Time.deltaTime); // 프레임 독립 보간
            _downNow = Mathf.Lerp(_downNow, targetDown, t);
            _horiNow = Mathf.Lerp(_horiNow, targetHori, t);
        }

        MovePattern();
        ShootingPattern();
    }

    int GetStage()
    {
        // ✅ 대표님 GameManager.cs 기준 확정
        return (GameManager.I != null) ? Mathf.Max(1, GameManager.I.CurrentStage) : 1;
    }

    float GetTargetDownSpeed()
    {
        int stage = GetStage();
        float s = moveSpeedStage1 + (stage - 1) * moveSpeedPerStage;
        return Mathf.Clamp(s, moveSpeedClamp.x, moveSpeedClamp.y);
    }

    float GetTargetHorizontalSpeed()
    {
        int stage = GetStage();
        float s = horizontalSpeedStage1 + (stage - 1) * horizontalSpeedPerStage;
        return Mathf.Clamp(s, horizontalSpeedClamp.x, horizontalSpeedClamp.y);
    }

    void MovePattern()
    {
        Vector3 pos = transform.position;

        // 좌우 이동
        pos.x += _currentDir * _horiNow * Time.deltaTime;

        float minX = _centerX - horizontalRange;
        float maxX = _centerX + horizontalRange;

        if (pos.x < minX || pos.x > maxX)
        {
            _currentDir *= -1f;
            pos.x = Mathf.Clamp(pos.x, minX, maxX);
        }

        // 수직 웨이브 + 하강
        float waveY = Mathf.Sin(Time.time * verticalFrequency) * verticalAmplitude;
        pos.y = _baseY + waveY - _downNow * Time.deltaTime;

        transform.position = pos;

        // 랜덤 방향 변경
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
