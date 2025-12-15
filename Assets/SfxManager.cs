using UnityEngine;

/// <summary>
/// 효과음(SFX) 전용 매니저.
/// - 싱글톤 + DontDestroyOnLoad
/// - 아이템 / 폭발 사운드를 각각 다른 비율로 키울 수 있음.
/// </summary>
[DisallowMultipleComponent]
public class SfxManager : MonoBehaviour
{
    public static SfxManager I { get; private set; }

    [Header("필수 컴포넌트")]
    [SerializeField] private AudioSource sfxSource; // 2D용 AudioSource (OneShot 재생)

    [Header("전체 SFX 마스터 볼륨")]
    [Range(0f, 1f)] public float masterSfxVolume = 1f;

    [Header("타입별 가중치 (상대 볼륨)")]
    [Tooltip("아이템 획득 사운드 계수")]
    [Range(0f, 3f)] public float itemVolumeMul = 1.5f;

    [Tooltip("폭발 / 피격 사운드 계수")]
    [Range(0f, 3f)] public float explosionVolumeMul = 2.0f;

    private void Awake()
    {
        // 싱글톤 보장
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);

        // AudioSource 확보
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
                sfxSource = gameObject.AddComponent<AudioSource>();
        }

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f; // 2D 사운드
    }

    public void SetMasterVolume(float v)
    {
        masterSfxVolume = Mathf.Clamp01(v);
    }

    /// <summary>아이템 획득 사운드 재생</summary>
    public void PlayItem(AudioClip clip, float localVolume = 1f)
    {
        PlayInternal(clip, localVolume * itemVolumeMul);
    }

    /// <summary>폭발 / 피격 사운드 재생</summary>
    public void PlayExplosion(AudioClip clip, float localVolume = 1f)
    {
        PlayInternal(clip, localVolume * explosionVolumeMul);
    }

    private void PlayInternal(AudioClip clip, float mul)
    {
        if (clip == null || sfxSource == null) return;

        // master * 타입계수 * 로컬볼륨 (최대 3배까지 허용)
        float v = Mathf.Clamp(masterSfxVolume * mul, 0f, 3f);
        if (v <= 0f) return;

        sfxSource.PlayOneShot(clip, v);
    }
}
