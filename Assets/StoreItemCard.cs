using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreItemCard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descText;      // optional (weapon stats)
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button actionButton;

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private GameObject equippedBadge;

    [Header("Background Colors")]
    [SerializeField] private Color colorLocked = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color colorUnlocked = new Color(0.92f, 0.92f, 1f, 1f);
    [SerializeField] private Color colorOwned = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color colorEquipped = new Color(0.75f, 0.95f, 0.75f, 1f);

    [Header("Icon Colors (Optional)")]
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

    private bool IsUnlockedNow()
    {
        if (_item == null) return false;

        if (_item.unlockOnStageClear <= 0) return true;

        return CosmeticSaveManager.IsUnlocked(_item.id) || CosmeticSaveManager.IsOwned(_item.id);
    }

    public void Redraw()
    {
        if (_item == null) return;

        bool owned = CosmeticSaveManager.IsOwned(_item.id);
        bool isEquipped = CosmeticSaveManager.GetEquipped(_item.category) == _item.id;
        bool unlocked = IsUnlockedNow();

        if (titleText != null)
            titleText.text = string.IsNullOrWhiteSpace(_item.displayName) ? _item.id : _item.displayName;

        if (iconImage != null)
        {
            Sprite sp = _item.icon != null ? _item.icon : _item.shipSprite;
            iconImage.sprite = sp;
            iconImage.enabled = (sp != null);

            if (dimIconWhenLocked)
                iconImage.color = (!unlocked && !owned && !isEquipped) ? iconLockedColor : iconNormalColor;
            else
                iconImage.color = iconNormalColor;
        }

        // Weapon description (optional)
        if (descText != null)
        {
            if (_item.category == CosmeticCategory.Weapon)
            {
                var lines = _item.GetWeaponDescriptionLines();
                descText.text = (lines != null && lines.Count > 0) ? string.Join("\n", lines) : "";
                descText.gameObject.SetActive(!string.IsNullOrEmpty(descText.text));
            }
            else
            {
                descText.text = "";
                descText.gameObject.SetActive(false);
            }
        }

        if (priceText != null)
        {
            if (!unlocked || owned) priceText.text = "";
            else
            {
                int p = Mathf.Max(0, _item.priceCoins);
                priceText.text = p <= 0 ? "FREE" : $"{p} COINS";
            }
        }

        if (stateText != null)
        {
            if (isEquipped) stateText.text = "EQUIPPED";
            else if (owned) stateText.text = "OWNED";
            else if (unlocked) stateText.text = "UNLOCKED";
            else stateText.text = "LOCKED";
        }

        if (actionButton != null)
        {
            var txt = actionButton.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                if (!unlocked && !owned) txt.text = "LOCKED";
                else if (isEquipped) txt.text = "EQUIPPED";
                else if (owned) txt.text = "EQUIP";
                else txt.text = "BUY";
            }

            actionButton.interactable = (unlocked || owned) && !isEquipped;
        }

        if (backgroundImage != null)
        {
            if (!unlocked && !owned) backgroundImage.color = colorLocked;
            else if (isEquipped) backgroundImage.color = colorEquipped;
            else if (owned) backgroundImage.color = colorOwned;
            else backgroundImage.color = colorUnlocked;
        }

        if (equippedBadge != null)
            equippedBadge.SetActive(isEquipped);
    }

    private void OnEnable() => Redraw();
}
