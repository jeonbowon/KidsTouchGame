using UnityEngine;

public class DestroyAfterAnimation : MonoBehaviour
{
    private Vector3 _originalScale;

    void Awake()
    {
        _originalScale = transform.localScale;
    }

    void OnEnable()
    {
        // 풀 재사용 시 스케일 초기화 (외부에서 scaleFactor 적용 전 상태로 복원)
        transform.localScale = _originalScale;

        float delay = 0.4f;
        var anim = GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            var clips = anim.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
                delay = clips[0].length;
        }

        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), delay);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(ReturnToPool));
    }

    private void ReturnToPool()
    {
        if (PoolManager.I != null)
            PoolManager.I.Return(gameObject);
        else
            Destroy(gameObject);
    }
}
