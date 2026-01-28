using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PanelLoading : UICanvas
{
    [Header("Text")]
    public Text uiText;

    [Header("Config")]
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
        {
            StopCoroutine(loopCR);
            loopCR = null;
        }
    }

    IEnumerator Loop()
    {
        int dot = 0;

        while (true)
        {
            dot++;

            if (dot > maxDots)
                dot = 1;

            if (uiText != null)
                uiText.text = string.Join(" ", new string('.', dot).ToCharArray());

            yield return new WaitForSeconds(interval);
        }
    }
}
