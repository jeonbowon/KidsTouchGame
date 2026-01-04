using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreItemCard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;              // 선택: ShipSprite 등을 표시
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button actionButton;

    private CosmeticItem _item;
    private Action<CosmeticItem> _onClick;

    public void Configure(CosmeticItem item, Action<CosmeticItem> onClick)
    {
        _item = item;
        _onClick = onClick;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() =>
            {
                if (_item != null) _onClick?.Invoke(_item);
            });
        }

        Redraw();
    }

    public void Redraw()
    {
        if (_item == null) return;

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(_item.displayName) ? _item.id : _item.displayName;

        // 아이콘 우선: item.icon → shipSprite
        if (iconImage != null)
        {
            Sprite sp = _item.icon != null ? _item.icon : _item.shipSprite;
            iconImage.sprite = sp;
            iconImage.enabled = (sp != null);
        }

        bool owned = CosmeticSaveManager.IsOwned(_item.id);
        bool isEquipped = CosmeticSaveManager.GetEquipped(_item.category) == _item.id;

        // 가격
        if (priceText != null)
        {
            if (owned) priceText.text = "";
            else
            {
                int p = Mathf.Max(0, _item.priceCoins);
                priceText.text = p <= 0 ? "FREE" : $"{p} COINS";
            }
        }

        // 상태 텍스트
        if (stateText != null)
        {
            if (isEquipped) stateText.text = "USING";
            else if (owned) stateText.text = "OWNED";
            else stateText.text = "LOCKED";
        }

        // 버튼 텍스트 / 상태
        if (actionButton != null)
        {
            var txt = actionButton.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                if (isEquipped) txt.text = "EQUIPPED";
                else if (owned) txt.text = "EQUIP";
                else txt.text = "BUY";
            }

            actionButton.interactable = !isEquipped;
        }
    }

    private void OnEnable()
    {
        // 저장 상태가 바뀐 뒤 다시 켜질 때 상태 갱신
        Redraw();
    }
}
