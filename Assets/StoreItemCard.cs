using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreItemCard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;              // 추가(선택)
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button actionButton;

    private CosmeticItem _item;
    private Action<CosmeticItem> _onClick;

    public void Bind(CosmeticItem item, Action<CosmeticItem> onClick)
    {
        _item = item;
        _onClick = onClick;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(() => _onClick?.Invoke(_item));
        }

        Redraw();
    }

    private void Redraw()
    {
        if (_item == null) return;

        if (titleText != null) titleText.text = _item.displayName;

        // 아이콘: icon 있으면 icon, 없으면 shipSprite
        if (iconImage != null)
        {
            var spr = (_item.icon != null) ? _item.icon : _item.shipSprite;
            iconImage.sprite = spr;
            iconImage.enabled = (spr != null);
        }

        bool owned = CosmeticSaveManager.IsOwned(_item.id);
        string equipped = CosmeticSaveManager.GetEquipped(_item.category);
        bool isEquipped = (!string.IsNullOrEmpty(equipped) && equipped == _item.id);

        if (priceText != null) priceText.text = $"PRICE: {_item.priceCoins}";

        if (stateText != null)
        {
            if (isEquipped) stateText.text = "EQUIPPED";
            else if (owned) stateText.text = "OWNED";
            else stateText.text = "LOCKED";
        }

        if (actionButton != null)
        {
            var txt = actionButton.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                if (isEquipped) txt.text = "EQUIPPED";
                else if (owned) txt.text = "EQUIP";
                else txt.text = "BUY";
            }

            actionButton.interactable = !isEquipped;
        }
    }

    void OnEnable()
    {
        // 저장 상태가 바뀐 뒤 다시 켜질 때 상태 갱신
        Redraw();
    }
}
