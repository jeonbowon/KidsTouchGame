using UnityEngine;
using System.Collections;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("필수 컴포넌트")]
    [SerializeField] private AudioSource bgmSource;   // BGM 재생용 AudioSource (드래그 연결)

    [Header("초기 설정")]
    [SerializeField] private AudioClip initialClip;   // 처음에 틀고 싶은 음악(없으면 생략 가능)
    [SerializeField] private bool playOnStart = true; // 시작 시 자동 재생
    [SerializeField] private bool loop = true;        // 기본 루프
    [Range(0f, 1f)][SerializeField] private float initialVolume = 0.8f;

    public AudioSource Source => bgmSource;

    private void Awake()
    {
        // 싱글톤 + 중복 제거
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (bgmSource == null)
        {
            // 안전장치: 없으면 자동 추가
            bgmSource = gameObject.GetComponent<AudioSource>();
            if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false; // 제어는 스크립트가 함
        bgmSource.loop = loop;
        bgmSource.volume = initialVolume;

        if (playOnStart && initialClip != null)
            Play(initialClip, loop);
    }

    /// <summary>즉시 재생(교체). 페이드 없이.</summary>
    public void Play(AudioClip clip, bool isLoop = true, float volume = -1f)
    {
        if (clip == null || bgmSource == null) return;

        bgmSource.loop = isLoop;
        if (volume >= 0f) bgmSource.volume = Mathf.Clamp01(volume);

        if (bgmSource.clip != clip)
            bgmSource.clip = clip;

        if (!bgmSource.isPlaying) bgmSource.Play();
        else { bgmSource.Stop(); bgmSource.Play(); }
    }

    /// <summary>크로스 페이드로 다른 곡으로 전환.</summary>
    public void PlayWithFade(AudioClip nextClip, float fadeTime = 0.5f, bool isLoop = true)
    {
        if (nextClip == null || bgmSource == null) return;
        StopAllCoroutines();
        StartCoroutine(Co_FadeTo(nextClip, fadeTime, isLoop));
    }

    /// <summary>볼륨 즉시 반영.</summary>
    public void SetVolume(float v)
    {
        if (bgmSource == null) return;
        bgmSource.volume = Mathf.Clamp01(v);
    }

    public float GetVolume() => bgmSource ? bgmSource.volume : 0f;

    IEnumerator Co_FadeTo(AudioClip nextClip, float t, bool isLoop)
    {
        float startVol = bgmSource.volume;
        float time = 0f;

        // 페이드 아웃
        while (time < t)
        {
            time += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, time / t);
            yield return null;
        }

        bgmSource.Stop();
        bgmSource.clip = nextClip;
        bgmSource.loop = isLoop;
        bgmSource.Play();

        // 페이드 인
        time = 0f;
        while (time < t)
        {
            time += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, startVol, time / t);
            yield return null;
        }
    }
}
