using System.Collections;
using UnityEngine;

public class UIBoot : MonoBehaviour
{
    [SerializeField] private float loadingSeconds = 5.0f;

    private IEnumerator Start()
    {
        // 1) Loading
        //var loading = UIManager.Instance.OpenUI<PanelLoading>();
        //if (loading != null) loading.transform.SetAsLastSibling();

        // 2) Chỉ init progress, KHÔNG load level prefab
        if (LevelManager.Instance != null)
            LevelManager.Instance.InitNormalProgressOnly();

        // 3) Mở UI chính
        UIManager.Instance.OpenUI<PanelHome>();
        UIManager.Instance.OpenUI<PanelCalendar>();
        UIManager.Instance.OpenUI<PanelLock>();
        UIManager.Instance.OpenUI<FooterTabBar>();
        UIManager.Instance.CloseUIDirectly<PanelCalendar>();

        // 4) Chờ UI/layout
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();

        //// 5) Giữ loading đủ lâu để nhìn thấy (tuỳ bạn)
        //if (loadingSeconds > 0f)
        //    yield return new WaitForSecondsRealtime(loadingSeconds);

        //// 6) Tắt loading
        //UIManager.Instance.CloseUIDirectly<PanelLoading>();
    }
}
