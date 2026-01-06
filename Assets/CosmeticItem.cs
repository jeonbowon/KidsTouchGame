using UnityEngine;

[CreateAssetMenu(menuName = "TNB/Cosmetics/Cosmetic Item", fileName = "CosmeticItem_")]
public class CosmeticItem : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName = "New Item";
    public CosmeticCategory category = CosmeticCategory.ShipSkin;

    [Header("Unlock Rule")]
    [Tooltip("0이면 처음부터 구매 가능(Unlocked). 1이면 Stage1 클리어 시 Unlocked, 2이면 Stage2 클리어 시 Unlocked ...")]
    public int unlockOnStageClear = 0;

    [Header("Price (Soft Currency)")]
    public int priceCoins = 100;

    [Header("UI")]
    public Sprite icon; // 카드에 표시할 아이콘(없으면 shipSprite 사용)

    [Header("Ship Skin")]
    public Sprite shipSprite;
}
