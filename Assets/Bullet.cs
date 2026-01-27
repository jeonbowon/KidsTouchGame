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

    private const float PIERCE_PUSH_DISTANCE = 0.12f;

    // EnemyBullet로 인한 pierce 소비가 "같은 프레임"에 중복 발생하는 것을 차단
    private int _lastEnemyBulletHitFrame = -999;

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
        if (weapon == null || weapon.category != CosmeticCategory.Weapon)
            return;

        damage = Mathf.Max(0, Mathf.RoundToInt(1f * weapon.damageMul));

        critChance = Mathf.Clamp01(weapon.critChance);
        critMultiplier = Mathf.Max(1f, weapon.critMul);

        pierceRemain = Mathf.Max(0, weapon.pierceCount);
        explosionRadius = Mathf.Max(0f, weapon.explosionRadius);

        homingStrength = Mathf.Max(0f, weapon.homingStrength);
        turnRate = Mathf.Max(0f, weapon.turnRate);

        float hitMul = Mathf.Max(0.1f, weapon.hitRadiusMul);
        transform.localScale = _initialLocalScale * hitMul;

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

        if (sr != null) sr.sprite = initialSprite;

        _lastEnemyBulletHitFrame = -999;

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

        float maxTurn = Mathf.Max(0f, turnRate) * Time.fixedDeltaTime;
        Vector2 next = Vector2.Lerp(current, desired, Mathf.Clamp01(homingStrength));
        next = Vector2.Lerp(current, next, Mathf.Clamp01(maxTurn));

        dir = next.normalized;
        ApplyVelocityAndRotation();
    }

    private Transform FindNearestEnemy(Vector3 from, float radius)
    {
        var hits = Physics2D.OverlapCircleAll(from, radius);
        Transform best = null;
        float bestD = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null) continue;
            if (!h.CompareTag("Enemy")) continue;

            Transform t = (h.attachedRigidbody != null) ? h.attachedRigidbody.transform : h.transform;
            float d = ((Vector2)t.position - (Vector2)from).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = t;
            }
        }

        return best;
    }

    private void ApplyVelocityAndRotation()
    {
        if (rb == null) return;

        float spd = (speedOverride >= 0f) ? speedOverride : baseSpeed;

        rb.linearVelocity = dir.normalized * spd;

        if (spritePointsUp) transform.up = dir;
        else transform.right = dir;
    }

    /// <summary>
    /// EnemyBullet(적 탄환)과 충돌했을 때 EnemyBullet 쪽에서 호출한다.
    /// 관통(pierceRemain)을 '적탄 충돌 1회'로 소비한다.
    /// 단, 같은 프레임에 중복 호출되는 케이스(정면/겹침/동시접촉)를 방어한다.
    /// </summary>
    public void OnHitByEnemyBullet(Collider2D enemyBulletCollider)
    {
        if (owner != BulletOwner.Player) return;

        // 핵심 가드: 같은 프레임에 EnemyBullet 충돌 처리가 2번 이상 들어오면
        // pierce가 연속으로 깎여서 "첫 타에 터지는" 현상이 난다.
        // 그래서 EnemyBullet에 의한 pierce 소비는 1프레임 1회만 허용한다.
        int f = Time.frameCount;
        if (_lastEnemyBulletHitFrame == f)
        {
            // 그래도 충돌 무시 + 살짝 밀어주는 건 해주면 끼임이 줄어든다.
            if (col != null && enemyBulletCollider != null)
                Physics2D.IgnoreCollision(col, enemyBulletCollider, true);

            transform.position += (Vector3)(dir.normalized * PIERCE_PUSH_DISTANCE);
            ApplyVelocityAndRotation();
            return;
        }
        _lastEnemyBulletHitFrame = f;

        Debug.Log($"[PierceDBG] Hit EnemyBullet | pierceRemain BEFORE = {pierceRemain}");

        if (pierceRemain > 0)
        {
            pierceRemain--;
            Debug.Log($"[PierceDBG] Pierce consumed by EnemyBullet, remain = {pierceRemain}");

            if (col != null && enemyBulletCollider != null)
                Physics2D.IgnoreCollision(col, enemyBulletCollider, true);

            transform.position += (Vector3)(dir.normalized * PIERCE_PUSH_DISTANCE);
            ApplyVelocityAndRotation();
            return;
        }

        Debug.Log("[PierceDBG] No pierce left (EnemyBullet) -> Despawn");
        Despawn();
    }

    public void DespawnFromOutside()
    {
        Despawn();
    }

    void Despawn()
    {
        Debug.Log($"[PierceDBG] Bullet Despawn() called. pierceRemain={pierceRemain}");
        speedOverride = -1f;
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 탄-탄 충돌(pierce 카운트 소비)은 EnemyBullet.cs가 Bullet.OnHitByEnemyBullet() 호출로 처리한다.
        // 여기서 같이 처리하면 "양쪽 트리거"로 2번 깎이는 사고가 난다.
        if (other.GetComponent<EnemyBullet>() != null)
            return;

        if (owner == BulletOwner.Player)
        {
            if (!other.CompareTag("Enemy"))
                return;

            Transform enemyT = (other.attachedRigidbody != null) ? other.attachedRigidbody.transform : other.transform;

            int id = enemyT.GetInstanceID();
            if (_hitOnce.Contains(id))
                return;
            _hitOnce.Add(id);

            int dealt = damage;
            if (critChance > 0f && Random.value < critChance)
                dealt = Mathf.RoundToInt(dealt * critMultiplier);

            enemyT.SendMessage("TakeDamage", dealt, SendMessageOptions.DontRequireReceiver);

            if (explosionRadius > 0f)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
                HashSet<int> boomOnce = new HashSet<int>();

                for (int i = 0; i < hits.Length; i++)
                {
                    var h = hits[i];
                    if (h == null) continue;
                    if (!h.CompareTag("Enemy")) continue;

                    Transform t = (h.attachedRigidbody != null) ? h.attachedRigidbody.transform : h.transform;
                    int tid = t.GetInstanceID();
                    if (boomOnce.Contains(tid)) continue;
                    boomOnce.Add(tid);

                    t.SendMessage("TakeDamage", dealt, SendMessageOptions.DontRequireReceiver);
                }
            }

            Debug.Log($"[PierceDBG] Hit Enemy | pierceRemain BEFORE = {pierceRemain}");

            if (pierceRemain > 0)
            {
                pierceRemain--;
                Debug.Log($"[PierceDBG] Pierce consumed, remain = {pierceRemain}");
                if (col != null && other != null)
                    Physics2D.IgnoreCollision(col, other, true);

                transform.position += (Vector3)(dir.normalized * PIERCE_PUSH_DISTANCE);
                ApplyVelocityAndRotation();
                return;
            }

            Debug.Log("[PierceDBG] No pierce left -> Despawn");
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
        }
    }
}
