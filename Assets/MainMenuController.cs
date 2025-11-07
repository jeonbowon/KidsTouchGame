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
    [SerializeField] private AudioSource bgmSource; // ★ 전용 AudioSource를 드래그로 연결
    // 만약 태그로 찾고 싶다면: private const string BGM_TAG = "BGM";

    [Header("BGM")]
    [SerializeField] private AudioClip menuClip;   // ★ 메인 메뉴에서 틀 음악

    private const string KEY_BGM = "opt_bgm";

    private void Start()
    {
        panelMain.SetActive(true);
        panelSettings.SetActive(false);
        Time.timeScale = 1f;

        // ★ 드래그 연결을 못했으면(널이면) 마지막 방어적으로 찾아보기
        if (bgmSource == null)
        {
            // 1) 태그 방식 (추천) : BGM 오브젝트에 "BGM" 태그 부여 후 사용
            // var go = GameObject.FindGameObjectWithTag(BGM_TAG);
            // if (go) bgmSource = go.GetComponent<AudioSource>();

            // 2) 최후의 수단: FindObjectOfType (권장하진 않음)
            bgmSource = FindObjectOfType<AudioSource>();
        }

        // 저장된 볼륨 로드
        float bgm = PlayerPrefs.GetFloat(KEY_BGM, 0.8f);

        if (sliderBGM != null)
        {
            sliderBGM.value = bgm;
            sliderBGM.onValueChanged.AddListener(OnBgmVolumeChanged);
        }

        if (bgmSource != null)
            bgmSource.volume = bgm;

        // 메뉴 씬에 들어올 때 메뉴 BGM이 아니면 페이드 전환
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
            GameManager.I.NewRun();
        else
            Debug.LogError("[MainMenuController] GameManager.I 가 null 입니다.");
    }


    public void OnClickSettings()
    {
        panelMain.SetActive(false);
        panelSettings.SetActive(true);
    }

    public void OnClickCloseSettings()
    {
        // 닫을 때 현재값 저장/반영 (슬라이더 이벤트 안 거쳤을 수도 있으니)
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
