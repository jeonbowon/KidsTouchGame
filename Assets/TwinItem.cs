using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TwinItem : MonoBehaviour
{
    [Header("낙하 설정")]
    [Tooltip("아래로 떨어지는 속도(유닛/초)")]
    public float fallSpeed = 1.5f;

    [Tooltip("이 Y좌표보다 더 내려가면 자동으로 삭제")]
    public float destroyY = -6f;

    [Header("Twin 모드 설정")]
    [Tooltip("Twin 모드 지속 시간(초). 0 이하이면 PlayerPowerUp의 기본값 사용")]
    public float twinDuration = 8f;

    [Tooltip("먹었을 때 나올 이펙트(없으면 비워둠)")]
    public GameObject pickupEffect;

    [Header("획득 사운드")]
    [SerializeField] private AudioClip pickupSfx;     // 아이템을 먹을 때 나는 소리
    [SerializeField, Range(0f, 2f)]
    private float sfxVolume = 1f;                    // 로컬 볼륨(최대 2배)

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Update()
    {
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        if (transform.position.y <= destroyY)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 1. Player 태그만 받는다
        if (!other.CompareTag("Player"))
            return;

        // 2. Player에서 PlayerPowerUp 찾기
        var power = other.GetComponent<PlayerPowerUp>();
        if (power != null)
        {
            if (twinDuration > 0f)
                power.ActivateTwin(twinDuration);
            else
                power.ActivateTwin();
        }

        // 3. 사운드 재생 (ScoreItem과 동일한 방식)
        if (pickupSfx != null)
        {
            if (SfxManager.I != null)
            {
                SfxManager.I.PlayItem(pickupSfx, sfxVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(
                    pickupSfx,
                    transform.position,
                    Mathf.Clamp01(sfxVolume)
                );
            }
        }

        // 4. 이펙트 생성
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        // 5. 자기 자신 제거
        Destroy(gameObject);
    }
}
