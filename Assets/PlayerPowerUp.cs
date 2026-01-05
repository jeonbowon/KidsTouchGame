using UnityEngine;

[RequireComponent(typeof(PlayerShoot))]
public class PlayerPowerUp : MonoBehaviour
{
    [Header("Twin 모드 기본 설정")]
    [Tooltip("Twin 모드 기본 지속 시간(초). 0 이하이면 무제한 유지.")]
    public float defaultTwinDuration = 8f;

    [Tooltip("Twin 모드일 때 보여줄 두 번째 기체(스프라이트)")]
    public GameObject twinShipVisual;   // Player 자식: ShipTwin

    private PlayerShoot shooter;

    private bool twinActive = false;
    private float twinTimer = 0f;    // 남은 시간 (0 이하면 무제한)

    // 추가: 렌더러 캐시
    private SpriteRenderer _mainShipRenderer;
    private SpriteRenderer _twinShipRenderer;

    void Awake()
    {
        shooter = GetComponent<PlayerShoot>();

        // 시작할 때는 Twin 비활성
        if (twinShipVisual != null)
            twinShipVisual.SetActive(false);

        if (shooter != null)
            shooter.twinMode = false;

        CacheRenderers();
        SyncTwinSpriteFromMain(); // (초기엔 꺼져있지만, 혹시 모를 상황 대비)
    }

    void Update()
    {
        if (!twinActive)
            return;

        if (twinTimer > 0f)
        {
            twinTimer -= Time.deltaTime;
            if (twinTimer <= 0f)
                DeactivateTwin();
        }
    }

    /// <summary>
    /// Twin 모드 활성화. duration > 0 이면 해당 시간 동안 유지, duration <= 0 이면 기본값/무제한.
    /// </summary>
    public void ActivateTwin(float duration = -1f)
    {
        twinActive = true;

        if (shooter != null)
            shooter.twinMode = true;

        // 핵심: 켜기 전에 현재 장착 스킨(현재 Player 스프라이트)을 Twin에 복사
        CacheRenderers();
        SyncTwinSpriteFromMain();

        if (twinShipVisual != null)
            twinShipVisual.SetActive(true);

        // 지속 시간 설정
        if (duration > 0f)
            twinTimer = duration;
        else if (defaultTwinDuration > 0f)
            twinTimer = defaultTwinDuration;
        else
            twinTimer = 0f; // 무제한
    }

    /// <summary>
    /// Twin 모드 비활성화.
    /// </summary>
    public void DeactivateTwin()
    {
        twinActive = false;

        if (shooter != null)
            shooter.twinMode = false;

        if (twinShipVisual != null)
            twinShipVisual.SetActive(false);

        twinTimer = 0f;
    }

    // -------------------------
    // 내부 유틸
    // -------------------------

    private void CacheRenderers()
    {
        // Twin 쪽 렌더러
        if (_twinShipRenderer == null && twinShipVisual != null)
            _twinShipRenderer = twinShipVisual.GetComponentInChildren<SpriteRenderer>(true);

        // Main(현재 플레이어) 렌더러
        if (_mainShipRenderer == null)
            _mainShipRenderer = FindMainShipRendererExcludingTwin();
    }

    private SpriteRenderer FindMainShipRendererExcludingTwin()
    {
        var renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0) return null;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            // Twin 오브젝트 하위 렌더러는 제외
            if (twinShipVisual != null && r.transform.IsChildOf(twinShipVisual.transform))
                continue;

            // 첫 번째로 잡히는 "Twin이 아닌" SpriteRenderer를 메인으로 사용
            return r;
        }

        return null;
    }

    private void SyncTwinSpriteFromMain()
    {
        if (_mainShipRenderer == null || _twinShipRenderer == null)
            return;

        // 현재 장착된 스킨 스프라이트를 Twin에 그대로 복사
        _twinShipRenderer.sprite = _mainShipRenderer.sprite;

        // (선택) 외형 일치를 더 원하면 아래도 같이 복사
        _twinShipRenderer.color = _mainShipRenderer.color;
        _twinShipRenderer.flipX = _mainShipRenderer.flipX;
        _twinShipRenderer.flipY = _mainShipRenderer.flipY;

        // 정렬 레이어가 꼬이면 시각적으로 이상해질 수 있으니 맞춰줌
        _twinShipRenderer.sortingLayerID = _mainShipRenderer.sortingLayerID;
        _twinShipRenderer.sortingOrder = _mainShipRenderer.sortingOrder;
    }
}
