using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동/조작")]
    public float moveSpeed = 9f;

    [Header("경계(자동)")]
    public Camera cam;
    public float xPadding = 0.3f;
    public float yPadding = 0.3f;

    private Rigidbody2D rb;
    private float minX, maxX, minY, maxY;

    // 드래그 상태
    private bool dragging = false;
    private Vector2 dragStartScreen;
    private Vector2 startPosWorld;

    // 키보드 입력(New Input System)
    private Vector2 kbInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        RecalcBounds();

        // 시작 위치를 화면 안으로 수습
        var p = rb.position;
        p.x = Mathf.Clamp(p.x, minX, maxX);
        p.y = Mathf.Clamp(p.y, minY, maxY);
        rb.position = p;
    }

    void OnEnable() => RecalcBounds();

    void Update()
    {
        HandlePointerNewInput();

        // 키보드 입력 (좌우 + 상하)
        kbInput = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) kbInput.x -= 1;
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) kbInput.x += 1;
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) kbInput.y += 1;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) kbInput.y -= 1;
        }
        kbInput = kbInput.normalized;
    }

    void FixedUpdate()
    {
        if (rb == null || cam == null) return;

        Vector2 pos = rb.position;

        if (kbInput.sqrMagnitude > 0f)
            pos += kbInput * moveSpeed * Time.fixedDeltaTime;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        rb.MovePosition(pos);
    }

    void RecalcBounds()
    {
        if (cam == null) return;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        minX = -halfWidth + xPadding;
        maxX = halfWidth - xPadding;
        minY = -halfHeight + yPadding;
        maxY = halfHeight - yPadding;
    }

    // ───────── New Input System 기반 포인터(마우스/터치) 처리 ─────────
    void HandlePointerNewInput()
    {
        // 공통: 픽셀→월드 스케일
        float worldPerPixelY = (cam.orthographicSize * 2f) / Screen.height;
        float worldPerPixelX = worldPerPixelY * cam.aspect;

        // 터치 우선
        var ts = Touchscreen.current;
        if (ts != null)
        {
            var pt = ts.primaryTouch;
            if (pt.press.isPressed)
            {
                if (pt.press.wasPressedThisFrame)
                {
                    dragging = true;
                    dragStartScreen = pt.position.ReadValue();
                    startPosWorld = rb.position;
                }
                else if (pt.press.wasReleasedThisFrame)
                {
                    dragging = false;
                }

                if (dragging)
                {
                    Vector2 cur = pt.position.ReadValue();
                    Vector2 delta = cur - dragStartScreen;
                    Vector2 target = startPosWorld + new Vector2(delta.x * worldPerPixelX, delta.y * worldPerPixelY);

                    target.x = Mathf.Clamp(target.x, minX, maxX);
                    target.y = Mathf.Clamp(target.y, minY, maxY);

                    rb.MovePosition(target);
                }
                return; // 터치가 활성중이면 마우스 무시
            }
        }

        // 마우스
        var ms = Mouse.current;
        if (ms != null)
        {
            if (ms.leftButton.wasPressedThisFrame)
            {
                dragging = true;
                dragStartScreen = ms.position.ReadValue();
                startPosWorld = rb.position;
            }
            else if (ms.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
            }

            if (dragging && ms.leftButton.isPressed)
            {
                Vector2 cur = ms.position.ReadValue();
                Vector2 delta = cur - dragStartScreen;
                Vector2 target = startPosWorld + new Vector2(delta.x * worldPerPixelX, delta.y * worldPerPixelY);

                target.x = Mathf.Clamp(target.x, minX, maxX);
                target.y = Mathf.Clamp(target.y, minY, maxY);

                rb.MovePosition(target);
            }
        }
    }
}
