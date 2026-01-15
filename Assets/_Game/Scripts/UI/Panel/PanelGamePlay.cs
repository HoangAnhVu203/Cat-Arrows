using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelGamePlay : UICanvas
{
    [Header("UI - Level")]
    [SerializeField] private Text levelText;

    [Header("UI - Heart")]       
    [SerializeField] private List<Image> heartIcons;     

     void OnEnable()
    {
   

        if (GameManager.Instance != null)
            GameManager.Instance.OnHeartChanged += HandleHeartChanged;

        if (LevelManager.Instance != null)
            LevelManager.Instance.OnLevelLoaded += HandleLevelLoaded;

        RefreshAll();
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnHeartChanged -= HandleHeartChanged;

        if (LevelManager.Instance != null)
            LevelManager.Instance.OnLevelLoaded -= HandleLevelLoaded;

    }

    private void RefreshAll()
    {
        // Heart
        if (GameManager.Instance != null)
            HandleHeartChanged(GameManager.Instance.CurrentHeart, GameManager.Instance.MaxHeart);

        // Level
        if (LevelManager.Instance != null)
            HandleLevelLoaded(LevelManager.Instance.CurrentLevelNumber, LevelManager.Instance.TotalLevels);
    }

    private void HandleHeartChanged(int current, int max)
    {
        if (heartIcons != null && heartIcons.Count > 0)
        {
            for (int i = 0; i < heartIcons.Count; i++)
            {
                if (heartIcons[i] == null) continue;
                heartIcons[i].enabled = (i < current);
            }
        }
    }

    private void HandleLevelLoaded(int levelNumber, int total)
    {
        if (levelText == null) return;
        levelText.text = $"Level {levelNumber}";
    }

    public void RePlayBTN()
    {
        LevelManager.Instance.ReplayLevel();
    }

    public void OpenSetiingUI()
    {
        UIManager.Instance.OpenUI<PanelSetting>();
    }
}
