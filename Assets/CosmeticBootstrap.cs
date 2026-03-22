using UnityEngine;

public class CosmeticBootstrap : MonoBehaviour
{
    [Tooltip("ó�� ����/������ �⺻ ��Ų id")]
    public string defaultShipId = "ship_default";

    void Start()
    {
        // �̹� ������ �Ǿ� ������ �ƹ� �͵� �� ��
        string equipped = CosmeticSaveManager.GetEquipped(CosmeticCategory.ShipSkin);
        if (!string.IsNullOrEmpty(equipped)) return;

        // �⺻ ��Ų ���� + ����
        CosmeticSaveManager.GrantOwned(defaultShipId);
        CosmeticSaveManager.Equip(CosmeticCategory.ShipSkin, defaultShipId);
    }
}
