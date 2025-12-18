using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyGalaga : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseSpeed = 2f;        // 스테이지 1 기본 속도
    public float speedPerStage = 0.3f;  // 스테이지 증가 시 속도 증가량
    public float horizontalAmplitude = 1.2f; // 좌우 진폭
    public float horizontalFrequency = 1f;   // 좌우 속도

    [Tooltip("true 이면 Galaga 패턴으로 위에서 아래로 + 좌우 흔들리며 이동")]
    public bool useGalagaMove = true;   // ★ 랜덤 이동 적/보너스 적에서는 false 로 설정

    private float moveSpeed;
    private float sinTime = 0f;

    [Header("HP")]
    public float hp = 1f;               // ★ 탱커는 프리팹에서 2로 설정

    // 내부적으로는 정수 HP로 운영 (A안: 2칸 표시는 정수 기반이 가장 깔끔)
    private int _maxHpInt = 1;
    private int _hpInt = 1;

    [Header("Hit Feedback (A안)")]
    [Tooltip("HP가 2 이상일 때만 두 칸 표시(■■/■□/□□)를 사용")]
    [SerializeField] private bool useTwoPipHpUI = true;

    [Tooltip("맞았을 때 HP 표시를 몇 초 보여줄지 (0이면 계속 표시)")]
    [SerializeField] private float hpUiShowTimeAfterHit = 1.0f;

    [Tooltip("피격 깜빡임 지속 시간")]
    [SerializeField] private float hitFlashDuration = 0.18f;

    [Tooltip("피격 깜빡임 횟수 (짝수 권장)")]
    [SerializeField] private int hitFlashCount = 4;

    [Tooltip("피격 깜빡임 색(잠깐 바뀌는 색)")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 1f, 1f, 1f);

    [Header("Damage Smoke FX")]
    [Tooltip("HP가 1이 되었을 때 나오는 연기 프리팹 (Smoke_Damage.prefab)")]
    [SerializeField] private GameObject damageSmokePrefab;

    [Tooltip("연기 프리팹을 적 자식으로 만들 때 localPosition을 강제로 덮어쓸지 여부")]
    [SerializeField] private bool applyPrefabLocalPosition = true;

    private GameObject _damageSmokeInstance;

    [Header("Shooting")]
    public EnemyShooter shooter;

    [Header("FX")]
    [SerializeField] private GameObject explosionPrefab;
    [SerializeField] private AudioClip dieSfx;

    [Header("Item Drop")]
    [Tooltip("적이 파괴될 때 떨어뜨릴 아이템 프리팹 (ScoreItem 등)")]
    [SerializeField] private GameObject itemPrefab;

    [Tooltip("아이템 드랍 확률 (1이면 항상 드랍)")]
    [Range(0f, 1f)]
    [SerializeField] private float itemDropChance = 1f;

    private bool isDying = false;

    // ─────────────────────────────────────────────
    // Hit Flash / HP UI refs
    private SpriteRenderer _sr;
    private Color _origColor;
    private Coroutine _flashCo;

    private Transform _hpRoot;
    private SpriteRenderer _pip1;
    private SpriteRenderer _pip2;
    private Coroutine _hpHideCo;
    private static Sprite _whiteSprite; // 1x1 흰색 스프라이트 (런타임 생성)

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;

        if (shooter == null)
            shooter = GetComponentInChildren<EnemyShooter>();

        // 본체 스프라이트(대표님 스샷 구조면 루트에 1개라 완벽)
        _sr = GetComponentInChildren<SpriteRenderer>(true);
        if (_sr != null) _origColor = _sr.color;

        // 정수 HP 초기화
        _maxHpInt = Mathf.Max(1, Mathf.RoundToInt(hp));
        _hpInt = _maxHpInt;

        // 두 칸 UI 준비(HP가 2 이상일 때만)
        if (useTwoPipHpUI && _maxHpInt >= 2)
        {
            EnsureWhiteSprite();
            BuildTwoPipUI();
            UpdateTwoPipUI();

            // 시작 시 숨김 (요구: “한발 맞고나서 표시”)
            SetHpUIVisible(false);
        }
    }

    private void Start()
    {
        int stage = (GameManager.I != null) ? GameManager.I.CurrentStage : 1;
        moveSpeed = baseSpeed + speedPerStage * (stage - 1);

        if (shooter != null)
            shooter.EnableAutoFire(true);

        sinTime = Random.Range(0f, 100f);

        // 시작 시 이미 HP가 1이면(디자인에 따라) 연기를 켜고 싶다면 아래를 켜도 됨
        // if (_hpInt == 1) SpawnDamageSmoke();
    }

    private void Update()
    {
        if (!useGalagaMove) return;

        float dt = Time.deltaTime;
        sinTime += dt * horizontalFrequency;

        float xOffset = Mathf.Sin(sinTime) * horizontalAmplitude;
        transform.position += new Vector3(xOffset * dt, -moveSpeed * dt, 0);

        if (transform.position.y < -6f)
            Destroy(gameObject);

        // HP UI 위치 보정
        if (_hpRoot != null)
            PositionHpUI();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDying) return;

        // ✅ 총알 태그가 Bullet 이어야 함
        if (!other.CompareTag("Bullet")) return;

        // 총알 제거
        Destroy(other.gameObject);

        // 데미지(정수)
        _hpInt -= 1;
        hp = _hpInt; // 외부 디버깅/표시용 동기화

        // 피격 연출(깜빡임 + HP표시)
        PlayHitFeedback();

        // ✅ 핵심: HP가 2→1 되는 최초 순간 연기 시작
        if (_hpInt == 1)
            SpawnDamageSmoke();

        if (_hpInt <= 0)
            Die();
    }

    private void PlayHitFeedback()
    {
        // 1) 깜빡임 (무적 아님, 연출만)
        if (_sr != null)
        {
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(Co_HitFlash());
        }

        // 2) 두 칸 HP 표시(■■ -> ■□)
        if (useTwoPipHpUI && _maxHpInt >= 2 && _hpRoot != null)
        {
            UpdateTwoPipUI();
            SetHpUIVisible(true);

            // 맞고 잠깐 보여주고 숨기기(0이면 계속 표시)
            if (hpUiShowTimeAfterHit > 0f)
            {
                if (_hpHideCo != null) StopCoroutine(_hpHideCo);
                _hpHideCo = StartCoroutine(Co_HideHpUIAfter(hpUiShowTimeAfterHit));
            }
        }
    }

    private IEnumerator Co_HitFlash()
    {
        float step = Mathf.Max(0.01f, hitFlashDuration / Mathf.Max(1, hitFlashCount));
        for (int i = 0; i < hitFlashCount; i++)
        {
            if (_sr == null) yield break;

            _sr.color = (i % 2 == 0) ? hitFlashColor : _origColor;
            yield return new WaitForSeconds(step);
        }

        if (_sr != null) _sr.color = _origColor;
        _flashCo = null;
    }

    private IEnumerator Co_HideHpUIAfter(float t)
    {
        yield return new WaitForSeconds(t);
        SetHpUIVisible(false);
        _hpHideCo = null;
    }

    private void SpawnDamageSmoke()
    {
        if (damageSmokePrefab == null) return;
        if (_damageSmokeInstance != null) return; // 중복 방지

        _damageSmokeInstance = Instantiate(damageSmokePrefab, transform);
        if (applyPrefabLocalPosition)
            _damageSmokeInstance.transform.localPosition = damageSmokePrefab.transform.localPosition;
        else
            _damageSmokeInstance.transform.localPosition = Vector3.zero;
    }

    private void Die()
    {
        if (isDying) return;
        isDying = true;

        if (shooter != null)
            shooter.StopAll();

        // 폭발 이펙트
        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // 폭발 사운드
        if (dieSfx != null)
        {
            if (SfxManager.I != null)
            {
                SfxManager.I.PlayExplosion(dieSfx, 1f);
            }
            else
            {
                AudioSource.PlayClipAtPoint(dieSfx, transform.position);
            }
        }

        // 아이템 드랍
        if (itemPrefab != null && Random.value <= itemDropChance)
            Instantiate(itemPrefab, transform.position, Quaternion.identity);

        // GameManager에 적 사망 보고
        if (GameManager.I != null)
            GameManager.I.OnEnemyKilled();

        Destroy(gameObject);
    }

    // ─────────────────────────────────────────────
    // Two-Pip HP UI (A안: ■■ / ■□ / □□)

    private void BuildTwoPipUI()
    {
        GameObject root = new GameObject("HP_UI_TwoPip");
        root.transform.SetParent(transform, false);
        _hpRoot = root.transform;

        _pip1 = CreatePip("Pip1", _hpRoot);
        _pip2 = CreatePip("Pip2", _hpRoot);

        const float size = 0.14f;
        const float gap = 0.05f;

        _pip1.transform.localScale = new Vector3(size, size, 1f);
        _pip2.transform.localScale = new Vector3(size, size, 1f);

        _pip1.transform.localPosition = new Vector3(-(size * 0.5f + gap * 0.5f), 0f, 0f);
        _pip2.transform.localPosition = new Vector3(+(size * 0.5f + gap * 0.5f), 0f, 0f);

        PositionHpUI();
    }

    private SpriteRenderer CreatePip(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _whiteSprite;
        sr.sortingLayerID = (_sr != null) ? _sr.sortingLayerID : 0;
        sr.sortingOrder = (_sr != null) ? (_sr.sortingOrder + 10) : 10; // 본체 위로
        return sr;
    }

    private void UpdateTwoPipUI()
    {
        if (_pip1 == null || _pip2 == null) return;

        // A안은 2칸만 표시(클램프)
        int filled = Mathf.Clamp(_hpInt, 0, 2);

        Color filledCol = new Color(1f, 0.25f, 0.25f, 1f);
        Color emptyCol = new Color(0.2f, 0.2f, 0.2f, 0.85f);

        _pip1.color = (filled >= 1) ? filledCol : emptyCol;
        _pip2.color = (filled >= 2) ? filledCol : emptyCol;
    }

    private void SetHpUIVisible(bool on)
    {
        if (_hpRoot == null) return;
        _hpRoot.gameObject.SetActive(on);
    }

    private void PositionHpUI()
    {
        float y = 0.5f;

        if (_sr != null)
        {
            var b = _sr.bounds;
            y = (b.size.y * 0.5f) + 0.18f;
        }
        else
        {
            var col = GetComponent<Collider2D>();
            if (col != null) y = col.bounds.size.y * 0.5f + 0.18f;
        }

        _hpRoot.localPosition = new Vector3(0f, y, 0f);
    }

    private static void EnsureWhiteSprite()
    {
        if (_whiteSprite != null) return;

        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
