using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : Singleton<LevelManager>
{
    [SerializeField] private List<GameObject> levels = new List<GameObject>();
    [SerializeField] private Transform levelRoot;
    [SerializeField] private float loadDelay = 0.05f;
    [SerializeField] private bool saveProgress = true;

    private int currentLevelIndex = 0;
    private GameObject currentLevelInstance;
    private Coroutine loadCR;

    public int CurrentLevelNumber => currentLevelIndex + 1;
    public int TotalLevels => levels != null ? levels.Count : 0;

    public event Action<int, int> OnLevelLoaded;

    // ===================== SAVE (ADDED) =====================
    private const string PREF_LEVEL_INDEX = "LM_CURRENT_LEVEL_INDEX";

    /// <summary>
    /// Gọi hàm này khi mở game để load level đã lưu (nếu có).
    /// Nếu chưa từng lưu thì sẽ load level 0.
    /// </summary>
    public void LoadSavedLevel()
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError("[LevelManager] Levels list is empty!");
            return;
        }
        if (!saveProgress)
        {
            LoadLevelByIndex(0);
            return;
        }

        int saved = PlayerPrefs.GetInt(PREF_LEVEL_INDEX, 0);
        saved = Mathf.Clamp(saved, 0, levels.Count - 1);

        LoadLevelByIndex(saved); // giữ nguyên flow cũ
    }

    private void SaveCurrentLevelIndex()
    {
        if (!saveProgress) return;
        PlayerPrefs.SetInt(PREF_LEVEL_INDEX, currentLevelIndex);
        PlayerPrefs.Save();
    }

    // ===================== OLD API (KEEP) =====================
    public void LoadFirstLevel() => LoadLevelByIndex(0);

    public void NextLevel()
    {
        if (levels == null || levels.Count == 0) return;
        int next = currentLevelIndex + 1;
        if (next >= levels.Count) next = 0;
        LoadLevelByIndex(next);
    }

    public void ReplayLevel()
    {
        if (levels == null || levels.Count == 0) return;
        LoadLevelByIndex(currentLevelIndex);
    }

    private void LoadLevelByIndex(int idx)
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError("[LevelManager] Levels list is empty!");
            return;
        }

        idx = Mathf.Clamp(idx, 0, levels.Count - 1);
        currentLevelIndex = idx;

        // ===== SAVE LEVEL INDEX (ADDED) =====
        SaveCurrentLevelIndex();

        if (loadCR != null) StopCoroutine(loadCR);
        loadCR = StartCoroutine(LoadLevelCR(idx));
    }

    private IEnumerator LoadLevelCR(int idx)
    {
        // ===== BẮT ĐẦU LOAD LEVEL =====
        if (GameManager.Instance != null)
            GameManager.Instance.SetLoading(true);

        // 1) Xoá level cũ (ở đây UnregisterLine có thể bị gọi)
        ClearCurrentLevel();

        // 2) Delay (nếu có)
        if (loadDelay > 0f)
            yield return new WaitForSeconds(loadDelay);
        else
            yield return null;

        // 3) Root
        if (!levelRoot) levelRoot = transform;

        // 4) Instantiate level mới
        var prefab = levels[idx];
        if (!prefab)
        {
            Debug.LogError($"[LevelManager] Level prefab at index {idx} is NULL.");
            if (GameManager.Instance != null)
                GameManager.Instance.SetLoading(false);
            yield break;
        }

        currentLevelInstance = Instantiate(prefab, levelRoot);
        currentLevelInstance.name = $"Level_{idx + 1:00}";

        // 5) Reset state + heart + line count
        if (GameManager.Instance != null)
            GameManager.Instance.StartLevel();

        // 6) Báo UI
        OnLevelLoaded?.Invoke(CurrentLevelNumber, TotalLevels);

        Debug.Log($"[LevelManager] Loaded Level {idx + 1}/{levels.Count}");

        // ===== KẾT THÚC LOAD LEVEL =====
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
}
