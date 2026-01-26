using System;
using System.Collections;
using CandyCoded.HapticFeedback;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : Singleton<GameManager>
{
    public enum GameState { GamePlay, Win, Fail, Pause }

    private GameState currentState = GameState.GamePlay;
    public GameState CurrentState => currentState;

    [Header("Heart")]
    [SerializeField] private int maxHeart = 3;
    private PanelLoading loadingPanel;
    private int currentHeart;
    private int movingLineCount = 0;
    private bool winTriggered = false;

    [SerializeField] private GameObject VFX1;
    [SerializeField] private GameObject VFX2;

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
    private const string PREF_BOOT_TUTORIAL_DONE = "BOOT_TUTORIAL_DONE";

        public void SetLoading(bool v)
    {
        IsLoadingLevel = v;

        if (v)
        {
            loadingPanel = UIManager.Instance.OpenUI<PanelLoading>();

            // Đưa lên top ngay
            BringLoadingToFront();

            // Và đưa lên top lại ở cuối frame (đề phòng UI khác mở sau đó)
            StartCoroutine(BringLoadingToFrontEndOfFrame());
        }
        else
        {
            UIManager.Instance.CloseUIDirectly<PanelLoading>();
            loadingPanel = null;
        }
    }


    [Header("Boot Loading")]
    [SerializeField] private float bootLoadingSeconds = 10f;
    private float dailyReturnDelay = 0f;
    private Coroutine returnHomeCR;
    public bool BootTutorialActive { get; private set; }

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

    public bool IsBootTutorialDone()
    {
        return PlayerPrefs.GetInt(PREF_BOOT_TUTORIAL_DONE, 0) == 1;
    }

    public void MarkBootTutorialDone()
    {
        PlayerPrefs.SetInt(PREF_BOOT_TUTORIAL_DONE, 1);
        PlayerPrefs.Save();
    }

    IEnumerator OffPanel()
    {
        yield return new WaitForSeconds(5.0f);

        UIManager.Instance.CloseUIDirectly<PanelLoading>();
    }

    public void SetBootTutorial(bool on)
    {
        BootTutorialActive = on;

        if (on)
        {
            // Trong tutorial: không cho PanelGamePlay bật lên
            UIManager.Instance.CloseUIDirectly<PanelHome>();
            UIManager.Instance.CloseUIDirectly<FooterTabBar>();
            UIManager.Instance.CloseUIDirectly<PanelCalendar>();
            UIManager.Instance.CloseUIDirectly<PanelWin>();
            UIManager.Instance.CloseUIDirectly<PanelFail>();
            UIManager.Instance.CloseUIDirectly<PanelGamePlay>();
        }
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
        ResetLevelRuntimeState();

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

        if (!BootTutorialActive)
            UIManager.Instance.OpenUI<PanelGamePlay>();

        // ZOOM IN
        var camZoom = Camera.main?.GetComponent<CameraZoomController>();
        camZoom?.ZoomFromOverviewToGameplayCR();

        Debug.Log("[GameState] GamePlay");
    }



   void EnterWin()
    {
        Time.timeScale = 1f;

        // Nếu là tutorial level 0: về home + set progress -> level 1
        if (BootTutorialActive)
        {
            StartCoroutine(ReturnHomeAfterTutorialCR());
            return;
        }
        VFX1.SetActive(true);
        VFX2.SetActive(true);

        StartCoroutine(WaitWin());
        // UIManager.Instance.OpenUI<PanelWin>();
        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();

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

        if (BootTutorialActive)
        {
            StartCoroutine(ReturnHomeAfterTutorialCR());
            return;
        }

        UIManager.Instance.OpenUI<PanelFail>();
        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();

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

        // Nếu win đã trigger từ StartMove thì bỏ qua
        if (winTriggered) return;

        if (IsLoadingLevel) return;

        if (currentState == GameState.GamePlay &&
            currentHeart > 0 &&
            activeLineCount == 0)
        {
            winTriggered = true;
            ChangeState(GameState.Win);
        }
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

     public void BringLoadingToFront()
    {
        if (loadingPanel == null) return;

        // Đưa lên top theo sibling
        loadingPanel.transform.SetAsLastSibling();

        // Nếu PanelLoading có Canvas override sorting thì cũng set luôn
        var c = loadingPanel.GetComponent<Canvas>();
        if (c != null && c.overrideSorting)
            c.sortingOrder = 9999;
    }

    private IEnumerator ReturnHomeAfterTutorialCR()
    {
        SetLoading(true);

        UIManager.Instance.CloseUIDirectly<PanelWin>();
        UIManager.Instance.CloseUIDirectly<PanelFail>();
        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();

        if (LevelManager.Instance != null)
            LevelManager.Instance.ClearLevelOnly();

        yield return new WaitForSecondsRealtime(1.0f);

        // QUAN TRỌNG: đánh dấu đã xong tutorial (để lần sau không vào nữa)
        MarkBootTutorialDone();

        // Set progress sang level 1 (index=1) để Play sau đó chạy như cũ
        if (LevelManager.Instance != null)
            LevelManager.Instance.ForceSetNormalProgressIndex(1);

        SetBootTutorial(false);

        UIManager.Instance.OpenUI<PanelHome>();
         UIManager.Instance.OpenUI<PanelCalendar>();
        UIManager.Instance.OpenUI<FooterTabBar>();
        var tab = FindObjectOfType<TabTransitionController>(true);
        if (tab != null) tab.RefreshAfterUIOpened();

        UIManager.Instance.CloseUIDirectly<PanelCalendar>();

        SetLoading(false);
    }

    IEnumerator WaitWin()
    {
        yield return new WaitForSeconds(0.5f);

        UIManager.Instance.OpenUI<PanelWin>();
    }

    public void NotifyLineStartMove()
    {
        if (currentState != GameState.GamePlay) return;
        if (IsLoadingLevel) return;
        if (winTriggered) return;
        if (currentHeart <= 0) return;

        if (activeLineCount == 1)
        {
            winTriggered = true;

            ChangeState(GameState.Win);
        }
    }



    public void ResetLevelRuntimeState()
    {
        movingLineCount = 0;
        winTriggered = false;
    }

    private IEnumerator BringLoadingToFrontEndOfFrame()
    {
        yield return null; // cuối frame
        BringLoadingToFront();
    }

}
