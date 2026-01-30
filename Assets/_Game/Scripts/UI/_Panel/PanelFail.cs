using UnityEngine;

public class PanelFail : UICanvas
{
    public void RePlayBTN()
    {
        LevelManager.Instance.ReplayLevel();
        Destroy(gameObject);
        AudioManager.Instance.PlayButton();
    }
}
