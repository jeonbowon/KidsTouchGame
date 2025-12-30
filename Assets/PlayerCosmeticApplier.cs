using UnityEngine;

public class PlayerCosmeticApplier : MonoBehaviour
{
    [Header("DB (Resources/Cosmetics/CosmeticDatabase.asset)")]
    [SerializeField] private CosmeticDatabase database;

    [Header("적용 대상 (비우면 자식 포함 SpriteRenderer 중 'Player' 본체로 보이는 첫 번째 사용)")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Header("Resources 자동 로드 경로")]
    [SerializeField] private string dbResourcePath = "Cosmetics/CosmeticDatabase";

    void Awake()
    {
        if (database == null && !string.IsNullOrEmpty(dbResourcePath))
        {
            database = Resources.Load<CosmeticDatabase>(dbResourcePath);
        }

        if (targetRenderer == null)
        {
            // 자식 포함에서 가장 “메인”처럼 보이는 renderer를 하나 잡는다.
            var rds = GetComponentsInChildren<SpriteRenderer>(true);
            if (rds != null && rds.Length > 0) targetRenderer = rds[0];
        }
    }

    void Start()
    {
        ApplyEquipped();
    }

    public void ApplyEquipped()
    {
        if (database == null || targetRenderer == null) return;

        string equippedId = CosmeticSaveManager.GetEquipped(CosmeticCategory.ShipSkin);
        if (string.IsNullOrEmpty(equippedId)) return;

        var item = database.GetById(equippedId);
        if (item == null) return;

        if (item.category == CosmeticCategory.ShipSkin && item.shipSprite != null)
        {
            targetRenderer.sprite = item.shipSprite;
        }
    }
}
