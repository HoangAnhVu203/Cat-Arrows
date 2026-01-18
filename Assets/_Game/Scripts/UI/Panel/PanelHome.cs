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

    readonly List<HomeLevelItemUI> items = new();

    void OnEnable()
    {
        BuildSixLevels();

        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClick);
        }
    }

    void OnDisable()
    {
        if (playButton != null)
            playButton.onClick.RemoveAllListeners();
    }

    void BuildSixLevels()
    {
        Clear();

        if (LevelManager.Instance == null || itemPrefab == null || content == null) return;

        int total = LevelManager.Instance.TotalLevels;
        int cur = LevelManager.Instance.CurrentLevelNumber;

        int maxShow = Mathf.Min(cur + showNextCount, total);

        for (int lv = cur; lv <= maxShow; lv++)
        {
            var it = Instantiate(itemPrefab, content);
            it.name = $"HomeLevel_{lv:000}";
            it.Setup(lv, cur);
            items.Add(it);
            it.transform.SetAsFirstSibling();
        }

        // ép layout + ép xuống đáy thật sự
        StartCoroutine(FixScrollToBottomNextFrame());
    }

    IEnumerator FixScrollToBottomNextFrame()
    {
        // chờ 1 frame để layout/CSF tính Preferred Size
        yield return null;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // ÉP CONTENT CHẠM ĐÁY VIEWPORT
        // (vì content pivot Y=0 và anchor Y=0 -> y=0 nghĩa là chạm đáy)
        var p = content.anchoredPosition;
        p.y = 0f;
        content.anchoredPosition = p;

        // ÉP SCROLL XUỐNG ĐÁY
        var sr = content.GetComponentInParent<ScrollRect>();
        if (sr)
        {
            sr.StopMovement();
            sr.verticalNormalizedPosition = 0f;
        }
    }


    void OnPlayClick()
    {
        // Vào level hiện tại
        UIManager.Instance.CloseUIDirectly<PanelHome>();
        UIManager.Instance.OpenUI<PanelGamePlay>();

        // Load đúng level current (đã lưu)
        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadSavedLevel();
    }

    void Clear()
    {
        for (int i = 0; i < items.Count; i++)
            if (items[i]) Destroy(items[i].gameObject);
        items.Clear();
    }
}
