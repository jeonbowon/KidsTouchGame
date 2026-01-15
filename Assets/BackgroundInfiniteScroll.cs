using UnityEngine;

public class BackgroundInfiniteScroll : MonoBehaviour
{
    [Header("Sprite Renderers")]
    [SerializeField] private SpriteRenderer bgA;
    [SerializeField] private SpriteRenderer bgB;

    [Header("Scroll")]
    [SerializeField] private float scrollSpeed = 0.6f; // 월드유닛/초
    [SerializeField] private bool scrollDown = true;   // true면 아래로 흐름
    [SerializeField] private float padding = 1.02f;    // 가장자리 여유

    private float spriteHeight; // 스케일 반영된 실제 월드 높이
    private Camera _cam;
    private bool _ready;

    void Start()
    {
        _cam = Camera.main;
        RebuildAndReset();
    }

    void Update()
    {
        if (!_ready) return;

        float dir = scrollDown ? -1f : 1f;
        Vector3 delta = Vector3.up * dir * scrollSpeed * Time.deltaTime;

        bgA.transform.position += delta;
        bgB.transform.position += delta;

        if (scrollDown)
        {
            if (bgA.transform.position.y <= -spriteHeight)
                bgA.transform.position += Vector3.up * spriteHeight * 2f;
            if (bgB.transform.position.y <= -spriteHeight)
                bgB.transform.position += Vector3.up * spriteHeight * 2f;
        }
        else
        {
            if (bgA.transform.position.y >= spriteHeight)
                bgA.transform.position += Vector3.down * spriteHeight * 2f;
            if (bgB.transform.position.y >= spriteHeight)
                bgB.transform.position += Vector3.down * spriteHeight * 2f;
        }

        // 항상 화면 위쪽에 있는 것이 bgB가 되도록(선택사항)
        if (bgA.transform.position.y > bgB.transform.position.y)
        {
            var t = bgA; bgA = bgB; bgB = t;
        }
    }

    // =========================
    // 런타임 제어용 API
    // =========================

    public void SetSprites(Sprite sprite)
    {
        if (!bgA || !bgB) return;
        bgA.sprite = sprite;
        bgB.sprite = sprite;
        RebuildAndReset();
    }

    public void SetSpeed(float speed) => scrollSpeed = speed;
    public void SetScrollDown(bool down) => scrollDown = down;

    public void RebuildAndReset()
    {
        if (!bgA || !bgB)
        {
            Debug.LogError($"[BG] Assign bgA/bgB. ({name})");
            _ready = false;
            return;
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null)
        {
            _ready = false;
            return;
        }

        if (bgA.sprite == null || bgB.sprite == null)
        {
            // 스프라이트가 없으면 업데이트 중지
            _ready = false;
            return;
        }

        FitToCamera(bgA, _cam);
        FitToCamera(bgB, _cam);

        spriteHeight = bgA.bounds.size.y;

        // 위치 재배치
        bgA.transform.position = new Vector3(0f, 0f, 0f);
        bgB.transform.position = bgA.transform.position + Vector3.up * spriteHeight;

        _ready = true;
    }

    private void FitToCamera(SpriteRenderer sr, Camera cam)
    {
        float worldH = cam.orthographicSize * 2f;
        float worldW = worldH * cam.aspect;

        float ppu = sr.sprite.pixelsPerUnit;
        Vector2 spriteWorld = new Vector2(sr.sprite.rect.width / ppu,
                                          sr.sprite.rect.height / ppu);

        float scale = Mathf.Max(worldW / spriteWorld.x, worldH / spriteWorld.y) * padding;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
