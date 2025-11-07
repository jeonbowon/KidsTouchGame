using System.Collections;
using UnityEngine;

public class EnemyShooter : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private GameObject bulletPrefab;   // 반드시 IBullet 구현이 붙은 프리팹
    [SerializeField] private Transform muzzle;          // 없으면 transform.position 사용

    [Header("Fire Intervals")]
    [SerializeField] private float fireIntervalInFormation = 2.5f; // 포메이션 중 사격 간격
    [SerializeField] private float fireIntervalAttacking = 0.6f;  // 어택 중 사격 간격

    [Header("Auto")]
    [SerializeField] private bool autoStartOnEnable = true; // 활성화 시 자동 발사 시작
    [SerializeField] private bool logVerbose = true; // 상세 로그

    // 상태
    private bool autoFire = false;
    private bool inFormation = false;
    private bool isAttacking = false;
    private Coroutine coFire;

    // 간단 풀
    private const int POOL_SIZE = 32;
    private int poolCursor = 0;
    private GameObject[] pool;

    void Awake()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("[EnemyShooter] bulletPrefab 이 비어있습니다. 프리팹을 할당하세요.", this);
            return;
        }

        // 프리팹에 'IBullet' 구현이 있는지 미리 검사(루트 또는 자식)
        if (bulletPrefab.GetComponent<IBullet>() == null &&
            bulletPrefab.GetComponentInChildren<IBullet>(true) == null)
        {
            Debug.LogError("[EnemyShooter] bulletPrefab 에 IBullet 구현이 없습니다. " +
                           "EnemyBullet/Bullet 중 하나가 IBullet을 구현해야 합니다.", bulletPrefab);
        }

        // 풀 생성
        pool = new GameObject[POOL_SIZE];
        for (int i = 0; i < POOL_SIZE; i++)
        {
            pool[i] = Instantiate(bulletPrefab);
            pool[i].SetActive(false);
        }

        if (logVerbose) Debug.Log($"[EnemyShooter] Pool created x{POOL_SIZE}", this);
    }

    void OnEnable()
    {
        if (autoStartOnEnable) EnableAutoFire(true);
    }

    void OnDisable()
    {
        StopAll();
    }

    public void EnableAutoFire(bool on)
    {
        autoFire = on;
        if (logVerbose) Debug.Log($"[EnemyShooter] AutoFire={(on ? "ON" : "OFF")}", this);
        RestartFire();
    }

    public void OnEnterFormation()
    {
        inFormation = true;
        isAttacking = false;
        if (logVerbose) Debug.Log("[EnemyShooter] InFormation", this);
        RestartFire();
    }

    public void OnStartAttack()
    {
        inFormation = false;
        isAttacking = true;
        if (logVerbose) Debug.Log("[EnemyShooter] Attacking", this);
        RestartFire();
    }

    public void OnReturnToFormation()
    {
        inFormation = true;
        isAttacking = false;
        if (logVerbose) Debug.Log("[EnemyShooter] ReturnToFormation", this);
        RestartFire();
    }

    public void StopAll()
    {
        if (coFire != null)
        {
            StopCoroutine(coFire);
            coFire = null;
        }
    }

    private void RestartFire()
    {
        if (!autoFire || bulletPrefab == null) return;

        if (coFire != null) StopCoroutine(coFire);
        coFire = StartCoroutine(CoFireLoop());
        if (logVerbose) Debug.Log("[EnemyShooter] Fire loop started", this);
    }

    private IEnumerator CoFireLoop()
    {
        while (true)
        {
            float iv = inFormation ? fireIntervalInFormation : fireIntervalAttacking;
            FireOne();
            yield return new WaitForSeconds(iv);
        }
    }

    /// <summary>필요 시 버튼/이벤트에서 수동 테스트용</summary>
    public void ManualFire() => FireOne();

    private void FireOne()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError("[EnemyShooter] bulletPrefab 이 비어있어 발사 불가", this);
            return;
        }

        GameObject go = FetchBullet();
        if (go == null)
        {
            Debug.LogError("[EnemyShooter] FetchBullet() 가 null을 반환", this);
            return;
        }

        Vector3 pos = (muzzle != null) ? muzzle.position : transform.position;

        // 플레이어 조준(없으면 아래)
        Vector2 dir = Vector2.down;
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null) dir = ((Vector2)player.position - (Vector2)pos).normalized;

        float enemySpeed = (GameManager.I != null) ? GameManager.I.GetEnemyBulletSpeed() : 4f;

        var b = go.GetComponent<IBullet>();
        if (b == null)
        {
            Debug.LogError("[EnemyShooter] 가져온 탄에 IBullet 구현이 없습니다.", go);
            go.SetActive(false);
            return;
        }

        // 활성화 전 설정(위치/회전/상태)
        go.transform.position = pos;
        go.transform.rotation = Quaternion.identity;

        b.SetOwner(BulletOwner.Enemy);
        b.SetDirection(dir);
        b.SetSpeed(enemySpeed);
        b.ActivateAt(pos); // 필요 시 각 탄 내부 초기화

        go.SetActive(true);

        if (logVerbose)
            Debug.Log($"[EnemyShooter] FIRE  pos={pos} dir={dir} spd={enemySpeed} stage={GameManager.I?.CurrentStage}", this);
    }

    private GameObject FetchBullet()
    {
        if (pool == null || pool.Length == 0)
        {
            if (bulletPrefab == null) return null;
            var extra = Instantiate(bulletPrefab);
            extra.SetActive(false);
            if (logVerbose) Debug.Log("[EnemyShooter] Pool empty -> extra bullet instantiated", this);
            return extra;
        }

        for (int i = 0; i < pool.Length; i++)
        {
            poolCursor = (poolCursor + 1) % pool.Length;
            if (!pool[poolCursor].activeSelf) return pool[poolCursor];
        }

        var extra2 = Instantiate(bulletPrefab);
        extra2.SetActive(false);
        if (logVerbose) Debug.Log("[EnemyShooter] Pool full -> extra bullet instantiated", this);
        return extra2;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 p = (muzzle != null) ? muzzle.position : transform.position;
        Gizmos.DrawWireSphere(p, 0.08f);
    }
#endif
}
