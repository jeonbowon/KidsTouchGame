using System;
using System.Collections.Generic;

[Serializable]
public class CosmeticSaveData
{
    public int coins = 0;

    // 구매한 아이템 id 목록
    public List<string> ownedIds = new List<string>();

    // 카테고리별 장착 id (예: ShipSkin -> "ship_blue_01")
    public List<CategoryEquipPair> equipped = new List<CategoryEquipPair>();

    [Serializable]
    public class CategoryEquipPair
    {
        public CosmeticCategory category;
        public string itemId;
    }

    public bool IsOwned(string id)
    {
        return ownedIds != null && ownedIds.Contains(id);
    }

    public string GetEquipped(CosmeticCategory cat)
    {
        if (equipped == null) return null;
        for (int i = 0; i < equipped.Count; i++)
        {
            if (equipped[i].category == cat) return equipped[i].itemId;
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
