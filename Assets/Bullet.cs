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
    private float _initialBaseSpeed;

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

    // EnemyBullet�� ���� pierce �Һ� "���� ������"�� �ߺ� �߻��ϴ� ���� ����
    private int _lastEnemyBulletHitFrame = -999;

    // 호밍 타겟 캐싱 (매 프레임 OverlapCircleAll 방지)
    private Transform _homingTarget;
    private float _homingSearchTimer = 0f;
    private const float HOMING_SEARCH_INTERVAL = 0.1f; // 0.1초 간격으로만 재탐색

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
        _initialBaseSpeed = baseSpeed;
    }

    void OnEnable()
    {
        _hitOnce.Clear();

        baseSpeed = _initialBaseSpeed;
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
        _homingTarget = null;
        _homingSearchTimer = 0f;

        ApplyVelocityAndRotation();

        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifeTime);
    }

    void FixedUpdate()
    {
        if (owner != BulletOwner.Player) return;
        if (homingStrength <= 0f) return;
        if (rb == null) return;

        // 캐시된 타겟이 비활성화/파괴됐거나 재탐색 주기가 됐을 때만 OverlapCircleAll 호출
        _homingSearchTimer -= Time.fixedDeltaTime;
        if (_homingSearchTimer <= 0f || _homingTarget == null || !_homingTarget.gameObject.activeInHierarchy)
        {
            _homingTarget = FindNearestEnemy(transform.position, 10f);
            _homingSearchTimer = HOMING_SEARCH_INTERVAL;
        }

        if (_homingTarget == null) return;

        Vector2 to = ((Vector2)_homingTarget.position - (Vector2)transform.position);
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
    /// EnemyBullet(�� źȯ)�� �浹���� �� EnemyBullet �ʿ��� ȣ���Ѵ�.
    /// ����(pierceRemain)�� '��ź �浹 1ȸ'�� �Һ��Ѵ�.
    /// ��, ���� �����ӿ� �ߺ� ȣ��Ǵ� ���̽�(����/��ħ/��������)�� ����Ѵ�.
    /// </summary>
    public void OnHitByEnemyBullet(Collider2D enemyBulletCollider)
    {
        if (owner != BulletOwner.Player) return;

        // �ٽ� ����: ���� �����ӿ� EnemyBullet �浹 ó���� 2�� �̻� ������
        // pierce�� �������� �𿩼� "ù Ÿ�� ������" ������ ����.
        // �׷��� EnemyBullet�� ���� pierce �Һ�� 1������ 1ȸ�� ����Ѵ�.
        int f = Time.frameCount;
        if (_lastEnemyBulletHitFrame == f)
        {
            // �׷��� �浹 ���� + ��¦ �о��ִ� �� ���ָ� ������ �پ���.
            if (col != null && enemyBulletCollider != null)
                Physics2D.IgnoreCollision(col, enemyBulletCollider, true);

            transform.position += (Vector3)(dir.normalized * PIERCE_PUSH_DISTANCE);
            ApplyVelocityAndRotation();
            return;
        }
        _lastEnemyBulletHitFrame = f;

        if (pierceRemain > 0)
        {
            pierceRemain--;

            if (col != null && enemyBulletCollider != null)
                Physics2D.IgnoreCollision(col, enemyBulletCollider, true);

            transform.position += (Vector3)(dir.normalized * PIERCE_PUSH_DISTANCE);
            ApplyVelocityAndRotation();
            return;
        }

        Despawn();
    }

    public void DespawnFromOutside()
    {
        Despawn();
    }

    void Despawn()
    {
        speedOverride = -1f;
        if (PoolManager.I != null)
            PoolManager.I.Return(gameObject);
        else
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ź-ź �浹(pierce ī��Ʈ �Һ�)�� EnemyBullet.cs�� Bullet.OnHitByEnemyBullet() ȣ��� ó���Ѵ�.
        // ���⼭ ���� ó���ϸ� "���� Ʈ����"�� 2�� ���̴� ����� ����.
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

            enemyT.GetComponent<ITakeDamage>()?.TakeDamage(dealt);

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

                    t.GetComponent<ITakeDamage>()?.TakeDamage(dealt);
                }
            }

            if (pierceRemain > 0)
            {
                pierceRemain--;
                if (col != null && other != null)
                    Physics2D.IgnoreCollision(col, other, true);

                transform.position += (Vector3)(dir.normalized * PIERCE_PUSH_DISTANCE);
                ApplyVelocityAndRotation();
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
        }
    }
}
