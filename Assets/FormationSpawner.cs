using System.Collections.Generic;
using UnityEngine;

public class FormationSpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] formationPoints;
    // 포메이션 기준점들(그리드의 좌표/웨이포인트 등). 이미 자체 로직이 있으면 그걸 사용.

    private readonly List<GameObject> _spawnedEnemies = new();

    /// <summary>
    /// 스테이지 번호에 따라 적의 수, 속도, 발사 빈도 등을 조정해서 생성
    /// 반환: 생성된 적 총 수
    /// </summary>
    public int SpawnFormation(int stage)
    {
        ClearList();
        int count = 0;

        // 예시: stage가 오를수록 더 많은 포인트를 사용
        int useCount = Mathf.Min(formationPoints.Length, 5 + stage);

        for (int i = 0; i < useCount; i++)
        {
            var pos = formationPoints[i].position;
            var e = Instantiate(enemyPrefab, pos, Quaternion.identity);
            _spawnedEnemies.Add(e);

            // (선택) 적 스테이터스 강화: 속도, 발사 간격 조절 같은 파라미터를 stage 기반으로 세팅
            var enemy = e.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.InitForStage(stage);
            }

            count++;
        }

        return count;
    }

    public void KillAllEnemiesAndBullets()
    {
        // 적 전부 제거
        foreach (var e in _spawnedEnemies)
        {
            if (e != null) Destroy(e);
        }
        ClearList();

        // 씬 내 남은 EnemyBullet 제거
        var bullets = GameObject.FindGameObjectsWithTag("EnemyBullet");
        foreach (var b in bullets)
        {
            Destroy(b);
        }
    }

    private void ClearList()
    {
        _spawnedEnemies.RemoveAll(x => x == null);
        _spawnedEnemies.Clear();
    }
}
