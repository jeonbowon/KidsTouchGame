using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동/조작 (키보드)")]
    [Tooltip("키보드 이동 속도 (초당 월드 단위)")]
    public float moveSpeed = 9f;

    [Header("드래그 이동 설정")]
    [Tooltip("드래그 델타 배율 (1 = 손가락과 동일 속도로 이동)")]
    public float dragMultiplier = 1f;

    [Header("경계(자동)")]
    public Camera cam;
    public float xPadding = 0.3f;
    public float yPadding = 0.3f;

    [Header("디버그")]
    [Tooltip("현재 이동 방향 (정규화)")]
    public Vector2 currentInputDir;

    /// <summary>마지막으로 움직였던 방향 (총알 조준 방향으로 사용)</summary>
    public Vector2 LastMoveDirection => _lastMoveDir;

    // ── 내부 상태 ────────────────────────────────────────────
    private Rigidbody2D _rb;
    private float _minX, _maxX, _minY, _maxY;

    // 키보드
    private Vector2 _kbInput;

    // 터치 / 마우스 드래그 델타
    private int     _activeFingerId = -1;
    private Vector2 _dragDelta;

    // 마지막 이동 방향 (총알 기준)
    private Vector2 _lastMoveDir = Vector2.up;

    // ── Unity 생명주기 ────────────────────────────────────
    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale   = 0f;
        _rb.freezeRotation = true;
        _rb.interpolation  = RigidbodyInterpolation2D.Interpolate;

        if (cam == null) cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();

        RecalcBounds();

        var p = _rb.position;
        p.x = Mathf.Clamp(p.x, _minX, _maxX);
        p.y = Mathf.Clamp(p.y, _minY, _maxY);
        _rb.position = p;

        _lastMoveDir = Vector2.up;
    }

    void OnEnable() => RecalcBounds();

    void Update()
    {
        _kbInput        = Vector2.zero;
        _dragDelta      = Vector2.zero;
        currentInputDir = Vector2.zero;

        HandleKeyboardInput();
        HandlePointerInput();
    }

    void FixedUpdate()
    {
        if (_rb == null || cam == null) return;

        Vector2 pos = _rb.position;

        // ── 1) 키보드 (우선) ──────────────────────────────
        if (_kbInput.sqrMagnitude > 0f)
        {
            Vector2 dir = _kbInput.normalized;
            currentInputDir = dir;
            _lastMoveDir    = dir;

            pos += dir * moveSpeed * Time.fixedDeltaTime;
            pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
            pos.y = Mathf.Clamp(pos.y, _minY, _maxY);
            _rb.MovePosition(pos);
            return;
        }

        // ── 2) 드래그 델타 ────────────────────────────────
        if (_dragDelta.sqrMagnitude > 0f)
        {
            Vector2 worldDelta = ScreenDeltaToWorld(_dragDelta) * dragMultiplier;

            pos += worldDelta;
            pos.x = Mathf.Clamp(pos.x, _minX, _maxX);
            pos.y = Mathf.Clamp(pos.y, _minY, _maxY);

            if (worldDelta.sqrMagnitude > 0.0001f)
            {
                currentInputDir = worldDelta.normalized;
                _lastMoveDir    = currentInputDir;
            }

            _rb.MovePosition(pos);
        }
    }

    // ── 경계 계산 ─────────────────────────────────────────
    void RecalcBounds()
    {
        if (cam == null) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        _minX = -halfW + xPadding;
        _maxX =  halfW - xPadding;
        _minY = -halfH + yPadding;
        _maxY =  halfH - yPadding;
    }

    // ── 키보드 ────────────────────────────────────────────
    void HandleKeyboardInput()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.leftArrowKey.isPressed  || Keyboard.current.aKey.isPressed)  _kbInput.x -= 1;
        if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)  _kbInput.x += 1;
        if (Keyboard.current.upArrowKey.isPressed    || Keyboard.current.wKey.isPressed)  _kbInput.y += 1;
        if (Keyboard.current.downArrowKey.isPressed  || Keyboard.current.sKey.isPressed)  _kbInput.y -= 1;

        if (_kbInput.sqrMagnitude > 1f)
            _kbInput = _kbInput.normalized;
    }

    // ── 터치 / 마우스 ─────────────────────────────────────
    void HandlePointerInput()
    {
        if (cam == null || _rb == null) return;

        var ts = Touchscreen.current;
        if (ts != null) { HandleTouchscreen(ts); return; }

        var ms = Mouse.current;
        if (ms != null) HandleMouse(ms);
    }

    void HandleTouchscreen(Touchscreen ts)
    {
        // 이미 잡은 손가락 추적
        if (_activeFingerId != -1)
        {
            TouchControl active = null;
            foreach (var t in ts.touches)
            {
                if (t.touchId.ReadValue() == _activeFingerId)
                {
                    active = t;
                    break;
                }
            }

            if (active == null || !active.press.isPressed)
            {
                _activeFingerId = -1;
                return;
            }

            _dragDelta = active.delta.ReadValue();
            return;
        }

        // 새 터치 잡기 (첫 프레임 델타는 무시)
        foreach (var t in ts.touches)
        {
            if (t.press.wasPressedThisFrame)
            {
                _activeFingerId = t.touchId.ReadValue();
                break;
            }
        }
    }

    void HandleMouse(Mouse ms)
    {
        if (ms.leftButton.isPressed)
            _dragDelta = ms.delta.ReadValue();
    }

    // ── 스크린 픽셀 델타 → 월드 단위 변환 ─────────────────
    Vector2 ScreenDeltaToWorld(Vector2 screenDelta)
    {
        float worldH        = cam.orthographicSize * 2f;
        float pixelsPerUnit = Screen.height / worldH;
        return screenDelta / pixelsPerUnit;
    }
}
