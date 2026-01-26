using System.Collections;
using UnityEngine;

public class CameraZoomController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Camera cam;

    [Header("Sizes")]
    [SerializeField] private float paddingWorld = 1.5f;
    [SerializeField] private float gameplaySize = 7f;

    [Header("Timing")]
    [SerializeField] private float duration = 1.0f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);

    public bool IsZooming { get; private set; }

    private float overviewSize;
    private Vector3 overviewPos;

    void Reset()
    {
        cam = GetComponent<Camera>();
    }

    public void SnapToOverviewOf(Transform levelRoot)
    {
        if (!cam) cam = Camera.main;
        var b = CalcBounds(levelRoot);
        overviewPos = new Vector3(b.center.x, b.center.y, cam.transform.position.z);

        float aspect = cam.aspect;
        float needHalfH = b.extents.y;
        float needHalfW = b.extents.x / aspect;

        overviewSize = Mathf.Max(needHalfH, needHalfW) + paddingWorld;

        cam.transform.position = overviewPos;
        cam.orthographicSize = overviewSize;
    }

    public IEnumerator ZoomFromOverviewToGameplayCR()
    {
        if (!cam) yield break;

        IsZooming = true;

        float t = 0f;
        float startSize = cam.orthographicSize;
        Vector3 startPos = cam.transform.position;

        float endSize = gameplaySize;
        Vector3 endPos = startPos; // nếu bạn muốn zoom vào 1 điểm khác, set tại đây

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
            float k = ease.Evaluate(Mathf.Clamp01(t));

            cam.orthographicSize = Mathf.Lerp(startSize, endSize, k);
            cam.transform.position = Vector3.Lerp(startPos, endPos, k);

            yield return null;
        }

        cam.orthographicSize = endSize;
        cam.transform.position = endPos;

        IsZooming = false;
    }

    private Bounds CalcBounds(Transform root)
    {
        // Ưu tiên Renderer bounds
        var rs = root.GetComponentsInChildren<Renderer>(true);
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        // Fallback Collider bounds
        var cs = root.GetComponentsInChildren<Collider2D>(true);
        if (cs.Length > 0)
        {
            var b = cs[0].bounds;
            for (int i = 1; i < cs.Length; i++) b.Encapsulate(cs[i].bounds);
            return b;
        }

        return new Bounds(root.position, Vector3.one * 5f);
    }
}
