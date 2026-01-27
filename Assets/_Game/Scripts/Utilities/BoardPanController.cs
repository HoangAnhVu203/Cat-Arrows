using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BoardPanController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera cam;

    [Header("Enable")]
    public bool enablePan = true;

    [Header("Clamp (world offset from start)")]
    public Vector2 maxOffset = new Vector2(3.5f, 6.0f);

    [Header("Smoothing")]
    [Range(0f, 30f)] public float follow = 18f;
    [Range(0f, 30f)] public float inertia = 10f;

    [Header("Drag")]
    [SerializeField] private float dragStartThresholdPx = 12f;

    [Header("Block Pan When Touching Line")]
    [SerializeField] private LayerMask blockPanMask;

    [Header("Zoom (Orthographic)")]
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float zoomSpeedMouse = 2.0f;
    [SerializeField] private float zoomSpeedPinch = 0.01f;
    [SerializeField] private float zoomLerp = 20f;
    [SerializeField] private float fitMargin = 0.25f;
    [SerializeField] private float maxZoomInMultiplier = 1.0f;

    // ==== runtime pan ====
    private Vector3 camStartPos;
    private Vector3 camTargetPos;

    private bool dragging;
    private bool pendingDrag;
    private Vector2 pressScreenPos;
    private Vector2 lastScreenPos;
    private Vector3 velocity;           // world units / sec
    private bool blockThisPress;

    // ==== runtime zoom ====
    private float orthoInitial;
    private float orthoMinFit;          // zoom out max (size lớn)
    private float orthoMaxIn;           // zoom in max (size nhỏ)
    private float orthoTarget;
    private float orthoMaxOut;   // zoom out tối đa (size lớn nhất)

    // pinch
    private bool pinching;
    private float lastPinchDist;

    // lock (cinematic/loading)
    public bool IsLocked { get; private set; }

    private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>(32);

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!cam) { enabled = false; return; }

        camStartPos = cam.transform.position;
        camTargetPos = camStartPos;

        // set target = size hiện tại trước
        if (cam.orthographic) orthoTarget = cam.orthographicSize;

        SetupZoomLimits();
    }


    private void OnEnable()
    {
        if (cam && cam.orthographic)
        {
            if (orthoTarget <= 0f) orthoTarget = cam.orthographicSize;
            SetupZoomLimits();
        }
    }


    private void Start()
    {
        SyncToCurrentCameraAsOrigin(true);
    }

    public void Lock(bool v)
    {
        IsLocked = v;

        if (!cam) cam = Camera.main;
        if (!cam) return;

        if (v)
        {
            // Đồng bộ trạng thái nội bộ = camera hiện tại
            camTargetPos = cam.transform.position;
            camStartPos = camTargetPos;
            velocity = Vector3.zero;

            // reset input state
            dragging = false;
            pendingDrag = false;
            blockThisPress = false;
            pinching = false;

            // Quan trọng: sync zoom target nếu bạn có zoom nội bộ
            if (cam.orthographic) orthoTarget = cam.orthographicSize;
        }
    }


    private void Update()
    {
        if (!cam) return;

        // Khi cinematic/loading: tuyệt đối không ghi đè camera
        if (IsLocked) return;

        // ===== ZOOM =====
        ApplyZoomOnly();

        // ===== PAN =====
        if (!enablePan)
        {
            ApplyCameraFollowOnly();
            return;
        }

        ApplyCameraFollowOnly();
        ApplyInertia();

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

        // Input PAN (Touch) - chỉ khi không pinch
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

    // ===================== APPLY =====================

    private void ApplyZoomOnly()
    {
        if (!enableZoom || !cam.orthographic) return;

        HandleZoom();

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            orthoTarget,
            1f - Mathf.Exp(-zoomLerp * Time.unscaledDeltaTime)
        );
    }

    private void ApplyCameraFollowOnly()
    {
        // Luôn lerp theo camTargetPos (không “snap” khi kéo để tránh feedback loop)
        cam.transform.position = Vector3.Lerp(
            cam.transform.position,
            camTargetPos,
            1f - Mathf.Exp(-follow * Time.unscaledDeltaTime)
        );
    }

    private void ApplyInertia()
    {
        if (dragging) return;
        if (velocity.sqrMagnitude <= 0.0001f) return;

        camTargetPos += velocity * Time.unscaledDeltaTime;

        velocity = Vector3.Lerp(
            velocity,
            Vector3.zero,
            1f - Mathf.Exp(-inertia * Time.unscaledDeltaTime)
        );

        camTargetPos = ClampCamToStart(camTargetPos);
    }

    // ===================== ZOOM =====================

    private void SetupZoomLimits()
    {
        if (!cam || !cam.orthographic) return;

        // orthoInitial = size hiện tại lúc setup (coi như "default" của scene)
        orthoInitial = cam.orthographicSize;

        float aspect = cam.aspect;

        float halfW = maxOffset.x + fitMargin;
        float halfH = maxOffset.y + fitMargin;

        float needSizeForWidth = halfW / Mathf.Max(0.0001f, aspect);
        float needSizeForHeight = halfH;

        orthoMinFit = Mathf.Max(needSizeForWidth, needSizeForHeight);

        // zoom in tối đa (size nhỏ nhất)
        orthoMaxIn = orthoInitial / Mathf.Max(0.0001f, maxZoomInMultiplier);

        // zoom out tối đa (size lớn nhất) - KHÔNG được nhỏ hơn size ban đầu
        orthoMaxOut = Mathf.Max(orthoInitial, orthoMinFit);

        // IMPORTANT: chỉ clamp orthoTarget, không ép camSize hiện tại
        orthoTarget = Mathf.Clamp(orthoTarget, orthoMaxIn, orthoMaxOut);
    }

    private void HandleZoom()
    {
        if (!cam || !cam.orthographic) return;

        Vector2 anyPos = GetAnyPointerPos();
        if (IsPointerBlockedByUI(anyPos))
        {
            pinching = false;
            return;
        }

        // Pinch (Touch)
        if (Touchscreen.current != null && Touchscreen.current.touches.Count >= 2)
        {
            var t0 = Touchscreen.current.touches[0];
            var t1 = Touchscreen.current.touches[1];

            if (t0.press.isPressed && t1.press.isPressed)
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

                    // delta > 0: tách ra -> zoom in (ortho giảm)
                    orthoTarget -= delta * zoomSpeedPinch;
                    orthoTarget = Mathf.Clamp(orthoTarget, orthoMaxIn, orthoMinFit);
                }

                // pinch thì không pan
                blockThisPress = true;
                pendingDrag = false;
                dragging = false;
                velocity = Vector3.zero;
                return;
            }
        }

        pinching = false;

        // Mouse wheel
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                orthoTarget -= scroll * 0.01f * zoomSpeedMouse;
                orthoTarget = Mathf.Clamp(orthoTarget, orthoMaxIn, orthoMinFit);
            }
        }
    }

    private Vector2 GetAnyPointerPos()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Vector2.zero;
    }

    public void ZoomToFitPanArea()
    {
        if (!cam || !cam.orthographic) return;
        SetupZoomLimits();
        orthoTarget = orthoMinFit;
    }

    public void ZoomToInitial()
    {
        if (!cam || !cam.orthographic) return;
        SetupZoomLimits();
        orthoTarget = Mathf.Clamp(orthoInitial, orthoMaxIn, orthoMinFit);
    }

    // ===================== PAN (STABLE: SCREEN DELTA -> WORLD DELTA) =====================

    private void BeginDrag(Vector2 screenPos)
    {
        if (pinching) return;
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
        lastScreenPos = screenPos;

        velocity = Vector3.zero;
    }

    private void Drag(Vector2 screenPos)
    {
        if (blockThisPress) return;
        if (pinching) return;

        if (pendingDrag && !dragging)
        {
            float dist = (screenPos - pressScreenPos).magnitude;
            if (dist < dragStartThresholdPx) return;

            dragging = true;
            pendingDrag = false;
            lastScreenPos = screenPos;
        }

        if (!dragging) return;

        Vector2 dScreen = screenPos - lastScreenPos;
        lastScreenPos = screenPos;

        // convert pixel delta -> world delta (ổn định, không phụ thuộc cam.position trong frame)
        Vector3 dWorld = ScreenDeltaToWorldDelta(dScreen);

        // kéo bản đồ: tay đi đâu map đi đó => camera đi ngược
        camTargetPos -= dWorld;
        camTargetPos = ClampCamToStart(camTargetPos);

        // velocity cho inertia
        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        Vector3 v = (-dWorld) / dt; // world/sec
        velocity = Vector3.Lerp(velocity, v, 0.25f);
    }

    private Vector3 ScreenDeltaToWorldDelta(Vector2 dScreen)
    {
        // Ortho: world height visible = 2*orthoSize
        float size = cam.orthographicSize;
        float worldPerPixelY = (2f * size) / Mathf.Max(1f, Screen.height);
        float worldPerPixelX = worldPerPixelY * cam.aspect;

        return new Vector3(dScreen.x * worldPerPixelX, dScreen.y * worldPerPixelY, 0f);
    }

    private void EndDrag()
    {
        pendingDrag = false;
        dragging = false;
        blockThisPress = false;
    }

    // ===================== BLOCKERS =====================

    private bool IsPointerBlockedByUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        var ped = new PointerEventData(EventSystem.current) { position = screenPos };
        _uiHits.Clear();
        EventSystem.current.RaycastAll(ped, _uiHits);
        return _uiHits.Count > 0;
    }

    private bool IsPointerBlockedByWorld(Vector2 screenPos)
    {
        if (!cam) return false;

        // Với ortho: z input không quan trọng, dùng z-dist từ camera đến plane z=0
        float zDist = -cam.transform.position.z;
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDist));
        Vector2 p2 = new Vector2(wp.x, wp.y);

        var hit = Physics2D.OverlapPoint(p2, blockPanMask);
        return hit != null;
    }

    // ===================== CLAMP / API =====================

    private Vector3 ClampCamToStart(Vector3 p)
    {
        Vector3 off = p - camStartPos;
        off.x = Mathf.Clamp(off.x, -maxOffset.x, maxOffset.x);
        off.y = Mathf.Clamp(off.y, -maxOffset.y, maxOffset.y);
        return camStartPos + off;
    }

    public void Recenter()
    {
        camTargetPos = camStartPos;
        velocity = Vector3.zero;
    }

    public float GetDistanceFromCenter()
    {
        return (cam.transform.position - camStartPos).magnitude;
    }

    public void SyncToCurrentCameraAsOrigin(bool zeroVelocity = true)
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        camStartPos = cam.transform.position;
        camTargetPos = camStartPos;

        if (zeroVelocity) velocity = Vector3.zero;

        dragging = pendingDrag = false;
        blockThisPress = false;
        pinching = false;

        if (cam.orthographic)
        {
            orthoTarget = cam.orthographicSize;
            SetupZoomLimits(); 
        }
    }




    public void SetTargetPosition(Vector3 worldPos, bool setAsOrigin = false)
    {
        if (!cam) return;

        worldPos.z = cam.transform.position.z;
        camTargetPos = worldPos;

        if (setAsOrigin)
        {
            camStartPos = camTargetPos;
            velocity = Vector3.zero;
        }
    }

    public void SnapTo(Vector3 worldPos, bool alsoSetAsOrigin = true)
    {
        if (!cam) cam = Camera.main;
        if (!cam) return;

        // giữ z camera
        worldPos.z = cam.transform.position.z;

        cam.transform.position = worldPos;

        camTargetPos = worldPos;

        if (alsoSetAsOrigin)
        {
            camStartPos = worldPos;
        }

        velocity = Vector3.zero;
        dragging = pendingDrag = false;
        blockThisPress = false;
        pinching = false;
    }


}
