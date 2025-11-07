using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [Header("Effect")]
    public GameObject explosionPrefab;

    [Header("Hit 설정")]
    public string[] lethalTags = { "Enemy", "EnemyBullet" }; // 필요하면 태그 추가

    private bool dead = false;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (dead) return;

        foreach (var t in lethalTags)
        {
            if (other.CompareTag(t))
            {
                Die();
                return;
            }
        }
    }

    public void Die()
    {
        if (dead) return;
        dead = true;

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // 플레이어 오브젝트 제거(또는 비활성화)
        Destroy(gameObject);

        // GameManager에 알림
        if (GameManager.I != null)
            GameManager.I.OnPlayerDied();
    }
}
