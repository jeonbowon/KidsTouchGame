﻿using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBullet : MonoBehaviour, IBullet
{
    [Header("속도(기본값)")]
    public float speed = 6f;

    [Header("수명(초)")]
    public float lifetime = 4f;   // 8초 → 4초로 줄여서 화면에 오래 안 남도록

    [Header("폭발 이펙트 프리팹")]
    [SerializeField] private GameObject hitEffectOnPlayer;   // 플레이어에 맞았을 때
    [SerializeField] private GameObject hitEffectOnBullet;   // 탄끼리 부딪힐 때(연기 적은 이펙트)

    [Header("폭발 크기 설정")]
    [SerializeField] private float explosionScaleOnPlayerHit = 1.0f;  // 플레이어 맞을 때(큰 폭발)
    [SerializeField] private float explosionScaleOnBulletHit = 0.4f;  // 탄끼리 부딪힐 때(작은 폭발)

    [Header("사운드 설정")]
    [Tooltip("총알이 부딪힐 때 재생할 효과음 (선택)")]
    [SerializeField] private AudioClip hitSfx;
    [SerializeField, Range(0f, 1f)]
    private float hitSfxVolume = 1.0f;   // 로컬 볼륨(상대값)

    [Header("사운드 스팸 방지(권장)")]
    [Tooltip("같은 프레임에 hitSfx가 너무 많이 나면 귀가 피곤해집니다. 기본 3개까지만 허용.")]
    [SerializeField] private bool limitHitSfxPerFrame = true;

    [SerializeField, Range(1, 10)]
    private int maxHitSfxPerFrame = 3;

    private static int _lastSfxFrame = -1;
    private static int _sfxCountThisFrame = 0;

    // 진행 방향 (아래가 기본)
    private Vector2 _dir = Vector2.down;
    private bool _spawned;

    // 오너(팀킬 방지용)
    private BulletOwner _owner = BulletOwner.Enemy;

    private Rigidbody2D _rb;
    private Collider2D _col;

    // ===== IBullet 구현 =====
    public void SetOwner(BulletOwner owner) => _owner = owner;

    public void ActivateAt(Vector3 pos)
    {
        transform.position = pos;
    }

    public void SetDirection(Vector2 d)
    {
        _dir = (d.sqrMagnitude > 1e-6f) ? d.normalized : Vector2.down;

        float ang = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg + 90f;
        transform.rotation = Quaternion.Euler(0, 0, ang);

        ApplyVelocity();
    }

    public void SetSpeed(float s)
    {
        speed = Mathf.Max(0f, s);
        ApplyVelocity();
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();

        // 트리거는 물리 업데이트(FixedUpdate) 기반으로 안정적으로 처리
        _col.isTrigger = true;

        // EnemyBullet은 “물리로 튕기는 물체”가 아니라 “날아가는 트리거”다.
        // Dynamic도 가능하지만, 안정성을 위해 Kinematic 권장.
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;

        // 충돌 안정성
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void OnEnable()
    {
        _spawned = true;
        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifetime);

        ApplyVelocity();
    }

    void OnDisable()
    {
        _spawned = false;
        CancelInvoke(nameof(Despawn));
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
    }

    private void ApplyVelocity()
    {
        if (_rb == null) return;
        if (!_spawned) return;

        _rb.linearVelocity = _dir * speed;
    }

    private bool CanPlayHitSfxThisFrame()
    {
        if (!limitHitSfxPerFrame) return true;

        int f = Time.frameCount;
        if (_lastSfxFrame != f)
        {
            _lastSfxFrame = f;
            _sfxCountThisFrame = 0;
        }

        if (_sfxCountThisFrame >= maxHitSfxPerFrame) return false;
        _sfxCountThisFrame++;
        return true;
    }

    /// <summary>
    /// 폭발 이펙트 + 사운드 생성. prefab/scaleFactor 로 제어.
    /// </summary>
    private void SpawnHitEffect(GameObject prefab, Vector3 pos, float scaleFactor)
    {
        if (prefab != null)
        {
            GameObject fx = Instantiate(prefab, pos, Quaternion.identity);
            fx.transform.localScale *= scaleFactor;
        }

        if (hitSfx != null && CanPlayHitSfxThisFrame())
        {
            if (SfxManager.I != null) SfxManager.I.PlayExplosion(hitSfx, hitSfxVolume);
            else AudioSource.PlayClipAtPoint(hitSfx, pos, hitSfxVolume);
        }
    }

    /// <summary>
    /// 무적 플레이어에 맞았을 때 호출: 플레이어는 안죽고, 이 총알만 폭발 후 풀로 복귀
    /// </summary>
    public void DespawnWithEffectOnInvincibleHit()
    {
        SpawnHitEffect(hitEffectOnPlayer, transform.position, explosionScaleOnPlayerHit);
        Despawn();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Player 피격
        if (_owner == BulletOwner.Enemy && other.CompareTag("Player"))
        {
            var ph = other.GetComponent<PlayerHealth>();
            if (ph != null && ph.IsInvincible)
            {
                SpawnHitEffect(hitEffectOnPlayer, transform.position, explosionScaleOnPlayerHit);
                Despawn();
                return;
            }

            if (ph != null) ph.Die();

            SpawnHitEffect(hitEffectOnPlayer, transform.position, explosionScaleOnPlayerHit);
            Despawn();
            return;
        }

        // PlayerBullet과 충돌
        Bullet playerBullet = other.GetComponent<Bullet>();
        if (playerBullet != null && playerBullet.owner == BulletOwner.Player)
        {
            Vector3 mid = (transform.position + playerBullet.transform.position) * 0.5f;
            GameObject fxPrefab = hitEffectOnBullet != null ? hitEffectOnBullet : hitEffectOnPlayer;

            SpawnHitEffect(fxPrefab, mid, explosionScaleOnBulletHit);

            // pierce 소비는 Bullet이 한다
            playerBullet.OnHitByEnemyBullet(_col);

            // 적탄은 항상 사라진다.
            Despawn();
        }
    }

    private void Despawn()
    {
        if (!_spawned) return;
        _spawned = false;

        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        gameObject.SetActive(false);
    }
}
