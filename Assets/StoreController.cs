using System.Collections.Generic;
using TMPro;
using UnityEngine;

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

    [Header("Preview Player (선택)")]
    [SerializeField] private PlayerCosmeticApplier previewApplier;

    private readonly List<StoreItemCard> _spawned = new List<StoreItemCard>();

    private void OnEnable()
    {
        if (database == null && !string.IsNullOrEmpty(dbResourcePath))
            database = Resources.Load<CosmeticDatabase>(dbResourcePath);

        RefreshAll();
    }

    /// <summary>
    /// 버튼 OnClick에서 연결: Ship/Bullet/Effect 탭 변경용
    /// </summary>
    public void SetCategory(int catValue)
    {
        var next = (CosmeticCategory)catValue;
        if (category == next) return;
        category = next;
        RefreshAll();
    }

    public void SetCategory(CosmeticCategory cat)
    {
        if (category == cat) return;
        category = cat;
        RefreshAll();
    }

    // MainMenuController 등 다른 스크립트가 호출할 수 있도록 public
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

        // 기존 카드 제거
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i].gameObject);
        }
        _spawned.Clear();

        // DB에서 카테고리 목록 가져오기
        List<CosmeticItem> list = database.GetByCategory(category);
        if (list == null) return;

        // 정렬: 1) 장착중 2) 소유 3) 미소유 / 그 다음 가격 / 이름
        list.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            bool aEquipped = CosmeticSaveManager.GetEquipped(a.category) == a.id;
            bool bEquipped = CosmeticSaveManager.GetEquipped(b.category) == b.id;

            if (aEquipped != bEquipped) return aEquipped ? -1 : 1;

            bool aOwned = CosmeticSaveManager.IsOwned(a.id);
            bool bOwned = CosmeticSaveManager.IsOwned(b.id);

            if (aOwned != bOwned) return aOwned ? -1 : 1;

            int price = a.priceCoins.CompareTo(b.priceCoins);
            if (price != 0) return price;

            return string.Compare(a.displayName, b.displayName, System.StringComparison.Ordinal);
        });

        // 카드 생성
        foreach (var item in list)
        {
            if (item == null) continue;

            var card = Instantiate(cardPrefab, contentRoot);
            card.Configure(item, OnClickItem);
            _spawned.Add(card);
        }
    }

    private void OnClickItem(CosmeticItem item)
    {
        if (item == null) return;

        bool owned = CosmeticSaveManager.IsOwned(item.id);

        // 미소유면 코인으로 "구매"
        if (!owned)
        {
            int price = Mathf.Max(0, item.priceCoins);

            if (price > 0)
            {
                // SpendCoins 대신 TrySpendCoins 사용 (프로젝트에 실제 존재하는 메서드)
                if (!CosmeticSaveManager.TrySpendCoins(price))
                {
                    int coins = CosmeticSaveManager.GetCoins();
                    Debug.Log($"[STORE] 코인 부족: 보유={coins}, 가격={price}, id={item.id}");
                    return;
                }
            }

            CosmeticSaveManager.GrantOwned(item.id);
        }

        // 장착(선택)
        CosmeticSaveManager.Equip(item.category, item.id);

        // UI 갱신
        RefreshAll();

        // 프리뷰 반영
        if (previewApplier != null)
            previewApplier.ApplyEquipped();
    }
}
