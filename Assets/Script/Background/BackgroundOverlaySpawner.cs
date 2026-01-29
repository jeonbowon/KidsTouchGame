using System.Collections;
using UnityEngine;

public class BackgroundOverlaySpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnRoot; // 없으면 자기 자신
    private Coroutine _loop;

    private void Reset()
    {
        spawnRoot = transform;
    }

    public void StopAll()
    {
        if (_loop != null) StopCoroutine(_loop);
        _loop = null;
    }

    public void ApplyTheme(BackgroundThemeConfig theme)
    {
        StopAll();

        if (theme == null) return;
        if (!theme.enableOverlay) return;
        if (theme.overlayPrefabs == null || theme.overlayPrefabs.Length == 0) return;

        // 시작 버스트 스폰
        int burst = Random.Range(theme.overlayBurstCountRange.x, theme.overlayBurstCountRange.y + 1);
        for (int i = 0; i < burst; i++)
            SpawnOne(theme);

        // 주기 스폰
        if (theme.overlaySpawnIntervalRange.x > 0f && theme.overlaySpawnIntervalRange.y > 0f)
        {
            _loop = StartCoroutine(Co_Loop(theme));
        }
    }

    private IEnumerator Co_Loop(BackgroundThemeConfig theme)
    {
        while (true)
        {
            float wait = Random.Range(theme.overlaySpawnIntervalRange.x, theme.overlaySpawnIntervalRange.y);
            yield return new WaitForSeconds(wait);
            SpawnOne(theme);
        }
    }

    private void SpawnOne(BackgroundThemeConfig theme)
    {
        if (spawnRoot == null) spawnRoot = transform;

        var prefab = theme.overlayPrefabs[Random.Range(0, theme.overlayPrefabs.Length)];
        if (prefab == null) return;

        // 화면 밖 위쪽에서 시작해서 아래로 지나가게 만드는 게 가장 안전합니다.
        // 카메라 기준 월드 좌표로 생성 위치를 잡습니다.
        var cam = Camera.main;
        if (cam == null) return;

        float worldH = cam.orthographicSize * 2f;
        float worldW = worldH * cam.aspect;

        float x = Random.Range(-worldW * 0.45f, worldW * 0.45f);
        float y = worldH * 0.6f; // 화면 위쪽 조금 바깥

        var go = Instantiate(prefab, new Vector3(x, y, 0f), Quaternion.identity, spawnRoot);

        if (theme.overlayLifeTime > 0f)
            Destroy(go, theme.overlayLifeTime);
    }
}
