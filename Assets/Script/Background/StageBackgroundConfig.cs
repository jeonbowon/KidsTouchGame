using UnityEngine;

public class StageBackgroundConfig : MonoBehaviour
{
    [Tooltip("이 스테이지에서 켤 레이어 키들. 예: Base 또는 Base, Nebula 등")]
    public string[] enabledLayerKeys = new string[] { "Base" };

    [Tooltip("true면 enabledLayerKeys에 없는 레이어는 모두 끕니다.")]
    public bool exclusive = true;
}
