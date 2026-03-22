using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DoubleScoreItem : MonoBehaviour
{
    [Header("�������� �ӵ�")]
    public float fallSpeed = 2.5f;

    [Header("�⺻ ���� (���� ������ 2��)")]
    public int baseScoreValue = 10;

    [Header("ȭ�� �Ʒ��� �������� ����� Y �Ѱ�")]
    public float destroyY = -6f;

    [Header("ȹ�� ����")]
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
            ReturnToPool();
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

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (PoolManager.I != null)
            PoolManager.I.Return(gameObject);
        else
            Destroy(gameObject);
    }
}
