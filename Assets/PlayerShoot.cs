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

    [Header("발사 각도 제한(중요)")]
    [Tooltip("앞(Vector2.up) 기준 허용 반각도. 90이면 기존과 동일(좌/우 90도). 값을 줄이면 더 '전방'으로만 나갑니다.")]
    [Range(0f, 90f)]
    public float maxAimHalfAngle = 90f;

    [Header("디버그")]
    public Vector2 aimDir = Vector2.up;

    private float nextFireTime = 0f;
    private PlayerMovement movement;

    [Header("Weapon (Cosmetic)")]
    [Tooltip("비워두면 Resources에서 자동 로드합니다.")]
    [SerializeField] private CosmeticDatabase cosmeticDb;

    private CosmeticItem equippedWeapon; // category == Weapon

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        movement = GetComponent<PlayerMovement>();
        aimDir = Vector2.up;
    }

    void Start()
    {
        RefreshEquippedWeapon();
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

            if (angle > maxAimHalfAngle)
                aimDir = forward;
            else
                aimDir = dir.normalized;
        }
    }

    public void RefreshEquippedWeapon()
    {
        equippedWeapon = null;
        if (cosmeticDb == null) return;

        // Weapon만 사용 (BulletSkin 호환 제거)
        string wid = CosmeticSaveManager.GetEquipped(CosmeticCategory.Weapon);
        if (string.IsNullOrEmpty(wid)) return;

        var it = cosmeticDb.GetById(wid);
        if (it == null) return;

        if (it.category == CosmeticCategory.Weapon)
        {
            equippedWeapon = it;
            equippedWeapon.IsWeaponValid(); // 내부검증
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

        int shotCount = GetWeaponShotCount();
        float spreadAngle = GetWeaponSpreadAngle();
        float finalInterval = GetFinalFireInterval();

        ShootMultiBullets(firePoint, dir, shotCount, spreadAngle);

        if (twinMode && twinFirePoint != null)
            ShootMultiBullets(twinFirePoint, dir, shotCount, spreadAngle);

        nextFireTime = Time.time + finalInterval;
    }

    private void ShootMultiBullets(Transform muzzle, Vector2 dir, int shotCount, float spreadAngle)
    {
        shotCount = Mathf.Max(1, shotCount);

        if (shotCount == 1 || spreadAngle <= 0.0001f)
        {
            ShootOneBullet(muzzle, dir);
            return;
        }

        float half = spreadAngle * 0.5f;

        for (int i = 0; i < shotCount; i++)
        {
            float t = (shotCount == 1) ? 0.5f : (float)i / (shotCount - 1);
            float ang = Mathf.Lerp(-half, +half, t);
            Vector2 rotated = Rotate(dir, ang);
            ShootOneBullet(muzzle, rotated);
        }
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        return new Vector2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
    }

    private void ShootOneBullet(Transform muzzle, Vector2 dir)
    {
        var b = Instantiate(bulletPrefab, muzzle.position, Quaternion.identity);

        float speed = b.baseSpeed;

        if (overrideBaseSpeed > 0f)
            speed = overrideBaseSpeed;

        if (IsBulletSpeedBonusActive)
            speed *= bulletSpeedBonusMul;

        speed *= GetWeaponSpeedMul();

        b.baseSpeed = speed;

        b.owner = BulletOwner.Player;

        // Bullet에 Weapon 주입
        b.ApplyWeapon(equippedWeapon);

        b.SetDirection(dir);
    }

    public bool IsBulletSpeedBonusActive => Time.time < bulletSpeedBonusEndTime;

    public void ActivateBulletSpeedBonus(float mul, float duration)
    {
        bulletSpeedBonusMul = Mathf.Max(1f, mul);
        bulletSpeedBonusEndTime = Time.time + duration;
    }

    public void SetBulletPrefab(Bullet newPrefab)
    {
        if (newPrefab == null) return;
        bulletPrefab = newPrefab;
    }

    private float GetFinalFireInterval()
    {
        if (equippedWeapon == null) return fireInterval;
        return fireInterval * Mathf.Max(0.1f, equippedWeapon.fireIntervalMul);
    }

    private int GetWeaponShotCount()
    {
        if (equippedWeapon == null) return 1;
        return Mathf.Max(1, equippedWeapon.shotCount);
    }

    private float GetWeaponSpreadAngle()
    {
        if (equippedWeapon == null) return 0f;
        return Mathf.Max(0f, equippedWeapon.spreadAngle);
    }

    private float GetWeaponSpeedMul()
    {
        if (equippedWeapon == null) return 1f;
        return Mathf.Max(0.1f, equippedWeapon.speedMul);
    }
}
