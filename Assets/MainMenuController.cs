using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuController : MonoBehaviour
{
    [Header("패널 연결")]
    [SerializeField] private GameObject panelMain;
    [SerializeField] private GameObject panelSettings;

    [Header("Store 패널(추가)")]
    [SerializeField] private GameObject panelStore;
    [SerializeField] private StoreController storeController; // panelStore 안에 붙여도 됨

    [Header("씬 이름")]
    [SerializeField] private string gameSceneName = "Stage1";

    [Header("Settings UI")]
    [SerializeField] private Slider sliderBGM;
    [SerializeField] private Slider sliderSFX;
    [SerializeField] private Dropdown dropdownDiff;
    [SerializeField] private Toggle toggleAutoFire;

    [Header("오디오")]
    [SerializeField] private AudioSource bgmSource;

    [Header("BGM")]
    [SerializeField] private AudioClip menuClip;

    private const string KEY_BGM = "opt_bgm";

    // =========================
    // DEV CHEATS (Hidden UI)
    // =========================
    [Header("DEV Cheats (Hidden)")]
    [Tooltip("좌상단 탭 횟수 -> 우하단 탭 횟수 (예: 7,7)")]
    [SerializeField] private int devTapCount = 7;

    [Tooltip("시퀀스 전체 제한 시간(초)")]
    [SerializeField] private float devSequenceTimeout = 6f;

    [Tooltip("코너 판정 영역(화면 비율). 0.18이면 가로/세로 18% 영역이 코너로 잡힘")]
    [Range(0.08f, 0.3f)]
    [SerializeField] private float cornerAreaRatio = 0.18f;

    private int _tlCount = 0;
    private int _brCount = 0;
    private float _seqStartTime = -1f;

    // 런타임 생성 UI
    private GameObject _devRoot;
    private InputField _devInput;
    private Text _devInfoText;
    private Text _devCoinsText;

    // Unity 6 built-in font: LegacyRuntime.ttf
    private Font _builtinFont;

    private void Awake()
    {
        _builtinFont = LoadBuiltinFontSafe();
        ValidateRefs();
    }

    private void Start()
    {
        // 초기 상태
        SetActiveSafe(panelMain, true);
        SetActiveSafe(panelSettings, false);
        SetActiveSafe(panelStore, false);

        // 볼륨 로드
        float bgm = PlayerPrefs.GetFloat(KEY_BGM, 0.8f);

        if (sliderBGM != null)
        {
            sliderBGM.value = bgm;
            sliderBGM.onValueChanged.AddListener(OnBgmVolumeChanged);
        }

        if (bgmSource != null)
            bgmSource.volume = bgm;

        // 메뉴 BGM 전환
        if (BGMManager.Instance != null && menuClip != null)
        {
            var cur = BGMManager.Instance.Source ? BGMManager.Instance.Source.clip : null;
            if (cur != menuClip)
                BGMManager.Instance.PlayWithFade(menuClip, 0.6f, true);
        }

        // 날짜가 바뀌면 dev 자동 무효. (IsDevEnabled()가 내부에서 처리)
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
        // Input System / Legacy 둘 다 안전하게 처리
        HandleDevGesture();
    }

    private void ValidateRefs()
    {
        if (panelMain == null) Debug.LogWarning("[MainMenuController] panelMain 연결 안됨(Inspector).");
        if (panelSettings == null) Debug.LogWarning("[MainMenuController] panelSettings 연결 안됨(Inspector).");
        if (panelStore == null) Debug.LogWarning("[MainMenuController] panelStore 연결 안됨(Inspector).");

        // panelStore가 panelMain의 자식이면 panelMain을 끄는 순간 같이 꺼져서 Store가 절대 안 보입니다.
        if (panelMain != null && panelStore != null && panelStore.transform.IsChildOf(panelMain.transform))
        {
            Debug.LogWarning("[MainMenuController] panelStore가 panelMain의 자식입니다. panelMain을 끄면 panelStore도 같이 꺼집니다. panelStore를 panelMain과 형제(같은 레벨)로 두세요.");
        }
    }

    private static void SetActiveSafe(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf != active) go.SetActive(active);
    }

    public void OnClickStart()
    {
        Debug.Log("[MainMenuController] OnClickStart()");
        if (GameManager.I != null)
        {
            GameManager.I.NewRun();
        }
        else
        {
            Debug.LogError("[MainMenuController] GameManager.I 가 null 입니다.");
        }
    }

    public void OnClickSettings()
    {
        Debug.Log("[MainMenuController] OnClickSettings()");
        SetActiveSafe(panelMain, false);
        SetActiveSafe(panelSettings, true);
        SetActiveSafe(panelStore, false);
    }

    public void OnClickCloseSettings()
    {
        Debug.Log("[MainMenuController] OnClickCloseSettings()");
        if (sliderBGM != null)
            OnBgmVolumeChanged(sliderBGM.value);

        SetActiveSafe(panelSettings, false);
        SetActiveSafe(panelMain, true);
    }

    // Store 열기
    public void OnClickStore()
    {
        Debug.Log("[MainMenuController] OnClickStore()");

        if (panelStore == null)
        {
            Debug.LogError("[MainMenuController] panelStore가 null 입니다. (Button이 다른 MainMenuController를 호출 중이거나 Inspector 연결이 안 됨)");
            return;
        }

        SetActiveSafe(panelMain, false);
        SetActiveSafe(panelSettings, false);
        SetActiveSafe(panelStore, true);

        Debug.Log($"[MainMenuController] panelStore 활성화 완료. activeSelf={panelStore.activeSelf}, activeInHierarchy={panelStore.activeInHierarchy}");

        if (storeController != null)
            storeController.RefreshAll();
        else
            Debug.LogWarning("[MainMenuController] storeController가 null 입니다(선택).");
    }

    // Store 닫기
    public void OnClickCloseStore()
    {
        Debug.Log("[MainMenuController] OnClickCloseStore()");

        if (panelStore == null)
        {
            Debug.LogError("[MainMenuController] panelStore가 null 입니다.");
            return;
        }

        SetActiveSafe(panelStore, false);
        SetActiveSafe(panelMain, true);
    }

    public void OnClickQuit()
    {
        Debug.Log("[MainMenuController] OnClickQuit()");
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
    // DEV Gesture + Runtime UI
    // ============================================================

    private void HandleDevGesture()
    {
        // 마우스/터치 모두 처리
        bool down = false;
        Vector2 pos = default;

#if ENABLE_INPUT_SYSTEM
        // -------- Input System --------
        // 터치 우선
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                down = true;
                pos = touch.position.ReadValue();
            }
        }

        // 마우스
        if (!down && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            down = true;
            pos = Mouse.current.position.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        // -------- Legacy Input --------
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

        // 시간 초과면 리셋
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
            // TL을 다 채운 다음에만 BR 카운트
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

    private void EnsureDevUI()
    {
        if (_devRoot != null) return;

        // "아무 Canvas"가 아니라, MainMenu의 Canvas를 우선적으로 찾는다.
        Canvas canvas = null;

        // 1) 활성 포함 전체 Canvas에서 "MainMenu 씬 Canvas" 같은 걸 우선으로 잡고 싶으면
        //    현재는 MainMenu에 Canvas가 1개인 전제라 FindObjectOfType로 충분.
        canvas = FindObjectOfType<Canvas>(true);

        if (canvas == null)
        {
            Debug.LogError("[DEV] Canvas를 찾지 못했습니다. MainMenu 씬에 Canvas가 있어야 합니다.");
            return;
        }

        // Root
        _devRoot = new GameObject("DevCheatUI");
        _devRoot.transform.SetParent(canvas.transform, false);

        var rootRT = _devRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // Blocker
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
        bBtn.onClick.AddListener(() => ShowDevUI(false)); // 바깥 클릭하면 닫기

        // Panel
        var panel = new GameObject("Panel");
        panel.transform.SetParent(_devRoot.transform, false);
        var pRT = panel.AddComponent<RectTransform>();
        pRT.anchorMin = new Vector2(0.5f, 0.5f);
        pRT.anchorMax = new Vector2(0.5f, 0.5f);
        pRT.sizeDelta = new Vector2(640, 520);
        pRT.anchoredPosition = Vector2.zero;

        var pImg = panel.AddComponent<Image>();
        pImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

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
        CreateButton(panel.transform, "BtnAddCoins", "+1000 COINS", new Vector2(-160, -360), () =>
        {
            if (!DevCheats.IsDevEnabled()) return;
            DevCheats.AddCoins(1000);
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
        string hint = "TODAY CODE = DDMMYY (예: 070126)";
#endif

        _devInfoText.text =
            $"Status: {(enabled ? "ENABLED" : "LOCKED")}\n" +
            $"Date(Local): {System.DateTime.Now:yyyyMMdd}\n" +
            $"{hint}\n" +
            $"Gesture: TL x{devTapCount} -> BR x{devTapCount}";

        _devCoinsText.text = $"COINS: {DevCheats.GetCoins()}";
    }

    // ----------------- UI Builders -----------------

    private Font LoadBuiltinFontSafe()
    {
        // Unity 6: LegacyRuntime.ttf
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;

        // 혹시 구버전/환경에서만 남아있을 수 있는 fallback
        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) return f;

        // 최후 fallback: null 허용(이 경우 Text가 깨질 수 있음)
        Debug.LogWarning("[DEV] Built-in Font 로드 실패. LegacyRuntime.ttf/Arial.ttf 둘 다 없음.");
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

        // Unity 6 오류 방지: LegacyRuntime.ttf 사용
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

        // Placeholder
        var ph = CreateText(go.transform, "Placeholder", "Enter Code (DDMMYY, 6 digits)", 14, TextAnchor.MiddleLeft);
        ph.color = new Color(0, 0, 0, 0.4f);
        var phRT = ph.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(10, 6);
        phRT.offsetMax = new Vector2(-10, -6);

        // Text
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
