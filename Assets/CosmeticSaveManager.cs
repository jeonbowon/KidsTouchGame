using UnityEngine;

public static class CosmeticSaveManager
{
    private const string KEY = "cosmetic_save_v1";
    private static CosmeticSaveData _cache;

    public static CosmeticSaveData Data
    {
        get
        {
            if (_cache == null) _cache = Load();
            return _cache;
        }
    }

    public static CosmeticSaveData Load()
    {
        try
        {
            string json = PlayerPrefs.GetString(KEY, "");
            if (string.IsNullOrEmpty(json))
            {
                var fresh = new CosmeticSaveData();
                Save(fresh);
                return fresh;
            }

            var d = JsonUtility.FromJson<CosmeticSaveData>(json);
            if (d == null) d = new CosmeticSaveData();
            if (d.ownedIds == null) d.ownedIds = new System.Collections.Generic.List<string>();
            if (d.equipped == null) d.equipped = new System.Collections.Generic.List<CosmeticSaveData.CategoryEquipPair>();
            return d;
        }
        catch
        {
            var fresh = new CosmeticSaveData();
            Save(fresh);
            return fresh;
        }
    }

    public static void Save(CosmeticSaveData d)
    {
        _cache = d;
        string json = JsonUtility.ToJson(d);
        PlayerPrefs.SetString(KEY, json);
        PlayerPrefs.Save();
    }

    public static int GetCoins() => Data.coins;

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Data.coins += amount;
        Save(Data);
        Debug.Log($"[CosmeticSave] +coins {amount} => {Data.coins}");
    }

    public static bool TrySpendCoins(int cost)
    {
        if (cost <= 0) return true;
        if (Data.coins < cost) return false;

        Data.coins -= cost;
        Save(Data);
        Debug.Log($"[CosmeticSave] -coins {cost} => {Data.coins}");
        return true;
    }

    public static bool IsOwned(string id) => Data.IsOwned(id);

    public static void GrantOwned(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        if (!Data.ownedIds.Contains(id))
        {
            Data.ownedIds.Add(id);
            Save(Data);
            Debug.Log($"[CosmeticSave] owned += {id}");
        }
    }

    public static string GetEquipped(CosmeticCategory cat) => Data.GetEquipped(cat);

    public static void Equip(CosmeticCategory cat, string itemId)
    {
        Data.SetEquipped(cat, itemId);
        Save(Data);
        Debug.Log($"[CosmeticSave] equip {cat} = {itemId}");
    }
}
