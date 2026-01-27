using UnityEngine;
using System;
using System.Collections;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;

#region BOOTSTRAP
public static class AdServiceBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        if (GameObject.Find("AdServiceRuntime") != null) return;

        var go = new GameObject("AdServiceRuntime");
        UnityEngine.Object.DontDestroyOnLoad(go);

        go.AddComponent<InterstitialAdManager>();
        go.AddComponent<RewardedAdManager>();
        go.AddComponent<AdServiceRuntime>();

        Debug.Log("[AdServiceBootstrap] Runtime initialized.");
    }
}
#endregion

#region RUNTIME SERVICE CONTAINER
public class AdServiceRuntime : MonoBehaviour
{
    public static AdServiceRuntime Instance;

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
#endregion

/*-------------------------------------------------------------
 *  INTERSTITIAL MANAGER
 *-------------------------------------------------------------*/
#region INTERSTITIAL MANAGER
public class InterstitialAdManager : MonoBehaviour
{
    public static InterstitialAdManager Instance;

#if UNITY_ANDROID
    [SerializeField] private string adUnitId = "";
#elif UNITY_IOS
    [SerializeField] private string adUnitId = "ca-app-pub-1650520002983936/1757127039";

#else
    [SerializeField] private string adUnitId = "unused";
#endif

    private InterstitialAd _ad;
    private Action _onClosed;

    private int _retries = 0;
    private const int MAX_RETRIES = 3;
    private const float BASE_DELAY = 2f;
    private const float MAX_DELAY = 30f;

    private DateTime _lastShown = DateTime.MinValue;
    private readonly TimeSpan COOLDOWN = TimeSpan.FromSeconds(45);

    private const float RELOAD_AFTER_CLOSE = 45f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
          

            LoadAd();
        }
        else Destroy(gameObject);
    }

    public bool IsReady() =>
        _ad != null && _ad.CanShowAd();

public void Show(Action onAdClosed, Action onNoAd)
{
    if (DateTime.Now - _lastShown < COOLDOWN)
    {
        Debug.Log("[Interstitial] Cooldown → no ad");
        onNoAd?.Invoke();
        return;
    }

    if (IsReady())
    {
        _onClosed = onAdClosed;
        _ad.Show();
    }
    else
    {
        Debug.Log("[Interstitial] Not ready");
        onNoAd?.Invoke();
        LoadAd();
    }
}


    public void LoadAd()
    {
     

    

        if (_ad != null)
        {
            _ad.Destroy();
            _ad = null;
        }

        var request = new AdRequest();
        InterstitialAd.Load(adUnitId, request, (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning("[Interstitial] Load failed: " + error);

                if (_retries < MAX_RETRIES)
                {
                    _retries++;
                    float delay = Mathf.Min(MAX_DELAY, BASE_DELAY * Mathf.Pow(2, _retries - 1));
                    StartCoroutine(LoadDelayed(delay));
                    return;
                }

                Debug.LogWarning("[Interstitial] Retry limit reached.");
                return;
            }

            _ad = ad;
            _retries = 0;

            ad.OnAdFullScreenContentClosed += OnClosed;
            ad.OnAdFullScreenContentFailed += _ =>
            {
                _onClosed?.Invoke();
                LoadAd();
            };

            Debug.Log("[Interstitial] Loaded.");
        });
    }

    private void OnClosed()
    {
        _lastShown = DateTime.Now;

        _onClosed?.Invoke();
        _onClosed = null;

        _ad?.Destroy();
        _ad = null;

        StartCoroutine(LoadDelayed(RELOAD_AFTER_CLOSE));
    }

    private IEnumerator LoadDelayed(float sec)
    {
        yield return new WaitForSeconds(sec);
        LoadAd();
    }
}
#endregion

/*-------------------------------------------------------------
 *  REWARDED MANAGER
 *-------------------------------------------------------------*/
#region REWARDED MANAGER
public class RewardedAdManager : MonoBehaviour
{
    public static RewardedAdManager Instance;

#if UNITY_ANDROID
    [SerializeField] private string adUnitId = "";
#elif UNITY_IOS
    [SerializeField] private string adUnitId = "ca-app-pub-1650520002983936/6345633954";

#else
    [SerializeField] private string adUnitId = "unused";
#endif

    private RewardedAd _ad;
    private Action _onEarned;
    private Action _onClosed;

    private int _retries = 0;
    private const int MAX_RETRIES = 3;
    private const float BASE_DELAY = 2f;
    private const float MAX_DELAY = 30f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadAd();
        }
        else Destroy(gameObject);
    }

    public bool IsReady() =>
        _ad != null && _ad.CanShowAd();

    public void Show(Action onEarned, Action onClosed)
    {
        if (!IsReady())
        {
            Debug.Log("[Rewarded] Not ready");
            onClosed?.Invoke();
            LoadAd();
            return;
        }

        _onEarned = onEarned;
        _onClosed = onClosed;

        _ad.Show(reward =>
        {
            _onEarned?.Invoke();
        });
    }

    public void LoadAd()
    {
        if (_ad != null)
        {
            _ad.Destroy();
            _ad = null;
        }

        RewardedAd.Load(adUnitId, new AdRequest(), (ad, err) =>
        {
            if (err != null || ad == null)
            {
                Debug.LogWarning("[Rewarded] Load failed: " + err);

                if (_retries < MAX_RETRIES)
                {
                    _retries++;
                    float delay = Mathf.Min(MAX_DELAY, BASE_DELAY * Mathf.Pow(2, _retries - 1));
                    StartCoroutine(LoadDelayed(delay));
                    return;
                }

                Debug.LogWarning("[Rewarded] Retry limit reached.");
                return;
            }

            _ad = ad;
            _retries = 0;

            ad.OnAdFullScreenContentClosed += OnClosed;
            ad.OnAdFullScreenContentFailed += _ =>
            {
                _onClosed?.Invoke();
                LoadAd();
            };

            Debug.Log("[Rewarded] Loaded.");
        });
    }

    private void OnClosed()
    {
        _onClosed?.Invoke();
        _onClosed = null;
        _onEarned = null;

        _ad?.Destroy();
        _ad = null;

        StartCoroutine(LoadDelayed(0f));
    }

    private IEnumerator LoadDelayed(float sec)
    {
        yield return new WaitForSeconds(sec);
        LoadAd();
    }
}
#endregion

/*-------------------------------------------------------------
 *  PUBLIC API FOR GAMEPLAY
 *-------------------------------------------------------------*/
#region AD SERVICE API
public static class AdService
{
public static void ShowInterstitial(
    Action onAdClosed,
    Action onNoAd
)
{
    if (InterstitialAdManager.Instance == null)
    {
        onNoAd?.Invoke();
        return;
    }

    InterstitialAdManager.Instance.Show(onAdClosed, onNoAd);
}


    public static bool IsInterstitialReady()
        => InterstitialAdManager.Instance != null &&
           InterstitialAdManager.Instance.IsReady();

    public static void ShowRewarded(Action onEarned, Action onClosed = null)
        => RewardedAdManager.Instance?.Show(onEarned, onClosed);

    public static bool IsRewardedReady()
        => RewardedAdManager.Instance != null &&
           RewardedAdManager.Instance.IsReady();
}
#endregion
