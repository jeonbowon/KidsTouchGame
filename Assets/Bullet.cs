using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Bullet : MonoBehaviour, IBullet
{
    [Header("Base Speed (player bullets get stage multiplier)")]
    public float baseSpeed = 12f;

    [Header("Life (sec)")]
    public float lifeTime = 3f;

    [Header("Sprite Forward")]
    public bool spritePointsUp = true;

    [Header("Owner")]
    public BulletOwner owner = BulletOwner.Player;

    private Vector2 dir = Vector2.up;
    private Rigidbody2D rb;
    private Collider2D col;

    private float speedOverride = -1f;

    // Visual
    private SpriteRenderer sr;
    private Sprite initialSprite;
    private Vector3 _initialLocalScale;

    // Weapon runtime (optional)
    private int damage = 1;
    private float critChance = 0f;
    private float critMultiplier = 2f;
    private int pierceRemain = 0;
    private float explosionRadius = 0f;
    private float homingStrength = 0f;
    private float turnRate = 0f;

    private readonly HashSet<int> _hitOnce = new HashSet<int>();

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

    public void ApplyWeapon(CosmeticItem weapon)
    {
        // Weapon만 적용
        if (weapon == null || weapon.category != CosmeticCategory.Weapon)
            return;

        // ==== 스탯 적용 ====
        damage = Mathf.Max(0, Mathf.RoundToInt(1f * weapon.damageMul));

        critChance = Mathf.Clamp01(weapon.critChance);
        critMultiplier = Mathf.Max(1f, weapon.critMul);

        pierceRemain = Mathf.Max(0, weapon.pierceCount);
        explosionRadius = Mathf.Max(0f, weapon.explosionRadius);

        homingStrength = Mathf.Max(0f, weapon.homingStrength);
        turnRate = Mathf.Max(0f, weapon.turnRate);

        float hitMul = Mathf.Max(0.1f, weapon.hitRadiusMul);
        transform.localScale = _initialLocalScale * hitMul;

        // ==== 비주얼(스킨) 적용 ====
        // bulletSprite 우선, 없으면 icon 사용
        if (sr != null)
        {
            Sprite s = weapon.bulletSprite != null ? weapon.bulletSprite : weapon.icon;
            if (s != null)
                sr.sprite = s;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        col = GetComponent<Collider2D>();
        col.isTrigger = true;

        sr = GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null) initialSprite = sr.sprite;

        _initialLocalScale = transform.localScale;
    }

    void OnEnable()
    {
        _hitOnce.Clear();

        damage = 1;
        critChance = 0f;
        critMultiplier = 2f;
        pierceRemain = 0;
        explosionRadius = 0f;
        homingStrength = 0f;
        turnRate = 0f;

        transform.localScale = _initialLocalScale;

        // 기본 스프라이트로 리셋 (그 뒤 PlayerShoot에서 ApplyWeapon으로 다시 바뀜)
        if (sr != null) sr.sprite = initialSprite;

        ApplyVelocityAndRotation();

        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifeTime);
    }

    void FixedUpdate()
    {
        if (owner != BulletOwner.Player) return;
        if (homingStrength <= 0f) return;
        if (rb == null) return;

        var target = FindNearestEnemy(transform.position, 10f);
        if (target == null) return;

        Vector2 to = ((Vector2)target.position - (Vector2)transform.position);
        if (to.sqrMagnitude < 0.0001f) return;

        Vector2 desired = to.normalized;

        Vector2 current = rb.linearVelocity.sqrMagnitude > 0.0001f ? rb.linearVelocity.normalized : dir;

        float maxStep = (turnRate > 0f) ? (turnRate * Time.fixedDeltaTime) : (360f * Time.fixedDeltaTime);
        Vector2 newDir = RotateTowards(current, desired, maxStep);

        newDir = Vector2.Lerp(current, newDir, Mathf.Clamp01(homingStrength)).normalized;

        dir = newDir;
        ApplyVelocityAndRotation();
    }

    private static Vector2 RotateTowards(Vector2 from, Vector2 to, float maxDegrees)
    {
        float angle = Vector2.SignedAngle(from, to);
        float clamped = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
        float rad = clamped * Mathf.Deg2Rad;
        float cs = Mathf.Cos(rad);
        float sn = Mathf.Sin(rad);
        return new Vector2(from.x * cs - from.y * sn, from.x * sn + from.y * cs).normalized;
    }

    private static Transform FindNearestEnemy(Vector2 pos, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius);
        Transform best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;
            if (!h.CompareTag("Enemy")) continue;

            float sqr = ((Vector2)h.transform.position - pos).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = h.transform;
            }
        }
        return best;
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
        if (other.GetComponent<EnemyBullet>() != null)
            return;

        if (owner == BulletOwner.Player)
        {
            if (!other.CompareTag("Enemy"))
                return;

            int id = other.GetInstanceID();
            if (_hitOnce.Contains(id))
                return;
            _hitOnce.Add(id);

            int dealt = damage;
            if (critChance > 0f && Random.value < critChance)
                dealt = Mathf.RoundToInt(dealt * critMultiplier);

            other.SendMessage("TakeDamage", dealt, SendMessageOptions.DontRequireReceiver);

            if (explosionRadius > 0f)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
                for (int i = 0; i < hits.Length; i++)
                {
                    var h = hits[i];
                    if (h == null) continue;
                    if (!h.CompareTag("Enemy")) continue;
                    h.SendMessage("TakeDamage", dealt, SendMessageOptions.DontRequireReceiver);
                }
            }

            if (pierceRemain > 0)
            {
                pierceRemain--;
                return;
            }

            Despawn();
            return;
        }
        else
        {
            if (!other.CompareTag("Player"))
                return;

            var ph = other.GetComponent<PlayerHealth>();
            if (ph != null) ph.Die();

            Despawn();
            return;
        }
    }
}
