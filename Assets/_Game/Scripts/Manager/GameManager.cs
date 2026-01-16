using System;
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
    public event System.Action<bool> OnShowPathChanged;
    public event System.Action<bool> OnHintChanged;

    private int activeLineCount = 0;
    public bool IsLoadingLevel { get; private set; }
    public bool EraseMode { get; private set; }
    public bool ShowPathMode { get; private set; }
    public bool HintMode {get; private set;}
    public void SetLoading(bool v) => IsLoadingLevel = v;
    
   


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Lưu ý: StartLevel sẽ bắn event, nên UI sẽ update ngay.
        LevelManager.Instance.LoadSavedLevel();
        UIManager.Instance.OpenUI<PanelGamePlay>();
    }

    // ===================== LEVEL =====================
    public void StartLevel()
    {
        currentHeart = maxHeart; 
        ChangeState(GameState.GamePlay);

        // bắn event để UI update
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

    void EnterGamePlay()
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
        Debug.Log("[GameState] WIN");
    }

    void EnterFail()
    {
        Time.timeScale = 1f;
        UIManager.Instance.OpenUI<PanelFail>();
        UIManager.Instance.CloseUIDirectly<PanelGamePlay>();
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

        Debug.Log($"Heart left: {currentHeart}");

        if (currentHeart <= 0)
            ChangeState(GameState.Fail);
    }
    public void RegisterLine()
    {
        activeLineCount++;
    }

    // ===================== LINE MANAGEMENT =====================
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
    public void SetEraseMode(bool on)
    {
        EraseMode = on;
    }

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
}
