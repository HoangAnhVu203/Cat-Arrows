using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PanelWin : UICanvas
{
    [Header("Stars (size = 3)")]
    [SerializeField] private List<Image> stars; 

    [Header("Optional Sprites")]
    [SerializeField] private Sprite starOn;
    [SerializeField] private Sprite starOff;

    [SerializeField] private Text winTxt;
    string[] winMessages =
    {
        "Well Done",
        "Excellent",
        "Great Job",
        "Awesome",
        "Perfect",
        "Nice Move",
        "Fantastic"
    };

    void OnEnable()
    {
        RefreshStars();
        RefreshWinText();
    }

    public void RefreshStars()
    {
        if (stars == null || stars.Count == 0) return;

        int heartLeft = 0;
        if (GameManager.Instance != null)
            heartLeft = Mathf.Clamp(GameManager.Instance.CurrentHeart, 0, stars.Count);

        // option 1: bật/tắt sao
        if (starOn == null || starOff == null)
        {
            for (int i = 0; i < stars.Count; i++)
            {
                if (!stars[i]) continue;
                stars[i].enabled = (i < heartLeft);
            }
        }
        // option 2: đổi sprite sao sáng/sao rỗng
        else
        {
            for (int i = 0; i < stars.Count; i++)
            {
                if (!stars[i]) continue;
                stars[i].enabled = true;
                stars[i].sprite = (i < heartLeft) ? starOn : starOff;
            }
        }

        Debug.Log($"[PanelWin] Hearts left = {heartLeft} => Stars = {heartLeft}");
    }
   public void NextLVBTN()
   {
    AdService.ShowInterstitial(
    onAdClosed: () =>
    {
        Debug.Log("Ad closed → resume after ad");
    },
    onNoAd: () =>
    {
        Debug.Log("No ad → alternate flow");
    }
);
       LevelManager.Instance.NextLevel();
       Destroy(gameObject);
   }

    void RefreshWinText()
    {
        if (winTxt == null || winMessages == null || winMessages.Length == 0)
            return;

        int idx = Random.Range(0, winMessages.Length);
        winTxt.text = winMessages[idx];
    }




}
