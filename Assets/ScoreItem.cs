using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ScoreItem : MonoBehaviour
{
    [Header("떨어지는 속도")]
    public float fallSpeed = 2.5f;

    [Header("획득 시 올라가는 점수")]
    public int scoreValue = 10;

    [Header("화면 아래로 떨어져서 사라질 Y 한계")]
    public float destroyY = -6f;

    [Header("획득 사운드")]
    [SerializeField] private AudioClip collectSfx;   // 먹을 때 소리
    [SerializeField, Range(0f, 2f)]
    private float sfxVolume = 1f;   // 로컬 볼륨(아이템별 상대값, 최대 2배까지)

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    void Update()
    {
        transform.position += Vector3.down * fallSpeed * Time.deltaTime;

        if (transform.position.y < destroyY)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameManager.I != null)
            {
                GameManager.I.AddScore(scoreValue);
            }

            // ★ SfxManager 우선 사용
            if (collectSfx != null)
            {
                if (SfxManager.I != null)
                {
                    SfxManager.I.PlayItem(collectSfx, sfxVolume);
                }
                else
                {
                    // 혹시 SfxManager 없을 때 대비
                    AudioSource.PlayClipAtPoint(collectSfx, transform.position, Mathf.Clamp01(sfxVolume));
                }
            }

            Destroy(gameObject);
        }
    }
}
