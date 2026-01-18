using UnityEngine.EventSystems;

public static class UIUtils
{
    public static void ClearCurrentSelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }
}
