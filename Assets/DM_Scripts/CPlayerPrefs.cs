using UnityEngine;

/// <summary>
/// Minimal PlayerPrefs wrapper restoring the CPlayerPrefs API used across the
/// project (AppEvent*, GameMain, PeriodEventData, PlayerDataManager).
/// The original third-party class is not in the project; this forwards to
/// UnityEngine.PlayerPrefs with bool helpers stored as 1/0 ints, mirroring
/// the ObscuredPrefs call pattern used under ENABLE_ANTI_CHEAT.
/// </summary>
public static class CPlayerPrefs
{
    public static int GetInt(string key, int defaultValue = 0)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
    }

    public static string GetString(string key, string defaultValue = "")
    {
        return PlayerPrefs.GetString(key, defaultValue);
    }

    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
    }

    public static void SetBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
    }

    public static bool HasKey(string key)
    {
        return PlayerPrefs.HasKey(key);
    }

    public static void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
    }

    public static void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
