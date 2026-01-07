using System;
using UnityEngine;

/// <summary>
/// 날짜 기반(DDMMYY) 치트 잠금 해제 + 코인/코스메틱 개발자 기능
/// - 릴리즈에서도 사용 가능(숨은 제스처 + DDMMYY 비번)
/// - PlayerPrefs 직접 파일 조작 없이 CosmeticSaveManager만 통해 조작
/// </summary>
public static class DevCheats
{
    private const string KEY_DEV_ENABLED = "dev_cheats_enabled_v2";
    private const string KEY_DEV_ENABLED_DATE = "dev_cheats_enabled_date_v2"; // yyyyMMdd (로컬 날짜)

    private const string COSMETIC_DB_RESOURCE_PATH = "Cosmetics/CosmeticDatabase";

    /// <summary>
    /// 오늘의 코드(DDMMYY). 예: 2026-01-07 => "070126"
    /// 로컬 날짜 기준(DateTime.Now).
    /// </summary>
    public static string GetTodayCode()
    {
        DateTime now = DateTime.Now; // ✅ 로컬 날짜(대표님이 보는 '오늘')
        return now.ToString("ddMMyy"); // DDMMYY
    }

    /// <summary>
    /// dev 활성 상태인지(날짜 바뀌면 자동 무효)
    /// </summary>
    public static bool IsDevEnabled()
    {
        string savedDate = PlayerPrefs.GetString(KEY_DEV_ENABLED_DATE, "");
        string todayKey = GetTodayKey_Local();

        if (savedDate != todayKey)
        {
            // 날짜가 바뀌면 자동 무효
            if (PlayerPrefs.GetInt(KEY_DEV_ENABLED, 0) != 0)
            {
                PlayerPrefs.SetInt(KEY_DEV_ENABLED, 0);
                PlayerPrefs.SetString(KEY_DEV_ENABLED_DATE, todayKey);
                PlayerPrefs.Save();
            }
            return false;
        }

        return PlayerPrefs.GetInt(KEY_DEV_ENABLED, 0) == 1;
    }

    /// <summary>
    /// 입력한 코드가 오늘의 코드(DDMMYY)와 같으면 dev 활성화
    /// </summary>
    public static bool TryUnlockDev(string inputCode)
    {
        if (string.IsNullOrEmpty(inputCode))
            return false;

        string expected = GetTodayCode();
        bool ok = string.Equals(inputCode.Trim(), expected, StringComparison.Ordinal);

        if (ok)
        {
            PlayerPrefs.SetInt(KEY_DEV_ENABLED, 1);
            PlayerPrefs.SetString(KEY_DEV_ENABLED_DATE, GetTodayKey_Local());
            PlayerPrefs.Save();
        }

        return ok;
    }

    public static void LockDevNow()
    {
        PlayerPrefs.SetInt(KEY_DEV_ENABLED, 0);
        PlayerPrefs.SetString(KEY_DEV_ENABLED_DATE, GetTodayKey_Local());
        PlayerPrefs.Save();
    }

    // -------------------------
    // Coins
    // -------------------------
    public static int GetCoins() => CosmeticSaveManager.GetCoins();

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        CosmeticSaveManager.AddCoins(amount);
    }

    public static void SetCoins(int value)
    {
        if (value < 0) value = 0;
        var d = CosmeticSaveManager.Data;
        d.coins = value;
        CosmeticSaveManager.Save(d);
        Debug.Log($"[DevCheats] SetCoins -> {value}");
    }

    // -------------------------
    // Cosmetics (Unlocked / Owned)
    // -------------------------
    public static void ResetAllCosmeticsAndCoins()
    {
        var fresh = new CosmeticSaveData();
        CosmeticSaveManager.Save(fresh);
        Debug.Log("[DevCheats] ResetAllCosmeticsAndCoins()");
    }

    public static void UnlockAll()
    {
        var db = Resources.Load<CosmeticDatabase>(COSMETIC_DB_RESOURCE_PATH);
        if (db == null)
        {
            Debug.LogWarning($"[DevCheats] CosmeticDatabase 로드 실패: Resources/{COSMETIC_DB_RESOURCE_PATH}");
            return;
        }

        foreach (var it in db.items)
        {
            if (it == null) continue;
            if (string.IsNullOrEmpty(it.id)) continue;

            // ✅ Unlocked만 열기
            CosmeticSaveManager.GrantUnlocked(it.id);
        }

        Debug.Log("[DevCheats] UnlockAll()");
    }

    public static void OwnAll()
    {
        var db = Resources.Load<CosmeticDatabase>(COSMETIC_DB_RESOURCE_PATH);
        if (db == null)
        {
            Debug.LogWarning($"[DevCheats] CosmeticDatabase 로드 실패: Resources/{COSMETIC_DB_RESOURCE_PATH}");
            return;
        }

        foreach (var it in db.items)
        {
            if (it == null) continue;
            if (string.IsNullOrEmpty(it.id)) continue;

            CosmeticSaveManager.GrantOwned(it.id);
        }

        Debug.Log("[DevCheats] OwnAll()");
    }

    public static void UnlockUpToStage(int stage)
    {
        if (stage < 1) stage = 1;

        var db = Resources.Load<CosmeticDatabase>(COSMETIC_DB_RESOURCE_PATH);
        if (db == null)
        {
            Debug.LogWarning($"[DevCheats] CosmeticDatabase 로드 실패: Resources/{COSMETIC_DB_RESOURCE_PATH}");
            return;
        }

        foreach (var it in db.items)
        {
            if (it == null) continue;
            if (string.IsNullOrEmpty(it.id)) continue;

            if (it.unlockOnStageClear > 0 && it.unlockOnStageClear <= stage)
                CosmeticSaveManager.GrantUnlocked(it.id);
        }

        Debug.Log($"[DevCheats] UnlockUpToStage({stage})");
    }

    // -------------------------
    private static string GetTodayKey_Local()
    {
        // 날짜 변경 감지용(로컬)
        return DateTime.Now.ToString("yyyyMMdd");
    }
}
