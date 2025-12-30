using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TNB/Cosmetics/Cosmetic Database", fileName = "CosmeticDatabase")]
public class CosmeticDatabase : ScriptableObject
{
    public List<CosmeticItem> items = new List<CosmeticItem>();

    private Dictionary<string, CosmeticItem> _map;

    public void BuildIndex()
    {
        _map = new Dictionary<string, CosmeticItem>(StringComparer.Ordinal);
        foreach (var it in items)
        {
            if (it == null) continue;
            if (string.IsNullOrWhiteSpace(it.id)) continue;

            if (_map.ContainsKey(it.id))
            {
                Debug.LogWarning($"[CosmeticDB] id ม฿บน: {it.id} ({it.name})");
                continue;
            }
            _map.Add(it.id, it);
        }
    }

    public CosmeticItem GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (_map == null) BuildIndex();
        _map.TryGetValue(id, out var it);
        return it;
    }

    public List<CosmeticItem> GetByCategory(CosmeticCategory cat)
    {
        var list = new List<CosmeticItem>();
        foreach (var it in items)
        {
            if (it == null) continue;
            if (it.category == cat) list.Add(it);
        }
        return list;
    }
}
