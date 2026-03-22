using System;
using System.Collections.Generic;
using UnityEngine;

public static class CosmeticSaveManager
{
    private const string KEY = "cosmetic_save_v1";
    private static CosmeticSaveData _cache;

    public static event Action<int> OnCoinsChanged;

    /// <summary>무기/스킨 장착이 변경될 때 발행 (category 전달)</summary>
    public static event Action<CosmeticCategory> OnEquipChanged;

    private static void NotifyCoinsChanged()
    {
        try { OnCoinsChanged?.Invoke(Data.coins); }
        catch (Exception e) { Debug.LogWarning($"[CosmeticSave] OnCoinsChanged invoke failed: {e.Message}"); }
    }

    private static void NotifyEquipChanged(CosmeticCategory cat)
    {
        try { OnEquipChanged?.Invoke(cat); }
        catch (Exception e) { Debug.LogWarning($"[CosmeticSave] OnEquipChanged invoke failed: {e.Message}"); }
    }

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

            if (d.unlockedIds == null) d.unlockedIds = new List<string>();
            if (d.ownedIds == null) d.ownedIds = new List<string>();
            if (d.equipped == null) d.equipped = new List<CosmeticSaveData.CategoryEquipPair>();

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
        NotifyCoinsChanged();
    }

    public static bool TrySpendCoins(int cost)
    {
        if (cost <= 0) return true;
        if (Data.coins < cost) return false;

        Data.coins -= cost;
        Save(Data);
        NotifyCoinsChanged();
        return true;
    }

    // -----------------------------
    // Unlock () / Owned ()
    // -----------------------------
    public static bool IsUnlocked(string id) => Data.IsUnlocked(id);

    public static void GrantUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (!Data.IsUnlocked(id))
        {
            Data.AddUnlocked(id);
            Save(Data);
        }
    }

    public static bool IsOwned(string id) => Data.IsOwned(id);

    public static void GrantOwned(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        if (!Data.IsOwned(id))
        {
            Data.AddOwned(id);
            Save(Data);
        }
    }

    public static string GetEquipped(CosmeticCategory cat) => Data.GetEquipped(cat);

    public static void Equip(CosmeticCategory cat, string itemId)
    {
        Data.SetEquipped(cat, itemId);
        Save(Data);
        NotifyEquipChanged(cat);
    }
}
