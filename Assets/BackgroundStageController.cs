using UnityEngine;

public class BackgroundStageController : MonoBehaviour
{
    [System.Serializable]
    public class LayerPreset
    {
        public bool enabled = true;
        public Sprite sprite;
        public float speed = 0.2f;
        public bool scrollDown = true;
    }

    [System.Serializable]
    public class StagePreset
    {
        [Header("Base (필수)")]
        public LayerPreset baseLayer = new LayerPreset { enabled = true, speed = 0.6f, scrollDown = true };

        [Header("Galaxy (Far/Near)")]
        public LayerPreset galaxyFar = new LayerPreset { enabled = false, speed = 0.12f, scrollDown = true };
        public LayerPreset galaxyNear = new LayerPreset { enabled = false, speed = 0.22f, scrollDown = true };

        [Header("Planet (Far/Near)")]
        public LayerPreset planetFar = new LayerPreset { enabled = false, speed = 0.08f, scrollDown = true };
        public LayerPreset planetNear = new LayerPreset { enabled = false, speed = 0.16f, scrollDown = true };
    }

    [Header("Layer Scrollers (BG_Root 아래 레이어들)")]
    [SerializeField] private BackgroundInfiniteScroll baseScroll;
    [SerializeField] private BackgroundInfiniteScroll galaxyFarScroll;
    [SerializeField] private BackgroundInfiniteScroll galaxyNearScroll;
    [SerializeField] private BackgroundInfiniteScroll planetFarScroll;
    [SerializeField] private BackgroundInfiniteScroll planetNearScroll;

    [Header("Stage Presets (index 0 = Stage1)")]
    [SerializeField] private StagePreset[] presets = new StagePreset[10];

    public void ApplyStage(int stage)
    {
        int idx = Mathf.Clamp(stage - 1, 0, presets.Length - 1);
        var p = presets[idx];

        ApplyLayer(baseScroll, p.baseLayer);
        ApplyLayer(galaxyFarScroll, p.galaxyFar);
        ApplyLayer(galaxyNearScroll, p.galaxyNear);
        ApplyLayer(planetFarScroll, p.planetFar);
        ApplyLayer(planetNearScroll, p.planetNear);
    }

    private void ApplyLayer(BackgroundInfiniteScroll scroller, LayerPreset lp)
    {
        if (scroller == null) return;

        scroller.gameObject.SetActive(lp.enabled);

        if (!lp.enabled) return;

        // sprite가 없으면 켜져도 아무것도 안 보이니, 실수 방지 로그
        if (lp.sprite == null)
        {
            Debug.LogWarning($"[BG] Enabled layer has no sprite: {scroller.name}");
        }
        else
        {
            scroller.SetSprites(lp.sprite);
        }

        scroller.SetSpeed(lp.speed);
        scroller.SetScrollDown(lp.scrollDown);

        // 스프라이트/카메라 기준 재정렬
        scroller.RebuildAndReset();
    }
}
