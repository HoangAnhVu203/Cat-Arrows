using UnityEngine;
using UnityEngine.UI;

public class HomeLevelItemUI : MonoBehaviour
{
    [SerializeField] private Image bubble;
    [SerializeField] private Text levelText;

    [Header("Sprites (optional)")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite currentSprite;

    [Header("Colors (fallback)")]
    [SerializeField] private Color normalColor = new Color(0.7f, 0.85f, 1f, 1f);
    [SerializeField] private Color currentColor = new Color(0.75f, 0.55f, 1f, 1f);

    public void Setup(int levelNumber, int currentLevelNumber)
    {
        if (levelText) levelText.text = levelNumber.ToString();

        bool isCurrent = (levelNumber == currentLevelNumber);

        if (bubble)
        {
            if (normalSprite != null && currentSprite != null)
                bubble.sprite = isCurrent ? currentSprite : normalSprite;
            else
                bubble.color = isCurrent ? currentColor : normalColor;
        }

        transform.localScale = isCurrent ? Vector3.one * 1.0f : Vector3.one;
    }
}
