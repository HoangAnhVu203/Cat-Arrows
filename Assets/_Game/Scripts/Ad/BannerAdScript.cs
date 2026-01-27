using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using GoogleMobileAds.Api;
using System;
public class BannerAdScript : MonoBehaviour
{




 private BannerView bannerView;
    
    [Header("AdMob Settings")]


        public string adUnitIdAndroid = ""; // Test Ad
    public string adUnitIdIOS = "ca-app-pub-1650520002983936/3160864150";     // Test Ad
   
    public AdPosition adPosition = AdPosition.Bottom;

void Start()
{



StartCoroutine(LoadBannerAfterDelay());
        //           if (PlayerPrefs.GetInt("NoAds", 0) != 1)
        // {
              
        // }

}

IEnumerator LoadBannerAfterDelay()
{
    yield return new WaitForSeconds(4f);


    RequestBanner();
}

    

    public void RequestBanner()
    {
#if UNITY_ANDROID
        string adUnitId = adUnitIdAndroid;
#elif UNITY_IOS
        string adUnitId = adUnitIdIOS;
#else
        string adUnitId = "unexpected_platform";
#endif

        // Hủy banner cũ nếu tồn tại
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }

        // Tạo banner
        bannerView = new BannerView(adUnitId, AdSize.Banner, adPosition);

        // Đăng ký event handlers đúng signature mới
        bannerView.OnBannerAdLoaded += HandleOnBannerAdLoaded;
        bannerView.OnBannerAdLoadFailed += HandleOnBannerAdLoadFailed;
        bannerView.OnAdClicked += HandleOnAdClicked;
        bannerView.OnAdImpressionRecorded += HandleOnAdImpressionRecorded;
        bannerView.OnAdFullScreenContentOpened += HandleOnAdFullScreenOpened;
        bannerView.OnAdFullScreenContentClosed += HandleOnAdFullScreenClosed;
        bannerView.OnAdPaid += HandleOnAdPaid;

        // Load banner
     // Tạo AdRequest
AdRequest request = new AdRequest(); // Thay vì AdRequest.Builder().Build()
bannerView.LoadAd(request);

    }

    #region Event Handlers

    private void HandleOnBannerAdLoaded()
    {
        Debug.Log("Banner loaded successfully.");
    }

    private void HandleOnBannerAdLoadFailed(LoadAdError error)
    {
        Debug.LogError("Banner failed to load: " + error.GetMessage());
        // Thử load lại sau 10 giây
        Invoke(nameof(RequestBanner), 10f);
    }

    private void HandleOnAdClicked()
    {
        Debug.Log("Banner clicked.");
    }

    private void HandleOnAdImpressionRecorded()
    {
        Debug.Log("Banner impression recorded.");
    }

    private void HandleOnAdFullScreenOpened()
    {
        Debug.Log("Banner opened fullscreen content.");
    }

    private void HandleOnAdFullScreenClosed()
    {
        Debug.Log("Banner closed fullscreen content.");
    }

    private void HandleOnAdPaid(AdValue adValue)
    {
        Debug.Log("Banner earned value: " + adValue.Value);
    }

    #endregion

    private void OnDestroy()
    {
        if (bannerView != null)
        {
            bannerView.Destroy();
            bannerView = null;
        }
    }
}