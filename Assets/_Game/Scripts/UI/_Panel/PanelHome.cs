using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelHome : UICanvas
{
    [Header("Scroll")]
    [SerializeField] private RectTransform content;
    [SerializeField] private HomeLevelItemUI itemPrefab;

    [Header("Buttons")]
    [SerializeField] private Button playButton;

    [Header("Config")]
    [SerializeField] private int showNextCount = 4;

    private readonly List<HomeLevelItemUI> items = new();
    private Coroutine buildCo;

    private void OnEnable()
    {
        // Bind button
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClick);
        }

        if (buildCo != null) StopCoroutine(buildCo);
        buildCo = StartCoroutine(BuildWhenReady());
    }

    private void OnDisable()
    {
        if (buildCo != null)
        {
            StopCoroutine(buildCo);
            buildCo = null;
        }

        if (playButton != null)
            playButton.onClick.RemoveAllListeners();
    }

    private IEnumerator BuildWhenReady()
    {
        yield return null;

        const int maxWaitFrames = 60;
        int waited = 0;

        while ((LevelManager.Instance == null || LevelManager.Instance.GetNormalTotalLevels() <= 0) && waited < maxWaitFrames)
        {
            waited++;
            yield return null;
        }

        BuildLevels();
    }

    private void BuildLevels()
    {
        Clear();

        if (LevelManager.Instance == null || itemPrefab == null || content == null) return;

        int total = LevelManager.Instance.GetNormalTotalLevels();
        int cur = Mathf.Clamp(LevelManager.Instance.GetSavedNormalLevelNumber(), 1, total);

        int maxShow = Mathf.Min(cur + showNextCount, total);

        for (int lv = cur; lv <= maxShow; lv++)
        {
            var it = Instantiate(itemPrefab, content);
            it.name = $"HomeLevel_{lv:000}";
            it.Setup(lv, cur);
            items.Add(it);
            it.transform.SetAsFirstSibling();
        }

        StartCoroutine(FixScrollToBottomNextFrame());
    }

    private IEnumerator FixScrollToBottomNextFrame()
    {
        yield return null;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        var p = content.anchoredPosition;
        p.y = 0f;
        content.anchoredPosition = p;

        var sr = content.GetComponentInParent<ScrollRect>();
        if (sr)
        {
            sr.StopMovement();
            sr.verticalNormalizedPosition = 0f;
        }
    }

    private void OnPlayClick()
    {
        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadSavedLevel();

        UIManager.Instance.CloseUIDirectly<PanelHome>();
        UIManager.Instance.CloseUIDirectly<PanelCalendar>();
        UIManager.Instance.CloseUIDirectly<FooterTabBar>();

        UIManager.Instance.OpenUI<PanelGamePlay>();

        // UIManager.Instance.OpenUI<PanelLoading>();

        // StartCoroutine(OffPanel());
    }

    private void Clear()
    {
        for (int i = 0; i < items.Count; i++)
            if (items[i]) Destroy(items[i].gameObject);
        items.Clear();
    }

    public void SettingBTN()
    {
        UIManager.Instance.OpenUI<PanelSetting>();
    }

    IEnumerator OffPanel()
    {
        yield return new WaitForSeconds(2.0f);

        UIManager.Instance.CloseUIDirectly<PanelLoading>();
    }
}
