using System.Collections;
using UnityEngine;

public class UIPanelSlider : MonoBehaviour
{
    [SerializeField] private RectTransform rect;
    [SerializeField] private float duration = 0.22f;
    private Coroutine co;

    private void Reset() => rect = GetComponent<RectTransform>();

    public float Duration => duration;

    public void Snap(Vector2 pos)
    {
        if (co != null) StopCoroutine(co);
        rect.anchoredPosition = pos;
    }

    public void Slide(Vector2 from, Vector2 to)
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Run(from, to));
    }

    IEnumerator Run(Vector2 from, Vector2 to)
    {
        rect.anchoredPosition = from;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
            float k = EaseOutCubic(t);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, k);
            yield return null;
        }
        rect.anchoredPosition = to;
        co = null;
    }

    float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        float a = 1f - x;
        return 1f - a * a * a;
    }
}
