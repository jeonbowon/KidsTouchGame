using UnityEngine;

public class StageBackgroundConfig : MonoBehaviour
{
    [Header("Layer Keys")]
    [Tooltip("이 스테이지에서 켤 레이어 키들. 예: Base 또는 Base, Nebula 등")]
    public string[] enabledLayerKeys = new string[] { "Base" };

    [Tooltip("true면 enabledLayerKeys에 없는 레이어는 모두 끕니다.")]
    public bool exclusive = true;

    [Header("Theme (Optional)")]
    [Tooltip("테마를 지정하면 Base 스프라이트/속도/틴트/오버레이를 자동 변주합니다.")]
    public BackgroundThemeConfig theme;

    [Tooltip("테마가 있어도, enabledLayerKeys에 Base가 없으면 자동으로 Base를 켤지 여부")]
    public bool forceEnableBaseKey = true;

    [Tooltip("Base 레이어 키 이름(기본 Base)")]
    public string baseKeyName = "Base";
}
