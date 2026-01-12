using System;
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

    public void ShowPurchaseConfirm(string itemName, int price, int haveCoins, Action onConfirm)
    {
        _onConfirm = onConfirm;

        if (titleText != null) titleText.text = "구매 확인";
        if (messageText != null)
            messageText.text =
                $"\"{itemName}\" 을(를) 구매하시겠습니까?\n" +
                $"가격: {price} COINS\n" +
                $"보유: {haveCoins} COINS\n" +
                $"구매 후: {Mathf.Max(0, haveCoins - price)} COINS";

        if (confirmButtonText != null) confirmButtonText.text = "구매";
        if (cancelButtonText != null) cancelButtonText.text = "취소";

        if (confirmButton != null) confirmButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(true);

        if (root != null) root.SetActive(true);
        else gameObject.SetActive(true);
    }

    public void ShowMessage(string message, string title = "알림")
    {
        _onConfirm = null;

        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        if (confirmButtonText != null) confirmButtonText.text = "확인";
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);

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
