using UnityEngine;

[DisallowMultipleComponent]
public class BackgroundLayer : MonoBehaviour
{
    [Tooltip("Stage에서 선택할 레이어 키. 예: Base, GalaxyFar, Nebula 등")]
    public string key = "Base";

    [Tooltip("이 레이어를 켜고 끌 루트 오브젝트(기본: 자기 자신)")]
    public GameObject root;

    private void Reset()
    {
        root = gameObject;
    }

    public void SetActive(bool on)
    {
        if (root != null) root.SetActive(on);
        else gameObject.SetActive(on);
    }
}
