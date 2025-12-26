using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverPanel : MonoBehaviour
{
    [Header("Auto Bind (optional)")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private TMP_Text infoText;

    public event Action OnContinueClicked;
    public event Action OnMenuClicked;

    private bool _wired = false;

    private void Awake()
    {
        AutoBindIfNeeded();
        WireOnce();
        if (root != null) root.SetActive(false);
    }

    private void AutoBindIfNeeded()
    {
        if (root == null)
        {
            var t = transform.Find("GameOverPanelRoot");
            root = (t != null) ? t.gameObject : gameObject;
        }

        if (continueButton == null || menuButton == null)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                var n = b.name.ToLowerInvariant();
                if (continueButton == null && (n.Contains("continue") || n.Contains("cont"))) continueButton = b;
                if (menuButton == null && n.Contains("menu")) menuButton = b;
            }
        }

        if (infoText == null)
        {
            var tmps = GetComponentsInChildren<TMP_Text>(true);
            foreach (var t in tmps)
            {
                string self = t.name.ToLowerInvariant();
                string parent = (t.transform.parent != null) ? t.transform.parent.name.ToLowerInvariant() : "";
                if (self.Contains("info") || parent.Contains("info"))
                {
                    infoText = t;
                    break;
                }
            }
            if (infoText == null && tmps.Length > 0) infoText = tmps[0];
        }
    }

    private void WireOnce()
    {
        if (_wired) return;
        _wired = true;

        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => OnContinueClicked?.Invoke());
        }

        if (menuButton != null)
        {
            menuButton.onClick.RemoveAllListeners();
            menuButton.onClick.AddListener(() => OnMenuClicked?.Invoke());
        }
    }

    public void Show(string info, bool showButtons)
    {
        SetInfo(info);

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(showButtons);
            continueButton.interactable = showButtons;
        }
        if (menuButton != null)
        {
            menuButton.gameObject.SetActive(showButtons);
            menuButton.interactable = showButtons;
        }

        if (root != null) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    public void SetInfo(string info)
    {
        // SetInfo 호출만으로 패널을 “강제 표시”하면 Hide 이후에도 다시 켜질 수 있습니다.
        // 메시지는 Show()가 켜줄 때만 보이게 하는 게 맞습니다.
        if (infoText != null) infoText.text = info;
    }

    public void SetButtonsInteractable(bool interactable)
    {
        if (continueButton != null && continueButton.gameObject.activeSelf)
            continueButton.interactable = interactable;
        if (menuButton != null && menuButton.gameObject.activeSelf)
            menuButton.interactable = interactable;
    }
}
