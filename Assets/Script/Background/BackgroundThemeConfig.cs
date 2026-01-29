using UnityEngine;

[CreateAssetMenu(menuName = "KidsTouchGame/Background Theme Config", fileName = "BGTheme_")]
public class BackgroundThemeConfig : ScriptableObject
{
    [Header("Base Sprites (pick 1 randomly)")]
    public Sprite[] baseSprites;

    [Header("Scroll Speed Random Range")]
    public Vector2 scrollSpeedRange = new Vector2(0.45f, 0.75f);

    [Header("Tint (Color Variation)")]
    [Tooltip("화이트(1,1,1,1) 기준으로 살짝 틴트. 알파는 그대로 두는 것을 권장.")]
    public Color tintMin = Color.white;
    public Color tintMax = Color.white;

    [Header("Overlay Spawn")]
    public bool enableOverlay = true;

    [Tooltip("스폰할 오버레이 프리팹들(혜성/먼지/별반짝 등)")]
    public GameObject[] overlayPrefabs;

    [Tooltip("스테이지 시작 시 즉시 스폰 개수 범위")]
    public Vector2Int overlayBurstCountRange = new Vector2Int(0, 2);

    [Tooltip("추가 스폰 간격(초) 범위. 0이면 추가 스폰 없음")]
    public Vector2 overlaySpawnIntervalRange = new Vector2(0f, 0f);

    [Tooltip("오버레이가 화면에 남아있는 시간(초)")]
    public float overlayLifeTime = 6f;
}
