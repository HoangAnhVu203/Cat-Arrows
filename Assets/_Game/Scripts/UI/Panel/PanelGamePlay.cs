using System.Collections;
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

    List<LineRenderer> previewLines = new();

    void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHeartChanged += HandleHeartChanged;
            GameManager.Instance.OnShowPathChanged += HandleShowPathChanged;
        }
            

        if (LevelManager.Instance != null)
            LevelManager.Instance.OnLevelLoaded += HandleLevelLoaded;

        RefreshAll();
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHeartChanged -= HandleHeartChanged;
            GameManager.Instance.OnShowPathChanged -= HandleShowPathChanged;
        }
            

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

    //=== Button ===

    public void RePlayBTN()
    {
        LevelManager.Instance.ReplayLevel();
    }

    public void OpenSetiingUI()
    {
        UIManager.Instance.OpenUI<PanelSetting>();
    }

    public void OnEraseButtonClick()
    {
        GameManager.Instance.SetEraseMode(true);

        StartCoroutine(ScaleButton(
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                .transform as RectTransform
        ));
    }

    public void OnShowPathButtonClick()
    {
        bool next = !GameManager.Instance.ShowPathMode;
        GameManager.Instance.SetShowPathMode(next);

        StartCoroutine(ScaleButton(
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                .transform as RectTransform
        ));
    }


    //===Helper===
    IEnumerator ScaleButton(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector3 normal = Vector3.one;
        Vector3 big = Vector3.one * 1.08f;

        float t = 0f;

        // scale up
        while (t < 0.08f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(normal, big, t / 0.08f);
            yield return null;
        }

        t = 0f;

        // scale down
        while (t < 0.08f)
        {
            t += Time.unscaledDeltaTime;
            rt.localScale = Vector3.Lerp(big, normal, t / 0.08f);
            yield return null;
        }

        rt.localScale = normal;
    }

    void BuildPreviewPaths()
    {
        ClearPreviewPaths();

        var lines = FindObjectsOfType<GridWavyLineMesh>(true);
        if (lines == null || lines.Length == 0) return;

        Material mat = new Material(Shader.Find("Sprites/Default"));


        foreach (var l in lines)
        {
            if (!l.TryGetPreviewPathWorld(out var pts)) continue;

            GameObject go = new GameObject("PathPreview");
            go.transform.SetParent(GameObject.Find("Root").transform, false);

            var lr = go.AddComponent<LineRenderer>();

            lr.material = mat;
            lr.useWorldSpace = true;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);

            lr.startWidth = 0.04f;
            lr.endWidth = 0.04f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = -1;

            Color gray = new Color(0.6f, 0.6f, 0.6f, 1f);
            lr.startColor = gray;
            lr.endColor = gray;
            previewLines.Add(lr);
        }
    }

    void ClearPreviewPaths()
    {
        foreach (var lr in previewLines)
            if (lr) Destroy(lr.gameObject);

        previewLines.Clear();
    }

    void HandleShowPathChanged(bool on)
    {
        Debug.Log("ShowPathChanged: " + on);
        if (on) BuildPreviewPaths();
        else ClearPreviewPaths();
    }


}

