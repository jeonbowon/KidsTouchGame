using UnityEngine;

public class CosmeticBootstrap : MonoBehaviour
{
    [Tooltip("처음 지급/장착할 기본 스킨 id")]
    public string defaultShipId = "ship_default";

    void Start()
    {
        // 이미 장착이 되어 있으면 아무 것도 안 함
        string equipped = CosmeticSaveManager.GetEquipped(CosmeticCategory.ShipSkin);
        if (!string.IsNullOrEmpty(equipped)) return;

        // 기본 스킨 지급 + 장착
        CosmeticSaveManager.GrantOwned(defaultShipId);
        CosmeticSaveManager.Equip(CosmeticCategory.ShipSkin, defaultShipId);

        Debug.Log($"[CosmeticBootstrap] First run -> grant+equip {defaultShipId}");
    }
}
