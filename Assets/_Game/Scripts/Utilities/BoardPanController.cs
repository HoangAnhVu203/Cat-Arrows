using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BoardPanController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform boardRoot;
    [SerializeField] Camera cam;

    [Header("Enable")]
    public bool enablePan = true;

    [Header("Clamp (world offset from start)")]
    public Vector2 maxOffset = new Vector2(3.5f, 6.0f);

    [Header("Smoothing")]
    [Range(0f, 30f)] public float follow = 18f;
    [Range(0f, 30f)] public float inertia = 10f;

    [Header("Block Pan When Touching Line")]
    [SerializeField] private LayerMask blockPanMask;
    [SerializeField] private float dragStartThresholdPx = 12f;

    [Header("Zoom")]
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float zoomSpeedMouse = 2.0f;     // cuộn chuột
    [SerializeField] private float zoomSpeedPinch = 0.01f;    // pinch
    [SerializeField] private float zoomLerp = 20f;            // mượt zoom
    [SerializeField] private float fitMargin = 0.25f;         // nới thêm biên cho vừa khít

    // Nếu muốn cho zoom-in sâu hơn size ban đầu, set > 1
    [SerializeField] private float maxZoomInMultiplier = 1.0f;

    Vector3 startPos;
    Vector3 targetPos;

    bool dragging;
    Vector3 lastWorld;
    Vector3 velocity;

    bool pendingDrag;
    Vector2 pressScreenPos;
    Vector3 pressWorldPos;
    bool blockThisPress;

    // Zoom runtime
    float orthoInitial;
    float orthoMinFit;      // zoom out tối đa (nhìn rộng nhất) -> size lớn hơn
    float orthoMax;         // zoom in tối đa (nhìn gần nhất)  -> size nhỏ hơn
    float orthoTarget;

    // Pinch state
    bool pinching;
    float lastPinchDist;

    static readonly List<RaycastResult> _uiHits = new List<RaycastResult>(32);

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!boardRoot) boardRoot = transform;

        startPos = boardRoot.position;
        targetPos = startPos;

        SetupZoomLimits();
    }

    void OnEnable()
    {
        SetupZoomLimits(); // phòng khi đổi orientation/aspect
    }

    void Update()
    {
        if (!cam || !boardRoot) return;

        // ===== ZOOM =====
        if (enableZoom)
        {
            HandleZoom();

            if (cam.orthographic)
            {
                cam.orthographicSize = Mathf.Lerp(
                    cam.orthographicSize,
                    orthoTarget,
                    1f - Mathf.Exp(-zoomLerp * Time.unscaledDeltaTime)
                );
            }
        }

        // ===== PAN =====
        if (!enablePan) return;

        // Smooth follow
        boardRoot.position = Vector3.Lerp(
            boardRoot.position,
            targetPos,
            1f - Mathf.Exp(-follow * Time.unscaledDeltaTime)
        );

        // Inertia when not dragging
        if (!dragging && velocity.sqrMagnitude > 0.0001f)
        {
            targetPos += velocity * Time.unscaledDeltaTime;
            velocity = Vector3.Lerp(
                velocity,
                Vector3.zero,
                1f - Mathf.Exp(-inertia * Time.unscaledDeltaTime)
            );
            targetPos = ClampToStart(targetPos);
        }

        // Input PAN (Mouse)
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                BeginDrag(Mouse.current.position.ReadValue());
            else if (Mouse.current.leftButton.isPressed)
                Drag(Mouse.current.position.ReadValue());
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

        // Input PAN (Touch - chỉ khi không pinch)
        if (Touchscreen.current != null)
        {
            var t = Touchscreen.current.primaryTouch;
            if (t.press.wasPressedThisFrame)
                BeginDrag(t.position.ReadValue());
            else if (t.press.isPressed)
                Drag(t.position.ReadValue());
            else if (t.press.wasReleasedThisFrame)
                EndDrag();
        }
    }

    // ===================== ZOOM =====================

    void SetupZoomLimits()
    {
        if (!cam) return;

        if (!cam.orthographic)
        {
            Debug.LogWarning("[BoardPanController] Zoom fit theo maxOffset đang code cho Orthographic camera.");
            return;
        }

        orthoInitial = cam.orthographicSize;

        // “fit” vùng có thể pan: startPos +/- maxOffset
        // Camera orthographicSize là nửa chiều cao nhìn thấy.
        // Nửa chiều rộng nhìn thấy = orthographicSize * aspect
        float aspect = cam.aspect;

        float halfW = maxOffset.x + fitMargin;
        float halfH = maxOffset.y + fitMargin;

        // size cần để vừa khít theo chiều ngang: halfW / aspect
        float needSizeForWidth = halfW / Mathf.Max(0.0001f, aspect);
        float needSizeForHeight = halfH;

        // orthoMinFit = size lớn nhất cần để thấy toàn vùng pan (zoom out tới đây là “khít”)
        orthoMinFit = Mathf.Max(needSizeForWidth, needSizeForHeight);

        // Zoom in tối đa: mặc định về size ban đầu (hoặc sâu hơn nếu multiplier > 1)
        orthoMax = orthoInitial / Mathf.Max(0.0001f, maxZoomInMultiplier);

        // Quy ước:
        // - ortho lớn => zoom out (nhìn rộng)
        // - ortho nhỏ => zoom in  (nhìn gần)
        // Giới hạn: [orthoMax (nhỏ), orthoMinFit (lớn)]
        orthoTarget = Mathf.Clamp(cam.orthographicSize, orthoMax, orthoMinFit);
    }

    void HandleZoom()
    {
        if (!cam || !cam.orthographic) return;

        // Không zoom nếu đang chạm UI
        if (IsPointerBlockedByUI(GetAnyPointerPos()))
        {
            pinching = false;
            return;
        }

        // Pinch zoom (Touch)
        if (Touchscreen.current != null)
        {
            var t0 = Touchscreen.current.touches.Count > 0 ? Touchscreen.current.touches[0] : default;
            var t1 = Touchscreen.current.touches.Count > 1 ? Touchscreen.current.touches[1] : default;

            bool twoTouches = Touchscreen.current.touches.Count >= 2 &&
                              t0.press.isPressed && t1.press.isPressed;

            if (twoTouches)
            {
                Vector2 p0 = t0.position.ReadValue();
                Vector2 p1 = t1.position.ReadValue();
                float dist = Vector2.Distance(p0, p1);

                if (!pinching)
                {
                    pinching = true;
                    lastPinchDist = dist;
                }
                else
                {
                    float delta = dist - lastPinchDist;
                    lastPinchDist = dist;

                    // delta > 0 => 2 ngón tách ra => zoom in (ortho nhỏ lại)
                    // delta < 0 => chụm lại      => zoom out (ortho lớn lên)
                    orthoTarget -= delta * zoomSpeedPinch;
                    orthoTarget = Mathf.Clamp(orthoTarget, orthoMax, orthoMinFit);
                }

                // Khi pinch thì khóa pan
                blockThisPress = true;
                pendingDrag = false;
                dragging = false;

                return;
            }
            else
            {
                pinching = false;
            }
        }

        // Mouse wheel zoom (PC)
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                // scroll dương => thường là cuộn lên => zoom in (ortho giảm)
                orthoTarget -= scroll * 0.01f * zoomSpeedMouse;
                orthoTarget = Mathf.Clamp(orthoTarget, orthoMax, orthoMinFit);
            }
        }
    }

    Vector2 GetAnyPointerPos()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();

        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();

        return Vector2.zero;
    }

    // Public: zoom out “vừa khít” vùng pan
    public void ZoomToFitPanArea()
    {
        if (!cam || !cam.orthographic) return;
        SetupZoomLimits();
        orthoTarget = orthoMinFit;
    }

    // Public: zoom về size ban đầu
    public void ZoomToInitial()
    {
        if (!cam || !cam.orthographic) return;
        SetupZoomLimits();
        orthoTarget = Mathf.Clamp(orthoInitial, orthoMax, orthoMinFit);
    }

    // ===================== PAN =====================

    void BeginDrag(Vector2 screenPos)
    {
        if (IsPointerBlockedByUI(screenPos)) return;

        if (IsPointerBlockedByWorld(screenPos))
        {
            blockThisPress = true;
            pendingDrag = false;
            dragging = false;
            return;
        }

        blockThisPress = false;
        pendingDrag = true;
        dragging = false;

        pressScreenPos = screenPos;
        pressWorldPos = ScreenToWorldPlane(screenPos);

        velocity = Vector3.zero;
        lastWorld = pressWorldPos;
    }

    void Drag(Vector2 screenPos)
    {
        if (blockThisPress) return;

        if (pendingDrag && !dragging)
        {
            float dist = (screenPos - pressScreenPos).magnitude;
            if (dist < dragStartThresholdPx) return;

            dragging = true;
            pendingDrag = false;
            lastWorld = ScreenToWorldPlane(screenPos);
        }

        if (!dragging) return;

        Vector3 w = ScreenToWorldPlane(screenPos);
        Vector3 delta = w - lastWorld;
        lastWorld = w;

        targetPos += delta;
        targetPos = ClampToStart(targetPos);

        velocity = Vector3.Lerp(
            velocity,
            delta / Mathf.Max(0.0001f, Time.unscaledDeltaTime),
            0.25f
        );
    }

    void EndDrag()
    {
        pendingDrag = false;
        dragging = false;
        blockThisPress = false;
    }

    Vector3 ScreenToWorldPlane(Vector2 screenPos)
    {
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        w.z = startPos.z;
        return w;
    }

    Vector3 ClampToStart(Vector3 p)
    {
        Vector3 off = p - startPos;
        off.x = Mathf.Clamp(off.x, -maxOffset.x, maxOffset.x);
        off.y = Mathf.Clamp(off.y, -maxOffset.y, maxOffset.y);
        return startPos + off;
    }

    bool IsPointerBlockedByUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        _uiHits.Clear();
        EventSystem.current.RaycastAll(ped, _uiHits);
        return _uiHits.Count > 0;
    }

    bool IsPointerBlockedByWorld(Vector2 screenPos)
    {
        if (!cam) return false;

        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        Vector2 p2 = new Vector2(wp.x, wp.y);

        var hit = Physics2D.OverlapPoint(p2, blockPanMask);
        return hit != null;
    }

    // ==== PUBLIC API ====
    public void Recenter()
    {
        targetPos = startPos;
        velocity = Vector3.zero;
    }

    public float GetDistanceFromCenter()
    {
        return (boardRoot.position - startPos).magnitude;
    }
}
