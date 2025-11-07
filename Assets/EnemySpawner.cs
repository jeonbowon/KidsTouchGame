using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public GameObject enemyPrefab;   // 적 프리팹 연결용
    public float spawnInterval = 2f; // 생성 주기 (초 단위)
    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            // 화면 위쪽에서 랜덤한 X위치에 적 생성
            Vector3 pos = new Vector3(Random.Range(-8f, 8f), 6f, 0);
            Instantiate(enemyPrefab, pos, Quaternion.identity);

            timer = 0f; // 타이머 초기화
        }
    }
}
