using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    // ===================== STATE =====================

    public enum GameState
    {
        GamePlay,
        Win,
        Fail,
        Pause
    }
    private GameState currentState = GameState.GamePlay;
    public GameState CurrentState => currentState;


    // ===================== HEART =====================
    private int maxHeart = 3;
    private int currentHeart;

    // ===================== LINE COUNT =====================

    private int activeLineCount = 0;

    // ===================== UNITY =====================

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
        StartLevel();
        LevelManager.Instance.LoadFirstLevel();
        UIManager.Instance.OpenUI<PanelGamePlay>();
    }

    // ===================== LEVEL =====================

    public void StartLevel()
    {
        currentHeart = maxHeart;
        activeLineCount = 0;

        ChangeState(GameState.GamePlay);
    }

    // ===================== STATE MACHINE =====================

    public void ChangeState(GameState newState)
    {
        if (currentState == newState) return;

        currentState = newState;

        switch (currentState)
        {
            case GameState.GamePlay:
                EnterGamePlay();
                break;

            case GameState.Win:
                EnterWin();
                break;

            case GameState.Fail:
                EnterFail();
                break;

            case GameState.Pause:
                EnterPause();
                break;
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

    void EnterPause()
    {
        Time.timeScale = 0f;

        Debug.Log("[GameState] PAUSE");
    }

    // ===================== HEART LOGIC =====================

    /// <summary>
    /// Gọi khi line bị block
    /// </summary>
    public void LoseHeart()
    {
        if (currentState != GameState.GamePlay) return;

        currentHeart--;
        currentHeart = Mathf.Max(0, currentHeart);

        Debug.Log($"Heart left: {currentHeart}");

        if (currentHeart <= 0)
        {
            ChangeState(GameState.Fail);
        }
    }

    // ===================== LINE MANAGEMENT =====================

    /// <summary>
    /// Line gọi khi spawn
    /// </summary>
    public void RegisterLine()
    {
        activeLineCount++;
    }

    /// <summary>
    /// Line gọi khi destroy / bay ra ngoài
    /// </summary>
    public void UnregisterLine()
    {
        activeLineCount = Mathf.Max(0, activeLineCount - 1);

        if (currentState == GameState.GamePlay &&
            currentHeart > 0 &&
            activeLineCount == 0)
        {
            ChangeState(GameState.Win);
        }
    }

    public void SetActiveLineCount(int count)
    {
        activeLineCount = Mathf.Max(0, count);
    }


}
