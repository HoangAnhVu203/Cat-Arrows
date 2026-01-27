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
    Coroutine hintCR;
    GridWavyLineMesh hintedLine;
    LineRenderer hintPathLR;

    void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHeartChanged += HandleHeartChanged;
            GameManager.Instance.OnShowPathChanged += HandleShowPathChanged;
            GameManager.Instance.OnHintChanged += HandleHintChanged;
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
            GameManager.Instance.OnHintChanged -= HandleHintChanged;
        }
            

        if (LevelManager.Instance != null)
            LevelManager.Instance.OnLevelLoaded -= HandleLevelLoaded;

        StopHintLoop();
        ClearHintVisual();
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

        AdService.ShowInterstitial(
    onAdClosed: () =>
    {
        Debug.Log("Ad closed → resume after ad");
    },
    onNoAd: () =>
    {
        Debug.Log("No ad → alternate flow");
    }
);
        
    }

    public void OpenSetiingUI()
    {
        UIManager.Instance.OpenUI<PanelSetting>();
    }

    public void OnEraseButtonClick()
    {



                    AdService.ShowRewarded(
    onEarned: () => 
    {
        // Debug.Log("Earned reward");
        // // Nếu bạn muốn NextLevelSystem() chạy luôn cả khi earn:
             GameManager.Instance.SetEraseMode(true);

        StartCoroutine(ScaleButton(
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                .transform as RectTransform
        ));
    },
    onClosed: () => 
    {
        // Debug.Log("Closed (no reward)");
        // NextLevelSystem();   // chạy khi đóng quảng cáo

    }
);
        
   
    }

    public void OnShowPathButtonClick()
    {
AdService.ShowInterstitial(
    onAdClosed: () =>
    {
        Debug.Log("Ad closed → resume after ad");
    },
    onNoAd: () =>
    {
        Debug.Log("No ad → alternate flow");
    }
);

        bool next = !GameManager.Instance.ShowPathMode;
        GameManager.Instance.SetShowPathMode(next);

        StartCoroutine(ScaleButton(
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                .transform as RectTransform
        ));
    }

    public void OnHintButtonClick()
    {

                    AdService.ShowRewarded(
    onEarned: () => 
    {
        // Debug.Log("Earned reward");
        // // Nếu bạn muốn NextLevelSystem() chạy luôn cả khi earn:
         bool next = !GameManager.Instance.HintMode;
        GameManager.Instance.SetHintMode(next);

        StartCoroutine(ScaleButton(
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                .transform as RectTransform
        ));
    },
    onClosed: () => 
    {
        // Debug.Log("Closed (no reward)");
        // NextLevelSystem();   // chạy khi đóng quảng cáo

    }
);
        
       
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

        var mat = new Material(Shader.Find("Sprites/Default"));
        Color gray = new Color(0.9f, 0.9f, 0.9f, 0.8f);

        foreach (var l in lines)
        {
            if (l == null) continue;
            if (!l.TryGetPreviewPathWorld(out var ptsW) || ptsW == null || ptsW.Length < 2) continue;

            Transform parent = l.transform;

            GameObject go = new GameObject("PathPreview");
            go.transform.SetParent(parent, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.material = mat;

            lr.useWorldSpace = false;

            lr.positionCount = ptsW.Length;

            for (int i = 0; i < ptsW.Length; i++)
                ptsW[i] = parent.InverseTransformPoint(ptsW[i]);

            lr.SetPositions(ptsW);

            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;

            // để nằm dưới head/line một chút
            lr.sortingOrder = -1;

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

    void HandleHintChanged(bool on)
    {
        if (on)
        {
            if (GameManager.Instance != null && GameManager.Instance.ShowPathMode)
                GameManager.Instance.SetShowPathMode(false);

            StartHintLoop();
        }
        else
        {
            StopHintLoop();
            ClearHintVisual();
        }
    }

    void StartHintLoop()
    {
        StopHintLoop();
        hintCR = StartCoroutine(HintLoop());
    }

    void StopHintLoop()
    {
        if (hintCR != null) StopCoroutine(hintCR);
        hintCR = null;
    }

    IEnumerator HintLoop()
    {
        WaitForSeconds w = new WaitForSeconds(0.25f);

        while (GameManager.Instance != null && GameManager.Instance.HintMode)
        {
            PickAndShowHint();
            yield return w;
        }
    }
    
    void PickAndShowHint()
    {
        var lines = FindObjectsOfType<GridWavyLineMesh>(true);
        GridWavyLineMesh pick = null;

        // chọn line đầu tiên có thể đi (không bị chặn)
        foreach (var l in lines)
        {
            if (!l || !l.gameObject.activeInHierarchy) continue;
            if (l.CanExitWithoutBlock_ForHint())
            {
                pick = l;
                break;
            }
        }

        if (pick == hintedLine) return;

        ClearHintVisual();

        hintedLine = pick;
        if (!hintedLine) return;

        hintedLine.SetHintHighlight(true);
        ShowHintPath(hintedLine);
    }

    void ShowHintPath(GridWavyLineMesh line)
    {
        if (line == null) return;
        if (!line.TryGetPreviewPathWorld(out var ptsW) || ptsW == null || ptsW.Length < 2) return;

        Transform parent = line.transform;

        if (hintPathLR == null)
        {
            var go = new GameObject("HintPath");
            go.transform.SetParent(parent, false);

            hintPathLR = go.AddComponent<LineRenderer>();
            hintPathLR.useWorldSpace = false;            // QUAN TRỌNG
            hintPathLR.material = new Material(Shader.Find("Sprites/Default"));
            hintPathLR.numCapVertices = 4;
            hintPathLR.numCornerVertices = 4;
            hintPathLR.startWidth = 0.08f;
            hintPathLR.endWidth = 0.08f;

            // đảm bảo nằm dưới head / mesh
            hintPathLR.sortingOrder = -1;

            // xanh lá gợi ý
            Color green = new Color(0.2f, 0.9f, 0.25f, 0.95f);
            hintPathLR.startColor = Color.green;
            hintPathLR.endColor = Color.green;
        }
        else
        {
            if (hintPathLR.transform.parent != parent)
                hintPathLR.transform.SetParent(parent, false);
        }

        hintPathLR.positionCount = ptsW.Length;

        // WORLD -> LOCAL theo LINE (chuẩn nhất)
        for (int i = 0; i < ptsW.Length; i++)
            ptsW[i] = parent.InverseTransformPoint(ptsW[i]);

        hintPathLR.SetPositions(ptsW);
    }



    void ClearHintVisual()
    {
        if (hintedLine != null) hintedLine.SetHintHighlight(false);
        hintedLine = null;

        if (hintPathLR != null)
        {
            Destroy(hintPathLR.gameObject);
            hintPathLR = null;
        }
    }

    public void OnBackClick()
    {
        AdService.ShowInterstitial(
    onAdClosed: () =>
    {
        Debug.Log("Ad closed → resume after ad");
    },
    onNoAd: () =>
    {
        Debug.Log("No ad → alternate flow");
    }
);
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.HintMode) GameManager.Instance.SetHintMode(false);
            if (GameManager.Instance.ShowPathMode) GameManager.Instance.SetShowPathMode(false);
            GameManager.Instance.SetEraseMode(false);
        }

        StopHintLoop();
        ClearHintVisual();
        ClearPreviewPaths();

        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();

        UIManager.Instance.OpenUI<FooterTabBar>();

        if (LevelManager.Instance != null && LevelManager.Instance.CurrentMode == LevelManager.LevelMode.Daily)
        {
            UIManager.Instance.OpenUI<PanelCalendar>();
            UIManager.Instance.CloseUIDirectly<PanelHome>();

        }
        else
        {
            UIManager.Instance.OpenUI<PanelHome>();
            UIManager.Instance.CloseUIDirectly<PanelCalendar>();
        }
    }




}

