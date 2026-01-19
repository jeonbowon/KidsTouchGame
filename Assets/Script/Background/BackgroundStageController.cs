using System;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundStageController : MonoBehaviour
{
    [Header("Options")]
    [SerializeField] private bool defaultExclusive = true;

    // key -> layer
    private readonly Dictionary<string, BackgroundLayer> _layers = new Dictionary<string, BackgroundLayer>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        CacheLayers();
    }

    private void OnEnable()
    {
        ApplyFromStageConfig();
    }

    private void CacheLayers()
    {
        _layers.Clear();

        // BG_Root 아래 모든 BackgroundLayer 자동 수집
        var layers = GetComponentsInChildren<BackgroundLayer>(true);
        foreach (var l in layers)
        {
            if (l == null) continue;
            if (string.IsNullOrWhiteSpace(l.key)) continue;

            if (_layers.ContainsKey(l.key))
            {
                Debug.LogWarning($"[BG] Duplicate layer key '{l.key}' under BG_Root. Only first will be used.", l);
                continue;
            }
            _layers.Add(l.key, l);
        }
    }

    public void ApplyFromStageConfig()
    {
        // Stage 씬에서 설정 컴포넌트 찾기
        var cfg = FindObjectOfType<StageBackgroundConfig>(true);

        bool exclusive = cfg != null ? cfg.exclusive : defaultExclusive;
        string[] keys = cfg != null ? cfg.enabledLayerKeys : null;

        Apply(keys, exclusive);
    }

    public void Apply(string[] enabledKeys, bool exclusive)
    {
        if (_layers.Count == 0) CacheLayers();

        // exclusive면 일단 전부 끄기
        if (exclusive)
        {
            foreach (var kv in _layers)
                kv.Value.SetActive(false);
        }

        if (enabledKeys == null) return;

        // 켜달라는 것만 켜기
        foreach (var k in enabledKeys)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;

            if (_layers.TryGetValue(k, out var layer))
            {
                layer.SetActive(true);
            }
            else
            {
                Debug.LogWarning($"[BG] Layer key not found: '{k}'. Add BackgroundLayer(key='{k}') under BG_Root.");
            }
        }
    }
}
