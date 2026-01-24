using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TabTransitionController : MonoBehaviour
{
    public enum Tab { Lock = 0, Home = 1, Calendar = 2 }

    [Header("Panels")]
    [SerializeField] private RectTransform panelContainer;
    [SerializeField] private GameObject lockPanelGO;
    [SerializeField] private GameObject homePanelGO;
    [SerializeField] private GameObject calendarPanelGO;

    [Header("Sliders")]
    [SerializeField] private UIPanelSlider lockSlider;
    [SerializeField] private UIPanelSlider homeSlider;
    [SerializeField] private UIPanelSlider calendarSlider;

    [Header("Start")]
    [SerializeField] private Tab startTab = Tab.Home;

    private bool busy;
    private Tab current;
    private bool initialized;     // đã init layout chưa

    private float PanelWidth
    {
        get
        {
            float w = panelContainer != null ? panelContainer.rect.width : 0f;
            return w > 1f ? w : Screen.width;
        }
    }

    private bool IsBootTutorialPhase()
    {
        // Phase tutorial: chưa done (lần đầu) HOẶC GameManager đang bật BootTutorialActive
        // Ưu tiên dùng PlayerPrefs cho chắc chắn.
        if (GameManager.Instance == null) return false;
        if (!GameManager.Instance.IsBootTutorialDone()) return true;
        if (GameManager.Instance.BootTutorialActive) return true;
        return false;
    }

    private void Start()
    {
        // Không spawn UI trong tutorial.
        // Nhưng vẫn có thể init nếu UI đã tồn tại (ví dụ bạn đã mở Home rồi).
        TryBindExistingInstancesOnly();

        // Chỉ init layout nếu đủ ref (đã có panel instances)
        TryInitIfReady();
    }

    // ==== PUBLIC API: gọi sau khi bạn mở Home/Calendar/Lock xong ====
    public void RefreshAfterUIOpened()
    {
        // Sau tutorial hoặc sau khi bạn OpenUI các panel
        EnsureAndBindInstances(); // lúc này mới được phép spawn nếu cần
        TryInitIfReady(force: true);
    }

    // ========= Switch =========
    public void SwitchToLock() => StartCoroutine(SwitchRoutine(Tab.Lock));
    public void SwitchToHome() => StartCoroutine(SwitchRoutine(Tab.Home));
    public void SwitchToCalendar() => StartCoroutine(SwitchRoutine(Tab.Calendar));

    private IEnumerator SwitchRoutine(Tab target)
    {
        if (busy) yield break;
        if (!initialized) yield break;                 // chưa init thì khỏi chạy
        if (target == current) yield break;

        busy = true;
        EventSystem.current?.SetSelectedGameObject(null);

        float w = PanelWidth;

        EnsureActive(current, true);
        EnsureActive(target, true);

        SnapLayoutForCurrent(current, w);

        int curIndex = TabIndex(current);
        int tarIndex = TabIndex(target);
        int delta = tarIndex - curIndex;

        // Slide current out
        if (current == Tab.Lock)
        {
            if (lockSlider != null) lockSlider.Slide(Vector2.zero, new Vector2(-delta * w, 0));
        }
        else if (current == Tab.Home)
        {
            if (homeSlider != null) homeSlider.Slide(Vector2.zero, new Vector2(-delta * w, 0));
        }
        else
        {
            if (calendarSlider != null) calendarSlider.Slide(Vector2.zero, new Vector2(-delta * w, 0));
        }

        // Slide target in
        if (target == Tab.Lock)
        {
            if (lockSlider != null) lockSlider.Slide(new Vector2(delta * w, 0), Vector2.zero);
        }
        else if (target == Tab.Home)
        {
            if (homeSlider != null) homeSlider.Slide(new Vector2(delta * w, 0), Vector2.zero);
        }
        else
        {
            if (calendarSlider != null) calendarSlider.Slide(new Vector2(delta * w, 0), Vector2.zero);
        }

        float wait = GetDurationForTarget(target);
        yield return new WaitForSecondsRealtime(wait);

        EnsureActive(Tab.Lock, target == Tab.Lock);
        EnsureActive(Tab.Home, target == Tab.Home);
        EnsureActive(Tab.Calendar, target == Tab.Calendar);

        ApplyStartTabSnap(target, w);

        current = target;
        busy = false;
    }

    // ========= Init / Layout =========
    private void TryInitIfReady(bool force = false)
    {
        if (initialized && !force) return;

        // cần đủ 3 panel + slider để hoạt động ổn
        if (lockPanelGO == null || homePanelGO == null || calendarPanelGO == null) return;
        if (lockSlider == null || homeSlider == null || calendarSlider == null) return;

        initialized = true;

        current = startTab;

        if (lockPanelGO != null) lockPanelGO.SetActive(current == Tab.Lock);
        if (homePanelGO != null) homePanelGO.SetActive(current == Tab.Home);
        if (calendarPanelGO != null) calendarPanelGO.SetActive(current == Tab.Calendar);

        float w = PanelWidth;

        ApplyStartTabSnap(current, w);
    }

    private void ApplyStartTabSnap(Tab tab, float w)
    {
        if (tab == Tab.Home)
        {
            if (homeSlider != null) homeSlider.Snap(Vector2.zero);
            if (lockSlider != null) lockSlider.Snap(new Vector2(-w, 0));
            if (calendarSlider != null) calendarSlider.Snap(new Vector2(w, 0));
        }
        else if (tab == Tab.Lock)
        {
            if (lockSlider != null) lockSlider.Snap(Vector2.zero);
            if (homeSlider != null) homeSlider.Snap(new Vector2(w, 0));
            if (calendarSlider != null) calendarSlider.Snap(new Vector2(2f * w, 0));
        }
        else
        {
            if (calendarSlider != null) calendarSlider.Snap(Vector2.zero);
            if (homeSlider != null) homeSlider.Snap(new Vector2(-w, 0));
            if (lockSlider != null) lockSlider.Snap(new Vector2(-2f * w, 0));
        }
    }

    private void SnapLayoutForCurrent(Tab cur, float w) => ApplyStartTabSnap(cur, w);

    private void EnsureActive(Tab tab, bool on)
    {
        switch (tab)
        {
            case Tab.Lock: if (lockPanelGO != null) lockPanelGO.SetActive(on); break;
            case Tab.Home: if (homePanelGO != null) homePanelGO.SetActive(on); break;
            case Tab.Calendar: if (calendarPanelGO != null) calendarPanelGO.SetActive(on); break;
        }
    }

    private int TabIndex(Tab t) => t == Tab.Lock ? -1 : (t == Tab.Home ? 0 : 1);

    private float GetDurationForTarget(Tab t)
    {
        if (t == Tab.Lock && lockSlider != null) return lockSlider.Duration;
        if (t == Tab.Home && homeSlider != null) return homeSlider.Duration;
        if (t == Tab.Calendar && calendarSlider != null) return calendarSlider.Duration;
        return 0.22f;
    }

    // ========= Binding =========

    // 1) Tutorial: chỉ bind cái nào đã tồn tại sẵn trong scene/canvas (KHÔNG instantiate)
    private void TryBindExistingInstancesOnly()
    {
        // tìm instance đang tồn tại
        var lockUI = FindObjectOfType<PanelLock>(true);
        var homeUI = FindObjectOfType<PanelHome>(true);
        var calUI  = FindObjectOfType<PanelCalendar>(true);

        if (lockUI != null)
        {
            lockPanelGO = lockUI.gameObject;
            lockSlider = lockUI.GetComponent<UIPanelSlider>();
        }
        if (homeUI != null)
        {
            homePanelGO = homeUI.gameObject;
            homeSlider = homeUI.GetComponent<UIPanelSlider>();
        }
        if (calUI != null)
        {
            calendarPanelGO = calUI.gameObject;
            calendarSlider = calUI.GetComponent<UIPanelSlider>();
        }
    }

    // 2) Sau tutorial: được phép spawn bằng UIManager (flow cũ)
    private void EnsureAndBindInstances()
    {
        // Trong tutorial phase: không spawn
        if (IsBootTutorialPhase())
        {
            TryBindExistingInstancesOnly();
            return;
        }

        // bình thường: đảm bảo có instance
        var lockUI = UIManager.Instance.GetUI<PanelLock>();
        var homeUI = UIManager.Instance.GetUI<PanelHome>();
        var calUI  = UIManager.Instance.GetUI<PanelCalendar>();

        if (lockUI != null)
        {
            lockPanelGO = lockUI.gameObject;
            lockSlider = lockUI.GetComponent<UIPanelSlider>();
        }
        if (homeUI != null)
        {
            homePanelGO = homeUI.gameObject;
            homeSlider = homeUI.GetComponent<UIPanelSlider>();
        }
        if (calUI != null)
        {
            calendarPanelGO = calUI.gameObject;
            calendarSlider = calUI.GetComponent<UIPanelSlider>();
        }
    }
}
