using System;
using System.Collections.Generic;

[Serializable]
public class CosmeticSaveData
{
    public int coins = 0;

    // ✅ Stage 클리어 등으로 '구매 가능(Unlocked)' 해진 아이템 id 목록
    public List<string> unlockedIds = new List<string>();

    // ✅ 실제로 구매한(Owned) 아이템 id 목록
    public List<string> ownedIds = new List<string>();

    // 장착 id (예: ShipSkin -> "ship_blue_01")
    public List<CategoryEquipPair> equipped = new List<CategoryEquipPair>();

    [Serializable]
    public class CategoryEquipPair
    {
        public CosmeticCategory category;
        public string itemId;
    }

    public bool IsOwned(string id) => ownedIds != null && ownedIds.Contains(id);

    public void AddOwned(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (ownedIds == null) ownedIds = new List<string>();
        if (!ownedIds.Contains(id)) ownedIds.Add(id);
    }

    public bool IsUnlocked(string id) => unlockedIds != null && unlockedIds.Contains(id);

    public void AddUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (unlockedIds == null) unlockedIds = new List<string>();
        if (!unlockedIds.Contains(id)) unlockedIds.Add(id);
    }

    public string GetEquipped(CosmeticCategory cat)
    {
        if (equipped == null) return null;

        for (int i = 0; i < equipped.Count; i++)
        {
            if (equipped[i].category == cat)
                return equipped[i].itemId;
        }
        return null;
    }

    public void SetEquipped(CosmeticCategory cat, string id)
    {
        if (equipped == null) equipped = new List<CategoryEquipPair>();

        for (int i = 0; i < equipped.Count; i++)
        {
            if (equipped[i].category == cat)
            {
                equipped[i].itemId = id;
                return;
            }
        }

        equipped.Add(new CategoryEquipPair { category = cat, itemId = id });
    }
}
