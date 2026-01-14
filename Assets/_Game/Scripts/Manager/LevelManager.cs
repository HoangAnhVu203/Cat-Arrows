using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : Singleton<LevelManager>
{
    [Header("Levels (Prefab list in order)")]
    [SerializeField] private List<GameObject> levels = new List<GameObject>();

    [Header("Root to spawn levels under (only level objects!)")]
    [SerializeField] private Transform levelRoot;

    [Header("Optional")]
    [SerializeField] private float loadDelay = 0.05f;

    // ===================== RUNTIME =====================
    private int currentLevelIndex = 0; // 0-based
    private GameObject currentLevelInstance;
    private Coroutine loadCR;

    // ===================== PUBLIC API =====================
    public int CurrentLevelNumber => currentLevelIndex + 1;
    public int LastLevelNumber => Mathf.Max(1, levels.Count);
    public int CurrentLevelIndex => currentLevelIndex;

    public void LoadFirstLevel() => LoadLevelByIndex(0);

    public void LoadLevelNumber(int levelNumber)
    {
        if (levels == null || levels.Count == 0) return;
        int idx = Mathf.Clamp(levelNumber - 1, 0, levels.Count - 1);
        LoadLevelByIndex(idx);
    }

    public void NextLevel()
    {
        if (levels == null || levels.Count == 0) return;

        int next = currentLevelIndex + 1;
        if (next >= levels.Count) next = 0; // LOOP
        LoadLevelByIndex(next);
    }

    public void ReplayLevel()
    {
        if (levels == null || levels.Count == 0) return;
        LoadLevelByIndex(currentLevelIndex);
    }

    public void ClearLevelOnly()
    {
        StopLoading();
        ClearCurrentLevel();
        // nếu muốn reset count khi clear:
        if (GameManager.Instance != null)
            GameManager.Instance.SetActiveLineCount(0);
    }

    // ===================== CORE LOAD =====================
    private void LoadLevelByIndex(int idx)
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogError("[LevelManager] Levels list is empty!");
            return;
        }

        idx = Mathf.Clamp(idx, 0, levels.Count - 1);
        currentLevelIndex = idx;

        StopLoading();
        loadCR = StartCoroutine(LoadLevelCR(idx));
    }

    private void StopLoading()
    {
        if (loadCR != null)
        {
            StopCoroutine(loadCR);
            loadCR = null;
        }
    }

    private IEnumerator LoadLevelCR(int idx)
    {
        // 1) clear old
        ClearCurrentLevel();

        // 2) delay / 1 frame
        if (loadDelay > 0f) yield return new WaitForSeconds(loadDelay);
        else yield return null;

        // 3) spawn new
        if (!levelRoot) levelRoot = transform;

        var prefab = levels[idx];
        if (!prefab)
        {
            Debug.LogError($"[LevelManager] Level prefab at index {idx} is NULL.");
            yield break;
        }

        currentLevelInstance = Instantiate(prefab, levelRoot);
        currentLevelInstance.name = $"Level_{idx + 1:00}";

        // 4) count lines (IMPORTANT: GridWavyLineMesh should NOT auto-Register)
        int lineCount = 0;
        var lines = currentLevelInstance.GetComponentsInChildren<GridWavyLineMesh>(true);
        if (lines != null) lineCount = lines.Length;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartLevel();                 
            GameManager.Instance.SetActiveLineCount(lineCount); 
        }

        Debug.Log($"[LevelManager] Loaded Level {idx + 1}/{levels.Count} | Lines: {lineCount}");

        loadCR = null;
    }

    // ===================== CLEAR =====================
    private void ClearCurrentLevel()
    {
        if (currentLevelInstance != null)
        {
            Destroy(currentLevelInstance);
            currentLevelInstance = null;
        }

        // bảo vệ: chỉ xoá children của levelRoot (và levelRoot chỉ chứa level)
        if (levelRoot != null)
        {
            for (int i = levelRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(levelRoot.GetChild(i).gameObject);
            }
        }
    }
}
