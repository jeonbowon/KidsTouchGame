using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float speed = 2f; // 아래로 이동 속도
                             
    // 예시 파라미터
    public int hp = 1;
    public float fireInterval = 1.5f;

    // 스테이지별 초기화
    public void InitForStage(int stage)
    {
        hp = 1 + Mathf.FloorToInt(stage * 0.2f);
        fireInterval = Mathf.Max(0.4f, 1.5f - stage * 0.08f); // 단계가 오를수록 빨리 쏘게
    }

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // 폭발 이펙트 등 처리...
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // 파괴가 '게임 진행 중'일 때만 카운트 감소
        if (GameManager.I != null)
        {
            GameManager.I.OnEnemyKilled();
        }
    }
    void Update()
    {
        // 아래 방향으로 이동
        transform.Translate(Vector2.down * speed * Time.deltaTime);

        // 화면 아래로 벗어나면 삭제
        if (transform.position.y < -6f)
        {
            Destroy(gameObject);
        }
    }

    // Bullet과 충돌 시 파괴
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Bullet"))
        {
            Destroy(other.gameObject); // 총알 삭제
            Destroy(gameObject);       // 적 삭제
        }
    }
}
