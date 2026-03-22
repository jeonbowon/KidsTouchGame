using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuController : MonoBehaviour
{
    // =========================
    // Overlay Store Mode (Additive)
    // =========================
    private static bool s_overlayStoreRequest = false;
    private static System.Action s_onOverlayStoreClosed = null;

    public static void RequestOverlayStore(System.Action onClosed)
    {
        s_overlayStoreRequest = true;
        s_onOverlayStoreClosed = onClosed;
    }

    private bool _overlayStoreMode = false;

    [Header("�г� ����")]
    [SerializeField] private GameObject panelMain;
    [SerializeField] private GameObject panelSettings;

    [Header("Store �г�(�߰�)")]
    [SerializeField] private GameObject panelStore;
    [SerializeField] private StoreController storeController; // panelStore �ȿ� �ٿ��� ��

    [Header("�� �̸�")]
    [SerializeField] private string gameSceneName = "Stage1";

    [Header("Settings UI")]
    [SerializeField] private Slider sliderBGM;
    [SerializeField] private Slider sliderSFX;
    [SerializeField] private Dropdown dropdownDiff;
    [SerializeField] private Toggle toggleAutoFire;

    [Header("�����")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGM")]
    [SerializeField] private AudioClip menuClip;

    private const string KEY_BGM = "opt_bgm";

    // =========================
    // DEV CHEATS (Hidden UI)
    // =========================
    [Header("DEV Cheats (Hidden)")]
    [Tooltip("�»�� �� Ƚ�� -> ���ϴ� �� Ƚ�� (��: 7,7)")]
    [SerializeField] private int devTapCount = 7;

    [Tooltip("������ ��ü ���� �ð�(��)")]
    [SerializeField] private float devSequenceTimeout = 6f;

    [Tooltip("�ڳ� ���� ����(ȭ�� ����). 0.18�̸� ����/���� 18% ������ �ڳʷ� ����")]
    [Range(0.08f, 0.3f)]
    [SerializeField] private float cornerAreaRatio = 0.18f;

    private int _tlCount = 0;
    private int _brCount = 0;
    private float _seqStartTime = -1f;

    // ��Ÿ�� ���� UI
    private GameObject _devRoot;
    private InputField _devInput;
    private Text _devInfoText;
    private Text _devCoinsText;

    // Unity 6 built-in font: LegacyRuntime.ttf
    private Font _builtinFont;

    // =========================
    // DEV CHEATS UI SCALE (auto enlarge on high-res devices)
    // =========================
    private const float DEV_PANEL_BASE_W = 640f;
    private const float DEV_PANEL_BASE_H = 520f;

    // Panel target size relative to the screen/canvas.
    // Increase these if you want the dev panel bigger.
    private const float DEV_PANEL_TARGET_W_RATIO = 0.92f; // 92% of width
    private const float DEV_PANEL_TARGET_H_RATIO = 0.78f; // 78% of height
    private const float DEV_PANEL_MAX_SCALE = 2.50f;

    private float ComputeDevPanelScale(Canvas canvas)
    {
        RectTransform crt = (canvas != null) ? canvas.GetComponent<RectTransform>() : null;

        Vector2 canvasSize =
            (crt != null && crt.rect.width > 1f && crt.rect.height > 1f)
                ? new Vector2(crt.rect.width, crt.rect.height)
                : new Vector2(Screen.width, Screen.height);

        float targetW = canvasSize.x * DEV_PANEL_TARGET_W_RATIO;
        float targetH = canvasSize.y * DEV_PANEL_TARGET_H_RATIO;

        float scale = Mathf.Min(targetW / DEV_PANEL_BASE_W, targetH / DEV_PANEL_BASE_H);
        scale = Mathf.Clamp(scale, 1f, DEV_PANEL_MAX_SCALE);
        return scale;
    }

    private void Awake()
    {
        _builtinFont = LoadBuiltinFontSafe();
        ValidateRefs();

        // Additive Shop �������� ��û�� ������ �� �ν��Ͻ��� �������� ���� ����
        if (s_overlayStoreRequest)
        {
            _overlayStoreMode = true;
            // ������ �� MainMenu�� ���� �ε����� �� ������ ���� �ʰ� ��� ����
            s_overlayStoreRequest = false;
        }
    }

    private void Start()
    {
        if (_overlayStoreMode)
        {
            // overlay���� panelStore/storeController ������ ������ �����ϵ��� ����
            ResolveStoreRefsForOverlay();

            // �������� ���: Shop�� �Ѱ� ������ ����� �ּ�ȭ
            SetActiveSafe(panelMain, false);
            SetActiveSafe(panelSettings, false);
            SetActiveSafe(panelStore, true);

            // Canvas�� Stage UI ���� ������ sorting �ø�(�����ϸ�)
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = 999;
            }

            // �ٽ�: �������� ��忡�� Close ��ư�� Ȯ���� �� ��ũ��Ʈ�� OnClickCloseStore()�� Ÿ���� ���� ����
            WireOverlayCloseButton_Strong();

            if (storeController != null)
                storeController.RefreshAll();

            return;
        }

        // -------- ���� MainMenu ���� --------

        // �ʱ� ����
        SetActiveSafe(panelMain, true);
        SetActiveSafe(panelSettings, false);
        SetActiveSafe(panelStore, false);

        // ���� �ε�
        float bgm = PlayerPrefs.GetFloat(KEY_BGM, 0.8f);

        if (sliderBGM != null)
        {
            sliderBGM.value = bgm;
            sliderBGM.onValueChanged.AddListener(OnBgmVolumeChanged);
        }

        if (bgmSource != null)
            bgmSource.volume = bgm;

        // �޴� BGM ��ȯ
        if (BGMManager.Instance != null && menuClip != null)
        {
            var cur = BGMManager.Instance.Source ? BGMManager.Instance.Source.clip : null;
            if (cur != menuClip)
                BGMManager.Instance.PlayWithFade(menuClip, 0.6f, true);
        }

        // ��¥�� �ٲ�� dev �ڵ� ��ȿ. (IsDevEnabled()�� ���ο��� ó��)
        if (DevCheats.IsDevEnabled())
        {
            EnsureDevUI();
            ShowDevUI(true);
            RefreshDevInfo();
        }
    }

    private void OnDestroy()
    {
        if (sliderBGM != null)
            sliderBGM.onValueChanged.RemoveListener(OnBgmVolumeChanged);
    }

    private void Update()
    {
        // �������� ��忡���� DEV ����ó/��Ÿ�� UI �� ����(���ʿ� + ����)
        if (_overlayStoreMode) return;

        HandleDevGesture();
    }

    private void ValidateRefs()
    {
        if (panelMain == null) Debug.LogWarning("[MainMenuController] panelMain ���� �ȵ�(Inspector).");
        if (panelSettings == null) Debug.LogWarning("[MainMenuController] panelSettings ���� �ȵ�(Inspector).");
        if (panelStore == null) Debug.LogWarning("[MainMenuController] panelStore ���� �ȵ�(Inspector).");

        // panelStore�� panelMain�� �ڽ��̸� panelMain�� ���� ���� ���� ������ Store�� ���� �� ���Դϴ�.
        if (panelMain != null && panelStore != null && panelStore.transform.IsChildOf(panelMain.transform))
        {
            Debug.LogWarning("[MainMenuController] panelStore�� panelMain�� �ڽ��Դϴ�. panelMain�� ���� panelStore�� ���� �����ϴ�. panelStore�� panelMain�� ����(���� ����)�� �μ���.");
        }
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf != active) go.SetActive(active);
    }

    // overlay���� panelStore/storeController�� null�� �� ������ ������ ã�� ä��
    private void ResolveStoreRefsForOverlay()
    {
        if (storeController == null)
            storeController = FindObjectOfType<StoreController>(true);

        if (panelStore == null)
        {
            if (storeController != null)
                panelStore = storeController.gameObject;
            else
            {
                // �̸� ��� fallback (������Ʈ���� ���� ���� �̸���)
                var byName = GameObject.Find("PanelStore") ?? GameObject.Find("panelStore") ?? GameObject.Find("StorePanel") ?? GameObject.Find("Panel_Store");
                if (byName != null) panelStore = byName;
            }
        }
    }

    // Overlay ��忡�� Close ��ư�� �� ���ϰ� ���´�:
    // - panelStore �Ʒ� ��� close/back/exit ��ư�� ���� OnClickCloseStore�� ����
    // - �� ã���� panelStore �Ʒ� ��� ��ư�� ������� 1�� ����(�־ǿ��� ������ �ǰ�)
    private void WireOverlayCloseButton_Strong()
    {
        if (panelStore == null)
        {
            Debug.LogWarning("[MainMenuController] OverlayClose ���� ����: panelStore�� null");
            return;
        }

        var buttons = panelStore.GetComponentsInChildren<Button>(true);
        if (buttons == null || buttons.Length == 0)
        {
            Debug.LogWarning("[MainMenuController] OverlayClose ���� ����: panelStore �Ʒ� Button�� ����");
            return;
        }

        int wired = 0;

        // 1) close/back/exit �켱 ���� ����
        foreach (var b in buttons)
        {
            if (b == null) continue;
            string n = b.name.ToLowerInvariant();
            if (n.Contains("close") || n.Contains("back") || n.Contains("exit"))
            {
                b.onClick.RemoveListener(OnClickCloseStore);
                b.onClick.AddListener(OnClickCloseStore);
                wired++;
            }
        }

        // 2) �ϳ��� �� ã����: ���� ����(overlay������ ���������� �ֿ켱)
        if (wired == 0)
        {
            foreach (var b in buttons)
            {
                if (b == null) continue;
                b.onClick.RemoveListener(OnClickCloseStore);
                b.onClick.AddListener(OnClickCloseStore);
                wired++;
            }
        }

    }

    public void OnClickStart()
    {
        if (GameManager.I != null)
        {
            GameManager.I.NewRun();
        }
        else
        {
            Debug.LogError("[MainMenuController] GameManager.I �� null �Դϴ�.");
        }
    }

    public void OnClickSettings()
    {
        if (_overlayStoreMode) return;

        SetActiveSafe(panelMain, false);
        SetActiveSafe(panelSettings, true);
        SetActiveSafe(panelStore, false);
    }

    public void OnClickCloseSettings()
    {
        if (_overlayStoreMode) return;

        if (sliderBGM != null)
            OnBgmVolumeChanged(sliderBGM.value);

        SetActiveSafe(panelSettings, false);
        SetActiveSafe(panelMain, true);
    }

    // Store ����
    public void OnClickStore()
    {
        if (panelStore == null)
        {
            Debug.LogError("[MainMenuController] panelStore�� null �Դϴ�. (Button�� �ٸ� MainMenuController�� ȣ�� ���̰ų� Inspector ������ �� ��)");
            return;
        }

        SetActiveSafe(panelMain, false);
        SetActiveSafe(panelSettings, false);
        SetActiveSafe(panelStore, true);

        if (storeController != null)
            storeController.RefreshAll();
        else
            Debug.LogWarning("[MainMenuController] storeController�� null �Դϴ�(����).");
    }

    // Store �ݱ�
    public void OnClickCloseStore()
    {
        if (_overlayStoreMode)
        {
            // �������� ���: MainMenu�� ���ư��� ����, GameManager���� "����" ��ȣ
            var cb = s_onOverlayStoreClosed;
            s_onOverlayStoreClosed = null;
            cb?.Invoke();
            return;
        }

        if (panelStore == null)
        {
            Debug.LogError("[MainMenuController] panelStore�� null �Դϴ�.");
            return;
        }

        SetActiveSafe(panelStore, false);
        SetActiveSafe(panelMain, true);
    }

    public void OnClickQuit()
    {
        if (_overlayStoreMode) return;

        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnBgmVolumeChanged(float value)
    {
        if (bgmSource != null)
            bgmSource.volume = value;

        PlayerPrefs.SetFloat(KEY_BGM, value);
        PlayerPrefs.Save();
    }

    // ============================================================
    // DEV Gesture + Runtime UI (���� ����)
    // ============================================================

    private void HandleDevGesture()
    {
        bool down = false;
        Vector2 pos = default;

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                down = true;
                pos = touch.position.ReadValue();
            }
        }

        if (!down && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            down = true;
            pos = Mouse.current.position.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!down)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    down = true;
                    pos = t.position;
                }
            }
            else if (Input.GetMouseButtonDown(0))
            {
                down = true;
                pos = Input.mousePosition;
            }
        }
#endif

        if (!down) return;

        if (_seqStartTime > 0f && Time.unscaledTime - _seqStartTime > devSequenceTimeout)
            ResetDevSequence();

        if (_seqStartTime < 0f)
            _seqStartTime = Time.unscaledTime;

        if (IsTopLeft(pos))
        {
            _tlCount++;
            if (_tlCount > devTapCount) _tlCount = devTapCount;
            return;
        }

        if (IsBottomRight(pos))
        {
            if (_tlCount >= devTapCount)
            {
                _brCount++;
                if (_brCount >= devTapCount)
                {
                    ResetDevSequence();
                    OpenDevPrompt();
                }
            }
            return;
        }
    }

    private void ResetDevSequence()
    {
        _tlCount = 0;
        _brCount = 0;
        _seqStartTime = -1f;
    }

    private bool IsTopLeft(Vector2 p)
    {
        float w = Screen.width;
        float h = Screen.height;
        float cw = w * cornerAreaRatio;
        float ch = h * cornerAreaRatio;

        return (p.x <= cw && p.y >= h - ch);
    }

    private bool IsBottomRight(Vector2 p)
    {
        float w = Screen.width;
        float h = Screen.height;
        float cw = w * cornerAreaRatio;
        float ch = h * cornerAreaRatio;

        return (p.x >= w - cw && p.y <= ch);
    }

    private void OpenDevPrompt()
    {
        EnsureDevUI();
        ShowDevUI(true);
        RefreshDevInfo();

        if (!DevCheats.IsDevEnabled() && _devInput != null)
            _devInput.text = "";
    }

    // ���� UI Builders ���� �״��(���� ���� ����)
    // ----------------- UI Builders -----------------
    private void EnsureDevUI()
    {
        if (_devRoot != null) return;

        Canvas canvas = FindObjectOfType<Canvas>(true);

        if (canvas == null)
        {
            Debug.LogError("[DEV] Canvas�� ã�� ���߽��ϴ�. MainMenu ���� Canvas�� �־�� �մϴ�.");
            return;
        }

        _devRoot = new GameObject("DevCheatUI");
        _devRoot.transform.SetParent(canvas.transform, false);

        var rootRT = _devRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        var blocker = new GameObject("Blocker");
        blocker.transform.SetParent(_devRoot.transform, false);
        var bRT = blocker.AddComponent<RectTransform>();
        bRT.anchorMin = Vector2.zero;
        bRT.anchorMax = Vector2.one;
        bRT.offsetMin = Vector2.zero;
        bRT.offsetMax = Vector2.zero;

        var bImg = blocker.AddComponent<Image>();
        bImg.color = new Color(0, 0, 0, 0.60f);

        var bBtn = blocker.AddComponent<Button>();
        bBtn.onClick.AddListener(() => ShowDevUI(false));

        var panel = new GameObject("Panel");
        panel.transform.SetParent(_devRoot.transform, false);
        var pRT = panel.AddComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0.5f, 0.5f);
        pRT.anchorMax = new Vector2(0.5f, 0.5f);
        pRT.sizeDelta = new Vector2(640, 520);
        pRT.anchoredPosition = Vector2.zero;

        var pImg = panel.AddComponent<Image>();
        pImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

        // Auto scale up on high-res devices (panel + all child texts/buttons)
        float devScale = ComputeDevPanelScale(canvas);
        panel.transform.localScale = new Vector3(devScale, devScale, 1f);

        // Title
        var title = CreateText(panel.transform, "Title", "DEV CHEATS", 26, TextAnchor.MiddleCenter);
        var tRT = title.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 1);
        tRT.anchorMax = new Vector2(1, 1);
        tRT.pivot = new Vector2(0.5f, 1f);
        tRT.sizeDelta = new Vector2(0, 60);
        tRT.anchoredPosition = new Vector2(0, 0);

        // Info
        _devInfoText = CreateText(panel.transform, "Info", "", 16, TextAnchor.UpperLeft);
        var iRT = _devInfoText.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0, 1);
        iRT.anchorMax = new Vector2(1, 1);
        iRT.pivot = new Vector2(0.5f, 1f);
        iRT.sizeDelta = new Vector2(-40, 120);
        iRT.anchoredPosition = new Vector2(0, -70);

        // InputField (code)
        var inputGO = CreateInputField(panel.transform, "CodeInput");
        _devInput = inputGO.GetComponent<InputField>();
        var inRT = inputGO.GetComponent<RectTransform>();
        inRT.anchorMin = new Vector2(0.5f, 1);
        inRT.anchorMax = new Vector2(0.5f, 1);
        inRT.pivot = new Vector2(0.5f, 1);
        inRT.sizeDelta = new Vector2(360, 44);
        inRT.anchoredPosition = new Vector2(0, -190);

        // Buttons row 1
        CreateButton(panel.transform, "BtnUnlock", "UNLOCK", new Vector2(-160, -250), () =>
        {
            if (_devInput == null) return;

            bool ok = DevCheats.TryUnlockDev(_devInput.text);
            RefreshDevInfo();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ok)
                Debug.Log($"[DEV] Wrong code. TODAY(DDMMYY)={DevCheats.GetTodayCode()}");
#endif
        });

        CreateButton(panel.transform, "BtnLock", "LOCK", new Vector2(160, -250), () =>
        {
            DevCheats.LockDevNow();
            RefreshDevInfo();
        });

        // Coins label
        _devCoinsText = CreateText(panel.transform, "Coins", "", 18, TextAnchor.MiddleCenter);
        var cRT = _devCoinsText.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.5f, 1);
        cRT.anchorMax = new Vector2(0.5f, 1);
        cRT.pivot = new Vector2(0.5f, 1);
        cRT.sizeDelta = new Vector2(600, 30);
        cRT.anchoredPosition = new Vector2(0, -310);

        // Buttons row 2
        CreateButton(panel.transform, "BtnAddCoins", "+10000 COINS", new Vector2(-160, -360), () =>
        {
            if (!DevCheats.IsDevEnabled()) return;
            DevCheats.AddCoins(10000);
            RefreshDevInfo();
        });

        CreateButton(panel.transform, "BtnZeroCoins", "COINS = 0", new Vector2(160, -360), () =>
        {
            if (!DevCheats.IsDevEnabled()) return;
            DevCheats.SetCoins(0);
            RefreshDevInfo();
        });

        // Buttons row 3
        CreateButton(panel.transform, "BtnUnlockAll", "UNLOCK ALL", new Vector2(-160, -420), () =>
        {
            if (!DevCheats.IsDevEnabled()) return;
            DevCheats.UnlockAll();
            RefreshDevInfo();
            if (storeController != null) storeController.RefreshAll();
        });

        CreateButton(panel.transform, "BtnResetAll", "RESET ALL", new Vector2(160, -420), () =>
        {
            if (!DevCheats.IsDevEnabled()) return;
            DevCheats.ResetAllCosmeticsAndCoins();
            RefreshDevInfo();
            if (storeController != null) storeController.RefreshAll();
        });

        // Close button
        CreateButton(panel.transform, "BtnClose", "CLOSE", new Vector2(0, -480), () => ShowDevUI(false));

        ShowDevUI(false);
    }

    private void ShowDevUI(bool show)
    {
        if (_devRoot == null) return;
        _devRoot.SetActive(show);
    }

    private void RefreshDevInfo()
    {
        if (_devInfoText == null || _devCoinsText == null) return;

        bool enabled = DevCheats.IsDevEnabled();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string hint = $"TODAY CODE(DDMMYY) = {DevCheats.GetTodayCode()}";
#else
        string hint = "TODAY CODE = DDMMYY (��: 070126)";
#endif

        _devInfoText.text =
            $"Status: {(enabled ? "ENABLED" : "LOCKED")}\n" +
            $"Date(Local): {System.DateTime.Now:yyyyMMdd}\n" +
            $"{hint}\n" +
            $"Gesture: TL x{devTapCount} -> BR x{devTapCount}";

        _devCoinsText.text = $"COINS: {DevCheats.GetCoins()}";
    }

    private Font LoadBuiltinFontSafe()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;

        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) return f;

        Debug.LogWarning("[DEV] Built-in Font �ε� ����. LegacyRuntime.ttf/Arial.ttf �� �� ����.");
        return null;
    }

    private Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.localScale = Vector3.one;

        var t = go.AddComponent<Text>();
        t.text = text;

        t.font = _builtinFont != null ? _builtinFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = Color.black;

        return t;
    }

    private GameObject CreateInputField(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.localScale = Vector3.one;

        var img = go.AddComponent<Image>();
        img.color = Color.white;

        var input = go.AddComponent<InputField>();

        var ph = CreateText(go.transform, "Placeholder", "Enter Code (DDMMYY, 6 digits)", 14, TextAnchor.MiddleLeft);
        ph.color = new Color(0, 0, 0, 0.4f);
        var phRT = ph.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(10, 6);
        phRT.offsetMax = new Vector2(-10, -6);

        var tx = CreateText(go.transform, "Text", "", 16, TextAnchor.MiddleLeft);
        var txRT = tx.GetComponent<RectTransform>();
        txRT.anchorMin = Vector2.zero;
        txRT.anchorMax = Vector2.one;
        txRT.offsetMin = new Vector2(10, 6);
        txRT.offsetMax = new Vector2(-10, -6);

        input.textComponent = tx;
        input.placeholder = ph;
        input.characterLimit = 6;
        input.contentType = InputField.ContentType.IntegerNumber;

        return go;
    }

    private void CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.sizeDelta = new Vector2(260, 44);
        rt.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        var t = CreateText(go.transform, "Text", label, 16, TextAnchor.MiddleCenter);
        t.color = Color.white;

        var tRT = t.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = Vector2.zero;
        tRT.offsetMax = Vector2.zero;
    }
}
