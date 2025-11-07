using UnityEngine;

public class SceneBGM : MonoBehaviour
{
    [SerializeField] private AudioClip sceneClip; // ¡Ú ÀÌ ¾À¿¡¼­ Æ² À½¾Ç
    [SerializeField] private float fadeTime = 0.6f;
    [SerializeField] private bool loop = true;

    private void Start()
    {
        if (BGMManager.Instance != null && sceneClip != null)
        {
            var cur = BGMManager.Instance.Source ? BGMManager.Instance.Source.clip : null;
            if (cur != sceneClip)
                BGMManager.Instance.PlayWithFade(sceneClip, fadeTime, loop);
        }
    }
}
