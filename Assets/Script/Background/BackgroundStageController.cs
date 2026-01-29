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
        var cfg = FindObjectOfType<StageBackgroundConfig>(true);

        bool exclusive = cfg != null ? cfg.exclusive : defaultExclusive;
        string[] keys = cfg != null ? cfg.enabledLayerKeys : null;

        // 테마가 있고 Base 강제 옵션이면 keys에 Base를 보장
        if (cfg != null && cfg.theme != null && cfg.forceEnableBaseKey)
        {
            string baseKey = string.IsNullOrWhiteSpace(cfg.baseKeyName) ? "Base" : cfg.baseKeyName;
            keys = EnsureKeyIncluded(keys, baseKey);
        }

        Apply(keys, exclusive);

        // 테마 적용(선택)
        if (cfg != null && cfg.theme != null)
        {
            ApplyTheme(cfg.theme, cfg.baseKeyName);
        }
    }

    private string[] EnsureKeyIncluded(string[] keys, string key)
    {
        if (keys == null) return new string[] { key };
        for (int i = 0; i < keys.Length; i++)
        {
            if (string.Equals(keys[i], key, StringComparison.OrdinalIgnoreCase))
                return keys;
        }

        var list = new List<string>(keys);
        list.Add(key);
        return list.ToArray();
    }

    public void Apply(string[] enabledKeys, bool exclusive)
    {
        if (_layers.Count == 0) CacheLayers();

        if (exclusive)
        {
            foreach (var kv in _layers)
                kv.Value.SetActive(false);
        }

        if (enabledKeys == null) return;

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

    private void ApplyTheme(BackgroundThemeConfig theme, string baseKeyName)
    {
        if (theme == null) return;

        // 1) Base 레이어의 InfiniteScroll 찾아서 Sprite/Speed/Tint 적용
        string baseKey = string.IsNullOrWhiteSpace(baseKeyName) ? "Base" : baseKeyName;

        if (_layers.TryGetValue(baseKey, out var baseLayer))
        {
            var scroll = baseLayer.GetComponentInChildren<BackgroundInfiniteScroll>(true);
            if (scroll != null)
            {
                // Sprite
                if (theme.baseSprites != null && theme.baseSprites.Length > 0)
                {
                    var sp = theme.baseSprites[UnityEngine.Random.Range(0, theme.baseSprites.Length)];
                    if (sp != null) scroll.SetSprites(sp);
                }

                // Speed
                float speed = UnityEngine.Random.Range(theme.scrollSpeedRange.x, theme.scrollSpeedRange.y);
                scroll.SetSpeed(speed);

                // Tint
                Color tint = new Color(
                    UnityEngine.Random.Range(theme.tintMin.r, theme.tintMax.r),
                    UnityEngine.Random.Range(theme.tintMin.g, theme.tintMax.g),
                    UnityEngine.Random.Range(theme.tintMin.b, theme.tintMax.b),
                    1f
                );
                scroll.SetTint(tint);
            }
        }

        // 2) 오버레이 스포너 적용
        var spawner = GetComponentInChildren<BackgroundOverlaySpawner>(true);
        if (spawner != null)
        {
            spawner.ApplyTheme(theme);
        }
    }
}
