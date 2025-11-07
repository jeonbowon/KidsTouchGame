using UnityEngine;

/// <summary>
/// ����� ���� ȸ�� + �̵� ��Ʈ�ѷ�
/// - TwinStick: ���� �̵�, ������ ����/ȸ��
/// - OneStick : �巡�� �������� �̵��ϸ� �� �������� ȸ��
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMobileController : MonoBehaviour
{
    public enum ControlMode { TwinStick, OneStick }
    [Header("Mode")]
    public ControlMode controlMode = ControlMode.TwinStick;

    [Header("Move")]
    public float moveSpeed = 6f;          // �ִ� �̵� �ӵ�
    public float moveDeadZone = 0.08f;    // �̵� ��ƽ ������(0~1, ȭ�� ����)
    public float moveSmooth = 12f;        // �̵� ����(����/���� �ε巴��)

    [Header("Rotate")]
    public float rotateSmooth = 18f;      // ȸ�� ����(���� Ŭ���� ������ ȸ��)
    public float aimDeadZone = 0.08f;     // ����(������) ������

    [Header("Auto Fire (optional)")]
    public bool autoFireOnAim = true;     // ������ ��ƽ �����̸� �ڵ� �߻�
    public float autoFireThreshold = 0.2f;
    public float autoFireInterval = 0.25f;
    public Transform muzzle;              // �ѱ�(�ʼ� �ƴ�, �ڵ��߻� �� �ʿ�)
    public GameObject bulletPrefab;
    public float bulletSpeed = 10f;

    Camera cam;
    Rigidbody2D rb;
    Vector2 targetVel;
    float lastFireTime = -999f;

    // ���� ����
    struct StickState
    {
        public int fingerId;
        public bool active;
        public Vector2 startPos;  // ȭ�� ��ǥ(px)
        public Vector2 delta;     // ȭ�� ��ǥ ����(px)
        public Vector2 norm;      // ����ȭ �巡�� ����(0~1 ������)
        public float mag01;       // 0~1 ũ��
        public void Reset() { active = false; fingerId = -1; startPos = delta = norm = Vector2.zero; mag01 = 0f; }
    }

    StickState left, right;

    void Awake()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        left.Reset(); right.Reset();
    }

    void Update()
    {
#if UNITY_EDITOR
        // �����Ϳ����� �׽�Ʈ ������ ���콺 ����(����). 
        // ����� ��⿡���� Touch ó���� �۵��մϴ�.
        HandleMouseEmulation();
#endif
        HandleTouches();

        // �̵� ���� ���
        Vector2 moveDir = Vector2.zero;
        if (controlMode == ControlMode.TwinStick)
        {
            if (left.active && left.mag01 > moveDeadZone) moveDir = left.norm;
        }
        else // OneStick
        {
            if (left.active && left.mag01 > moveDeadZone) moveDir = left.norm;
        }

        // ��ǥ �ӵ� = ���� * �ӵ� * ����
        Vector2 desiredVel = moveDir * moveSpeed * Mathf.Clamp01(left.mag01);
        targetVel = Vector2.Lerp(targetVel, desiredVel, 1f - Mathf.Exp(-moveSmooth * Time.deltaTime));

        // ȸ�� Ÿ�� ���� ���
        Vector2 aimDir = Vector2.zero;
        if (controlMode == ControlMode.TwinStick)
        {
            if (right.active && right.mag01 > aimDeadZone) aimDir = right.norm;
            else if (moveDir.sqrMagnitude > 0.0001f) aimDir = moveDir; // ���� ������ �̵����� �ٶ󺸱�(����)
        }
        else // OneStick
        {
            if (left.active && left.mag01 > aimDeadZone) aimDir = left.norm;
        }

        // ȸ�� ����: transform.up �� aimDir �� ������
        if (aimDir.sqrMagnitude > 0.0001f)
        {
            float targetAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg - 90f; // up ����
            float current = transform.eulerAngles.z;
            // �ε巯�� ȸ��
            float newAngle = Mathf.LerpAngle(current, targetAngle, 1f - Mathf.Exp(-rotateSmooth * Time.deltaTime));
            transform.rotation = Quaternion.Euler(0, 0, newAngle);

            // �ڵ� �߻�
            if (autoFireOnAim && right.mag01 > autoFireThreshold)
                TryAutoFire();
        }
    }

    void FixedUpdate()
    {
        rb.linearVelocity = targetVel;
    }

    // ---- �Է� ó�� ----
    void HandleTouches()
    {
        // �հ����� �پ��ٸ� ���� ��ƽ ����
        if (!AnyTouchHasId(left.fingerId)) left.Reset();
        if (!AnyTouchHasId(right.fingerId)) right.Reset();

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            Vector2 p = t.position; // ȭ�� ��ǥ(px)

            // ���� �� ��� ��ƽ�� �Ҵ����� ����
            if (t.phase == TouchPhase.Began)
            {
                if (controlMode == ControlMode.TwinStick)
                {
                    // ���� ���� �� �̵�, ������ ���� �� ����
                    if (p.x < Screen.width * 0.5f && !left.active) BeginStick(ref left, t);
                    else if (!right.active) BeginStick(ref right, t);
                }
                else // OneStick
                {
                    if (!left.active) BeginStick(ref left, t);
                }
            }

            // ���� ���̸� ������Ʈ
            if (left.active && t.fingerId == left.fingerId)
                UpdateStick(ref left, t);
            if (right.active && t.fingerId == right.fingerId)
                UpdateStick(ref right, t);

            // �հ��� ��
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (left.active && t.fingerId == left.fingerId) left.Reset();
                if (right.active && t.fingerId == right.fingerId) right.Reset();
            }
        }
    }

    void BeginStick(ref StickState s, Touch t)
    {
        s.active = true;
        s.fingerId = t.fingerId;
        s.startPos = t.position;
        s.delta = Vector2.zero; s.norm = Vector2.zero; s.mag01 = 0f;
    }

    void UpdateStick(ref StickState s, Touch t)
    {
        s.delta = t.position - s.startPos;            // px ����
        // ȭ�� ũ�⿡ ����� ����ȭ(ª�� �� ����)
        float scale = Mathf.Min(Screen.width, Screen.height);
        Vector2 v = s.delta / (scale * 0.3f);         // 0.3: ��ƽ �ִ� �ݰ� ������
        s.mag01 = Mathf.Clamp01(v.magnitude);
        s.norm = (v.sqrMagnitude > 0.0001f) ? v.normalized : Vector2.zero;
    }

    bool AnyTouchHasId(int fingerId)
    {
        if (fingerId < 0) return false;
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).fingerId == fingerId) return true;
        return false;
    }

#if UNITY_EDITOR
    // ���콺�� ����: ��Ŭ��=�޽�ƽ, ��Ŭ��=������ƽ
    void HandleMouseEmulation()
    {
        if (controlMode == ControlMode.TwinStick)
        {
            // �޽�ƽ
            if (Input.GetMouseButtonDown(0)) { left.active = true; left.fingerId = 0; left.startPos = (Vector2)Input.mousePosition; }
            if (Input.GetMouseButton(0)) { left.delta = (Vector2)Input.mousePosition - left.startPos; EmuStick(ref left); }
            if (Input.GetMouseButtonUp(0)) { left.Reset(); }

            // ������ƽ
            if (Input.GetMouseButtonDown(1)) { right.active = true; right.fingerId = 1; right.startPos = (Vector2)Input.mousePosition; }
            if (Input.GetMouseButton(1)) { right.delta = (Vector2)Input.mousePosition - right.startPos; EmuStick(ref right); }
            if (Input.GetMouseButtonUp(1)) { right.Reset(); }
        }
        else
        {
            if (Input.GetMouseButtonDown(0)) { left.active = true; left.fingerId = 0; left.startPos = (Vector2)Input.mousePosition; }
            if (Input.GetMouseButton(0)) { left.delta = (Vector2)Input.mousePosition - left.startPos; EmuStick(ref left); }
            if (Input.GetMouseButtonUp(0)) { left.Reset(); }
        }
    }
    void EmuStick(ref StickState s)
    {
        float scale = Mathf.Min(Screen.width, Screen.height);
        Vector2 v = s.delta / (scale * 0.3f);
        s.mag01 = Mathf.Clamp01(v.magnitude);
        s.norm = (v.sqrMagnitude > 0.0001f) ? v.normalized : Vector2.zero;
    }
#endif

    // ---- �ڵ� �߻� ----
    void TryAutoFire()
    {
        if (!bulletPrefab || !muzzle) return;
        if (Time.time - lastFireTime < autoFireInterval) return;
        lastFireTime = Time.time;

        var b = Instantiate(bulletPrefab, muzzle.position, muzzle.rotation);
        var rb2 = b.GetComponent<Rigidbody2D>();
        if (rb2) rb2.linearVelocity = muzzle.up * bulletSpeed;
    }
}
