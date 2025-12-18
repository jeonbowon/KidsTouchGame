using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour, IBullet
{
    [Header("기본 속도(플레이어 탄은 stage 배율 곱)")]
    public float baseSpeed = 12f;

    [Header("수명(초)")]
    public float lifeTime = 3f;

    [Header("스프라이트 방향")]
    public bool spritePointsUp = true; // 스프라이트가 '위쪽' 기준이면 true, '오른쪽' 기준이면 false

    [Header("발사 주체")]
    public BulletOwner owner = BulletOwner.Player;

    private Vector2 dir = Vector2.up;   // 진행 방향
    private Rigidbody2D rb;

    // 적 탄 전용: 이번 발사에 한해 속도 덮어쓰기. 음수면 미사용(플레이어 탄 경로 사용)
    private float speedOverride = -1f;

    public void SetOwner(BulletOwner o) => owner = o;

    public void SetDirection(Vector2 d)
    {
        dir = (d.sqrMagnitude > 0.0001f) ? d.normalized : Vector2.up;
        if (rb != null && isActiveAndEnabled) ApplyVelocityAndRotation();
    }

    public void SetSpeed(float s)
    {
        speedOverride = Mathf.Max(0f, s);
        if (rb != null && isActiveAndEnabled) ApplyVelocityAndRotation();
    }

    public void ActivateAt(Vector3 p)
    {
        transform.position = p;
        // 필요 시 추가 초기화 가능
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    // 풀/Instantiation 모두 매번 초기화
    void OnEnable()
    {
        ApplyVelocityAndRotation();

        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifeTime);
    }

    private void ApplyVelocityAndRotation()
    {
        float finalSpeed;

        if (owner == BulletOwner.Enemy && speedOverride >= 0f)
        {
            // 적 탄: Shooter에서 주입된 절대속도 사용
            finalSpeed = speedOverride;
        }
        else
        {
            // 플레이어 탄: stage 배율 적용
            float mul = (GameManager.I != null) ? GameManager.I.BulletSpeedMul : 1f;
            finalSpeed = baseSpeed * mul;
        }

        rb.linearVelocity = dir * finalSpeed;

        // 스프라이트 회전
        if (rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            if (spritePointsUp) transform.up = rb.linearVelocity;
            else transform.right = rb.linearVelocity;
        }
    }

    /// <summary>
    /// 외부(EnemyBullet 등)에서 강제로 비활성화시킬 때 사용
    /// </summary>
    public void DespawnFromOutside()
    {
        Despawn();
    }

    void Despawn()
    {
        // 다음 재사용을 위해 초기화
        speedOverride = -1f;

        // 🔹 현재는 풀링을 사용하지 않으므로, 무한히 쌓이지 않도록 파괴
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ⭕ 적 탄과의 충돌은 EnemyBullet 쪽에서 처리하므로 여기서는 스킵
        if (other.GetComponent<EnemyBullet>() != null)
            return;

        // 팀킬 방지 + 역할 분리:
        //  - 적의 HP/폭발/사운드/드랍/킬카운트는 EnemyGalaga가 담당
        //  - 총알은 "맞으면 사라짐"만 담당

        if (owner == BulletOwner.Player)
        {
            if (other.CompareTag("Enemy"))
            {
                // ✅ 적을 Destroy하거나 OnEnemyKilled를 여기서 호출하면 안됨
                //    (그럼 HP=2, 폭발, 폭발음, 드랍 로직이 전부 스킵됨)
                Despawn();
                return;
            }
        }
        else // Enemy bullet
        {
            if (other.CompareTag("Player"))
            {
                // ✅ 플레이어 제거도 GameManager가 담당하도록 위임
                if (GameManager.I != null) GameManager.I.OnPlayerDied();
                Despawn();
                return;
            }
        }
    }
}
