using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyFormation : MonoBehaviour
{
    [Header("Spawn/Formation")]
    public GameObject enemyPrefab;
    public int columns = 8;
    public int rows = 3;
    public Vector2 cellSize = new Vector2(1.2f, 1.0f);
    public Vector2 topLeft = new Vector2(-6f, 3.5f);  // 대형 내 좌상단 기준점(월드 좌표)
    public float enterFromY = 7f;                      // 적이 위에서 진입할 때 시작 Y
    public float enterDuration = 1.2f;                 // 착석까지 시간

    [Header("Formation Drift (대형 흔들림)")]
    public float driftAmplitude = 0.6f;   // 좌우 흔들림 폭
    public float driftSpeed = 1.0f;       // 흔들림 속도
    public float verticalBobAmplitude = 0.15f; // 상하 파도
    public float verticalBobSpeed = 0.7f;

    [Header("Attack Runs")]
    public float firstAttackDelay = 3.0f;     // 첫 어택 시작까지 대기
    public float attackInterval = 2.0f;       // 어택 주기(몇 초마다 한 기/두 기 출격)
    public int attackersPerWave = 1;          // 웨이브마다 돌격하는 적 수
    public AnimationCurve attackPathCurve;    // 공격 경로용 곡선(0→1)

    private List<EnemyGalaga> enemies = new List<EnemyGalaga>();
    private Vector3 basePos; // 드리프트의 기준점

    void Start()
    {
        basePos = transform.position;
        SpawnFormation();
        StartCoroutine(CoDrift());
        StartCoroutine(CoAttackLoop());
    }

    void SpawnFormation()
    {
        enemies.Clear();
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Vector2 localSlot = new Vector2(
                    topLeft.x + c * cellSize.x,
                    topLeft.y - r * cellSize.y
                );

                // 화면 위에서 시작 → 자리로 착석
                Vector3 spawnPos = new Vector3(localSlot.x, enterFromY, 0f) + transform.position;
                GameObject e = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
                var eg = e.GetComponent<EnemyGalaga>();
                eg.Init(this, localSlot, enterDuration);
                enemies.Add(eg);
            }
        }
    }

    IEnumerator CoDrift()
    {
        float t = 0;
        while (true)
        {
            t += Time.deltaTime;
            float x = Mathf.Sin(t * driftSpeed) * driftAmplitude;
            float y = Mathf.Sin(t * verticalBobSpeed) * verticalBobAmplitude;
            transform.position = basePos + new Vector3(x, y, 0);
            yield return null;
        }
    }

    IEnumerator CoAttackLoop()
    {
        yield return new WaitForSeconds(firstAttackDelay);

        var rnd = new System.Random();
        while (true)
        {
            yield return new WaitForSeconds(attackInterval);

            int launched = 0;
            // 현재 생존 & 포메이션에 앉아있는 적들 후보
            List<EnemyGalaga> candidates = enemies.FindAll(e => e && e.CanAttack());
            if (candidates.Count == 0) continue;

            // attackersPerWave 만큼 무작위 출격
            while (launched < attackersPerWave && candidates.Count > 0)
            {
                int pick = rnd.Next(candidates.Count);
                var eg = candidates[pick];
                candidates.RemoveAt(pick);
                if (eg) eg.StartAttackRun(attackPathCurve);
                launched++;
            }
        }
    }
}
