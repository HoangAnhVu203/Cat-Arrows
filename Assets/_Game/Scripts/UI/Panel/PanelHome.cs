using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelHome : UICanvas
{
    [Header("Scroll")]
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private HomeLevelItemUI itemPrefab;

    [Header("Buttons")]
    [SerializeField] private Button playButton;

    [Header("Config")]
    [SerializeField] private int showAround = 2;

    readonly List<HomeLevelItemUI> items = new();

    void OnEnable()
    {
        BuildList();

        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClick);
        }

        StartCoroutine(ScrollToCurrentCR());
    }

    void OnDisable()
    {
        if (playButton != null)
            playButton.onClick.RemoveAllListeners();
    }

    void BuildList()
    {
        Clear();

        int total = LevelManager.Instance != null ? LevelManager.Instance.TotalLevels : 0;
        int currentIndex = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelNumber : 1; 

        if (total <= 0 || itemPrefab == null || content == null) return;

        for (int lv = 1; lv <= total; lv++)
        {
            var it = Instantiate(itemPrefab, content);
            it.name = $"LevelItem_{lv:000}";
            it.Setup(lv, currentIndex);
            items.Add(it);
        }
    }

    System.Collections.IEnumerator ScrollToCurrentCR()
    {
        yield return null; // chờ layout

        int currentIndex1 = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelNumber : 1; // 1-based
        int idx0 = Mathf.Clamp(currentIndex1 - 1, 0, items.Count - 1);

        // Đưa item current về giữa viewport
        Canvas.ForceUpdateCanvases();

        if (scroll == null || scroll.viewport == null) yield break;

        float contentH = content.rect.height;
        float viewH = scroll.viewport.rect.height;
        if (contentH <= viewH + 0.01f)
        {
            scroll.verticalNormalizedPosition = 1f;
            yield break;
        }

        // Tính vị trí normalized dựa theo anchoredPosition của item
        float y = Mathf.Abs(items[idx0].GetComponent<RectTransform>().anchoredPosition.y);
        float maxScroll = contentH - viewH;

        float norm = 1f - Mathf.Clamp01(y / maxScroll);
        scroll.verticalNormalizedPosition = norm;
    }

    void OnPlayClick()
    {
        // Vào màn game: load level current (đã lưu) rồi mở gameplay
        UIManager.Instance.CloseUIDirectly<PanelHome>();
        UIManager.Instance.OpenUI<PanelGamePlay>();

        // Nếu bạn có scene gameplay riêng thì ở đây load scene.
        // Nếu cùng scene: chỉ cần LevelManager.LoadSavedLevel/LoadLevelByIndex(current)
        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadSavedLevel(); // bạn đã có hàm này
    }

    void Clear()
    {
        for (int i = 0; i < items.Count; i++)
            if (items[i] != null) Destroy(items[i].gameObject);
        items.Clear();
    }
}
