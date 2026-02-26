using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoreConfirmPopup : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Modal (Optional)")]
    [Tooltip("팝업이 떠 있는 동안 뒤 UI 클릭을 막는 투명 막입니다. Image의 Raycast Target은 켜져 있어야 합니다.")]
    [SerializeField] private GameObject modalBlocker;

    [Tooltip("팝업이 항상 최상위로 뜨게 하려면 Canvas(overrideSorting=true)로 지정하세요. (선택)")]
    [SerializeField] private Canvas popupCanvas;

    [Tooltip("ModalBlocker를 쓴다면, 이 Canvas sortingOrder는 blocker보다 크게 잡아야 합니다.")]
    [SerializeField] private int popupSortingOrder = 2000;

    [Tooltip("ModalBlocker Canvas가 따로 있다면, 팝업보다 낮은 sortingOrder로 잡으세요.")]
    [SerializeField] private Canvas blockerCanvas;

    [SerializeField] private int blockerSortingOrder = 1990;

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

    private void EnsureTopMost()
    {
        // (1) Canvas sorting을 사용하는 경우
        if (popupCanvas != null)
        {
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = popupSortingOrder;
        }

        if (blockerCanvas != null)
        {
            blockerCanvas.overrideSorting = true;
            blockerCanvas.sortingOrder = blockerSortingOrder;
        }

        // (2) Canvas sorting이 없더라도 sibling 순서로 최상단 보장
        if (modalBlocker != null && modalBlocker.transform.parent == transform.parent)
            modalBlocker.transform.SetAsLastSibling();

        if (root != null && root.transform.parent == transform.parent)
            root.transform.SetAsLastSibling();
        else
            transform.SetAsLastSibling();
    }

    private void ShowModalLayer(bool on)
    {
        if (modalBlocker != null)
            modalBlocker.SetActive(on);

        if (on)
            EnsureTopMost();
    }

    // (기존 호환) 코인 아이템 구매 확인
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

        // ✅ 모달 ON
        ShowModalLayer(true);
    }

    // (기존 호환) 메시지 팝업
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

        // ✅ 모달 ON
        ShowModalLayer(true);
    }

    // ✅ (신규) IAP 현금결제 확인용
    public void ShowConfirm(string title, string message,
        string confirmLabel, string cancelLabel,
        Action onConfirm)
    {
        _onConfirm = onConfirm;

        if (titleText != null) titleText.text = string.IsNullOrEmpty(title) ? "확인" : title;
        if (messageText != null) messageText.text = message ?? "";

        if (confirmButtonText != null) confirmButtonText.text = string.IsNullOrEmpty(confirmLabel) ? "확인" : confirmLabel;
        if (cancelButtonText != null) cancelButtonText.text = string.IsNullOrEmpty(cancelLabel) ? "취소" : cancelLabel;

        if (confirmButton != null) confirmButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);

        ShowModalLayer(true);
    }

    // (신규) CosmeticItem 기반 구매 확인 (기존 유지)
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

        ShowModalLayer(true);
    }

    public void Hide()
    {
        _onConfirm = null;

        if (root != null) root.SetActive(false);
        else gameObject.SetActive(false);

        // ✅ 모달 OFF
        ShowModalLayer(false);
    }

    private void OnClickConfirm()
    {
        var cb = _onConfirm;
        Hide();
        cb?.Invoke();
    }
}