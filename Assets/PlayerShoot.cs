using UnityEngine;
using UnityEngine.InputSystem; // 새 입력 시스템

public class PlayerShoot : MonoBehaviour
{
    [Header("프리팹/위치")]
    public Bullet bulletPrefab;       // Bullet.cs가 붙은 프리팹 권장
    public Transform firePoint;       // 발사 위치(총구)

    [Header("발사 설정")]
    public float fireInterval = 0.15f;     // 연사 간격
    public float overrideBaseSpeed = -1f;  // -1이면 프리팹 기본값 사용, 양수면 Bullet.baseSpeed를 이 값으로 덮어씀
    public bool autoFire = true;           // 계속 발사

    [Header("입력(디버그용)")]
    public bool allowKeyboard = true;      // PC 테스트용 스페이스 입력 허용
    public bool allowTouch = true;         // 모바일 터치 입력 허용 (아무 곳 터치 시 발사)

    private float nextFireTime = 0f;

    void Update()
    {
        // 발사 조건
        bool wantFire = false;

        if (autoFire)
        {
            // 자동 연사: 항상 true
            wantFire = true;
        }
        else
        {
            // 입력 기반
            if (allowKeyboard && Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                wantFire = true;

            if (allowTouch && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                wantFire = true;
        }

        if (wantFire && Time.time >= nextFireTime)
        {
            Fire(Vector2.up); // 기본: 위쪽으로 발사
        }
    }

    public void Fire(Vector2 dir)
    {
        if (bulletPrefab == null || firePoint == null)
        {
            Debug.LogWarning("[PlayerShoot] bulletPrefab 또는 firePoint가 설정되지 않았습니다.");
            return;
        }

        // 생성
        var b = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);

        // (선택) 총알 기본 속도 강제 덮어쓰기
        if (overrideBaseSpeed > 0f)
            b.baseSpeed = overrideBaseSpeed;

        // 진행 방향 지정 → Bullet.OnEnable()에서 회전/속도(스테이지 배율 포함) 자동 적용
        b.SetDirection(dir);

        // 쿨다운
        nextFireTime = Time.time + fireInterval;
    }
}
