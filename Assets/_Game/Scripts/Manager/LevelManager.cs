using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : Singleton<LevelManager>
{
    public enum LevelMode { Normal, Daily }

    [Header("Normal Levels (Home)")]
    [SerializeField] private List<GameObject> levelsNormal = new List<GameObject>();

    [Header("Daily Levels (Calendar) - 1..31")]
    [SerializeField] private List<GameObject> levelsDaily = new List<GameObject>();

    [SerializeField] private Transform levelRoot;
    [SerializeField] private float loadDelay = 0.05f;
    [SerializeField] private bool saveProgress = true;

    private int currentLevelIndex = 0;                 // index của mode hiện tại
    private GameObject currentLevelInstance;
    private Coroutine loadCR;

    private LevelMode currentMode = LevelMode.Normal;
    public LevelMode CurrentMode => currentMode;

    public int CurrentLevelNumber => currentLevelIndex + 1;

    public int CurrentDailyDay { get; private set; } = -1;

    public DateTime CurrentDailyDate { get; private set; } = default;
    public void SetCurrentDailyDate(DateTime d) => CurrentDailyDate = d.Date;


    public int TotalLevels
    {
        get
        {
            if (currentMode == LevelMode.Daily)
                return levelsDaily != null ? levelsDaily.Count : 0;
            return levelsNormal != null ? levelsNormal.Count : 0;
        }
    }

    public event Action<int, int> OnLevelLoaded;

    private const string PREF_LEVEL_INDEX = "LM_CURRENT_LEVEL_INDEX";   // normal index
    private const string PREF_DAILY_DAY = "LM_DAILY_SELECTED_DAY";      // day 1..31 (optional)

    // ===================== LOAD NORMAL =====================

    public void LoadSavedLevel()
    {
        if (levelsNormal == null || levelsNormal.Count == 0)
        {
            Debug.LogError("[LevelManager] Normal levels list is empty!");
            return;
        }

        currentMode = LevelMode.Normal;

        int saved = saveProgress ? PlayerPrefs.GetInt(PREF_LEVEL_INDEX, 0) : 0;
        saved = Mathf.Clamp(saved, 0, levelsNormal.Count - 1);

        LoadFromList(levelsNormal, saved, mode: LevelMode.Normal, saveNormalProgress: true);
    }

    public void LoadNormalByIndex(int idx)
    {
        if (levelsNormal == null || levelsNormal.Count == 0)
        {
            Debug.LogError("[LevelManager] Normal levels list is empty!");
            return;
        }

        idx = Mathf.Clamp(idx, 0, levelsNormal.Count - 1);
        LoadFromList(levelsNormal, idx, mode: LevelMode.Normal, saveNormalProgress: true);
    }

    public void NextLevel()
    {
        if (levelsNormal == null || levelsNormal.Count == 0) return;

        // Next chỉ dành cho Normal (progression)
        int next = currentLevelIndex + 1;
        if (currentMode != LevelMode.Normal) next = 0;

        if (next >= levelsNormal.Count) next = 0;

        LoadFromList(levelsNormal, next, mode: LevelMode.Normal, saveNormalProgress: true);
    }

    // ===================== LOAD DAILY =====================

    public void LoadDailyByDay(int dayOfMonth)
    {
        CurrentDailyDay = dayOfMonth;
        if (levelsDaily == null || levelsDaily.Count == 0)
        {
            Debug.LogError("[LevelManager] Daily levels list is empty!");
            return;
        }

        dayOfMonth = Mathf.Clamp(dayOfMonth, 1, 31);
        int idx = dayOfMonth - 1;
        idx = Mathf.Clamp(idx, 0, levelsDaily.Count - 1);

        currentMode = LevelMode.Daily;

        // Optional save selected day for UI
        PlayerPrefs.SetInt(PREF_DAILY_DAY, dayOfMonth);
        PlayerPrefs.Save();

        LoadFromList(levelsDaily, idx, mode: LevelMode.Daily, saveNormalProgress: false);
    }

    // ===================== REPLAY (MODE AWARE) =====================

    public void ReplayLevel()
    {
        if (currentMode == LevelMode.Daily)
        {
            if (levelsDaily == null || levelsDaily.Count == 0) return;
            int idx = Mathf.Clamp(currentLevelIndex, 0, levelsDaily.Count - 1);
            LoadFromList(levelsDaily, idx, mode: LevelMode.Daily, saveNormalProgress: false);
        }
        else
        {
            if (levelsNormal == null || levelsNormal.Count == 0) return;
            int idx = Mathf.Clamp(currentLevelIndex, 0, levelsNormal.Count - 1);
            LoadFromList(levelsNormal, idx, mode: LevelMode.Normal, saveNormalProgress: true);
        }
    }

    // ===================== CORE =====================

    private void LoadFromList(List<GameObject> list, int idx, LevelMode mode, bool saveNormalProgress)
    {
        if (list == null || list.Count == 0)
        {
            Debug.LogError("[LevelManager] Level list is empty!");
            return;
        }

        idx = Mathf.Clamp(idx, 0, list.Count - 1);

        currentMode = mode;
        currentLevelIndex = idx; // QUAN TRỌNG: set cho cả Normal + Daily

        if (saveNormalProgress && saveProgress)
        {
            PlayerPrefs.SetInt(PREF_LEVEL_INDEX, currentLevelIndex);
            PlayerPrefs.Save();
        }

        if (loadCR != null) StopCoroutine(loadCR);
        loadCR = StartCoroutine(LoadLevelCR(list, idx, mode));
    }

    private IEnumerator LoadLevelCR(List<GameObject> list, int idx, LevelMode mode)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SetLoading(true);

        ClearCurrentLevel();

        if (loadDelay > 0f) yield return new WaitForSeconds(loadDelay);
        else yield return null;

        if (!levelRoot) levelRoot = transform;

        var prefab = list[idx];
        if (!prefab)
        {
            Debug.LogError($"[LevelManager] Prefab NULL at index {idx} mode={mode}");
            if (GameManager.Instance != null) GameManager.Instance.SetLoading(false);
            yield break;
        }

        currentLevelInstance = Instantiate(prefab, levelRoot);
        currentLevelInstance.name = mode == LevelMode.Normal ? $"Level_Normal_{idx + 1:00}" : $"Level_Daily_{idx + 1:00}";

        if (GameManager.Instance != null)
            GameManager.Instance.StartLevel();

        OnLevelLoaded?.Invoke(idx + 1, list.Count);

        if (GameManager.Instance != null)
            GameManager.Instance.SetLoading(false);

        loadCR = null;
    }

    private void ClearCurrentLevel()
    {
        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
            currentLevelInstance = null;
        }

        if (levelRoot != null)
        {
            for (int i = levelRoot.childCount - 1; i >= 0; i--)
                Destroy(levelRoot.GetChild(i).gameObject);
        }
    }

    public int GetSavedNormalLevelNumber()
    {
        if (levelsNormal == null || levelsNormal.Count == 0) return 1;

        int idx = saveProgress ? PlayerPrefs.GetInt(PREF_LEVEL_INDEX, 0) : 0;
        idx = Mathf.Clamp(idx, 0, levelsNormal.Count - 1);
        return idx + 1; // number (1-based)
    }

    public int GetNormalTotalLevels()
    {
        return levelsNormal != null ? levelsNormal.Count : 0;
    }
    public void InitNormalProgressOnly()
    {
        if (levelsNormal == null || levelsNormal.Count == 0) return;

        int saved = saveProgress ? PlayerPrefs.GetInt(PREF_LEVEL_INDEX, 0) : 0;
        saved = Mathf.Clamp(saved, 0, levelsNormal.Count - 1);

        currentMode = LevelMode.Normal;
        currentLevelIndex = saved;   // chỉ set index thôi, KHÔNG load prefab
    }

}
