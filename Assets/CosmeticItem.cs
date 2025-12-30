using UnityEngine;

[CreateAssetMenu(menuName = "TNB/Cosmetics/Cosmetic Item", fileName = "CosmeticItem_")]
public class CosmeticItem : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName = "New Item";
    public CosmeticCategory category = CosmeticCategory.ShipSkin;

    [Header("Price (Soft Currency)")]
    public int priceCoins = 100;

    [Header("Ship Skin")]
    public Sprite shipSprite;
}
