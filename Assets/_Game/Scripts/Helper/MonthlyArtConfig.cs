using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Calendar/Monthly Art Config")]
public class MonthlyArtConfig : ScriptableObject
{
    [Header("Index 0 = January ... 11 = December")]
    public Sprite[] monthSprites = new Sprite[12];

    public Sprite GetSpriteForMonth(int month1to12)
    {
        int idx = Mathf.Clamp(month1to12, 1, 12) - 1;
        return monthSprites != null && monthSprites.Length >= 12 ? monthSprites[idx] : null;
    }
}
