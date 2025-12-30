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

        // 무적이면: 플레이어는 죽지 않고, 상대만 폭파/제거
        if (IsInvincible)
        {
            foreach (var t in lethalTags)
            {
                if (other.CompareTag(t))
                {
                    HandleInvincibleCollision(other);
                    return;
                }
            }
            return;
        }

        // 평상시: lethalTags 충돌이면 죽음
        foreach (var t in lethalTags)
        {
            if (other.CompareTag(t))
            {
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
    }

    private void HandleInvincibleCollision(Collider2D other)
    {
        // 1) 상대가 EnemyBullet이면: 이 총알만 폭발 후 비활성(풀)로
        var eb = other.GetComponent<EnemyBullet>();
        if (eb != null)
        {
            eb.DespawnWithEffectOnInvincibleHit();
            return;
        }

        // 2) 그 외(Enemy 등): 폭발 이펙트 만들고, "점수 없이" 제거
        GameObject fx = invincibleHitExplosionPrefab != null ? invincibleHitExplosionPrefab : explosionPrefab;
        if (fx != null)
            Instantiate(fx, other.transform.position, Quaternion.identity);

        // EnemyGalaga는 점수/아이템 없이 제거하는 전용 함수로 처리(무적 충돌은 조용히 제거 유지)
		// 무적 충돌에서도 적 폭발음이 필요하면: 조용히 제거(DespawnNoScore)가 아니라 FX+SFX 포함 제거를 호출해야 합니다.
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
