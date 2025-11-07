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

    void Start()
    {
        if (!bgA || !bgB) { Debug.LogError("[BG] Assign bgA/bgB."); return; }
        var cam = Camera.main;
        if (!cam) return;

        // 두 스프라이트를 현재 카메라 화면에 빈틈 없이 맞춤 (세로 기준, 가로도 자동)
        FitToCamera(bgA, cam);
        FitToCamera(bgB, cam);

        // 실제 월드 높이(스케일 반영된 bounds)
        spriteHeight = bgA.bounds.size.y;

        // 위치 재배치: A를 가운데, B를 그 위로 정확히 붙임
        bgA.transform.position = new Vector3(0f, 0f, 0f);
        bgB.transform.position = bgA.transform.position + Vector3.up * spriteHeight;
    }

    void Update()
    {
        float dir = scrollDown ? -1f : 1f;
        Vector3 delta = Vector3.up * dir * scrollSpeed * Time.deltaTime; // up * (-) = 아래로

        bgA.transform.position += delta;
        bgB.transform.position += delta;

        // 화면 아래로 완전히 내려간 배경을 위로 재배치 (아래로 흐르는 경우)
        if (scrollDown)
        {
            if (bgA.transform.position.y <= -spriteHeight)
                bgA.transform.position += Vector3.up * spriteHeight * 2f;
            if (bgB.transform.position.y <= -spriteHeight)
                bgB.transform.position += Vector3.up * spriteHeight * 2f;
        }
        else // 위로 흐르는 경우
        {
            if (bgA.transform.position.y >= spriteHeight)
                bgA.transform.position += Vector3.down * spriteHeight * 2f;
            if (bgB.transform.position.y >= spriteHeight)
                bgB.transform.position += Vector3.down * spriteHeight * 2f;
        }

        // 항상 화면 위쪽에 있는 것이 bgB가 되도록(가독성, 선택사항)
        if (bgA.transform.position.y > bgB.transform.position.y)
        {
            var t = bgA; bgA = bgB; bgB = t;
        }
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
