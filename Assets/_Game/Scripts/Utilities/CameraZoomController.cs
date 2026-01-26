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
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Return To Origin")]
    [SerializeField] private bool captureOriginOnFirstUse = true;
    [SerializeField] private float returnDelay = 0f;
    [SerializeField] private float returnDuration = 0.6f;
    [SerializeField] private AnimationCurve returnEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    public bool IsZooming { get; private set; }

    private float overviewSize;
    private Vector3 overviewPos;

    // Origin (gốc)
    private bool originCaptured;
    private Vector3 originPos;
    private float originSize;

    private Coroutine zoomCR;

    void Reset()
    {
        cam = GetComponent<Camera>();
    }

    private void CaptureOriginIfNeeded()
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        if (!originCaptured || !captureOriginOnFirstUse)
        {
            originPos = cam.transform.position;
            originSize = cam.orthographicSize;
            originCaptured = true;
        }
    }

    /// <summary>
    /// Snap camera để nhìn bao quát level.
    /// </summary>
    public void SnapToOverviewOf(Transform levelRoot)
    {
        if (!cam) cam = Camera.main;
        if (!cam || levelRoot == null) return;

        // Lưu gốc trước khi đổi camera
        CaptureOriginIfNeeded();

        var b = CalcBounds(levelRoot);
        overviewPos = new Vector3(b.center.x, b.center.y, cam.transform.position.z);

        float aspect = cam.aspect;
        float needHalfH = b.extents.y;
        float needHalfW = b.extents.x / Mathf.Max(0.0001f, aspect);

        overviewSize = Mathf.Max(needHalfH, needHalfW) + paddingWorld;

        cam.transform.position = overviewPos;
        cam.orthographicSize = overviewSize;
    }

    /// <summary>
    /// Zoom từ overview về gameplay size. Xong có thể tự trả về gốc.
    /// </summary>
    public IEnumerator ZoomFromOverviewToGameplayCR(bool returnToOriginAfter = true)
    {
        if (!cam) cam = Camera.main;
        if (!cam) yield break;

        // Bắt tay với BoardPanController để tránh tranh quyền điều khiển camera
        var pan = cam.GetComponent<BoardPanController>();
        if (pan != null) pan.Lock(true);

        // Đảm bảo có gốc (originPos, originSize)
        CaptureOriginIfNeeded();

        IsZooming = true;

        // ===== 1) Zoom overview -> gameplay =====
        {
            float t = 0f;
            float startSize = cam.orthographicSize;
            Vector3 startPos = cam.transform.position;

            float endSize = gameplaySize;
            Vector3 endPos = startPos; // nếu muốn zoom vào điểm khác thì set tại đây

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
        }

        IsZooming = false;

        // ===== 2) Delay (nếu có) =====
        if (returnDelay > 0f)
            yield return new WaitForSecondsRealtime(returnDelay);

        // ===== 3) Luôn return về gốc =====
        // (bạn muốn "sau khi zoom luôn về gốc", nên bỏ phụ thuộc param)
        yield return ReturnToOriginCR();

        // ===== 4) Đồng bộ lại BoardPan theo camera hiện tại (đang ở gốc) =====
        if (pan != null)
        {
            pan.SyncToCurrentCameraAsOrigin();
            pan.Lock(false);
        }
    }


    /// <summary>
    /// Trả camera về gốc (position + size).
    /// </summary>
    public IEnumerator ReturnToOriginCR()
    {
        if (!cam) cam = Camera.main;
        if (!cam) yield break;
        if (!originCaptured) yield break;

        IsZooming = true;

        float t = 0f;
        float startSize = cam.orthographicSize;
        Vector3 startPos = cam.transform.position;

        float endSize = originSize;
        Vector3 endPos = new Vector3(originPos.x, originPos.y, cam.transform.position.z);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, returnDuration);
            float k = returnEase.Evaluate(Mathf.Clamp01(t));

            cam.orthographicSize = Mathf.Lerp(startSize, endSize, k);
            cam.transform.position = Vector3.Lerp(startPos, endPos, k);

            yield return null;
        }

        cam.orthographicSize = endSize;
        cam.transform.position = endPos;

        IsZooming = false;
    }

    /// <summary>
    /// Nếu bạn muốn cập nhật gốc thủ công (ví dụ đổi scene / đổi camera setup).
    /// </summary>
    public void ForceSetOriginNow()
    {
        originCaptured = false;
        CaptureOriginIfNeeded();
    }

    private Bounds CalcBounds(Transform root)
    {
        var rs = root.GetComponentsInChildren<Renderer>(true);
        if (rs.Length > 0)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        var cs = root.GetComponentsInChildren<Collider2D>(true);
        if (cs.Length > 0)
        {
            var b = cs[0].bounds;
            for (int i = 1; i < cs.Length; i++) b.Encapsulate(cs[i].bounds);
            return b;
        }

        return new Bounds(root.position, Vector3.one * 5f);
    }

    // Helper nếu bạn muốn gọi kiểu "PlayZoom(levelRoot)"
    public void PlayZoomSequence(Transform levelRoot, bool returnToOriginAfter = true)
    {
        if (zoomCR != null) StopCoroutine(zoomCR);
        SnapToOverviewOf(levelRoot);
        zoomCR = StartCoroutine(ZoomFromOverviewToGameplayCR(returnToOriginAfter));
    }
}
