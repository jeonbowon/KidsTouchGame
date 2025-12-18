using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DoubleScoreItem : MonoBehaviour
{
    [Header("떨어지는 속도")]
    public float fallSpeed = 2.5f;

    [Header("기본 점수 (실제 지급은 2배)")]
    public int baseScoreValue = 10;

    [Header("화면 아래로 떨어져서 사라질 Y 한계")]
    public float destroyY = -6f;

    [Header("획득 사운드")]
    [SerializeField] private AudioClip collectSfx;
    [SerializeField, Range(0f, 2f)]
    private float sfxVolume = 1f;

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
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameManager.I != null)
        {
            int reward = baseScoreValue * 2;
            GameManager.I.AddScore(reward);
        }

        if (collectSfx != null)
        {
            if (SfxManager.I != null)
                SfxManager.I.PlayItem(collectSfx, sfxVolume);
            else
                AudioSource.PlayClipAtPoint(collectSfx, transform.position, Mathf.Clamp01(sfxVolume));
        }

        Destroy(gameObject);
    }
}
