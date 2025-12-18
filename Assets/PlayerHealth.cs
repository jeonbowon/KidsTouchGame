using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Effect")]
    public GameObject explosionPrefab;

    [Header("Hit 설정")]
    public string[] lethalTags = { "Enemy", "EnemyBullet" }; // 필요하면 태그 추가

    [Header("Invincibility")]
    [Tooltip("무적 중 충돌했을 때 상대를 폭발시킬 프리팹(비우면 explosionPrefab 사용)")]
    [SerializeField] private GameObject invincibleHitExplosionPrefab;

    [Tooltip("무적일 때 플레이어가 깜빡이도록(선택)")]
    [SerializeField] private bool blinkWhenInvincible = true;

    [SerializeField] private float blinkInterval = 0.08f;

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
                Die(); // 무적 아님이 보장된 상태
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

        // 핵심: 무적 충돌은 Destroy(...)로 바로 지우면
        // Enemy 내부의 Die() 경로를 타지 않아 카운트/정리 로직이 누락될 수 있음.
        // - EnemyGalaga는 점수/아이템 없이 제거하는 전용 함수로 처리
        // - 그 외는 Destroy하더라도 각 Enemy의 OnDestroy에서 카운트 보정하도록(이번 수정에서) 안전장치 추가
        var galaga = other.GetComponent<EnemyGalaga>();
        if (galaga != null)
        {
            galaga.DespawnNoScore();
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

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // 먼저 GameManager에 보고 (리스폰 코루틴이 확실히 돌게)
        if (GameManager.I != null)
            GameManager.I.OnPlayerDied();

        // 그 다음 제거
        Destroy(gameObject);
    }
}
