using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("Network Check")]
    public float checkInterval = 2f;

    bool isOfflinePanelShown = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(CheckNetworkLoop());
    }

    IEnumerator CheckNetworkLoop()
    {
        var wait = new WaitForSecondsRealtime(checkInterval);

        while (true)
        {
            bool hasInternet = HasInternet();

            if (!hasInternet && !isOfflinePanelShown)
            {
                isOfflinePanelShown = true;
                UIManager.Instance.OpenUI<PanelMessage>();
                GameManager.Instance.EnterPause();
            }
            else if (hasInternet && isOfflinePanelShown)
            {
                isOfflinePanelShown = false;
                UIManager.Instance.CloseUIDirectly<PanelMessage>();
                GameManager.Instance.EnterResume();
            }

            yield return wait;
        }
    }

    bool HasInternet()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }
}