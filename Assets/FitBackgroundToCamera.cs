using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FitBackgroundToCamera : MonoBehaviour
{
    [SerializeField] float padding = 1.02f; // 가장자리 여유 (1~2%)

    void Start()
    {
        var cam = Camera.main;
        var sr = GetComponent<SpriteRenderer>();
        if (!cam || !sr || sr.sprite == null) return;

        // 카메라 월드 크기
        float worldH = cam.orthographicSize * 2f;
        float worldW = worldH * cam.aspect;

        // 스프라이트 원본 월드 크기 (bounds 대신 rect/PPU 사용 → 더 정확)
        float ppu = sr.sprite.pixelsPerUnit;
        Vector2 spriteWorld =
            new Vector2(sr.sprite.rect.width / ppu,
                        sr.sprite.rect.height / ppu);

        // 빈틈 없이 덮는 스케일
        float scale = Mathf.Max(worldW / spriteWorld.x, worldH / spriteWorld.y) * padding;

        transform.localScale = new Vector3(scale, scale, 1f);
        transform.position = new Vector3(0f, 0f, transform.position.z);
    }
}
