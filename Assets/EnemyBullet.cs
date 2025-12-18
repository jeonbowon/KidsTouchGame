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

    // 진행 방향 (아래가 기본)
    private Vector2 _dir = Vector2.down;
    private bool _spawned;

    // 오너(팀킬 방지용)
    private BulletOwner _owner = BulletOwner.Enemy;

    // 물리
    private Rigidbody2D _rb;

    // ===== IBullet 구현 =====
    public void SetOwner(BulletOwner owner) => _owner = owner;

    public void ActivateAt(Vector3 pos)
    {
        transform.position = pos;
    }

    public void SetDirection(Vector2 d)
    {
        // 방향 벡터 정규화, 기본은 아래
        _dir = (d.sqrMagnitude > 1e-6f) ? d.normalized : Vector2.down;

        // ★ 스프라이트가 "아래(↓)"를 기본 방향으로 그려져 있다고 가정
        // dir = (0,-1) 이면 각도 0도, 그대로 보이게 +90 보정
        float ang = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg + 90f;
        transform.rotation = Quaternion.Euler(0, 0, ang);
    }

    public void SetSpeed(float s)
    {
        speed = Mathf.Max(0f, s);
    }
    // ========================

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();

        _rb.gravityScale = 0f;
        _rb.angularVelocity = 0f;
        _rb.rotation = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnEnable()
    {
        _spawned = true;
        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), lifetime);
    }

    void OnDisable()
    {
        _spawned = false;
        CancelInvoke(nameof(Despawn));
    }

    void Update()
    {
        transform.Translate(_dir * speed * Time.deltaTime, Space.World);
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

        if (hitSfx != null)
        {
            if (SfxManager.I != null)
            {
                // 폭발 전용 볼륨 계수를 사용
                SfxManager.I.PlayExplosion(hitSfx, hitSfxVolume);
            }
            else
            {
                // 매니저가 없을 경우를 대비한 백업
                AudioSource.PlayClipAtPoint(hitSfx, pos, hitSfxVolume);
            }
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
        // 1) Player에 맞았을 때
        if (_owner == BulletOwner.Enemy && other.CompareTag("Player"))
        {
            // ✅ 플레이어 죽음 처리는 PlayerHealth가 담당하도록 통일
            var ph = other.GetComponent<PlayerHealth>();
            if (ph != null && ph.IsInvincible)
            {
                // 무적이면: 큰 폭발만 보여주고 총알만 사라짐
                SpawnHitEffect(hitEffectOnPlayer, transform.position, explosionScaleOnPlayerHit);
                Despawn();
                return;
            }

            // 무적이 아니면: PlayerHealth.Die() 호출(중복 호출돼도 내부 dead 플래그로 안전)
            if (ph != null)
                ph.Die();

            SpawnHitEffect(hitEffectOnPlayer, transform.position, explosionScaleOnPlayerHit);
            Despawn();
            return;
        }

        // 2) 플레이어 탄과 서로 부딪혔을 때
        Bullet playerBullet = other.GetComponent<Bullet>();
        if (playerBullet != null && playerBullet.owner == BulletOwner.Player)
        {
            // 총알끼리 부딪힐 땐 연기 적은 작은 폭발
            Vector3 mid = (transform.position + playerBullet.transform.position) * 0.5f;
            // 만약 hitEffectOnBullet 이 비어있으면 hitEffectOnPlayer 를 대신 사용
            GameObject fxPrefab = hitEffectOnBullet != null ? hitEffectOnBullet : hitEffectOnPlayer;

            SpawnHitEffect(fxPrefab, mid, explosionScaleOnBulletHit);

            playerBullet.DespawnFromOutside();
            Despawn();
        }
    }

    private void Despawn()
    {
        if (!_spawned) return;
        _spawned = false;

        // ⭕ Destroy 하지 않고 풀로 되돌아감
        if (this != null && gameObject != null)
        {
            gameObject.SetActive(false);
        }
    }
}
