using UnityEngine;

public class PanelWin : UICanvas
{
   public void NextLVBTN()
   {
       LevelManager.Instance.NextLevel();
       Destroy(gameObject);
   }
}
