using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaTopOffset : MonoBehaviour
{
    [Tooltip("SafeArea만큼 내린 뒤 추가로 더 내릴 픽셀")]
    public float extraDown = 20f;

    RectTransform rt;
    float baseTop;
    bool cached;

    bool pending;     // 중복 적용 예약 방지
    bool applying;    // 적용 중 재진입 방지

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        CacheBase();
        RequestApply();
    }

    void OnEnable()
    {
        if (rt == null) rt = GetComponent<RectTransform>();
        CacheBase();
        RequestApply();
    }

    // RectTransform이 변하면 즉시 바꾸지 말고 "예약"만 한다.
    void OnRectTransformDimensionsChange()
    {
        RequestApply();
    }

    void CacheBase()
    {
        if (cached) return;
        if (rt == null) rt = GetComponent<RectTransform>();

        // Top Stretch 기준: offsetMax.y가 Top
        baseTop = rt.offsetMax.y;
        if (float.IsNaN(baseTop) || float.IsInfinity(baseTop))
            baseTop = 0f;

        cached = true;
    }

    void RequestApply()
    {
        if (!isActiveAndEnabled) return;
        if (pending) return;
        pending = true;
        StartCoroutine(ApplyNextFrame());
    }

    IEnumerator ApplyNextFrame()
    {
        // 레이아웃이 안정된 다음 프레임에 적용
        yield return null;

        pending = false;
        ApplySafe();
    }

    void ApplySafe()
    {
        if (applying) return;
        applying = true;

        try
        {
            if (rt == null) rt = GetComponent<RectTransform>();
            if (rt == null) return;

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0) return;

            float topCut = Screen.height - (safe.y + safe.height);
            if (float.IsNaN(topCut) || float.IsInfinity(topCut)) return;
            topCut = Mathf.Max(0f, topCut);

            float newTop = baseTop - topCut - extraDown;
            if (float.IsNaN(newTop) || float.IsInfinity(newTop)) return;

            Vector2 om = rt.offsetMax;

            // 값이 같으면 굳이 다시 안 건드린다 (불필요한 변경 방지)
            if (Mathf.Approximately(om.y, newTop)) return;

            rt.offsetMax = new Vector2(om.x, newTop);
        }
        finally
        {
            applying = false;
        }
    }
}