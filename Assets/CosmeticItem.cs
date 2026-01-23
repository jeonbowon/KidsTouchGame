using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TNB/Cosmetics/Cosmetic Item", fileName = "CosmeticItem_")]
public class CosmeticItem : ScriptableObject
{
    // -------------------------------------------------
    // Identity
    // -------------------------------------------------
    [Header("Identity")]
    public string id;
    public string displayName = "New Item";
    public CosmeticCategory category = CosmeticCategory.ShipSkin;

    // -------------------------------------------------
    // Unlock / Price
    // -------------------------------------------------
    [Header("Unlock Rule")]
    [Tooltip("0이면 처음부터 구매 가능(Unlocked). 1이면 Stage1 클리어 시 Unlocked ...")]
    public int unlockOnStageClear = 0;

    [Header("Price (Soft Currency)")]
    public int priceCoins = 100;

    // -------------------------------------------------
    // UI
    // -------------------------------------------------
    [Header("UI")]
    public Sprite icon;

    // -------------------------------------------------
    // Ship Skin
    // -------------------------------------------------
    [Header("Ship Skin")]
    public Sprite shipSprite;

    // =================================================
    // =============== WEAPON DATA =====================
    // =================================================

    [Header("Weapon - Visual")]
    [Tooltip("총알(투사체) 스프라이트. 비워두면 Bullet 프리팹의 기본 스프라이트를 그대로 사용합니다.")]
    public Sprite bulletSprite;

    [Header("Weapon - Core Stats (Weight 0)")]
    [Tooltip("Damage multiplier (1 = base)")]
    public float damageMul = 1f;

    [Tooltip("Bullet speed multiplier")]
    public float speedMul = 1f;

    [Tooltip("Fire interval multiplier (lower = faster)")]
    public float fireIntervalMul = 1f;

    [Tooltip("Number of bullets per shot")]
    public int shotCount = 1;

    [Tooltip("Spread angle (degrees)")]
    public float spreadAngle = 0f;

    // -------------------------------------------------
    // Major Effect (Max 1)
    // -------------------------------------------------
    [Header("Weapon - Major Effect (Max 1)")]
    public bool usePierce;
    public int pierceCount = 0;

    public bool useHoming;
    public float homingStrength = 0f;
    public float turnRate = 0f;

    public bool useExplosion;
    public float explosionRadius = 0f;

    // -------------------------------------------------
    // Minor Effects (0~2)
    // -------------------------------------------------
    [Header("Weapon - Minor Effects")]
    [Tooltip("Collider size multiplier")]
    public float hitRadiusMul = 1f;

    [Range(0f, 1f)]
    public float critChance = 0f;
    public float critMul = 1.5f;

    public float slowPercent = 0f;
    public float slowTime = 0f;

    // -------------------------------------------------
    // Internal Balance (NOT SHOWN TO USER)
    // -------------------------------------------------
    [Header("Weapon - Internal Balance")]
    [Tooltip("Max allowed weight for this weapon")]
    public int maxWeight = 5;

    // =================================================
    // INTERNAL CALCULATION
    // =================================================
    public int CalculateWeight()
    {
        int w = 0;

        // Major
        if (usePierce) w += 3;
        if (useHoming) w += 3;
        if (useExplosion) w += 4;

        // Minor
        if (hitRadiusMul > 1.01f) w += 2;
        if (critChance > 0f) w += 1;
        if (slowPercent > 0f) w += 1;

        return w;
    }

    public bool IsWeaponValid()
    {
        if (category != CosmeticCategory.Weapon)
            return true;

        int majorCount = 0;
        if (usePierce) majorCount++;
        if (useHoming) majorCount++;
        if (useExplosion) majorCount++;

        if (majorCount > 1)
        {
            Debug.LogError($"[Weapon Invalid] {displayName} has multiple Major Effects");
            return false;
        }

        int weight = CalculateWeight();
        if (weight > maxWeight)
        {
            Debug.LogError($"[Weapon Invalid] {displayName} weight {weight} > max {maxWeight}");
            return false;
        }

        return true;
    }

    // =================================================
    // UI DESCRIPTION (USER VISIBLE)
    // =================================================
    public List<string> GetWeaponDescriptionLines()
    {
        var lines = new List<string>();

        if (category != CosmeticCategory.Weapon)
            return lines;

        if (Mathf.Abs(speedMul - 1f) > 0.01f)
            lines.Add($"탄속 {(speedMul > 1f ? "+" : "")}{Mathf.RoundToInt((speedMul - 1f) * 100f)}%");

        if (Mathf.Abs(fireIntervalMul - 1f) > 0.01f)
            lines.Add($"연사 {(fireIntervalMul < 1f ? "+" : "")}{Mathf.RoundToInt((1f - fireIntervalMul) * 100f)}%");

        if (Mathf.Abs(damageMul - 1f) > 0.01f)
            lines.Add($"데미지 {(damageMul > 1f ? "+" : "")}{Mathf.RoundToInt((damageMul - 1f) * 100f)}%");

        if (shotCount > 1)
            lines.Add($"발사 수 +{shotCount - 1}");

        if (spreadAngle > 0f)
            lines.Add($"발사각 +{spreadAngle}°");

        if (usePierce)
            lines.Add($"관통 +{pierceCount}");

        if (useHoming)
            lines.Add("유도 탄환");

        if (useExplosion)
            lines.Add("범위 데미지");

        if (hitRadiusMul > 1.01f)
            lines.Add($"판정 +{Mathf.RoundToInt((hitRadiusMul - 1f) * 100f)}%");

        if (critChance > 0f)
            lines.Add($"치명타 {Mathf.RoundToInt(critChance * 100f)}%");

        if (slowPercent > 0f)
            lines.Add("슬로우 효과");

        return lines;
    }
}
