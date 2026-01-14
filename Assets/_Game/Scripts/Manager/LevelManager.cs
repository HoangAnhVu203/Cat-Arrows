using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : Singleton<LevelManager>
{
    [Header("Levels (Prefab list in order)")]
    [SerializeField] private List<GameObject> levels = new List<GameObject>();

    [Header("Root to spawn levels under")]
    [SerializeField] private Transform levelRoot;

    [Header("Optional")]
    [Tooltip("Delay nhỏ để tránh giật UI/state khi chuyển level.")]
    [SerializeField] private float loadDelay = 0.05f;

    // ===================== RUNTIME =====================

    private int currentLevelIndex = 0;      // 0-based
    private GameObject currentLevelInstance;

    // ===================== PUBLIC API =====================

    public int CurrentLevelNumber => currentLevelIndex + 1;
    public int LastLevelNumber => Mathf.Max(1, levels.Count);

    public bool HasNextLevel => (currentLevelIndex + 1) < levels.Count;
    public bool HasPrevLevel => (currentLevelIndex - 1) >= 0;

    /// <summary>
    /// Gọi khi bắt đầu game hoặc khi cần load level 1.
    /// </summary>
    public void LoadFirstLevel()
    {
        LoadLevelByIndex(0);
    }

    /// <summary>
    /// Load level theo số (1-based) để tiện gọi: LoadLevelNumber(1), (2), ...
    /// </summary>
    public void LoadLevelNumber(int levelNumber)
    {
        int idx = Mathf.Clamp(levelNumber - 1, 0, Mathf.Max(0, levels.Count - 1));
        LoadLevelByIndex(idx);
    }

    /// <summary>
    /// Next level. Nếu đang là level cuối -> có thể Win game / hoặc quay lại level 1 tuỳ bạn.
    /// </summary>
    public void NextLevel()
    {
        if (levels == null || levels.Count == 0)
            return;

        int nextIndex = currentLevelIndex + 1;

        // ===== LOOP =====
        if (nextIndex >= levels.Count)
            nextIndex = 0;

        LoadLevelByIndex(nextIndex);
    }


    /// <summary>
    /// Replay level hiện tại (dọn sạch và spawn lại).
    /// </summary>
    public void ReplayLevel()
    {
        LoadLevelByIndex(currentLevelIndex);
    }

    /// <summary>
    /// Dọn sạch level hiện tại (không spawn level mới).
    /// </summary>
    public void ClearLevelOnly()
    {
        ClearCurrentLevel();
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

        // Stop any pending routines if you want deterministic loads
        StopAllCoroutines();
        StartCoroutine(LoadLevelCR(idx));
    }

    private IEnumerator LoadLevelCR(int idx)
    {
        // 1) Dọn sạch level cũ
        ClearCurrentLevel();

        // 2) optional delay 1 frame (UI/state ổn định)
        if (loadDelay > 0f) yield return new WaitForSeconds(loadDelay);
        else yield return null;

        // 3) Spawn level mới
        var prefab = levels[idx];
        if (!prefab)
        {
            Debug.LogError($"[LevelManager] Level prefab at index {idx} is NULL.");
            yield break;
        }

        if (!levelRoot) levelRoot = transform;

        currentLevelInstance = Instantiate(prefab, levelRoot);
        currentLevelInstance.name = $"Level_{idx + 1:00}";

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartLevel();
        }

        Debug.Log($"[LevelManager] Loaded Level {idx + 1}/{levels.Count}");
    }

    // ===================== CLEAR =====================

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
            {
                Destroy(levelRoot.GetChild(i).gameObject);
            }
        }
    }
}
