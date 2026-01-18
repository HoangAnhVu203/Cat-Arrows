using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FooterTabBar : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button button;
        public RectTransform root;   // RectTransform của Tab (thường là chính Button)
        public RectTransform icon;   // RectTransform icon
        public GameObject labelGO;   // Label object (Text/TMP)
    }

    [Header("Refs")]
    [SerializeField] private RectTransform selection;  // nền trượt
    [SerializeField] private RectTransform selectionParent; // thường là FooterBar hoặc cùng parent với selection
    [SerializeField] private List<Tab> tabs = new();

    [Header("Anim")]
    [SerializeField] private float slideDuration = 0.18f;
    [SerializeField] private float scaleUp = 1.12f;
    [SerializeField] private float scaleDuration = 0.12f;

    [Header("Selection Size")]
    [SerializeField] private bool matchTabWidth = true;
    [SerializeField] private float selectionWidthPadding = 20f; // bớt/đệm thêm tùy thiết kế
    [SerializeField] private int defaultIndex = 1;

    [Header("Icon Motion")]
    [SerializeField] private float iconMoveUp = 18f;
    [SerializeField] private float iconAnimDuration = 0.12f;
    [SerializeField] private float iconBaseY = 0f;


    private int currentIndex = -1;
    private Coroutine slideCo;

    private void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            var b = tabs[i].button;
            if (b != null)
            {
                var nav = new Navigation { mode = Navigation.Mode.None };
                b.navigation = nav;

                int idx = i;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => Select(idx, true));
            }
        }
    }

    private void OnEnable()
    {
        StartCoroutine(ResetDefaultNextFrame());
    }
    private IEnumerator ResetDefaultNextFrame()
    {
        yield return null; 
        Select(defaultIndex, false);
    }

    private void Start()
    {
        //// Set initial state (Home = 0)
        //Select(Mathf.Clamp(0, 0, tabs.Count - 1), false);
        ////Select(defaultIndex, false);
    }
    public void SelectHome(bool animate) => Select(1, animate); // Home index = 1

    public void Select(int index, bool animate)
    {
        if (tabs == null || tabs.Count == 0) return;
        index = Mathf.Clamp(index, 0, tabs.Count - 1);
        if (index == currentIndex) return;

        // Update tabs visuals
        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = (i == index);

            // Label
            if (tabs[i].labelGO != null)
                tabs[i].labelGO.SetActive(active);

            // Icon: scale + move
            if (tabs[i].icon != null)
            {
                StartCoroutine(AnimateIcon(tabs[i].icon, active));
            }
        }


        // Move selection
        Vector2 targetPos = GetSelectionTargetAnchoredPos(tabs[index].root);

        // Resize selection to match tab width (optional)
        if (matchTabWidth && tabs[index].root != null)
        {
            float w = tabs[index].root.rect.width + selectionWidthPadding;
            var size = selection.sizeDelta;
            size.x = w;
            selection.sizeDelta = size;
        }

        // Animate slide (or snap)
        if (slideCo != null) StopCoroutine(slideCo);
        slideCo = StartCoroutine(animate ? SlideTo(targetPos) : SnapTo(targetPos));

        currentIndex = index;
    }

    private Vector2 GetSelectionTargetAnchoredPos(RectTransform tabRoot)
    {
        // Chuyển vị trí tab sang anchoredPosition trong hệ selectionParent
        // Cách làm: lấy world position của tab rồi đổi về local của selectionParent
        Vector3 world = tabRoot.TransformPoint(tabRoot.rect.center);
        Vector3 local = selectionParent.InverseTransformPoint(world);

        // Giữ y của selection (để không nhảy lên xuống)
        return new Vector2(local.x, selection.anchoredPosition.y);
    }

    private IEnumerator SnapTo(Vector2 target)
    {
        selection.anchoredPosition = target;
        yield break;
    }

    private IEnumerator SlideTo(Vector2 target)
    {
        Vector2 start = selection.anchoredPosition;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, slideDuration);
            float k = EaseOutCubic(t);
            selection.anchoredPosition = Vector2.LerpUnclamped(start, target, k);
            yield return null;
        }

        selection.anchoredPosition = target;
    }

    private float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        float a = 1f - x;
        return 1f - a * a * a;
    }

    private IEnumerator AnimateIcon(RectTransform icon, bool active)
    {
        Vector3 startScale = icon.localScale;
        Vector3 targetScale = active ? Vector3.one * scaleUp : Vector3.one;

        Vector2 startPos = icon.anchoredPosition;

        float targetY = active ? (iconBaseY + iconMoveUp) : iconBaseY;
        Vector2 targetPos = new Vector2(startPos.x, targetY);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, iconAnimDuration);
            float k = EaseOutCubic(t);

            icon.localScale = Vector3.LerpUnclamped(startScale, targetScale, k);
            icon.anchoredPosition = Vector2.LerpUnclamped(startPos, targetPos, k);
            yield return null;
        }

        icon.localScale = targetScale;
        icon.anchoredPosition = targetPos;
    }

}
