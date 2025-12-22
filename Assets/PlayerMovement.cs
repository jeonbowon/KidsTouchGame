using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동/조작")]
    [Tooltip("초당 이동 속도(월드 좌표 기준)")]
    public float moveSpeed = 9f;

    [Header("경계(자동)")]
    public Camera cam;
    public float xPadding = 0.3f;
    public float yPadding = 0.3f;

    [Header("전체 화면 드래그(델타)")]
    [Tooltip("드래그 델타를 이동 속도로 바꾸는 스케일(클수록 더 민감). 1.0~2.0 권장")]
    public float touchSensitivity = 1.2f;

    [Tooltip("아주 미세한 손떨림/노이즈 컷 (픽셀 단위)")]
    public float deadZonePixels = 1.0f;

    [Header("디버그")]
    [Tooltip("현재 프레임에서 쓰인 최종 입력 방향(정규화)")]
    public Vector2 currentInputDir;

    /// <summary>
    /// 마지막으로 움직였던 방향(정규화). 총알 방향으로 사용.
    /// 입력이 없으면 이전 값을 유지. 초기값은 위쪽.
    /// </summary>
    public Vector2 LastMoveDirection => lastMoveDir;

    private Rigidbody2D rb;
    private float minX, maxX, minY, maxY;

    // 키보드 입력(New Input System)
    private Vector2 kbInput;

    // 터치/마우스 드래그 델타 입력
    private Vector2 pointerDeltaInput; // 화면 delta를 월드 이동 벡터로 변환한 값
    private int activeFingerId = -1;   // 터치 추적용

    // 마지막 이동 방향 (총알 방향 기준)
    private Vector2 lastMoveDir = Vector2.up;

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

        lastMoveDir = Vector2.up;
    }

    void OnEnable() => RecalcBounds();

    void Update()
    {
        kbInput = Vector2.zero;
        pointerDeltaInput = Vector2.zero;
        currentInputDir = Vector2.zero;

        HandleKeyboardInput();
        HandlePointerInput();   // ✅ 화면 전체에서 드래그 입력
    }

    void FixedUpdate()
    {
        if (rb == null || cam == null) return;

        Vector2 pos = rb.position;

        // 1) 터치/마우스 드래그 우선
        Vector2 move = Vector2.zero;

        if (pointerDeltaInput.sqrMagnitude > 0f)
        {
            move = pointerDeltaInput;
        }
        else if (kbInput.sqrMagnitude > 0f)
        {
            // 키보드는 기존처럼 "방향 + moveSpeed"
            var dir = kbInput.normalized;
            currentInputDir = dir;
            lastMoveDir = dir;

            pos += dir * moveSpeed * Time.fixedDeltaTime;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            rb.MovePosition(pos);
            return;
        }

        // 2) 드래그 델타 방식: "델타 → 월드 이동량" 이므로 moveSpeed와 별개로 처리
        if (move.sqrMagnitude > 0f)
        {
            Vector2 dir = move.normalized;
            currentInputDir = dir;
            lastMoveDir = dir;

            pos += move; // move는 이미 월드 이동량
        }
        else
        {
            currentInputDir = Vector2.zero;
        }

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

    // ───────── 키보드 입력 처리 (PC/에디터용) ─────────
    void HandleKeyboardInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) kbInput.x -= 1;
        if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) kbInput.x += 1;
        if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) kbInput.y += 1;
        if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) kbInput.y -= 1;

        if (kbInput.sqrMagnitude > 1f)
            kbInput = kbInput.normalized;
    }

    // ───────── 터치/마우스 처리: ✅ 화면 전체 드래그 델타 이동 ─────────
    void HandlePointerInput()
    {
        if (cam == null || rb == null) return;

        // 1) 터치 우선
        var ts = Touchscreen.current;
        if (ts != null)
        {
            HandleTouchscreen(ts);
            return;
        }

        // 2) 마우스 (에디터/PC)
        var ms = Mouse.current;
        if (ms != null)
        {
            HandleMouse(ms);
        }
    }

    void HandleTouchscreen(Touchscreen ts)
    {
        // 이미 잡은 손가락이 있으면 계속 추적
        if (activeFingerId != -1)
        {
            TouchControl activeTouch = null;
            foreach (var t in ts.touches)
            {
                if (t.touchId.ReadValue() == activeFingerId)
                {
                    activeTouch = t;
                    break;
                }
            }

            if (activeTouch == null || !activeTouch.press.isPressed)
            {
                activeFingerId = -1;
                pointerDeltaInput = Vector2.zero;
                return;
            }

            Vector2 deltaPx = activeTouch.delta.ReadValue();

            if (deltaPx.sqrMagnitude <= deadZonePixels * deadZonePixels)
            {
                pointerDeltaInput = Vector2.zero;
                return;
            }

            // 픽셀 델타 → 월드 이동량으로 변환
            pointerDeltaInput = PixelDeltaToWorldMove(deltaPx);
            return;
        }

        // 새 터치 시작이면 그 손가락을 조작 손가락으로 할당 (✅ 화면 어디든)
        foreach (var t in ts.touches)
        {
            if (t.press.wasPressedThisFrame)
            {
                activeFingerId = t.touchId.ReadValue();
                pointerDeltaInput = Vector2.zero;
                break;
            }
        }
    }

    void HandleMouse(Mouse ms)
    {
        if (!ms.leftButton.isPressed)
        {
            pointerDeltaInput = Vector2.zero;
            return;
        }

        Vector2 deltaPx = ms.delta.ReadValue();
        if (deltaPx.sqrMagnitude <= deadZonePixels * deadZonePixels)
        {
            pointerDeltaInput = Vector2.zero;
            return;
        }

        pointerDeltaInput = PixelDeltaToWorldMove(deltaPx);
    }

    Vector2 PixelDeltaToWorldMove(Vector2 deltaPx)
    {
        // 화면 픽셀 델타를, 현재 카메라 뷰 기준 월드 이동량으로 바꿉니다.
        // 기준: 화면 전체 폭/높이가 월드에서 얼마인지
        float worldHeight = cam.orthographicSize * 2f;
        float worldWidth = worldHeight * cam.aspect;

        float dxWorld = (deltaPx.x / Screen.width) * worldWidth;
        float dyWorld = (deltaPx.y / Screen.height) * worldHeight;

        // 감도 적용 + FixedUpdate에서 바로 더할 수 있게 fixedDeltaTime도 곱해 "프레임 독립" 처리
        Vector2 worldMove = new Vector2(dxWorld, dyWorld) * touchSensitivity;

        // 드래그 델타 방식은 손가락 이동 속도에 비례하므로,
        // 너무 빠르게 느껴지면 touchSensitivity만 줄이면 됩니다.
        return worldMove;
    }
}
