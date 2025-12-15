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

    void Awake()
    {
        shooter = GetComponent<PlayerShoot>();

        // 시작할 때는 Twin 비활성
        if (twinShipVisual != null)
            twinShipVisual.SetActive(false);

        if (shooter != null)
            shooter.twinMode = false;
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
}
