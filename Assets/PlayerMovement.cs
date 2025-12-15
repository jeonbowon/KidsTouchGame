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

    [Header("터치 조작 영역(화면 하단)")]
    [Tooltip("화면 아래 몇 %를 조작 영역으로 사용할지 (0.3 = 아래 30%)")]
    [Range(0.1f, 0.6f)]
    public float touchControlHeightRatio = 0.35f;

    [Tooltip("터치 드래그 감도. 값이 작을수록 더 크게 문질러야 빨리 움직임")]
    public float touchSensitivity = 1.0f;

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

    // 터치/마우스 조작 입력 (가상 조이스틱 역할)
    private Vector2 touchMoveInput;   // -1~1 범위로 정규화된 방향 벡터
    private int activeFingerId = -1;  // 이동에 사용 중인 손가락 ID (없으면 -1)

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

        // 기본 방향은 위쪽
        lastMoveDir = Vector2.up;
    }

    void OnEnable() => RecalcBounds();

    void Update()
    {
        // 매 프레임 입력 초기화
        kbInput = Vector2.zero;
        touchMoveInput = Vector2.zero;
        currentInputDir = Vector2.zero;

        HandleKeyboardInput();
        HandlePointerInput();   // 터치/마우스 조작 입력 처리
    }

    void FixedUpdate()
    {
        if (rb == null || cam == null) return;

        Vector2 pos = rb.position;

        // 최종 입력 벡터 (키보드 + 터치)
        Vector2 input = Vector2.zero;

        // 모바일/터치가 있으면 우선 사용
        if (touchMoveInput.sqrMagnitude > 0f)
        {
            input = touchMoveInput;
        }
        else if (kbInput.sqrMagnitude > 0f)
        {
            input = kbInput;
        }

        if (input.sqrMagnitude > 0f)
        {
            // 정규화된 방향
            input = input.normalized;
            currentInputDir = input;
            lastMoveDir = input; // 마지막 이동 방향 갱신

            pos += input * moveSpeed * Time.fixedDeltaTime;
        }
        else
        {
            currentInputDir = Vector2.zero;
        }

        // 경계 안으로 제한
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

    // ───────── 터치/마우스 처리: 화면 하단 전체 조작 패드(한손 전용) ─────────
    void HandlePointerInput()
    {
        if (cam == null || rb == null) return;

        // 1) 터치 우선 (모바일)
        var ts = Touchscreen.current;
        if (ts != null)
        {
            HandleTouchscreen(ts);
            return; // 터치가 있으면 마우스는 무시
        }

        // 2) 마우스 (에디터/PC용 가상 터치 패드)
        var ms = Mouse.current;
        if (ms != null)
        {
            HandleMouse(ms);
        }
    }

    void HandleTouchscreen(Touchscreen ts)
    {
        float controlHeight = Screen.height * touchControlHeightRatio;

        // 이미 조작에 사용 중인 손가락이 있는 경우, 그 손가락을 계속 추적
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
                // 손을 떼거나 더 이상 추적할 수 없으면 해제
                activeFingerId = -1;
                touchMoveInput = Vector2.zero;
                return;
            }

            // 현재 프레임에서의 이동량(화면 좌표)
            Vector2 delta = activeTouch.delta.ReadValue();

            // 너무 작은 떨림은 무시
            if (delta.sqrMagnitude > 1f)
            {
                // 화면 좌표 delta를 방향으로 사용 (감도 보정)
                touchMoveInput = delta * (touchSensitivity / 50f); // 50은 실험적으로 조절용
            }
            else
            {
                touchMoveInput = Vector2.zero;
            }

            return;
        }

        // activeFingerId가 없으면, 새로 "조작용 손가락"을 할당
        foreach (var t in ts.touches)
        {
            if (t.press.wasPressedThisFrame)
            {
                Vector2 pos = t.position.ReadValue();

                // 화면 아래쪽에서 시작한 터치만 조작용으로 사용 (폭 전체 사용: 한손 전용)
                if (pos.y <= controlHeight)
                {
                    activeFingerId = t.touchId.ReadValue();
                    touchMoveInput = Vector2.zero;
                    break;
                }
            }
        }
    }

    void HandleMouse(Mouse ms)
    {
        float controlHeight = Screen.height * touchControlHeightRatio;

        if (ms.leftButton.isPressed)
        {
            Vector2 pos = ms.position.ReadValue();

            // 마우스도 화면 아래쪽 전체에서 조작 패드로 사용
            if (pos.y <= controlHeight)
            {
                Vector2 delta = ms.delta.ReadValue();

                if (delta.sqrMagnitude > 0.1f)
                {
                    touchMoveInput = delta * (touchSensitivity / 50f);
                }
                else
                {
                    touchMoveInput = Vector2.zero;
                }
            }
            else
            {
                touchMoveInput = Vector2.zero;
            }
        }
        else
        {
            touchMoveInput = Vector2.zero;
        }
    }
}
