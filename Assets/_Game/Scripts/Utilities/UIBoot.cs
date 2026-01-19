using System.Collections;
using UnityEngine;

public class UIBoot : MonoBehaviour
{
    private IEnumerator Start()
    {
        // 1) Mở Loading trước
        UIManager.Instance.OpenUI<PanelLoading>();

        // 2) Load dữ liệu cần thiết (level save, etc.)
        if (LevelManager.Instance != null)
            LevelManager.Instance.LoadSavedLevel();

        // 3) Mở UI chính
        UIManager.Instance.OpenUI<PanelHome>();
        UIManager.Instance.OpenUI<PanelCalendar>();      // nếu bạn dùng tab
        UIManager.Instance.CloseUIDirectly<PanelCalendar>();
        UIManager.Instance.OpenUI<FooterTabBar>();

        // 4) Chờ UI build + layout xong
        //    - chờ ít nhất 2 frame (để OnEnable/Start + LayoutGroup rebuild)
        yield return null;
        yield return null;

        // 5) Ép canvas update lần cuối (đảm bảo layout/anchoring đã ổn)
        Canvas.ForceUpdateCanvases();

        // 6) Tắt Loading
        UIManager.Instance.CloseUIDirectly<PanelLoading>();
    }
}
