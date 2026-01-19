using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class TabTransitionController : MonoBehaviour
{
    public enum Tab { Lock = 0, Home = 1, Calendar = 2 }

    [Header("Panels")]
    [SerializeField] private RectTransform panelContainer;
    [SerializeField] private GameObject homePanelGO;
    [SerializeField] private GameObject calendarPanelGO;

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
            return w > 1f ? w : Screen.width; // fallback
        }
    }

    private void Start()
    {
        BindRuntimeInstances();
        // Init trạng thái ban đầu
        current = startTab;

        if (homePanelGO != null) homePanelGO.SetActive(current == Tab.Home);
        if (calendarPanelGO != null) calendarPanelGO.SetActive(current == Tab.Calendar);

        // snap vị trí
        if (homeSlider != null) homeSlider.Snap(Vector2.zero);
        if (calendarSlider != null) calendarSlider.Snap(new Vector2(PanelWidth, 0)); // để ngoài phải
    }

    public void SwitchToHome() => StartCoroutine(SwitchRoutine(Tab.Home));
    public void SwitchToCalendar() => StartCoroutine(SwitchRoutine(Tab.Calendar));

    private IEnumerator SwitchRoutine(Tab target)
    {
        if (busy) yield break;
        if (target == current) yield break;

        busy = true;
        EventSystem.current?.SetSelectedGameObject(null);

        float w = PanelWidth;

        // IMPORTANT:
        // Bật tạm cả 2 panel để slider của panel inactive không bị lỗi StartCoroutine
        if (homePanelGO != null && !homePanelGO.activeSelf) homePanelGO.SetActive(true);
        if (calendarPanelGO != null && !calendarPanelGO.activeSelf) calendarPanelGO.SetActive(true);

        if (target == Tab.Calendar)
        {
            // calendar từ phải vào, home trượt sang trái ra
            if (calendarSlider != null) calendarSlider.Slide(new Vector2(w, 0), Vector2.zero);
            if (homeSlider != null) homeSlider.Slide(Vector2.zero, new Vector2(-w, 0));

            yield return new WaitForSecondsRealtime(calendarSlider != null ? calendarSlider.Duration : 0.22f);

            // chỉ tắt panel không phải target
            if (homePanelGO != null) homePanelGO.SetActive(false);

            // reset vị trí home để lần sau vào lại
            if (homeSlider != null) homeSlider.Snap(Vector2.zero);

            current = Tab.Calendar;
        }
        else // target == Tab.Home
        {
            // home từ trái vào, calendar trượt sang phải ra
            if (homeSlider != null) homeSlider.Slide(new Vector2(-w, 0), Vector2.zero);
            if (calendarSlider != null) calendarSlider.Slide(Vector2.zero, new Vector2(w, 0));

            yield return new WaitForSecondsRealtime(homeSlider != null ? homeSlider.Duration : 0.22f);

            if (calendarPanelGO != null) calendarPanelGO.SetActive(false);

            if (calendarSlider != null) calendarSlider.Snap(new Vector2(w, 0));

            current = Tab.Home;
        }

        busy = false;
    }

    public void BindRuntimeInstances()
    {
        var home = UIManager.Instance.GetUI<PanelHome>();
        var cal  = UIManager.Instance.GetUI<PanelCalendar>();

        homePanelGO = home.gameObject;
        calendarPanelGO = cal.gameObject;

        homeSlider = home.GetComponent<UIPanelSlider>();
        calendarSlider = cal.GetComponent<UIPanelSlider>();

        // panelContainer = RectTransform parent chứa 2 panel (ví dụ Canvas-Main hoặc PanelContainer)
    }

}
