using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬별 오브젝트 풀 매니저 (DontDestroyOnLoad 아님 — 씬마다 자체 풀 사용).
/// 사용법: PoolManager.I.Get(prefab, pos) / PoolManager.I.Return(go)
/// </summary>
public class PoolManager : MonoBehaviour
{
    private static PoolManager _instance;

    public static PoolManager I
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("PoolManager");
                _instance = go.AddComponent<PoolManager>();
            }
            return _instance;
        }
    }

    // prefab instanceID → 풀에 대기 중인 오브젝트 스택
    private readonly Dictionary<int, Stack<GameObject>> _pools = new();
    // 스폰된 오브젝트 instanceID → 원본 prefab instanceID
    private readonly Dictionary<int, int> _objToPrefabId = new();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ─────────────────────────────────────────────
    // Public API

    /// <summary>풀에서 오브젝트를 꺼내거나 없으면 새로 생성.</summary>
    public GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot = default)
    {
        int pid = prefab.GetInstanceID();

        if (_pools.TryGetValue(pid, out var stack))
        {
            while (stack.Count > 0)
            {
                var candidate = stack.Pop();
                if (candidate != null)
                {
                    candidate.transform.SetPositionAndRotation(pos, rot);
                    candidate.SetActive(true);
                    return candidate;
                }
                // null이면 외부에서 Destroy된 것 — 다음 항목 시도
            }
        }

        var inst = Instantiate(prefab, pos, rot);
        _objToPrefabId[inst.GetInstanceID()] = pid;
        return inst;
    }

    /// <summary>컴포넌트 타입으로 풀에서 꺼내기.</summary>
    public T Get<T>(T prefabComponent, Vector3 pos, Quaternion rot = default) where T : Component
    {
        return Get(prefabComponent.gameObject, pos, rot).GetComponent<T>();
    }

    /// <summary>오브젝트를 비활성화하고 풀에 반환.</summary>
    public void Return(GameObject go)
    {
        if (go == null) return;

        int iid = go.GetInstanceID();
        if (!_objToPrefabId.TryGetValue(iid, out int pid))
        {
            // 이 풀에서 생성된 오브젝트가 아님 — 그냥 파괴
            Destroy(go);
            return;
        }

        go.SetActive(false);

        if (!_pools.TryGetValue(pid, out var stack))
        {
            stack = new Stack<GameObject>();
            _pools[pid] = stack;
        }
        stack.Push(go);
    }

    /// <summary>씬 시작 시 풀을 미리 워밍업 (선택사항).</summary>
    public void WarmUp(GameObject prefab, int count)
    {
        int pid = prefab.GetInstanceID();
        if (!_pools.TryGetValue(pid, out var stack))
        {
            stack = new Stack<GameObject>();
            _pools[pid] = stack;
        }

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            _objToPrefabId[go.GetInstanceID()] = pid;
            go.SetActive(false);
            stack.Push(go);
        }
    }
}
