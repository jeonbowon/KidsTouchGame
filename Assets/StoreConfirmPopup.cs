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

    [Tooltip("ModalBlocker Canvas가 따로 있다면, 팝업보다 낮은 sortingOrder로 잡으세요. (선택)")]
    [SerializeField] private Canvas blockerCanvas;

    [SerializeField] private int popupSortingOrder = 2000;
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
    private bool _isVisible;

    // 팝업 비활성화되어도 코루틴이 돌도록 하는 러너
    private sealed class _Runner : MonoBehaviour { }
    private static _Runner s_runner;
    private static _Runner Runner
    {
        get
        {
            if (s_runner != null) return s_runner;
            var go = new GameObject("__StoreConfirmPopup_Runner");
            DontDestroyOnLoad(go);
            s_runner = go.AddComponent<_Runner>();
            return s_runner;
        }
    }

    private Transform PopupTransform => (root != null) ? root.transform : transform;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnClickConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
        HideImmediate();
    }

    private void OnDisable()
    {
        _isVisible = false;
        if (modalBlocker != null) modalBlocker.SetActive(false);
    }

    private Canvas GetRootCanvas()
    {
        var c = GetComponentInParent<Canvas>();
        if (c == null) return null;
        return c.rootCanvas != null ? c.rootCanvas : c;
    }

    // ✅ 핵심: "블로커"와 "팝업"을 둘 다 Root Canvas 아래로 올리고,
    //         정렬을 '블로커는 팝업 바로 아래'로 강제한다.
    private void EnsureModalAndPopupHierarchyAndOrder()
    {
        var rootCanvas = GetRootCanvas();
        if (rootCanvas == null) return;

        var canvasRoot = rootCanvas.transform;

        // 1) 블로커를 Root Canvas 아래로 (전체 화면 클릭 차단)
        if (modalBlocker != null)
        {
            if (modalBlocker.transform.parent != canvasRoot)
                modalBlocker.transform.SetParent(canvasRoot, false);

            var rt = modalBlocker.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
        }

        // 2) 팝업도 Root Canvas 아래로 (블로커와 같은 부모여야 sibling 정렬이 확실)
        var popT = PopupTransform;
        if (popT.parent != canvasRoot)
            popT.SetParent(canvasRoot, false);

        // 3) sortingOrder를 쓰는 경우: 팝업이 블로커보다 위
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

        // 4) sibling 정렬 강제:
        //    팝업을 최상위로 올리고, 블로커는 "팝업 바로 아래"로 둔다.
        popT.SetAsLastSibling();

        if (modalBlocker != null)
        {
            // popT가 last인 상태에서 그 바로 아래로
            int popupIndex = popT.GetSiblingIndex();
            modalBlocker.transform.SetSiblingIndex(Mathf.Max(0, popupIndex - 1));
        }
    }

    private void ShowModalLayer(bool on)
    {
        if (modalBlocker != null) modalBlocker.SetActive(on);
        if (on) EnsureModalAndPopupHierarchyAndOrder();
    }

    // -------------------------
    // 공개 API들
    // -------------------------

    public void ShowPurchaseConfirm(string itemName, int price, int haveCoins, Action onConfirm)
    {
        _onConfirm = onConfirm;
        _isVisible = true;

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

        ShowRoot(true);
        ShowModalLayer(true);
    }

    public void ShowMessage(string message, string title = "알림")
    {
        _onConfirm = null;
        _isVisible = true;

        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        if (confirmButtonText != null) confirmButtonText.text = "확인";
        if (cancelButtonText != null) cancelButtonText.text = "";

        if (confirmButton != null) confirmButton.gameObject.SetActive(true);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);

        ShowRoot(true);
        ShowModalLayer(true);
    }

    // IAP 확인용 범용 Confirm
    public void ShowConfirm(string title, string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel = null)
    {
        _onConfirm = onConfirm;
        _isVisible = true;

        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        if (confirmButtonText != null) confirmButtonText.text = string.IsNullOrEmpty(confirmLabel) ? "확인" : confirmLabel;
        if (cancelButtonText != null) cancelButtonText.text = string.IsNullOrEmpty(cancelLabel) ? "취소" : cancelLabel;

        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(true);
            confirmButton.interactable = true;
        }

        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = true;

            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(() =>
            {
                Hide();
                onCancel?.Invoke();
            });
        }

        ShowRoot(true);
        ShowModalLayer(true);
    }

    public void ShowPurchaseConfirm(CosmeticItem item, int haveCoins, Action onConfirm)
    {
        if (item == null)
        {
            ShowMessage("아이템 정보가 없습니다.");
            return;
        }

        _onConfirm = onConfirm;
        _isVisible = true;

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

        ShowRoot(true);
        ShowModalLayer(true);
    }

    // -------------------------
    // 닫기 (클릭 스루 방지)
    // -------------------------
    public void Hide()
    {
        _onConfirm = null;

        if (!_isVisible)
        {
            ShowModalLayer(false);
            ShowRoot(false);
            return;
        }

        _isVisible = false;

        // 닫는 순간 클릭이 뒤로 통과하는 걸 막기 위해 현재 프레임 입력 종료 후 닫는다.
        if (confirmButton != null) confirmButton.interactable = false;
        if (cancelButton != null) cancelButton.interactable = false;

        Runner.StartCoroutine(CoHideNextFrame());
    }

    private System.Collections.IEnumerator CoHideNextFrame()
    {
        yield return null;

        ShowRoot(false);
        ShowModalLayer(false);

        if (confirmButton != null) confirmButton.interactable = true;
        if (cancelButton != null) cancelButton.interactable = true;
    }

    private void HideImmediate()
    {
        _onConfirm = null;
        _isVisible = false;
        ShowRoot(false);
        ShowModalLayer(false);
    }

    private void ShowRoot(bool on)
    {
        if (root != null) root.SetActive(on);
        else gameObject.SetActive(on);
    }

    private void OnClickConfirm()
    {
        var cb = _onConfirm;
        Hide();
        cb?.Invoke();
    }
}