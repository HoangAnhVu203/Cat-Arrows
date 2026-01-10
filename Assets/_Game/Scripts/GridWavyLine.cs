using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class GridWavyLine : MonoBehaviour
{
    [Header("Refs")]
    public GridManager grid;
    public LineRenderer line;
    public EdgeCollider2D hitbox;
    public Transform destroyRoot; // nếu null -> Destroy(gameObject)

    [Header("Base path in GRID (turn points)")]
    public List<Vector2Int> turnCells = new();

    [Header("Line Look")]
    [Range(0.01f, 1f)] public float lineWidth = 0.18f;
    [Range(0, 12)] public int capVertices = 6;
    [Range(0, 12)] public int cornerVertices = 6;

    [Header("Wave Params (CELL units)")]
    [Range(0f, 0.45f)] public float amplitudeCells = 0.22f;
    [Range(0.5f, 6f)] public float wavelengthCells = 2.2f;
    public float phase = 0f;

    [Header("Sampling")]
    [Range(8, 80)] public int samplesPerCell = 18;

    [Header("Click Move")]
    public bool moveOnClick = true;
    [Range(0.1f, 20f)] public float moveSpeedCellsPerSec = 6f;

    [Header("Move Mode")]
    [Tooltip("Nếu bật: đi mãi không dừng (không clamp theo totalLen).")]
    public bool moveForever = true;

    [Tooltip("Nếu moveForever=false, sẽ đi ra thêm bao nhiêu cell sau điểm cuối rồi dừng.")]
    [Range(0f, 50f)] public float extraOutCells = 6f;

    [Header("Destroy")]
    public bool destroyAfterMove = false;
    [Range(0.1f, 10f)] public float destroyDelay = 2f;

    Camera cam;
    bool isMoving;

    // runtime
    float movingOffset;      // world units along path
    float totalLen;          // world units (base path)
    List<Vector3> basePts;
    List<float> cum;

    void Awake()
    {
        if (!grid) grid = FindObjectOfType<GridManager>();
        if (!line) line = GetComponentInChildren<LineRenderer>(true);
        if (!hitbox) hitbox = GetComponentInChildren<EdgeCollider2D>(true);
        if (!destroyRoot) destroyRoot = transform;

        cam = Camera.main;

        if (line) line.useWorldSpace = true;
        if (hitbox) hitbox.isTrigger = true;
    }

    void Start()
    {
        if (!grid || !line) return;

        if (Mathf.Approximately(phase, 0f)) phase = Random.Range(0f, 1000f);

        ApplyLineStyle();
        BakeBasePath();
        Rebuild();
    }

    void Update()
    {
        if (!moveOnClick) return;
        if (isMoving) return;
        if (!grid || !cam || !hitbox) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryPick(Mouse.current.position.ReadValue());

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            TryPick(Touchscreen.current.primaryTouch.position.ReadValue());
    }

    void ApplyLineStyle()
    {
        if (!line) return;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.numCapVertices = capVertices;
        line.numCornerVertices = cornerVertices;
        line.alignment = LineAlignment.TransformZ;
    }

    void BakeBasePath()
    {
        if (!grid || turnCells == null || turnCells.Count < 2) return;

        basePts = BuildBaseWorld(turnCells);
        if (basePts == null || basePts.Count < 2) return;

        cum = BuildCum(basePts);
        totalLen = cum[cum.Count - 1];

        movingOffset = 0f;
    }

    void TryPick(Vector2 screenPos)
    {
        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        world.z = 0f;

        Collider2D c = Physics2D.OverlapPoint(world);
        if (c == hitbox) StartMove();
    }

    public void StartMove()
    {
        if (isMoving) return;
        if (basePts == null || basePts.Count < 2) return;

        StopAllCoroutines();
        StartCoroutine(MoveRoutine());
    }

    IEnumerator MoveRoutine()
    {
        isMoving = true;

        float speed = moveSpeedCellsPerSec * grid.cellSize;

        // targetDist chỉ dùng khi moveForever=false
        float targetDist = totalLen + extraOutCells * grid.cellSize;

        while (true)
        {
            movingOffset += speed * Time.deltaTime; // ✅ KHÔNG clamp

            Rebuild();

            if (!moveForever && movingOffset >= targetDist)
                break;

            yield return null;
        }

        isMoving = false;

        if (destroyAfterMove)
        {
            yield return new WaitForSeconds(destroyDelay);
            Destroy(destroyRoot ? destroyRoot.gameObject : gameObject);
        }
    }

    public void Rebuild()
    {
        if (!grid || !line) return;
        if (basePts == null || basePts.Count < 2) return;

        float cellSize = grid.cellSize;

        // Giữ sampleCount theo baseLen để ổn định perf (không tăng vô hạn)
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt((totalLen / cellSize) * samplesPerCell));

        float amp = amplitudeCells * cellSize;
        float waveLen = Mathf.Max(0.0001f, wavelengthCells * cellSize);
        float maxAmp = 0.35f * cellSize;

        var outPts = new Vector3[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (sampleCount == 1) ? 0f : (float)i / (sampleCount - 1);
            float s = t * totalLen;

            // ✅ s2 sẽ tăng vô hạn => p sẽ extrapolate ra ngoài cuối đường
            float s2 = s + movingOffset;

            Vector3 p = PointAtExtended(basePts, cum, s2);

            // tangent vẫn clamp trong [0..totalLen] để normal ổn định
            Vector3 tan = TangentAt(basePts, cum, Mathf.Clamp(s2, 0f, totalLen));

            Vector3 n = new Vector3(-tan.y, tan.x, 0f).normalized;

            float w = Mathf.Sin((2f * Mathf.PI / waveLen) * s2 + phase) * amp;
            w = Mathf.Clamp(w, -maxAmp, maxAmp);

            outPts[i] = p + n * w;
            outPts[i].z = 0f;
        }

        line.positionCount = outPts.Length;
        line.SetPositions(outPts);

        SyncEdgeCollider(outPts);
    }

    // ================= helpers =================

    List<Vector3> BuildBaseWorld(List<Vector2Int> cells)
    {
        var pts = new List<Vector3>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            var p = grid.CellToWorld(cells[i]);
            p.z = 0f;
            pts.Add(p);
        }
        return pts;
    }

    List<float> BuildCum(List<Vector3> pts)
    {
        var cum = new List<float>(pts.Count);
        cum.Add(0f);
        float acc = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            acc += Vector3.Distance(pts[i - 1], pts[i]);
            cum.Add(acc);
        }
        return cum;
    }

    Vector3 PointAtExtended(List<Vector3> pts, List<float> cum, float dist)
    {
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (dist <= 0f) return pts[0];

        float total = cum[cum.Count - 1];
        if (dist >= total)
        {
            Vector3 last = pts[pts.Count - 1];
            Vector3 prev = pts[pts.Count - 2];
            Vector3 dir = (last - prev);
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.up;
            dir.Normalize();

            float extra = dist - total;
            return last + dir * extra;
        }

        int lo = 0, hi = cum.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (cum[mid] < dist) lo = mid;
            else hi = mid;
        }

        float segLen = Mathf.Max(0.0001f, cum[hi] - cum[lo]);
        float tt = (dist - cum[lo]) / segLen;
        return Vector3.Lerp(pts[lo], pts[hi], tt);
    }

    Vector3 TangentAt(List<Vector3> pts, List<float> cum, float dist)
    {
        if (dist <= 0f) return (pts[1] - pts[0]).normalized;

        float total = cum[cum.Count - 1];
        if (dist >= total) return (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;

        int lo = 0, hi = cum.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (cum[mid] < dist) lo = mid;
            else hi = mid;
        }

        Vector3 d = pts[hi] - pts[lo];
        if (d.sqrMagnitude < 1e-8f) d = Vector3.right;
        return d.normalized;
    }

    void SyncEdgeCollider(Vector3[] worldPts)
    {
        if (!hitbox || worldPts == null || worldPts.Length < 2) return;

        int stride = 2;
        int count = Mathf.Max(2, (worldPts.Length + stride - 1) / stride);

        var pts = new Vector2[count];
        int k = 0;
        for (int i = 0; i < worldPts.Length && k < count; i += stride)
        {
            Vector3 local = hitbox.transform.InverseTransformPoint(worldPts[i]);
            pts[k++] = local;
        }
        if (k < count)
        {
            Vector3 local = hitbox.transform.InverseTransformPoint(worldPts[worldPts.Length - 1]);
            pts[k++] = local;
        }

        hitbox.points = pts;
        hitbox.edgeRadius = 0.18f * (grid ? grid.cellSize : 1f);
        hitbox.isTrigger = true;
    }
}
