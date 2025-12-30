using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreController : MonoBehaviour
{
    [Header("DB")]
    [SerializeField] private CosmeticDatabase database;
    [SerializeField] private string dbResourcePath = "Cosmetics/CosmeticDatabase";

    [Header("UI")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private Transform contentRoot;       // ScrollView Content
    [SerializeField] private StoreItemCard cardPrefab;

    [Header("Category")]
    [SerializeField] private CosmeticCategory category = CosmeticCategory.ShipSkin;

    [Header("Preview Player (선택) - 메인메뉴에 Player 프리팹 프리뷰를 띄우는 경우 연결")]
    [SerializeField] private PlayerCosmeticApplier previewApplier;

    private readonly List<StoreItemCard> _spawned = new List<StoreItemCard>();

    void OnEnable()
    {
        if (database == null && !string.IsNullOrEmpty(dbResourcePath))
            database = Resources.Load<CosmeticDatabase>(dbResourcePath);

        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshCoinsUI();
        BuildList();
    }

    private void RefreshCoinsUI()
    {
        if (coinsText != null)
            coinsText.text = $"COINS: {CosmeticSaveManager.GetCoins()}";
    }

    private void BuildList()
    {
        if (database == null || contentRoot == null || cardPrefab == null) return;

        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i] != null) Destroy(_spawned[i].gameObject);
        _spawned.Clear();

        List<CosmeticItem> list = database.GetByCategory(category);
        foreach (var it in list)
        {
            var card = Instantiate(cardPrefab, contentRoot);
            card.Bind(it, OnClickBuyOrEquip);
            _spawned.Add(card);
        }
    }

    private void OnClickBuyOrEquip(CosmeticItem item)
    {
        if (item == null) return;

        bool owned = CosmeticSaveManager.IsOwned(item.id);

        // 구매
        if (!owned)
        {
            if (!CosmeticSaveManager.TrySpendCoins(item.priceCoins))
            {
                Debug.Log("[Store] 코인 부족");
                RefreshCoinsUI();
                return;
            }

            CosmeticSaveManager.GrantOwned(item.id);
        }

        // 장착
        CosmeticSaveManager.Equip(item.category, item.id);

        // UI 갱신
        RefreshAll();

        // 프리뷰 즉시 반영(선택)
        if (previewApplier != null)
            previewApplier.ApplyEquipped();
    }
}
