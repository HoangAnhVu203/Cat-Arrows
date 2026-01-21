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

    private float PanelWidth
    {
        get
        {
            float w = panelContainer != null ? panelContainer.rect.width : 0f;
            return w > 1f ? w : Screen.width;
        }
    }

    private void Start()
    {
        BindRuntimeInstances();

        current = startTab;

        // Active đúng panel start
        if (lockPanelGO != null) lockPanelGO.SetActive(current == Tab.Lock);
        if (homePanelGO != null) homePanelGO.SetActive(current == Tab.Home);
        if (calendarPanelGO != null) calendarPanelGO.SetActive(current == Tab.Calendar);

        // Snap vị trí chuẩn ban đầu:
        // Lock ở -w, Home ở 0, Calendar ở +w
        float w = PanelWidth;

        if (lockSlider != null) lockSlider.Snap(new Vector2(-w, 0));
        if (homeSlider != null) homeSlider.Snap(Vector2.zero);
        if (calendarSlider != null) calendarSlider.Snap(new Vector2(w, 0));

        // Nếu startTab không phải Home, đưa target về 0 (để nhìn đúng)
        // (đơn giản nhất: snap lại theo tab start)
        ApplyStartTabSnap(current, w);
    }

    private void ApplyStartTabSnap(Tab tab, float w)
    {
        if (tab == Tab.Home)
        {
            // Home ở giữa
            if (homeSlider != null) homeSlider.Snap(Vector2.zero);
            if (lockSlider != null) lockSlider.Snap(new Vector2(-w, 0));
            if (calendarSlider != null) calendarSlider.Snap(new Vector2(w, 0));
        }
        else if (tab == Tab.Lock)
        {
            // Lock ở giữa (0), Home ở +w, Calendar ở +2w (đẩy ra xa để không lộ)
            if (lockSlider != null) lockSlider.Snap(Vector2.zero);
            if (homeSlider != null) homeSlider.Snap(new Vector2(w, 0));
            if (calendarSlider != null) calendarSlider.Snap(new Vector2(2f * w, 0));
        }
        else // Calendar
        {
            // Calendar ở giữa (0), Home ở -w, Lock ở -2w
            if (calendarSlider != null) calendarSlider.Snap(Vector2.zero);
            if (homeSlider != null) homeSlider.Snap(new Vector2(-w, 0));
            if (lockSlider != null) lockSlider.Snap(new Vector2(-2f * w, 0));
        }
    }

    public void SwitchToLock() => StartCoroutine(SwitchRoutine(Tab.Lock));
    public void SwitchToHome() => StartCoroutine(SwitchRoutine(Tab.Home));
    public void SwitchToCalendar() => StartCoroutine(SwitchRoutine(Tab.Calendar));

    private IEnumerator SwitchRoutine(Tab target)
    {
        if (busy) yield break;
        if (target == current) yield break;

        busy = true;
        EventSystem.current?.SetSelectedGameObject(null);

        float w = PanelWidth;

        // Bật các panel cần để animate (ít nhất là current + target)
        EnsureActive(current, true);
        EnsureActive(target, true);

        // Khi đi qua Home từ Lock <-> Calendar (nhảy 2 bước),
        // ta vẫn animate trực tiếp (đẩy panel đi xa hơn 1w).
        // Quy ước: vị trí "center" = 0 của target.

        // Tính vị trí start/end theo current/target
        // Mỗi tab có "index": Lock=-1, Home=0, Calendar=+1
        int curIndex = TabIndex(current);
        int tarIndex = TabIndex(target);

        // delta = tarIndex - curIndex
        int delta = tarIndex - curIndex;

        // Animate: tất cả panels đang active sẽ dịch theo delta*w để target về 0
        // Cụ thể: panel của current đang ở 0 -> đi sang -delta*w
        // panel của target đang ở delta*w -> đi về 0
        // Ta chỉ cần slide 2 panel (current & target) để đơn giản,
        // các panel còn lại snap ra ngoài để tránh lộ.

        // Chuẩn hóa vị trí hiện tại trước khi chạy (tránh lệch khi width đổi)
        SnapLayoutForCurrent(current, w);

        // Slide current ra khỏi center
        if (current == Tab.Lock)
        {
            // Lock đang ở 0, đi sang trái (nếu qua Home/Calendar) hoặc sang phải (nếu về Home)
            if (lockSlider != null) lockSlider.Slide(Vector2.zero, new Vector2(-delta * w, 0));
        }
        else if (current == Tab.Home)
        {
            if (homeSlider != null) homeSlider.Slide(Vector2.zero, new Vector2(-delta * w, 0));
        }
        else // current == Calendar
        {
            if (calendarSlider != null) calendarSlider.Slide(Vector2.zero, new Vector2(-delta * w, 0));
        }

        // Slide target vào center (từ delta*w về 0)
        if (target == Tab.Lock)
        {
            if (lockSlider != null) lockSlider.Slide(new Vector2(delta * w, 0), Vector2.zero);
        }
        else if (target == Tab.Home)
        {
            if (homeSlider != null) homeSlider.Slide(new Vector2(delta * w, 0), Vector2.zero);
        }
        else // target == Calendar
        {
            if (calendarSlider != null) calendarSlider.Slide(new Vector2(delta * w, 0), Vector2.zero);
        }

        float wait = GetDurationForTarget(target);
        yield return new WaitForSecondsRealtime(wait);

        // Tắt panel không phải target (và không cần giữ current nữa)
        EnsureActive(Tab.Lock, target == Tab.Lock);
        EnsureActive(Tab.Home, target == Tab.Home);
        EnsureActive(Tab.Calendar, target == Tab.Calendar);

        // Snap lại layout chuẩn theo target để lần sau không lệch
        ApplyStartTabSnap(target, w);

        current = target;
        busy = false;
    }

    private void SnapLayoutForCurrent(Tab cur, float w)
    {
        // Đưa layout về đúng trạng thái "cur ở giữa" trước khi animate,
        // tránh tình trạng width thay đổi hoặc bị lệch anchoredPosition.
        ApplyStartTabSnap(cur, w);
    }

    private void EnsureActive(Tab tab, bool on)
    {
        switch (tab)
        {
            case Tab.Lock:
                if (lockPanelGO != null) lockPanelGO.SetActive(on);
                break;
            case Tab.Home:
                if (homePanelGO != null) homePanelGO.SetActive(on);
                break;
            case Tab.Calendar:
                if (calendarPanelGO != null) calendarPanelGO.SetActive(on);
                break;
        }
    }

    private int TabIndex(Tab t)
    {
        // Lock = -1, Home = 0, Calendar = +1
        return t == Tab.Lock ? -1 : (t == Tab.Home ? 0 : 1);
    }

    private float GetDurationForTarget(Tab t)
    {
        // lấy duration của slider target (fallback 0.22)
        if (t == Tab.Lock && lockSlider != null) return lockSlider.Duration;
        if (t == Tab.Home && homeSlider != null) return homeSlider.Duration;
        if (t == Tab.Calendar && calendarSlider != null) return calendarSlider.Duration;
        return 0.22f;
    }

    public void BindRuntimeInstances()
    {
        // Bạn cần có PanelLock trong UIManager
        var lockUI = UIManager.Instance.GetUI<PanelLock>();
        var home   = UIManager.Instance.GetUI<PanelHome>();
        var cal    = UIManager.Instance.GetUI<PanelCalendar>();

        if (lockUI != null) lockPanelGO = lockUI.gameObject;
        if (home != null) homePanelGO = home.gameObject;
        if (cal != null) calendarPanelGO = cal.gameObject;

        if (lockUI != null) lockSlider = lockUI.GetComponent<UIPanelSlider>();
        if (home != null) homeSlider = home.GetComponent<UIPanelSlider>();
        if (cal != null) calendarSlider = cal.GetComponent<UIPanelSlider>();

        // panelContainer = RectTransform parent chứa 3 panel (Canvas-Main/PanelContainer)
    }
}
