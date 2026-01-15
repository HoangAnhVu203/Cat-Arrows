using UnityEngine;
using UnityEngine.UI;

public class HideForeverOnClick : MonoBehaviour
{
    [Header("Image + Button")]
    [SerializeField] private GameObject banner;
    [SerializeField] private GameObject button;

    private const string KEY = "HIDE_TUTORIAL_FOREVER";

    void Start()
    {
        if (PlayerPrefs.GetInt(KEY, 0) == 1)
        {
            Destroy(banner);
            Destroy(button);
        }
    }

    public void OnButtonClick()
    {
        PlayerPrefs.SetInt(KEY, 1);
        PlayerPrefs.Save();

        Destroy(banner);
        Destroy(button);
    }
}
