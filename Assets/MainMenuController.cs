using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    private void Awake()
    {
        // 참조 체크: 여기서 경고가 뜨면 Inspector 연결이 잘못된 겁니다.
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
    }

    private void OnDestroy()
    {
        if (sliderBGM != null)
            sliderBGM.onValueChanged.RemoveListener(OnBgmVolumeChanged);
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
}
