using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Calendar/Monthly Art Config")]
public class MonthlyArtConfig : ScriptableObject
{
    [System.Serializable]
    public class MonthArt
    {
        public Sprite baseSprite;
        public Sprite fillSprite; 
    }

    [Tooltip("Index 0 = January ... 11 = December")]
    public MonthArt[] months = new MonthArt[12];

    public MonthArt Get(int month)
    {
        int idx = Mathf.Clamp(month - 1, 0, 11);
        return months[idx];
    }
}
