using System;
using UnityEngine;

public static class DailyProgress
{
    private const string KEY_PREFIX = "DAILY_WIN_";

    public static string DateKey(DateTime d) => $"{d:yyyyMMdd}";

    public static bool IsWin(DateTime d)
    {
        return PlayerPrefs.GetInt(KEY_PREFIX + DateKey(d), 0) == 1;
    }

    public static void SetWin(DateTime d, bool win)
    {
        PlayerPrefs.SetInt(KEY_PREFIX + DateKey(d), win ? 1 : 0);
        PlayerPrefs.Save();
    }
}
