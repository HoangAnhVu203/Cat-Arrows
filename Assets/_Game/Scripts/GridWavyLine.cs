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
    public Transform destroyRoot;

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
    public bool moveForever = true;
    [Range(0f, 50f)] public float extraOutCells = 6f;

    [Header("Destroy")]
    public bool destroyAfterMove = false;
    [Range(0.1f, 10f)] public float destroyDelay = 2f;

    [Header("Head (1 per line)")]
    public Transform headPrefab;
    public Transform head;
    public bool rotateHeadAlongTangent = true;
    public float headForwardOffsetCells = 0f;

    [Header("Head Fade (first N cells)")]
    public bool fadeHead = true;
    [Min(0f)] public float fadeHeadCells = 2f;
    [Range(0f, 1f)] public float headAlphaMin = 0.15f;

    [Header("Smoothing Corners")]
    [Range(0f, 0.49f)] public float cornerRadiusCells = 0.35f;
    [Range(2, 16)] public int cornerSegments = 8;

    // ===================== UNIFORM / WAVE AXIS =====================

    public enum LockedWaveAxis { Auto, WorldX, WorldY }

    [Header("Uniform Wave (global)")]
    public bool uniformWave = true;
    public float uniformPhase = 0f;
    public LockedWaveAxis lockedAxis = LockedWaveAxis.Auto;

    [Header("Auto Axis Stability")]
    [Range(1f, 2f)] public float autoAxisHysteresis = 1.35f;

    // ===================== BLOCK CHECK =====================

    [Header("Block Check (prevent move if blocked ahead)")]
    public bool blockIfAhead = true;

    [Tooltip("Layer của các line/collider cần chặn. Nên tạo Layer 'Line' và gán vào đây.")]
    public LayerMask blockMask = ~0;

    [Tooltip("Dò trước đầu line bao nhiêu CELL để xem có bị chắn không.")]
    [Range(0.1f, 2f)] public float blockProbeCells = 0.75f;

    [Tooltip("Bán kính dò (tỉ lệ theo độ dày line). Tăng nếu vẫn lọt qua nhau.")]
    [Range(0.3f, 1.2f)] public float blockRadiusMul = 0.9f;

    // ===================== runtime =====================

    Camera cam;
    bool isMoving;

    float movingOffset;
    float totalLen;
    List<Vector3> basePts;
    List<float> cum;
    bool autoIsVertical;

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

        ApplyLineStyle();
        BakeBasePath();
        BuildHead();
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

    void BuildHead()
    {
        if (headPrefab == null) return;

        if (head != null) Destroy(head.gameObject);
        head = Instantiate(headPrefab, transform);
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

        Vector3 d0 = basePts[1] - basePts[0];
        autoIsVertical = Mathf.Abs(d0.y) >= Mathf.Abs(d0.x);
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

        // ======= NEW: block check =======
        if (blockIfAhead && IsBlockedAhead())
            return;

        StopAllCoroutines();
        StartCoroutine(MoveRoutine());
    }

    IEnumerator MoveRoutine()
    {
        isMoving = true;

        float speed = moveSpeedCellsPerSec * grid.cellSize;
        float targetDist = totalLen + extraOutCells * grid.cellSize;

        while (true)
        {
            // nếu đang chạy mà phía trước xuất hiện vật chắn => dừng lại
            if (blockIfAhead && IsBlockedAhead())
                break;

            movingOffset += speed * Time.deltaTime;
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

    // ===================== BLOCK LOGIC =====================

    bool IsBlockedAhead()
    {
        if (!grid || basePts == null || basePts.Count < 2) return false;

        // vị trí đầu line (end của path + movingOffset)
        float sHead = totalLen + movingOffset;

        Vector3 p = PointAtExtended(basePts, cum, sHead);
        Vector3 tan = TangentAtExtended(basePts, cum, sHead);

        // snap hướng theo 4 hướng để dò "đúng kiểu grid"
        Vector2 dir = SnapToCardinal(tan);
        if (dir.sqrMagnitude < 1e-6f) return false;

        float dist = Mathf.Max(0.01f, blockProbeCells) * grid.cellSize;

        // radius theo độ dày line (world unit)
        float radius = Mathf.Max(0.001f, (lineWidth * 0.5f) * blockRadiusMul);

        // CircleCast để bắt chặn "dày" (đỡ lọt qua nhau)
        RaycastHit2D hit = Physics2D.CircleCast((Vector2)p, radius, dir, dist, blockMask);

        if (!hit.collider) return false;

        // bỏ qua chính mình
        if (hit.collider == hitbox) return false;

        // nếu bạn có nhiều collider con, có thể cần check root:
        if (hit.collider.transform.IsChildOf(transform)) return false;

        return true;
    }

    static Vector2 SnapToCardinal(Vector3 tan)
    {
        float ax = Mathf.Abs(tan.x);
        float ay = Mathf.Abs(tan.y);

        if (ax < 1e-6f && ay < 1e-6f) return Vector2.zero;

        if (ax >= ay)
            return (tan.x >= 0f) ? Vector2.right : Vector2.left;
        else
            return (tan.y >= 0f) ? Vector2.up : Vector2.down;
    }

    // ===================== DRAW =====================

    public void Rebuild()
    {
        if (!grid || !line) return;
        if (basePts == null || basePts.Count < 2) return;

        float cellSize = grid.cellSize;

        int sampleCount = Mathf.Max(2, Mathf.CeilToInt((totalLen / cellSize) * samplesPerCell));

        float amp = amplitudeCells * cellSize;
        float waveLen = Mathf.Max(0.0001f, wavelengthCells * cellSize);
        float maxAmp = 0.35f * cellSize;

        var outPts = new Vector3[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (sampleCount == 1) ? 0f : (float)i / (sampleCount - 1);
            float s = t * totalLen;
            float s2 = s + movingOffset;

            Vector3 p = PointAtExtended(basePts, cum, s2);

            Vector3 tan = TangentAtExtended(basePts, cum, s2);

            Vector3 n = new Vector3(-tan.y, tan.x, 0f).normalized;
            float waveCoord = s2;

            float phi = uniformWave ? uniformPhase : phase;

            float w = Mathf.Sin((2f * Mathf.PI / waveLen) * waveCoord + phi) * amp;
            w = Mathf.Clamp(w, -maxAmp, maxAmp);

            outPts[i] = p + n * w;
            outPts[i].z = 0f;
        }

        line.positionCount = outPts.Length;
        line.SetPositions(outPts);

        UpdateHeadFadeGradient();
        SyncEdgeCollider(outPts);
        UpdateHead();
    }

    void ResolveWaveFrame(Vector3 p, Vector3 tan, out Vector3 n, out float waveCoord)
    {
        if (lockedAxis == LockedWaveAxis.WorldX)
        {
            n = Vector3.right;
            waveCoord = p.y;
            return;
        }
        if (lockedAxis == LockedWaveAxis.WorldY)
        {
            n = Vector3.up;
            waveCoord = p.x;
            return;
        }

        float ax = Mathf.Abs(tan.x);
        float ay = Mathf.Abs(tan.y);

        float h = Mathf.Max(1f, autoAxisHysteresis);

        if (!autoIsVertical)
        {
            if (ay > ax * h) autoIsVertical = true;
        }
        else
        {
            if (ax > ay * h) autoIsVertical = false;
        }

        if (autoIsVertical)
        {
            n = Vector3.right;
            waveCoord = p.y;
        }
        else
        {
            n = Vector3.up;
            waveCoord = p.x;
        }
    }

    void UpdateHead()
    {
        if (!head) return;
        if (!grid || basePts == null || basePts.Count < 2) return;

        float cellSize = grid.cellSize;
        float waveLen = Mathf.Max(0.0001f, wavelengthCells * cellSize);
        float amp = amplitudeCells * cellSize;
        float maxAmp = 0.35f * cellSize;

        float s2 = totalLen + movingOffset + headForwardOffsetCells * cellSize;

        Vector3 p = PointAtExtended(basePts, cum, s2);
        Vector3 tan = TangentAtExtended(basePts, cum, s2);

        Vector3 n;
        float waveCoord;
        ResolveWaveFrame(p, tan, out n, out waveCoord);

        float phi = uniformWave ? uniformPhase : phase;

        float w = Mathf.Sin((2f * Mathf.PI / waveLen) * waveCoord + phi) * amp;
        w = Mathf.Clamp(w, -maxAmp, maxAmp);

        Vector3 pos = p + n * w;
        pos.z = 0f;

        head.position = pos;

        if (rotateHeadAlongTangent)
        {
            float ax = Mathf.Abs(tan.x);
            float ay = Mathf.Abs(tan.y);
            float z = (ay > ax) ? 90f : 0f;
            head.rotation = Quaternion.Euler(0f, 0f, z);
        }
    }

    // ===================== base path / corners =====================

    List<Vector3> BuildBaseWorld(List<Vector2Int> turns)
    {
        float cs = grid.cellSize;
        float r = Mathf.Clamp(cornerRadiusCells, 0f, 0.49f) * cs;

        var cells = ExpandCellsByStep(turns);
        if (cells.Count < 2) return new List<Vector3>();

        var raw = new List<Vector3>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            var p = grid.CellToWorld(cells[i]);
            p.z = 0f;
            raw.Add(p);
        }

        raw = RemoveConsecutiveDuplicates(raw, 1e-6f);
        if (raw.Count < 2 || r <= 0.0001f || cornerSegments < 2)
            return raw;

        var outPts = new List<Vector3>(raw.Count * 2);
        outPts.Add(raw[0]);

        for (int i = 1; i < raw.Count - 1; i++)
        {
            Vector3 A = raw[i - 1];
            Vector3 B = raw[i];
            Vector3 C = raw[i + 1];

            Vector3 d1 = (B - A);
            Vector3 d2 = (C - B);

            float len1 = d1.magnitude;
            float len2 = d2.magnitude;

            if (len1 < 1e-6f || len2 < 1e-6f)
            {
                outPts.Add(B);
                continue;
            }

            d1 /= len1;
            d2 /= len2;

            if (Vector3.Dot(d1, d2) > 0.999f)
            {
                outPts.Add(B);
                continue;
            }

            float t = Mathf.Min(r, 0.45f * Mathf.Min(len1, len2));

            Vector3 P = B - d1 * t;
            Vector3 Q = B + d2 * t;

            if ((outPts[outPts.Count - 1] - P).sqrMagnitude > 1e-10f)
                outPts.Add(P);

            for (int s = 1; s <= cornerSegments; s++)
            {
                float u = (float)s / cornerSegments;
                Vector3 pt = (1 - u) * (1 - u) * P + 2 * (1 - u) * u * B + u * u * Q;
                outPts.Add(pt);
            }
        }

        outPts.Add(raw[raw.Count - 1]);
        return RemoveConsecutiveDuplicates(outPts, 1e-6f);
    }

    // ===================== geometry sampling =====================

    List<float> BuildCum(List<Vector3> pts)
    {
        var c = new List<float>(pts.Count);
        c.Add(0f);

        float acc = 0f;
        for (int i = 1; i < pts.Count; i++)
        {
            acc += Vector3.Distance(pts[i - 1], pts[i]);
            c.Add(acc);
        }
        return c;
    }

    Vector3 PointAtExtended(List<Vector3> pts, List<float> c, float dist)
    {
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (dist <= 0f) return pts[0];

        float total = c[c.Count - 1];
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

        int lo = 0, hi = c.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (c[mid] < dist) lo = mid;
            else hi = mid;
        }

        float segLen = Mathf.Max(0.0001f, c[hi] - c[lo]);
        float tt = (dist - c[lo]) / segLen;
        return Vector3.Lerp(pts[lo], pts[hi], tt);
    }

    Vector3 TangentAtExtended(List<Vector3> pts, List<float> c, float dist)
    {
        if (pts == null || pts.Count < 2) return Vector3.up;

        if (dist <= 0f)
        {
            Vector3 d0 = pts[1] - pts[0];
            return (d0.sqrMagnitude < 1e-8f) ? Vector3.up : d0.normalized;
        }

        float total = c[c.Count - 1];
        if (dist >= total)
        {
            Vector3 d1 = pts[pts.Count - 1] - pts[pts.Count - 2];
            return (d1.sqrMagnitude < 1e-8f) ? Vector3.up : d1.normalized;
        }

        int lo = 0, hi = c.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (c[mid] < dist) lo = mid;
            else hi = mid;
        }

        Vector3 d = pts[hi] - pts[lo];
        if (d.sqrMagnitude < 1e-8f) return Vector3.up;
        return d.normalized;
    }

    // ===================== collider =====================

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

    // ===================== fade first N cells =====================

    void UpdateHeadFadeGradient()
    {
        if (!fadeHead || !line || !grid) return;

        float renderedLen = totalLen + movingOffset;
        if (renderedLen <= 0.0001f) return;

        float fadeLen = Mathf.Max(0f, fadeHeadCells) * grid.cellSize;
        float t1 = Mathf.Clamp01(fadeLen / renderedLen);

        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(headAlphaMin, 0f),
                new GradientAlphaKey(1f, t1),
                new GradientAlphaKey(1f, 1f)
            }
        );

        line.colorGradient = g;
    }

    // ===================== cell utilities =====================

    static List<Vector3> RemoveConsecutiveDuplicates(List<Vector3> pts, float eps = 1e-6f)
    {
        if (pts == null || pts.Count == 0) return pts;

        float eps2 = eps * eps;

        var clean = new List<Vector3>(pts.Count);
        clean.Add(pts[0]);

        for (int i = 1; i < pts.Count; i++)
        {
            if ((pts[i] - clean[clean.Count - 1]).sqrMagnitude > eps2)
                clean.Add(pts[i]);
        }

        return clean;
    }

    static List<Vector2Int> ExpandCellsByStep(List<Vector2Int> turns)
    {
        var outCells = new List<Vector2Int>();
        if (turns == null || turns.Count == 0) return outCells;

        outCells.Add(turns[0]);

        for (int i = 1; i < turns.Count; i++)
        {
            Vector2Int a = turns[i - 1];
            Vector2Int b = turns[i];

            Vector2Int d = b - a;
            int sx = d.x == 0 ? 0 : (d.x > 0 ? 1 : -1);
            int sy = d.y == 0 ? 0 : (d.y > 0 ? 1 : -1);

            while (a != b)
            {
                if (a.x != b.x) a.x += sx;
                else if (a.y != b.y) a.y += sy;

                if (outCells[outCells.Count - 1] != a)
                    outCells.Add(a);
            }
        }

        return outCells;
    }
}
