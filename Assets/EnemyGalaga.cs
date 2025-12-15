using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyGalaga : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseSpeed = 2f;        // 스테이지 1 기본 속도
    public float speedPerStage = 0.3f;  // 스테이지 증가 시 속도 증가량
    public float horizontalAmplitude = 1.2f; // 좌우 진폭
    public float horizontalFrequency = 1f;   // 좌우 속도

    [Tooltip("true 이면 Galaga 패턴으로 위에서 아래로 + 좌우 흔들리며 이동")]
    public bool useGalagaMove = true;   // ★ 보너스 적에서는 false 로 설정

    private float moveSpeed;
    private float sinTime = 0f;

    [Header("HP")]
    public float hp = 1f;

    [Header("Shooting")]
    public EnemyShooter shooter;

    [Header("FX")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private AudioClip dieSfx;

    [Header("Item Drop")]
    [Tooltip("적이 파괴될 때 떨어뜨릴 아이템 프리팹 (ScoreItem 등)")]
    [SerializeField] private GameObject itemPrefab;

    [Tooltip("아이템 드랍 확률 (1이면 항상 드랍)")]
    [Range(0f, 1f)]
    [SerializeField] private float itemDropChance = 1f;

    private bool isDying = false;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (shooter == null)
            shooter = GetComponentInChildren<EnemyShooter>();
    }

    void Start()
    {
        int stage = (GameManager.I != null) ? GameManager.I.CurrentStage : 1;

        moveSpeed = baseSpeed + speedPerStage * (stage - 1);

        if (shooter != null)
            shooter.EnableAutoFire(true);

        sinTime = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (useGalagaMove)
        {
            float dt = Time.deltaTime;
            sinTime += dt * horizontalFrequency;

            float xOffset = Mathf.Sin(sinTime) * horizontalAmplitude;

            transform.position += new Vector3(xOffset * dt, -moveSpeed * dt, 0);

            if (transform.position.y < -6f)
                Destroy(gameObject);
        }
        // useGalagaMove == false 인 경우:
        //  - 이동은 EnemyBonusMover 같은 다른 스크립트가 담당
        //  - 여기서는 충돌/Die 로직만 유지
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Bullet"))
        {
            Destroy(other.gameObject);
            hp -= 1f;

            if (hp <= 0) Die();
        }
    }

    private void Die()
    {
        if (isDying) return;
        isDying = true;

        if (shooter != null)
            shooter.StopAll();

        // 폭발 이펙트
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // ★ 폭발 사운드 (SfxManager 사용)
        if (dieSfx != null)
        {
            if (SfxManager.I != null)
            {
                // localVolume은 1로 두고, 실제 크기는 SfxManager.explosionVolumeMul 로 제어
                SfxManager.I.PlayExplosion(dieSfx, 1f);
            }
            else
            {
                // 백업: 혹시 SfxManager 없을 때
                AudioSource.PlayClipAtPoint(dieSfx, transform.position);
            }
        }

        // 아이템 드랍
        if (itemPrefab != null && Random.value <= itemDropChance)
        {
            Debug.Log($"[EnemyGalaga] Drop item : {itemPrefab.name}", this);
            Instantiate(itemPrefab, transform.position, Quaternion.identity);
        }

        // GameManager에 적 사망 보고
        if (GameManager.I != null)
            GameManager.I.OnEnemyKilled();

        Destroy(gameObject);
    }
}
