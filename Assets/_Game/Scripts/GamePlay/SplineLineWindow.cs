using System.Collections;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class SplineLineWindow : MonoBehaviour
{
    [Header("Refs")]
    public SplineContainer spline;
    public LineRenderer line;
    public EdgeCollider2D hitbox;
    public Transform head;

    [Header("Window (world units)")]
    public float startDistance = 0f;
    public float visibleLength = 3f;

    [Header("Sampling")]
    [Range(8, 300)] public int sampleCount = 80;
    [Range(64, 2048)] public int lutSamples = 512;

    [Header("Move on click")]
    public float speed = 2f;                 
    public float destroyAfterSeconds = 2f;
    public bool destroyParent = true;

    [Header("Line Look")]
    public float lineWidth = 0.18f;
    public int capVertices = 10;
    public int cornerVertices = 6;
    [Range(0.01f, 1f)] public float hitboxRadius = 0.12f;

    Camera cam;
    bool isMoving;

    float splineLength;
    float[] lutCum;
    float[] lutT;

    void Awake()
    {
        if (line == null) line = GetComponentInChildren<LineRenderer>(true);
        if (hitbox == null) hitbox = GetComponentInChildren<EdgeCollider2D>(true);
        cam = Camera.main;

        if (line != null)
        {
            line.useWorldSpace = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.numCapVertices = capVertices;
            line.numCornerVertices = cornerVertices;
        }

        if (hitbox != null) hitbox.isTrigger = true;
    }

    void Start()
    {
        if (spline == null) spline = FindObjectOfType<SplineContainer>();
        if (cam == null) cam = Camera.main;

        BuildDistanceLUT();
        Render(); 
    }

    void Update()
    {
        if (isMoving) return;
        if (hitbox == null || cam == null) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryPick(Mouse.current.position.ReadValue());

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            TryPick(Touchscreen.current.primaryTouch.position.ReadValue());
    }

    void TryPick(Vector2 screenPos)
    {
        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        world.z = 0f;

        var c = Physics2D.OverlapPoint(world);
        if (c == hitbox)
            StartCoroutine(MoveRoutine());
    }

    IEnumerator MoveRoutine()
    {
        isMoving = true;

        float tAlive = 0f;
        while (tAlive < destroyAfterSeconds)
        {
            startDistance = WrapDistance(startDistance + speed * Time.deltaTime, splineLength);

            Render();

            tAlive += Time.deltaTime;
            yield return null;
        }

        if (destroyParent && transform.parent != null) Destroy(transform.parent.gameObject);
        else Destroy(gameObject);
    }

    // ===================== Render =====================

    void Render()
    {
        if (spline == null || line == null || splineLength <= 0.0001f) return;

        int n = Mathf.Max(2, sampleCount);
        float step = visibleLength / (n - 1);

        line.positionCount = n;
        Vector3[] pts = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            float d = WrapDistance(startDistance + i * step, splineLength);
            EvaluateByDistance(d, out Vector3 pos, out Vector3 tan);

            pos.z = 0f;
            pts[i] = pos;
            line.SetPosition(i, pos);
        }

        // ✅ Head chỉ đi theo vị trí, KHÔNG xoay theo spline
        if (head != null && pts.Length >= 1)
        {
            Vector3 pHead = pts[pts.Length - 1];
            head.position = pHead;

            // luôn thẳng (tuỳ sprite của bạn: z=0 nghĩa là hướng lên hay hướng phải)
            head.rotation = Quaternion.Euler(0, 0, 0f);
        }



        // hitbox sync
        if (hitbox != null)
        {
            Vector2[] local = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++)
                local[i] = hitbox.transform.InverseTransformPoint(pts[i]);

            hitbox.points = local;
            hitbox.edgeRadius = hitboxRadius;
            hitbox.isTrigger = true;
        }
    }

    // ===================== Spline distance LUT =====================

    void BuildDistanceLUT()
    {
        lutSamples = Mathf.Max(64, lutSamples);

        lutCum = new float[lutSamples + 1];
        lutT = new float[lutSamples + 1];

        spline.Evaluate(0f, out Unity.Mathematics.float3 p0Float3, out _, out _);
        Vector3 prev = p0Float3;

        lutCum[0] = 0f;
        lutT[0] = 0f;

        float cum = 0f;
        for (int i = 1; i <= lutSamples; i++)
        {
            float t = (float)i / lutSamples;
            spline.Evaluate(t, out Unity.Mathematics.float3 pFloat3, out _, out _);
            cum += Vector3.Distance(prev, pFloat3);

            lutCum[i] = cum;
            lutT[i] = t;

            prev = pFloat3;
        }

        splineLength = Mathf.Max(0.0001f, cum);
    }

    void EvaluateByDistance(float dist, out Vector3 pos, out Vector3 tan)
    {
        dist = WrapDistance(dist, splineLength);

        int lo = 0, hi = lutCum.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (lutCum[mid] < dist) lo = mid;
            else hi = mid;
        }

        float d0 = lutCum[lo];
        float d1 = lutCum[hi];
        float t0 = lutT[lo];
        float t1 = lutT[hi];

        float u = (dist - d0) / Mathf.Max(0.0001f, (d1 - d0));
        float t = Mathf.Lerp(t0, t1, u);

        spline.Evaluate(t, out Unity.Mathematics.float3 posFloat3, out Unity.Mathematics.float3 tanFloat3, out _);
        pos = posFloat3;
        tan = tanFloat3;
        tan.z = 0f;
        if (tan.sqrMagnitude < 1e-8f) tan = Vector3.up;
        tan.Normalize();
    }

    static float WrapDistance(float d, float len)
    {
        if (len <= 0.0001f) return 0f;
        d %= len;
        if (d < 0f) d += len;
        return d;
    }

}
