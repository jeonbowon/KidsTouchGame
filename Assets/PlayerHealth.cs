using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Effect")]
    public GameObject explosionPrefab;

    [Header("SFX")]
    [Tooltip("플레이어가 죽을 때(폭발) 재생할 사운드")]
    [SerializeField] private AudioClip playerExplodeSfx;

    [Header("Hit 설정")]
    // 평상시에는 Enemy/EnemyBullet 둘 다 치명타로 처리
    public string[] lethalTags = { "Enemy", "EnemyBullet" };

    [Header("Invincibility")]
    [Tooltip("무적 중 충돌했을 때 상대를 폭발시킬 프리팹(비우면 explosionPrefab 사용)")]
    [SerializeField] private GameObject invincibleHitExplosionPrefab;

    [Tooltip("무적일 때 플레이어가 깜빡이도록(선택)")]
    [SerializeField] private bool blinkWhenInvincible = true;

    [SerializeField] private float blinkInterval = 0.08f;

    [SerializeField, Range(0.5f, 5f)]
    private float playerExplodeVolume = 2.5f;

    public bool IsInvincible => _invincible && !dead;

    private bool dead = false;
    private bool _invincible = false;
    private Coroutine _coInv;

    private SpriteRenderer[] _renderers;

    void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (dead) return;

        // =========================
        // 무적 처리 (중요)
        // =========================
        if (IsInvincible)
        {
            //    EnemyBullet은 여기서 절대 처리하지 않는다.
            //    EnemyBullet.cs가 PlayerHealth.IsInvincible을 보고 자기 스스로 이펙트+Despawn 한다.
            //    PlayerHealth가 EnemyBullet까지 건드리면 "중복 처리"가 발생해서
            //    pierce가 랜덤처럼 보이거나, 탄-탄 충돌이 이상하게 보이는 현상이 생긴다.
            if (other.CompareTag("Enemy"))
            {
                HandleInvincibleCollision_EnemyOnly(other);
            }
            return;
        }

        // =========================
        // 평상시 처리: lethalTags면 죽음
        // =========================
        foreach (var t in lethalTags)
        {
            if (!other.CompareTag(t)) continue;

            // Enemy와 충돌로 죽는 경우: 적도 같이 터뜨리되(점수/드랍 없음), 소리는 나게
            if (other.CompareTag("Enemy"))
            {
                var galaga = other.GetComponent<EnemyGalaga>();
                if (galaga != null)
                {
                    galaga.DespawnWithFxAndSfxNoReward();
                }
                else
                {
                    // EnemyGalaga가 아닌 적이면, 최소한 이펙트라도
                    if (explosionPrefab != null)
                        Instantiate(explosionPrefab, other.transform.position, Quaternion.identity);
                    Destroy(other.gameObject);
                }
            }

            Die();
            return;
        }
    }

    /// <summary>
    /// 무적 중에는 "Enemy만" 처리한다.
    /// EnemyBullet은 PlayerHealth에서 처리하지 않는다(EnemyBullet.cs가 처리).
    /// </summary>
    private void HandleInvincibleCollision_EnemyOnly(Collider2D other)
    {
        // Enemy 폭발 이펙트 만들고, "점수 없이" 제거
        GameObject fx = invincibleHitExplosionPrefab != null ? invincibleHitExplosionPrefab : explosionPrefab;
        if (fx != null)
            Instantiate(fx, other.transform.position, Quaternion.identity);

        // EnemyGalaga는 점수/아이템 없이 제거하는 전용 함수로 처리
        // 무적 충돌에서도 적 폭발음이 필요하면: FX+SFX 포함 제거를 호출해야 함
        var galaga = other.GetComponent<EnemyGalaga>();
        if (galaga != null)
        {
            galaga.DespawnWithFxAndSfxNoReward();
            return;
        }

        Destroy(other.gameObject);
    }

    public void BeginInvincibility(float duration)
    {
        if (dead) return;

        if (_coInv != null)
            StopCoroutine(_coInv);

        _coInv = StartCoroutine(Co_Invincibility(duration));
    }

    private IEnumerator Co_Invincibility(float duration)
    {
        _invincible = true;

        float endTime = Time.time + Mathf.Max(0f, duration);

        // 깜빡임(선택)
        if (!blinkWhenInvincible || _renderers == null || _renderers.Length == 0)
        {
            while (Time.time < endTime) yield return null;
        }
        else
        {
            bool on = true;
            while (Time.time < endTime)
            {
                on = !on;
                SetRenderersVisible(on);
                yield return new WaitForSeconds(blinkInterval);
            }
            SetRenderersVisible(true);
        }

        _invincible = false;
        _coInv = null;
    }

    private void SetRenderersVisible(bool on)
    {
        if (_renderers == null) return;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
                _renderers[i].enabled = on;
        }
    }

    /// <summary>
    /// 외부에서 "죽여라" 호출해도, 무적이면 절대 안 죽도록 보장.
    /// </summary>
    public void Die()
    {
        if (dead) return;

        // 핵심: 무적이면 어떤 경로로도 죽지 않는다
        if (IsInvincible) return;

        dead = true;

        // 1) 소리 먼저 (Player 폭발 사운드)
        if (SfxManager.I != null && playerExplodeSfx != null)
            SfxManager.I.PlayExplosion(playerExplodeSfx, playerExplodeVolume);

        // 2) 이펙트
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // 3) GameManager에 보고
        if (GameManager.I != null)
            GameManager.I.OnPlayerDied();

        // 4) 제거
        Destroy(gameObject);
    }

    // 코스메틱(사운드팩)에서 호출하기 위한 주입 함수
    public void SetPlayerExplodeSfx(AudioClip clip)
    {
        if (clip == null) return;
        playerExplodeSfx = clip;
    }
}
