using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreConfirmPopup : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text confirmButtonText;
    [SerializeField] private TMP_Text cancelButtonText;

    private Action _onConfirm;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnClickConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
        Hide();
    }

    // =========================================================
    // ✅ (기존 호환) StoreController가 호출하던 시그니처 유지
    // =========================================================
    public void ShowPurchaseConfirm(string itemName, int price, int haveCoins, Action onConfirm)
    {
        _onConfirm = onConfirm;

        if (titleText != null) titleText.text = "구매 확인";

        if (messageText != null)
        {
            messageText.text =
                $"\"{itemName}\" 을(를) 구매하시겠습니까?\n" +
                $"가격: {price} COINS\n" +
                $"보유: {haveCoins} COINS\n" +
                $"구매 후: {Mathf.Max(0, haveCoins - price)} COINS";
        }

        if (confirmButtonText != null) confirmButtonText.text = "구매";
        if (cancelButtonText != null) cancelButtonText.text = "취소";

        if (confirmButton != null) confirmButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);
    }

    // =========================================================
    // ✅ (기존 호환) ShowMessage 유지
    // =========================================================
    public void ShowMessage(string message, string title = "알림")
    {
        _onConfirm = null;

        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        if (confirmButtonText != null) confirmButtonText.text = "확인";
        if (cancelButtonText != null) cancelButtonText.text = "";

        if (confirmButton != null) confirmButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);
    }

    // =========================================================
    // ✅ (신규) Weapon 성능을 함께 보여주는 버전
    // StoreController를 나중에 이쪽으로 바꾸면 더 좋음
    // =========================================================
    public void ShowPurchaseConfirm(CosmeticItem item, int haveCoins, Action onConfirm)
    {
        if (item == null)
        {
            ShowMessage("아이템 정보가 없습니다.");
            return;
        }

        _onConfirm = onConfirm;

        if (titleText != null) titleText.text = "구매 확인";

        var sb = new StringBuilder();
        sb.Append($"\"{item.displayName}\" 을(를) 구매하시겠습니까?\n\n");
        sb.Append($"가격: {item.priceCoins} COINS\n");
        sb.Append($"보유: {haveCoins} COINS\n");
        sb.Append($"구매 후: {Mathf.Max(0, haveCoins - item.priceCoins)} COINS");

        // Weapon이면 성능 요약 추가(유저에게만 보여줌, Weight/Slot은 숨김)
        if (item.category == CosmeticCategory.Weapon)
        {
            var lines = item.GetWeaponDescriptionLines();
            if (lines != null && lines.Count > 0)
            {
                sb.Append("\n\n[무기 성능]\n");
                for (int i = 0; i < lines.Count; i++)
                    sb.Append("• ").Append(lines[i]).Append('\n');
            }
        }

        if (messageText != null) messageText.text = sb.ToString();

        if (confirmButtonText != null) confirmButtonText.text = "구매";
        if (cancelButtonText != null) cancelButtonText.text = "취소";

        if (confirmButton != null) confirmButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);
    }

    public void Hide()
    {
        _onConfirm = null;

        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    private void OnClickConfirm()
    {
        var cb = _onConfirm;
        Hide();
        cb?.Invoke();
    }
}
