using System;
using UnityEngine;

/// <summary>
/// EconomyManager
/// - 코인(소프트 커런시) 관련 책임을 한 곳으로 모읍니다.
/// - 내부 저장은 CosmeticSaveManager를 사용하되, 외부는 EconomyManager API만 쓰도록 유도합니다.
/// - GameManager는 "호출만" 하고, 코인 변경 이벤트를 구독하는 쪽(HUD 등)이 화면을 갱신합니다.
/// </summary>
[DisallowMultipleComponent]
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager I { get; private set; }

    /// <summary>코인 값이 변경될 때마다 발행됩니다.</summary>
    public event Action<int> OnCoinsChanged;

    /// <summary>
    /// 씬 어디에도 EconomyManager가 없으면 자동 생성합니다.
    /// (GameManager/HUDController에서 안전하게 호출 가능)
    /// </summary>
    public static void EnsureExists()
    {
        if (I != null) return;

        var found = FindObjectOfType<EconomyManager>(true);
        if (found != null)
        {
            I = found;
            return;
        }

        var go = new GameObject("EconomyManager (Auto)");
        var mgr = go.AddComponent<EconomyManager>();
        DontDestroyOnLoad(go);
        I = mgr;
    }

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        // CosmeticSaveManager 이벤트를 중계합니다.
        CosmeticSaveManager.OnCoinsChanged -= HandleCoinsChanged;
        CosmeticSaveManager.OnCoinsChanged += HandleCoinsChanged;

        // 초기 1회 발행
        HandleCoinsChanged(GetCoins());
    }

    private void OnDestroy()
    {
        if (I == this) I = null;
        CosmeticSaveManager.OnCoinsChanged -= HandleCoinsChanged;
    }

    private void HandleCoinsChanged(int coins)
    {
        try { OnCoinsChanged?.Invoke(coins); }
        catch (Exception e) { Debug.LogWarning($"[EconomyManager] OnCoinsChanged invoke failed: {e.Message}"); }
    }

    public int GetCoins() => CosmeticSaveManager.GetCoins();

    public void AddCoins(int amount)
    {
        CosmeticSaveManager.AddCoins(amount);
        // CosmeticSaveManager가 이벤트를 발행하므로, 여기서 추가 발행할 필요가 없습니다.
    }

    public bool TrySpendCoins(int cost)
    {
        return CosmeticSaveManager.TrySpendCoins(cost);
    }
}
