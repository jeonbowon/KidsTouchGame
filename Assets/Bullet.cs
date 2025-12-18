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
    public bool spritePointsUp = true;

    [Header("발사 주체")]
    public BulletOwner owner = BulletOwner.Player;

    private Vector2 dir = Vector2.up;
    private Rigidbody2D rb;

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
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

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
            finalSpeed = speedOverride;
        }
        else
        {
            float mul = (GameManager.I != null) ? GameManager.I.BulletSpeedMul : 1f;
            finalSpeed = baseSpeed * mul;
        }

        rb.linearVelocity = dir * finalSpeed;

        if (rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            if (spritePointsUp) transform.up = rb.linearVelocity;
            else transform.right = rb.linearVelocity;
        }
    }

    public void DespawnFromOutside()
    {
        Despawn();
    }

    void Despawn()
    {
        speedOverride = -1f;

        if (this != null && gameObject != null)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 적 탄 전용 클래스(EnemyBullet)가 있다면 그쪽이 처리하므로 스킵
        if (other.GetComponent<EnemyBullet>() != null)
            return;

        if (owner == BulletOwner.Player)
        {
            if (other.CompareTag("Enemy"))
            {
                Despawn();
                return;
            }
        }
        else // Enemy owner
        {
            if (other.CompareTag("Player"))
            {
                // ✅ 핵심: 무적/죽음 로직은 PlayerHealth 한 군데로
                var ph = other.GetComponent<PlayerHealth>();
                if (ph != null) ph.Die();

                Despawn();
                return;
            }
        }
    }
}
