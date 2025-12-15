using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BulletSpeedBonusItem : MonoBehaviour
{
    [Header("낙하 설정")]
    [Tooltip("아래로 떨어지는 속도(유닛/초)")]
    public float fallSpeed = 1.5f;

    [Tooltip("이 Y좌표보다 더 내려가면 자동으로 삭제")]
    public float destroyY = -6f;

    [Header("보너스 설정")]
    [Tooltip("총알 속도 배율 (예: 2면 2배 속도)")]
    public float speedMultiplier = 2f;

    [Tooltip("보너스 유지 시간(초)")]
    public float bonusDuration = 5f;

    [Header("획득 사운드")]
    [SerializeField] private AudioClip pickupSfx;   // 아이템 획득 효과음
    [SerializeField, Range(0f, 2f)]
    private float sfxVolume = 1f;                  // 로컬 볼륨(상대값, 최대 2배)

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void Update()
    {
        // TwinItem 과 동일한 낙하 로직
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        if (transform.position.y <= destroyY)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Player 만 반응
        if (!other.CompareTag("Player"))
            return;

        // PlayerShoot 찾아서 보너스 적용
        var shooter = other.GetComponent<PlayerShoot>();
        if (shooter != null)
        {
            shooter.ActivateBulletSpeedBonus(speedMultiplier, bonusDuration);

            // "SPEED UP!" 메시지 표시
            if (GameManager.I != null)
                GameManager.I.ShowSpeedUpMessage(1.0f);
        }

        // ★ 사운드 재생 (ScoreItem과 동일한 방식)
        if (pickupSfx != null)
        {
            if (SfxManager.I != null)
            {
                SfxManager.I.PlayItem(pickupSfx, sfxVolume);
            }
            else
            {
                // SfxManager 없을 때 대비
                AudioSource.PlayClipAtPoint(
                    pickupSfx,
                    transform.position,
                    Mathf.Clamp01(sfxVolume)
                );
            }
        }

        // 아이템은 사용 후 제거
        Destroy(gameObject);
    }
}
