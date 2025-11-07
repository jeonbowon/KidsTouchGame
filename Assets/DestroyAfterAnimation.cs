using UnityEngine;

public class DestroyAfterAnimation : MonoBehaviour
{
    void OnEnable()
    {
        var anim = GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            var clips = anim.runtimeAnimatorController.animationClips;
            if (clips != null && clips.Length > 0)
            {
                Destroy(gameObject, clips[0].length);
                return;
            }
        }
        Destroy(gameObject, 0.4f); // 백업 안전장치
    }
}
