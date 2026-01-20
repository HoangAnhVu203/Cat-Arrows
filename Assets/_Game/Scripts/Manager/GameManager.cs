using System;
using System.Collections;
using CandyCoded.HapticFeedback;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    public enum GameState { GamePlay, Win, Fail, Pause }

    private GameState currentState = GameState.GamePlay;
    public GameState CurrentState => currentState;

    [Header("Heart")]
    [SerializeField] private int maxHeart = 3;
    private int currentHeart;

    public int MaxHeart => maxHeart;
    public int CurrentHeart => currentHeart;

    public event Action<int, int> OnHeartChanged;
    public event Action<GameState> OnStateChanged;
    public event Action<bool> OnShowPathChanged;
    public event Action<bool> OnHintChanged;

    private int activeLineCount = 0;

    public bool IsLoadingLevel { get; private set; }
    public bool EraseMode { get; private set; }
    public bool ShowPathMode { get; private set; }
    public bool HintMode { get; private set; }

    public void SetLoading(bool v) => IsLoadingLevel = v;

    [Header("Boot Loading")]
    [SerializeField] private float bootLoadingSeconds = 10f;
    private float dailyReturnDelay = 0f;
    private Coroutine returnHomeCR;


    private bool booted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        //if (booted) return;
        //booted = true;

        //StartCoroutine(BootRoutine());
        UIManager.Instance.OpenUI<PanelLoading>();
        StartCoroutine(OffPanel());
    }

    IEnumerator OffPanel()
    {
        yield return new WaitForSeconds(5.0f);

        UIManager.Instance.CloseUIDirectly<PanelLoading>();
    }

    //private IEnumerator BootRoutine()
    //{
    //    // 1) Bật Loading trước
    //    var loading = UIManager.Instance.OpenUI<PanelLoading>();

    //    // Đảm bảo Loading nằm trên cùng (quan trọng)
    //    if (loading != null) loading.transform.SetAsLastSibling();

    //    // 2) Load save / init data
    //    LevelManager.Instance?.LoadSavedLevel();

    //    // 3) Chờ 2 giây realtime (không bị ảnh hưởng bởi timeScale)
    //    yield return new WaitForSecondsRealtime(10f);

    //    // 4) Tắt Loading
    //    UIManager.Instance.CloseUIDirectly<PanelLoading>();

    //    // 5) Mở UI chính sau khi loading tắt
    //    UIManager.Instance.OpenUI<PanelHome>();
    //    UIManager.Instance.OpenUI<PanelCalendar>();
    //    UIManager.Instance.OpenUI<FooterTabBar>();

    //    UIManager.Instance.CloseUIDirectly<PanelCalendar>();

    //    // Nếu bạn muốn chắc chắn layout ổn: chờ 1 frame rồi ép update
    //    yield return null;
    //    Canvas.ForceUpdateCanvases();
    //}

    // ===================== LEVEL =====================
    public void StartLevel()
    {
        currentHeart = maxHeart;
        ChangeState(GameState.GamePlay);

        OnHeartChanged?.Invoke(currentHeart, maxHeart);
    }

    // ===================== STATE MACHINE =====================
    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        OnStateChanged?.Invoke(currentState);

        switch (currentState)
        {
            case GameState.GamePlay: EnterGamePlay(); break;
            case GameState.Win: EnterWin(); break;
            case GameState.Fail: EnterFail(); break;
            case GameState.Pause: EnterPause(); break;
        }
    }

    private void EnterGamePlay()
    {
        Time.timeScale = 1f;
        UIManager.Instance.OpenUI<PanelGamePlay>();
        Debug.Log("[GameState] GamePlay");
    }

   void EnterWin()
    {
        Time.timeScale = 1f;

        UIManager.Instance.OpenUI<PanelWin>();
        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();

        // DAILY: auto về home
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentMode == LevelManager.LevelMode.Daily)
        {
            StartReturnHomeFromDaily();
            var d = LevelManager.Instance.CurrentDailyDate;
            if (d != default)
                DailyProgress.SetWin(d, true);
        }
            

        Debug.Log("[GameState] WIN");
    }

    void EnterFail()
    {
        Time.timeScale = 1f;

        UIManager.Instance.OpenUI<PanelFail>();
        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();

        // DAILY: auto về home
        if (LevelManager.Instance != null && LevelManager.Instance.CurrentMode == LevelManager.LevelMode.Daily)
            StartReturnHomeFromDaily();

        Debug.Log("[GameState] FAIL");
    }

    public void EnterPause()
    {
        Time.timeScale = 0f;
        Debug.Log("[GameState] PAUSE");
    }

    public void EnterResume()
    {
        Time.timeScale = 1f;
    }

    // ===================== HEART LOGIC =====================
    public void LoseHeart()
    {
        if (currentState != GameState.GamePlay) return;

        currentHeart = Mathf.Max(0, currentHeart - 1);
        OnHeartChanged?.Invoke(currentHeart, maxHeart);
        
        HapticFeedback.MediumFeedback();

        Debug.Log($"Heart left: {currentHeart}");

        if (currentHeart <= 0)
            ChangeState(GameState.Fail);
    }

    public void RegisterLine()
    {
        activeLineCount++;
    }

    public void UnregisterLine()
    {
        activeLineCount = Mathf.Max(0, activeLineCount - 1);

        if (IsLoadingLevel) return;
        if (currentState == GameState.GamePlay && currentHeart > 0 && activeLineCount == 0)
            ChangeState(GameState.Win);
    }

    public void SetActiveLineCount(int count)
    {
        activeLineCount = Mathf.Max(0, count);
    }

    // === Mode Game ===
    public void SetEraseMode(bool on) => EraseMode = on;

    public void SetShowPathMode(bool on)
    {
        if (ShowPathMode == on) return;
        ShowPathMode = on;
        OnShowPathChanged?.Invoke(on);
    }

    public void SetHintMode(bool on)
    {
        if (HintMode == on) return;
        HintMode = on;
        OnHintChanged?.Invoke(on);
    }

    private void StartReturnHomeFromDaily()
    {
        if (returnHomeCR != null) StopCoroutine(returnHomeCR);
        returnHomeCR = StartCoroutine(ReturnHomeFromDailyCR());
    }

    private IEnumerator ReturnHomeFromDailyCR()
    {
        // Cho UI Win/Fail kịp bật 1 chút
        yield return new WaitForSecondsRealtime(dailyReturnDelay);

        // Đóng kết quả + gameplay
        UIManager.Instance.CloseUIDirectly<PanelWin>();
        UIManager.Instance.CloseUIDirectly<PanelFail>();
        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();

        // QUAN TRỌNG: quay về Home UI
        UIManager.Instance.OpenUI<PanelHome>();
        UIManager.Instance.OpenUI<FooterTabBar>();

        UIManager.Instance.CloseUIDirectly<PanelCalendar>();

        returnHomeCR = null;
    }

}
