using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("패널 연결")]
    [SerializeField] private GameObject panelMain;
    [SerializeField] private GameObject panelSettings;

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
    [SerializeField] private AudioClip menuClip;   // 메인 메뉴에서 틀 음악

    private const string KEY_BGM = "opt_bgm";

    private void Start()
    {
        panelMain.SetActive(true);
        panelSettings.SetActive(false);

        // 여기서 timeScale을 직접 만지지 않는다
        // timeScale 제어는 GameManager만 담당한다

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

    public void OnClickStart()
    {
        if (GameManager.I != null)
        {
            // 게임 시작 시점에만 GameManager가 풀도록 위임
            GameManager.I.NewRun();
        }
        else
        {
            Debug.LogError("[MainMenuController] GameManager.I 가 null 입니다.");
        }
    }

    public void OnClickSettings()
    {
        panelMain.SetActive(false);
        panelSettings.SetActive(true);
    }

    public void OnClickCloseSettings()
    {
        if (sliderBGM != null)
            OnBgmVolumeChanged(sliderBGM.value);

        panelSettings.SetActive(false);
        panelMain.SetActive(true);
    }

    public void OnClickQuit()
    {
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
