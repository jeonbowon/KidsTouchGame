using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyBullet : MonoBehaviour, IBullet
{
    [Header("속도(기본값)")]
    public float speed = 6f;

    [Header("수명(초)")]
    public float lifetime = 5f;

    [Header("스프라이트가 ↑가 앞이면 체크")]
    public bool spritePointsUp = true;

    private Vector2 _dir = Vector2.down;
    private bool _spawned;

    // 추가: 오너 보관 + 인터페이스 구현
    private BulletOwner _owner = BulletOwner.Enemy;
    public void SetOwner(BulletOwner owner) => _owner = owner;
    

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

    // ===== IBullet =====
    public void ActivateAt(Vector3 pos) => transform.position = pos;

    public void SetDirection(Vector2 d)
    {
        _dir = (d.sqrMagnitude > 1e-6f) ? d.normalized : Vector2.down;
        float ang = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg + (spritePointsUp ? -90f : 0f);
        transform.rotation = Quaternion.Euler(0, 0, ang);
    }

    public void SetSpeed(float s) => speed = Mathf.Max(0f, s);
    // ===================

    void Update()
    {
        transform.Translate(_dir * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // (선택) 팀킬 방지: 오너가 Enemy일 때만 Player에 반응
        if (_owner == BulletOwner.Enemy && other.CompareTag("Player"))
        {
            GameManager.I?.OnPlayerDied();
            Destroy(other.gameObject);
            Despawn();
        }
    }

    private void Despawn()
    {
        if (!_spawned) return;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }
}
