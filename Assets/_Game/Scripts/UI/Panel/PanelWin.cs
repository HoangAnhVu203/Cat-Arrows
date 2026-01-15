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

    void OnEnable()
    {
        RefreshStars();
    }

    public void RefreshStars()
    {
        if (stars == null || stars.Count == 0) return;

        int heartLeft = 0;
        if (GameManager.Instance != null)
            heartLeft = Mathf.Clamp(GameManager.Instance.CurrentHeart, 0, stars.Count);

        // Cách A: bật/tắt sao
        if (starOn == null || starOff == null)
        {
            for (int i = 0; i < stars.Count; i++)
            {
                if (!stars[i]) continue;
                stars[i].enabled = (i < heartLeft);
            }
        }
        // Cách B: đổi sprite sao sáng/sao rỗng
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
       LevelManager.Instance.NextLevel();
       Destroy(gameObject);
   }
}
