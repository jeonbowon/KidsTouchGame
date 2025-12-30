using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    [Header("카메라")]
    public Camera cam;

    [Header("프리팹/위치")]
    public Bullet bulletPrefab;
    public Transform firePoint;
    [Tooltip("Twin 모드에서 사용할 두 번째 발사 위치(선택)")]
    public Transform twinFirePoint;

    [Header("발사 설정")]
    public float fireInterval = 0.15f;
    public float overrideBaseSpeed = -1f;
    public bool autoFire = true;

    [Header("입력(디버그용)")]
    public bool allowKeyboard = true;

    [Header("파워업 상태")]
    [Tooltip("Twin 모드 활성 여부 (PlayerPowerUp에서 제어)")]
    public bool twinMode = false;

    [Header("총알 속도 보너스")]
    [Tooltip("보너스가 걸렸을 때 곱해질 배율 (예: 2면 2배 속도)")]
    [SerializeField] private float bulletSpeedBonusMul = 1f;
    private float bulletSpeedBonusEndTime = 0f;

    [Header("디버그")]
    public Vector2 aimDir = Vector2.up;

    private float nextFireTime = 0f;
    private PlayerMovement movement;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        movement = GetComponent<PlayerMovement>();
        aimDir = Vector2.up;
    }

    void Update()
    {
        UpdateAimDirectionFromMovement();

        bool wantFire = false;

        if (autoFire)
        {
            wantFire = true;
        }
        else
        {
            if (allowKeyboard && Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                wantFire = true;
        }

        if (wantFire && Time.time >= nextFireTime)
        {
            Fire(aimDir);
        }
    }

    void UpdateAimDirectionFromMovement()
    {
        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
            if (movement == null)
            {
                aimDir = Vector2.up;
                return;
            }
        }

        Vector2 dir = movement.LastMoveDirection;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector2 forward = Vector2.up;
            float angle = Vector2.Angle(dir, forward);

            if (angle > 90f)
                aimDir = forward;
            else
                aimDir = dir.normalized;
        }
    }

    public void Fire(Vector2 dir)
    {
        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("[PlayerShoot] bulletPrefab 또는 firePoint가 설정되지 않았습니다.");
            return;
        }

        if (dir.sqrMagnitude < 1e-4f)
            dir = Vector2.up;

        // 1) 메인 총구
        ShootOneBullet(firePoint, dir);

        // 2) Twin 모드면 두 번째 총구
        if (twinMode && twinFirePoint != null)
            ShootOneBullet(twinFirePoint, dir);

        nextFireTime = Time.time + fireInterval;
    }

    private void ShootOneBullet(Transform muzzle, Vector2 dir)
    {
        var b = Instantiate(bulletPrefab, muzzle.position, Quaternion.identity);

        // 기본 속도는 프리팹의 baseSpeed
        float speed = b.baseSpeed;

        // overrideBaseSpeed가 설정되어 있으면 그 값으로 덮어씀
        if (overrideBaseSpeed > 0f)
            speed = overrideBaseSpeed;

        // 총알 속도 보너스가 활성이라면 배율 적용
        if (IsBulletSpeedBonusActive)
            speed *= bulletSpeedBonusMul;

        b.baseSpeed = speed;

        b.owner = BulletOwner.Player;
        b.SetDirection(dir);
    }

    // ────────────── 보너스 관련 공개 API ──────────────

    public bool IsBulletSpeedBonusActive => Time.time < bulletSpeedBonusEndTime;

    /// <summary>
    /// 외부(아이템/게임매니저)에서 호출: 일정 시간 동안 총알 속도 배율 적용
    /// </summary>
    public void ActivateBulletSpeedBonus(float mul, float duration)
    {
        bulletSpeedBonusMul = Mathf.Max(1f, mul);
        bulletSpeedBonusEndTime = Time.time + duration;
    }

    // ────────────── 코스메틱 적용 API (추가) ──────────────
    public void SetBulletPrefab(Bullet newPrefab)
    {
        if (newPrefab == null) return;
        bulletPrefab = newPrefab;
    }
}
