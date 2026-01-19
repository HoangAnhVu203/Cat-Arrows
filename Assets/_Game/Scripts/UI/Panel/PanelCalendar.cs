using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelCalendar : UICanvas
{
    [SerializeField, Range(0.1f, 1f)] private float futureAlpha = 0.35f;

    [Header("Month Nav")]
    [SerializeField] private Button prevMonthBtn;
    [SerializeField] private Button nextMonthBtn;

    [Header("Play")]
    [SerializeField] private Button playBtn;

    [Header("Refs")]
    public Transform[] weeks;       // 6 week rows, each row has 7 cells
    public Text MonthAndYear;       // header text

    // Calendar state
    public DateTime currDate = DateTime.Now; // viewing month (always day=1)
    private DateTime selectedDate;           // selected day (green)

    [Serializable]
    public class Day
    {
        public int dayNum;     // zero-based inside current month (0 = 1st)
        public GameObject obj;

        private Image circle;
        private Text label;
        private Button btn;

        public Day(int dayNum, GameObject obj)
        {
            this.obj = obj;
            this.dayNum = dayNum;

            circle = obj.GetComponent<Image>();
            label = obj.GetComponentInChildren<Text>(true);
            btn = obj.GetComponent<Button>();

            SetOutOfMonth(true);
        }

        public void SetOutOfMonth(bool outOfMonth)
        {
            // Hide circle + label for out-of-month
            if (circle) circle.enabled = !outOfMonth;
            if (label)  label.enabled  = !outOfMonth;

            if (btn)
            {
                btn.onClick.RemoveAllListeners();
                btn.interactable = !outOfMonth;
            }
        }

        public void SetDayNumber(int zeroBasedDay)
        {
            dayNum = zeroBasedDay;
            if (label != null) label.text = (dayNum + 1).ToString();
        }

        public void SetupClick(bool enable, int zeroBasedDay, Action<int> onClick)
        {
            if (btn == null) btn = obj.GetComponent<Button>();
            if (btn == null) return;

            btn.onClick.RemoveAllListeners();

            if (!enable)
            {
                btn.interactable = false;
                return;
            }

            btn.interactable = true;
            btn.onClick.AddListener(() => onClick?.Invoke(zeroBasedDay));
        }

        public void SetNormal()
        {
            if (circle) circle.color = new Color(1f, 1f, 1f, 1f);
            if (label)
            {
                var c = label.color;
                c.a = 1f;
                label.color = c;
            }
        }

        public void SetSelected()
        {
            if (circle) circle.color = Color.green;
            if (label)
            {
                var c = label.color;
                c.a = 1f;
                label.color = c;
            }
        }

        public void SetFutureDim(float alpha)
        {
            if (circle)
            {
                var c = circle.color;
                c.a = alpha;
                circle.color = c;
            }
            if (label)
            {
                var c = label.color;
                c.a = alpha;
                label.color = c;
            }
        }

        public void SetWon()
        {
            if (circle) circle.color = new Color(1f, 0.8f, 0.1f, 1f); // vàng
            if (label)
            {
                var c = label.color;
                c.a = 1f;
                label.color = c;
            }
        }

    }

    private readonly List<Day> days = new List<Day>(42);

    private void OnEnable()
    {
        BindButtonsOnce();

        // Init month view at current real month
        DateTime today = DateTime.Today;
        currDate = new DateTime(today.Year, today.Month, 1);

        // Default selected date = today (nếu mở calendar lần đầu)
        if (selectedDate == default)
            selectedDate = today;

        // Nếu selectedDate đang nằm ở tương lai (do lưu nhầm), clamp về today
        if (selectedDate.Date > today)
            selectedDate = today;

        // Nếu selectedDate không cùng tháng với currDate, cố giữ cùng "day" khi vào tháng hiện tại
        selectedDate = ClampSelectedIntoMonth(selectedDate, currDate.Year, currDate.Month);

        UpdateCalendar(currDate.Year, currDate.Month);
        RefreshNavButtons();
        RefreshPlayButton();

    }

    private bool bound;
    private void BindButtonsOnce()
    {
        if (bound) return;
        bound = true;

        if (prevMonthBtn != null)
        {
            prevMonthBtn.onClick.RemoveAllListeners();
            prevMonthBtn.onClick.AddListener(OnPrevMonth);
        }

        if (nextMonthBtn != null)
        {
            nextMonthBtn.onClick.RemoveAllListeners();
            nextMonthBtn.onClick.AddListener(OnNextMonth);
        }

        if (playBtn != null)
        {
            playBtn.onClick.RemoveAllListeners();
            playBtn.onClick.AddListener(OnPlayDaily);
        }
    }

    // ===================== Calendar Build =====================

    private void UpdateCalendar(int year, int month)
    {
        currDate = new DateTime(year, month, 1);

        // Clamp selected date into this month (giữ cùng ngày nếu có, không có thì về ngày cuối tháng)
        selectedDate = ClampSelectedIntoMonth(selectedDate, year, month);

        // Header shows selected day like: January 08, 2026
        if (MonthAndYear != null)
            MonthAndYear.text = selectedDate.ToString("MMMM dd, yyyy");

        int startDay = GetMonthStartDay(year, month); // Sunday=0..Saturday=6
        int daysInMonth = DateTime.DaysInMonth(year, month);

        // Create Day objects once
        if (days.Count == 0)
        {
            if (weeks == null || weeks.Length < 6)
            {
                Debug.LogError("[PanelCalendar] weeks must have 6 rows.");
                return;
            }

            for (int w = 0; w < 6; w++)
            {
                if (weeks[w] == null || weeks[w].childCount < 7)
                {
                    Debug.LogError($"[PanelCalendar] weeks[{w}] must have 7 children.");
                    return;
                }

                for (int i = 0; i < 7; i++)
                {
                    GameObject cellObj = weeks[w].GetChild(i).gameObject;
                    days.Add(new Day(0, cellObj));
                }
            }
        }

        DateTime today = DateTime.Today;

        for (int cell = 0; cell < 42; cell++)
        {
            bool outOfMonth = (cell < startDay) || (cell - startDay >= daysInMonth);

            if (outOfMonth)
            {
                days[cell].SetOutOfMonth(true);
                continue;
            }

            int zeroBasedDay = cell - startDay; // 0..daysInMonth-1
            int dayNumber = zeroBasedDay + 1;
            DateTime cellDate = new DateTime(year, month, dayNumber);

            days[cell].SetOutOfMonth(false);
            days[cell].SetDayNumber(zeroBasedDay);

            bool isFuture = cellDate.Date > today;
            bool isWon = DailyProgress.IsWin(cellDate);

            bool canClick = !isFuture;
            days[cell].SetupClick(canClick, zeroBasedDay, OnDayClicked);

            if (cellDate.Date == selectedDate.Date)
            {
                days[cell].SetSelected();      
            }
            else if (isWon)
            {
                days[cell].SetWon();          
            }
            else if (isFuture)
            {
                days[cell].SetFutureDim(futureAlpha);
            }
            else
            {
                days[cell].SetNormal();
            }

        }

        RefreshPlayButton();
    }

    private void OnDayClicked(int zeroBasedDay)
    {
        selectedDate = new DateTime(currDate.Year, currDate.Month, zeroBasedDay + 1);

        if (selectedDate.Date > DateTime.Today)
            selectedDate = DateTime.Today;

        if (MonthAndYear != null)
            MonthAndYear.text = selectedDate.ToString("MMMM dd, yyyy");

        // rebuild to update highlight + dim
        UpdateCalendar(currDate.Year, currDate.Month);
    }

    private DateTime ClampSelectedIntoMonth(DateTime currentSelected, int year, int month)
    {
        // giữ "ngày" đang chọn nếu có thể, không có thì clamp xuống ngày cuối tháng
        int maxDay = DateTime.DaysInMonth(year, month);
        int d = Mathf.Clamp(currentSelected.Day, 1, maxDay);
        return new DateTime(year, month, d);
    }

    private int GetMonthStartDay(int year, int month)
    {
        DateTime temp = new DateTime(year, month, 1);
        return (int)temp.DayOfWeek; // Sunday=0..Saturday=6
    }

    // ===================== Month Navigation =====================

    private void OnPrevMonth()
    {
        currDate = currDate.AddMonths(-1);

        // giữ selected day theo cùng "ngày" khi qua tháng trước
        selectedDate = ClampSelectedIntoMonth(selectedDate, currDate.Year, currDate.Month);

        UpdateCalendar(currDate.Year, currDate.Month);
        RefreshNavButtons();
    }

    private void OnNextMonth()
    {
        if (!CanGoNextMonth()) return;

        currDate = currDate.AddMonths(1);

        // giữ selected day theo cùng "ngày" khi qua tháng sau
        selectedDate = ClampSelectedIntoMonth(selectedDate, currDate.Year, currDate.Month);

        UpdateCalendar(currDate.Year, currDate.Month);
        RefreshNavButtons();
    }

    private bool CanGoNextMonth()
    {
        // Max allowed month is the real current month
        DateTime maxMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime next = currDate.AddMonths(1);
        return next <= maxMonth;
    }

    private void RefreshNavButtons()
    {
        if (nextMonthBtn != null)
            nextMonthBtn.interactable = CanGoNextMonth();
    }

    // ===================== Play Daily =====================

    private void RefreshPlayButton()
    {
        if (playBtn == null) return;

        // không cho play ngày tương lai
        playBtn.interactable = selectedDate.Date <= DateTime.Today;
    }

    private void OnPlayDaily()
    {
        if (LevelManager.Instance == null)
        {
            Debug.LogError("[PanelCalendar] LevelManager.Instance is null");
            return;
        }

        LevelManager.Instance.SetCurrentDailyDate(selectedDate);
        LevelManager.Instance.LoadDailyByDay(selectedDate.Day);

        if (selectedDate.Date > DateTime.Today) return;

        // Daily level = dayOfMonth (1..31)
        int day = selectedDate.Day;

        // Load level daily (31 prefab riêng) - bạn phải có hàm này trong LevelManager
        LevelManager.Instance.LoadDailyByDay(selectedDate.Day);

        // Chuyển UI sang gameplay theo flow của bạn
        UIManager.Instance.CloseUIDirectly<PanelHome>();
        UIManager.Instance.CloseUIDirectly<PanelCalendar>();
        UIManager.Instance.CloseUIDirectly<FooterTabBar>();
        UIManager.Instance.OpenUI<PanelGamePlay>();
    }
}
