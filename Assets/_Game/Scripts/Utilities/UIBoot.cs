using System.Collections;
using UnityEngine;

public class UIBoot : MonoBehaviour
{
    private IEnumerator Start()
    {
        // Lần đầu tiên mở game -> tutorial level 0
        if (GameManager.Instance != null && !GameManager.Instance.IsBootTutorialDone())
        {
            GameManager.Instance.SetLoading(true);
            GameManager.Instance.SetBootTutorial(true);

            LevelManager.Instance.LoadBootLevel0(); // load level index 0 (không save progress

            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield break;
        }

        // Các lần sau -> flow cũ (mở home UI)
        if (LevelManager.Instance != null)
            LevelManager.Instance.InitNormalProgressOnly();

        UIManager.Instance.OpenUI<PanelHome>();
        UIManager.Instance.OpenUI<PanelCalendar>();
        UIManager.Instance.OpenUI<PanelLock>();
        UIManager.Instance.OpenUI<FooterTabBar>();
        UIManager.Instance.CloseUIDirectly<PanelCalendar>();

        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
    }
}
