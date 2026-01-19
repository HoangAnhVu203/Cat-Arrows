using System;
using UnityEngine;

public static class DailyProgressMonth
{
    public static int CountWinInMonth(int year, int month)
    {
        int days = DateTime.DaysInMonth(year, month);
        int count = 0;

        for (int d = 1; d <= days; d++)
        {
            var date = new DateTime(year, month, d);
            if (DailyProgress.IsWin(date)) count++;
        }

        return count;
    }

    // Win 1 day => +3%
    public static int PercentInMonth_3PerWin(int year, int month)
    {
        int win = CountWinInMonth(year, month);
        return Mathf.Clamp(win * 3, 0, 100);
    }
}
