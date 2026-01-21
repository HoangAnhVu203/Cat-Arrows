using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PanelLoading : UICanvas
{
    [Header("Text")]
    public Text uiText;          

    [Header("Config")]
    public string baseText = "Loading";
    public int maxDots = 3;
    public float interval = 0.4f;

    Coroutine loopCR;

    void OnEnable()
    {
        loopCR = StartCoroutine(Loop());
    }

    void OnDisable()
    {
        if (loopCR != null)
            StopCoroutine(loopCR);
    }

    IEnumerator Loop()
    {
        int dot = 0;

        while (true)
        {
            dot = (dot + 1) % (maxDots + 1);

            string dots = new string('.', dot);
            string content = baseText + dots;

            if (uiText) uiText.text = content;

            yield return new WaitForSeconds(interval);
        }
    }

}
