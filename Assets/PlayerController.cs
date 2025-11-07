using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("옵션")]
    public GameObject explosionPrefab;

    private bool isDead = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDead) return;

        // 적의 총알에 닿으면 파괴
        if (other.CompareTag("EnemyBullet"))
        {
            Die();
        }
        // (선택) 적 본체와 충돌 시도 동일 처리
        else if (other.CompareTag("Enemy"))
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // 플레이어 오브젝트 제거
        Destroy(gameObject);

        // GM에게 보고 → 목숨 감소/리스폰/게임오버 판단
        if (GameManager.I != null)
            GameManager.I.OnPlayerDied();
    }
}
