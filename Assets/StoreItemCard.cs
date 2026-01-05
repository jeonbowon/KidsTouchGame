using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreItemCard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;              // 선택: item.icon 또는 shipSprite 표시
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button actionButton;

    [Header("Visual Feedback (Optional)")]
    [Tooltip("카드 배경(패널)의 Image를 넣으면, 상태에 따라 배경색을 바꿉니다.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("EQUIPPED 상태를 더 확실히 보이게 하는 배지(체크표시 등). 필요 없으면 비워도 됩니다.")]
    [SerializeField] private GameObject equippedBadge;

    [Header("Background Colors")]
    [SerializeField] private Color colorLocked = new Color(0.85f, 0.85f, 0.85f, 1f);   // 미소유
    [SerializeField] private Color colorOwned = new Color(1f, 1f, 1f, 1f);            // 소유
    [SerializeField] private Color colorEquipped = new Color(0.75f, 0.95f, 0.75f, 1f); // 장착중(연한 초록)

    [Header("Icon Colors (Optional)")]
    [Tooltip("미소유일 때 아이콘을 살짝 흐리게 보이고 싶으면 사용")]
    [SerializeField] private bool dimIconWhenLocked = true;
    [SerializeField] private Color iconLockedColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color iconNormalColor = new Color(1f, 1f, 1f, 1f);

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

        bool owned = CosmeticSaveManager.IsOwned(_item.id);
        bool isEquipped = CosmeticSaveManager.GetEquipped(_item.category) == _item.id;

        // 제목
        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(_item.displayName) ? _item.id : _item.displayName;

        // 아이콘: item.icon 우선, 없으면 shipSprite
        if (iconImage != null)
        {
            Sprite sp = _item.icon != null ? _item.icon : _item.shipSprite;
            iconImage.sprite = sp;
            iconImage.enabled = (sp != null);

            if (dimIconWhenLocked)
                iconImage.color = (!owned && !isEquipped) ? iconLockedColor : iconNormalColor;
            else
                iconImage.color = iconNormalColor;
        }

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
            if (isEquipped) stateText.text = "EQUIPPED";
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

            // 장착중이면 버튼 비활성
            actionButton.interactable = !isEquipped;
        }

        // ✅ 시각적 강조: 배경색 + 배지
        if (backgroundImage != null)
        {
            if (isEquipped) backgroundImage.color = colorEquipped;
            else if (owned) backgroundImage.color = colorOwned;
            else backgroundImage.color = colorLocked;
        }

        if (equippedBadge != null)
        {
            equippedBadge.SetActive(isEquipped);
        }
    }

    private void OnEnable()
    {
        Redraw();
    }
}
