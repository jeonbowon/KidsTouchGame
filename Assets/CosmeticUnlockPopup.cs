using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CosmeticUnlockPopup : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;

    [Header("Timings")]
    [SerializeField] private float fadeIn = 0.15f;
    [SerializeField] private float hold = 0.75f;
    [SerializeField] private float fadeOut = 0.2f;

    [Header("Punch")]
    [SerializeField] private RectTransform punchTarget;
    [SerializeField] private float punchScale = 1.06f;
    [SerializeField] private float punchTime = 0.12f;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (punchTarget == null) punchTarget = GetComponentInChildren<RectTransform>(true);
        HideImmediate();
    }

    public void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        gameObject.SetActive(false);
    }

    public IEnumerator Play(CosmeticItem item, float totalTime)
    {
        if (item == null)
        {
            HideImmediate();
            yield break;
        }

        gameObject.SetActive(true);

        if (titleText != null) titleText.text = "NEW UNLOCK!";
        if (subText != null) subText.text = string.IsNullOrWhiteSpace(item.displayName) ? item.id : item.displayName;

        if (iconImage != null)
        {
            Sprite sp = item.icon != null ? item.icon : item.shipSprite;
            iconImage.sprite = sp;
            iconImage.enabled = (sp != null);
        }

        float baseTotal = fadeIn + hold + fadeOut;
        float scale = baseTotal > 0.01f ? totalTime / baseTotal : 1f;

        float fi = Mathf.Max(0.01f, fadeIn * scale);
        float ho = Mathf.Max(0.01f, hold * scale);
        float fo = Mathf.Max(0.01f, fadeOut * scale);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        Vector3 baseScaleVec = punchTarget != null ? punchTarget.localScale : Vector3.one;

        float t = 0f;
        while (t < fi)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fi);
            if (canvasGroup != null) canvasGroup.alpha = a;

            if (punchTarget != null)
            {
                float p = Mathf.Clamp01(t / Mathf.Max(0.01f, punchTime));
                float s = Mathf.Lerp(1f, punchScale, p);
                punchTarget.localScale = baseScaleVec * s;
            }

            yield return null;
        }

        if (punchTarget != null) punchTarget.localScale = baseScaleVec;

        float h = 0f;
        while (h < ho)
        {
            h += Time.unscaledDeltaTime;
            yield return null;
        }

        float o = 0f;
        while (o < fo)
        {
            o += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(o / fo);
            if (canvasGroup != null) canvasGroup.alpha = a;
            yield return null;
        }

        HideImmediate();
    }
}
