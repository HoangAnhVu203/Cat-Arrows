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


    Vector3 startPos;
    Vector3 targetPos;

    bool dragging;
    Vector3 lastWorld;
    Vector3 velocity;

    bool pendingDrag;           
    Vector2 pressScreenPos;     
    Vector3 pressWorldPos;
    bool blockThisPress;

    static readonly List<RaycastResult> _uiHits = new List<RaycastResult>(32);

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!boardRoot) boardRoot = transform;

        startPos = boardRoot.position;
        targetPos = startPos;
    }

    void Update()
    {
        if (!enablePan || cam == null || boardRoot == null) return;

        // Smooth follow
        boardRoot.position = Vector3.Lerp(boardRoot.position, targetPos, 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime));

        // Inertia when not dragging
        if (!dragging && velocity.sqrMagnitude > 0.0001f)
        {
            targetPos += velocity * Time.unscaledDeltaTime;
            velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-inertia * Time.unscaledDeltaTime));
            targetPos = ClampToStart(targetPos);
        }

        // Input
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                BeginDrag(Mouse.current.position.ReadValue());
            else if (Mouse.current.leftButton.isPressed)
                Drag(Mouse.current.position.ReadValue());
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
                EndDrag();
        }

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

        velocity = Vector3.Lerp(velocity, delta / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.25f);
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

    bool IsPointerBlockedByWorld(Vector2 screenPos)
    {
        if (cam == null) return false;

        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
        Vector2 p2 = new Vector2(wp.x, wp.y);

        var hit = Physics2D.OverlapPoint(p2, blockPanMask);
        return hit != null;
    }

}
