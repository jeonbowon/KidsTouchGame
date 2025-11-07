using System.Collections;
using UnityEngine;

public enum EnemyState { Entering, InFormation, Attacking, Returning, Dead }

[RequireComponent(typeof(Collider2D))]
public class EnemyGalaga : MonoBehaviour
{
    [Header("Common")]
    public float moveToSeatSpeed = 3f;
    public float returnSpeed = 3.5f;
    public float hp = 1f;

    [Header("Shooting")]
    public EnemyShooter shooter; // 하위 컴포넌트로 붙일 것

    // ★ NEW: 폭발/사운드 옵션
    [Header("FX")]
    [SerializeField] private GameObject explosionPrefab;     // 폭발 프리팹
    [SerializeField] private AudioClip dieSfx;               // 사망 사운드(선택)
    [SerializeField] private bool explodeWhenOffscreen = false; // 화면 밖에서 정리될 때도 폭발 낼지

    private EnemyFormation formation;
    private Vector2 localSlot;     // 포메이션 로컬 위치(좌표)
    private float enterDuration;
    private EnemyState state = EnemyState.Entering;
    private float seatLerpT = 0f;
    private bool isDying = false;  // ★ NEW: 중복 폭발/파괴 방지

    private Vector3 targetSeatWorld => formation.transform.position + (Vector3)localSlot;

    public void Init(EnemyFormation formation, Vector2 localSlot, float enterDuration)
    {
        this.formation = formation;
        this.localSlot = localSlot;
        this.enterDuration = enterDuration;

        state = EnemyState.Entering;
        seatLerpT = 0f;

        // 충돌은 Trigger로(총알과만 충돌)
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (shooter == null) shooter = GetComponentInChildren<EnemyShooter>();
        if (shooter != null) shooter.EnableAutoFire(true); // 착석 후 발사하도록 내부에서 제어
    }

    void Update()
    {
        switch (state)
        {
            case EnemyState.Entering:
                seatLerpT += Time.deltaTime / enterDuration;
                transform.position = Vector3.Lerp(transform.position, targetSeatWorld, seatLerpT);
                if (Vector3.Distance(transform.position, targetSeatWorld) < 0.02f)
                {
                    transform.position = targetSeatWorld;
                    state = EnemyState.InFormation;
                    shooter?.OnEnterFormation();
                }
                break;

            case EnemyState.InFormation:
                // 포메이션이 흔들려도 자리 유지하려면, 매 프레임 갱신
                transform.position = targetSeatWorld;
                break;

            case EnemyState.Attacking:
                // 경로는 코루틴에서 처리
                break;

            case EnemyState.Returning:
                transform.position = Vector3.MoveTowards(transform.position, targetSeatWorld, returnSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, targetSeatWorld) < 0.02f)
                {
                    transform.position = targetSeatWorld;
                    state = EnemyState.InFormation;
                    shooter?.OnEnterFormation();
                }
                break;
        }
    }

    public bool CanAttack()
    {
        return state == EnemyState.InFormation && gameObject.activeInHierarchy;
    }

    public void StartAttackRun(AnimationCurve curve)
    {
        if (!CanAttack()) return;
        StartCoroutine(CoAttackRun(curve));
    }

    IEnumerator CoAttackRun(AnimationCurve curve)
    {
        state = EnemyState.Attacking;
        shooter?.OnStartAttack();

        // 간단한 곡선 경로
        float duration = 2.2f;
        float t = 0f;

        Vector3 p0 = targetSeatWorld;
        Vector3 p1 = new Vector3(0f, 0.5f, 0f);
        Vector3 p2 = new Vector3(2.5f, -3f, 0f);
        Vector3 p3 = new Vector3(-2.0f, -5.5f, 0f);
        Vector3 p4 = targetSeatWorld + new Vector3(Random.Range(-1f, 1f), 2.5f, 0f);

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float ct = Mathf.Clamp01(t);
            float w = curve != null ? curve.Evaluate(ct) : ct;

            Vector3 a = Vector3.Lerp(p0, p1, w);
            Vector3 b = Vector3.Lerp(p1, p2, w);
            Vector3 c = Vector3.Lerp(p2, p3, w);
            Vector3 d = Vector3.Lerp(p3, p4, w);

            Vector3 e = Vector3.Lerp(a, b, w);
            Vector3 f = Vector3.Lerp(b, c, w);
            Vector3 g = Vector3.Lerp(c, d, w);

            Vector3 h = Vector3.Lerp(e, f, w);
            Vector3 i = Vector3.Lerp(f, g, w);

            Vector3 pos = Vector3.Lerp(h, i, w);
            transform.position = pos;

            // 진행 방향으로 회전(연출)
            Vector3 dir = (i - h).normalized;
            if (dir.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            yield return null;
        }

        // 복귀 단계
        state = EnemyState.Returning;
        transform.rotation = Quaternion.identity;
        shooter?.OnReturnToFormation();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Player 탄환에 피격
        if (other.CompareTag("Bullet"))
        {
            Destroy(other.gameObject);
            hp -= 1f;
            if (hp <= 0f) Die(); // ★ 변경: Destroy → Die()
        }
    }

    void Die()
    {
        if (isDying) return; // ★ NEW: 중복 방지
        isDying = true;

        state = EnemyState.Dead;
        shooter?.StopAll();

        // ★ NEW: 폭발 이펙트/사운드
        if (explosionPrefab != null)
        {
            if (explodeWhenOffscreen || IsOnScreen(Camera.main))
            {
                Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            }
        }
        if (dieSfx != null)
        {
            AudioSource.PlayClipAtPoint(dieSfx, transform.position);
        }

        // 스테이지 전멸 체크를 위해 GameManager에 보고
        if (GameManager.I != null) GameManager.I.OnEnemyKilled();

        Destroy(gameObject);
    }

    // NEW: 화면 안에 있는지(폭발 연출 조건용)
    private bool IsOnScreen(Camera cam)
    {
        if (cam == null) return true;
        var vp = cam.WorldToViewportPoint(transform.position);
        return (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f);
    }
}
