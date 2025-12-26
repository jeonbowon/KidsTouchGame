using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyGalaga : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseSpeed = 2f;
    public float speedPerStage = 0.3f;
    public float horizontalAmplitude = 1.2f;
    public float horizontalFrequency = 1f;

    [Tooltip("true 이면 Galaga 패턴으로 위에서 아래로 + 좌우 흔들리며 이동")]
    public bool useGalagaMove = true;

    private float moveSpeed;
    private float sinTime = 0f;

    [Header("HP")]
    public float hp = 1f;

    private int _maxHpInt = 1;
    private int _hpInt = 1;

    [Header("Hit Feedback (A안)")]
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

    [Header("Die SFX (단일 폭발음)")]
    [Tooltip("이 적이 죽을 때 재생할 폭발음(프리팹마다 다르게 지정 가능)")]
    [SerializeField] private AudioClip dieSfx;

    [SerializeField, Range(0f, 1f)]
    private float dieSfxVolume = 1f;

    [Header("사운드 스팸 방지(권장)")]
    [Tooltip("같은 프레임에 적이 여러 마리 터질 수 있어 폭발음이 과해집니다. 기본 3개까지만 허용.")]
    [SerializeField] private bool limitDieSfxPerFrame = true;

    [SerializeField, Range(1, 10)]
    private int maxDieSfxPerFrame = 3;

    private static int _lastDieSfxFrame = -1;
    private static int _dieSfxCountThisFrame = 0;

    [Header("Item Drop")]
    [Tooltip("적이 파괴될 때 떨어뜨릴 아이템 프리팹 (ScoreItem 등)")]
    [SerializeField] private GameObject itemPrefab;
    [Range(0f, 1f)]
    [SerializeField] private float itemDropChance = 1f;

    private bool isDying = false;
    private bool _reportedRemoveToGM = false;

    private SpriteRenderer _sr;
    private Color _origColor;
    private Coroutine _flashCo;

    private Transform _hpRoot;
    private SpriteRenderer _pip1;
    private SpriteRenderer _pip2;
    private Coroutine _hpHideCo;
    private static Sprite _whiteSprite;

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
    }

    private void Update()
    {
        if (!useGalagaMove) return;

        float dt = Time.deltaTime;
        sinTime += dt * horizontalFrequency;

        float xOffset = Mathf.Sin(sinTime) * horizontalAmplitude;
        transform.position += new Vector3(xOffset * dt, -moveSpeed * dt, 0);

        // 화면 밖으로 빠져 "그냥 Destroy" 되는 경로도 카운트 누수 없이 처리
        if (transform.position.y < -6f)
        {
            ReportRemovedToGM();
            Destroy(gameObject);
        }

        if (_hpRoot != null)
            PositionHpUI();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDying) return;
        if (!other.CompareTag("Bullet")) return;

        // 총알 제거
        Destroy(other.gameObject);

        // 데미지(정수)
        _hpInt -= 1;
        hp = _hpInt; // 외부 디버깅/표시용 동기화

        PlayHitFeedback();

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
        if (_damageSmokeInstance != null) return;

        _damageSmokeInstance = Instantiate(damageSmokePrefab, transform);
        if (applyPrefabLocalPosition)
            _damageSmokeInstance.transform.localPosition = damageSmokePrefab.transform.localPosition;
        else
            _damageSmokeInstance.transform.localPosition = Vector3.zero;
    }

    private bool CanPlayDieSfxThisFrame()
    {
        if (!limitDieSfxPerFrame) return true;

        int f = Time.frameCount;
        if (_lastDieSfxFrame != f)
        {
            _lastDieSfxFrame = f;
            _dieSfxCountThisFrame = 0;
        }

        if (_dieSfxCountThisFrame >= maxDieSfxPerFrame) return false;
        _dieSfxCountThisFrame++;
        return true;
    }

    private void PlayDieSfx()
    {
        if (dieSfx == null) return;
        if (!CanPlayDieSfxThisFrame()) return;

        if (SfxManager.I != null) SfxManager.I.PlayExplosion(dieSfx, dieSfxVolume);
        else AudioSource.PlayClipAtPoint(dieSfx, transform.position, dieSfxVolume);
    }

    private void Die()
    {
        if (isDying) return;
        isDying = true;

        if (shooter != null)
            shooter.StopAll();

        if (explosionPrefab != null)
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);

        // 단일 폭발음(프리팹별로 dieSfx만 바꿔서 사용)
        PlayDieSfx();

        if (itemPrefab != null && Random.value <= itemDropChance)
            Instantiate(itemPrefab, transform.position, Quaternion.identity);

        ReportRemovedToGM();
        Destroy(gameObject);
    }

    /// <summary>
    /// 무적 충돌 등으로 "점수/드랍/사운드 없이" 적을 제거.
    /// </summary>
    public void DespawnNoScore()
    {
        if (isDying) return;
        isDying = true;

        if (shooter != null)
            shooter.StopAll();

        ReportRemovedToGM();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        ReportRemovedToGM();
    }

    private void ReportRemovedToGM()
    {
        if (_reportedRemoveToGM) return;
        _reportedRemoveToGM = true;

        if (GameManager.I != null)
            GameManager.I.OnEnemyRemoved();
    }

    // ─────────────────────────────────────────────
    // Two-Pip HP UI

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
        sr.sortingOrder = (_sr != null) ? (_sr.sortingOrder + 10) : 10;
        return sr;
    }

    private void UpdateTwoPipUI()
    {
        if (_pip1 == null || _pip2 == null) return;

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
