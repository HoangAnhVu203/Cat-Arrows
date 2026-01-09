using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ArrowLinePiece: MonoBehaviour
{
    public enum Dir { Up, Down, Left, Right }

    [Header("Refs")]
    public GridManager grid;
    public LineRenderer line;
    public Transform head;           
    public EdgeCollider2D hitbox;        

    [Header("Data (ordered tail -> head)")]
    public List<Vector2Int> cells = new();
    public Dir direction = Dir.Up;

    [Header("Line Look")]
    public float lineWidth = 0.18f;
    public int capVertices = 10;

    [Header("Corner (Rounded)")]
    [Range(0.0f, 0.5f)]
    public float cornerRadiusCells = 0.40f;

    [Range(2, 24)]
    public int arcSegments = 10;

    [Header("Hitbox")]
    [Range(0.05f, 0.5f)]
    public float edgeRadius = 0.15f;

    [Header("Head Look")]
    public float headOffset = 0.0f;

    [Header("Move (smooth)")]
    [Range(0.03f, 0.6f)]
    public float stepDuration = 0.18f;

    public int maxStepsWhenOutside = 50;

    Camera cam;
    bool isMoving;

    void Awake()
    {
        if (line == null) line = GetComponentInChildren<LineRenderer>(true);
        if (hitbox == null) hitbox = GetComponentInChildren<EdgeCollider2D>(true);

        cam = Camera.main;

        if (line != null) line.useWorldSpace = true;
        if (hitbox != null) hitbox.isTrigger = true;
    }

    void Start()
    {
        if (grid == null) grid = FindObjectOfType<GridManager>();
        if (cam == null) cam = Camera.main;

        ApplyStyleOnce();
        RenderImmediate();
    }

    void Update()
    {
        if (isMoving) return;
        if (grid == null || cam == null || hitbox == null) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryPick(Mouse.current.position.ReadValue());

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            TryPick(Touchscreen.current.primaryTouch.position.ReadValue());
    }

    void ApplyStyleOnce()
    {
        if (line == null) return;

        line.startWidth = lineWidth;
        line.endWidth = lineWidth;

        line.numCornerVertices = 0;
        line.numCapVertices = capVertices;

        line.alignment = LineAlignment.TransformZ;
        line.textureMode = LineTextureMode.Stretch;
    }

    void TryPick(Vector2 screenPos)
    {
        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        world.z = 0f;

        Collider2D c = Physics2D.OverlapPoint(world);
        if (c == hitbox)
        {
            StartMove();
            StartCoroutine(DestroyParentAfterDelay());
        }
    }
    IEnumerator DestroyParentAfterDelay()
    {
        yield return new WaitForSeconds(.75f);
        Destroy(transform.root.gameObject);
    }


    public void StartMove()
    {
        if (isMoving) return;
        if (cells == null || cells.Count < 2) return;

        StartCoroutine(MoveOutRoutine());
    }

    IEnumerator MoveOutRoutine()
    {
        isMoving = true;
        ApplyStyleOnce();

        int stepsOutside = 0;

        while (true)
        {
            if (!CanStepForward())
            {
                isMoving = false;
                yield break;
            }

            List<Vector2Int> fromCells = new List<Vector2Int>(cells);
            Vector2Int nextCell = fromCells[fromCells.Count - 1] + DirToVec(direction);

            List<Vector2Int> toCells = new List<Vector2Int>(fromCells);
            toCells.RemoveAt(0);
            toCells.Add(nextCell);

            List<Vector3> fromPath = BuildRoundedPath(fromCells);
            float visibleLen = PolylineLength(fromPath);
            List<Vector2Int> extCells = new List<Vector2Int>(fromCells);
            extCells.Add(nextCell);
            List<Vector3> extPath = BuildRoundedPath(extCells);

            float[] cum = BuildCum(extPath);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, stepDuration);
                float u = Smooth01(Mathf.Clamp01(t));

                float offset = Mathf.Lerp(0f, grid.cellSize, u);

                List<Vector3> window = ExtractWindow(extPath, cum, offset, visibleLen);

                RenderWorld(window);
                yield return null;
            }

            // commit
            cells = toCells;

            if (AllOutsideGrid())
            {
                stepsOutside++;
                if (stepsOutside >= maxStepsWhenOutside) break;
            }
            else stepsOutside = 0;
        }

        Destroy(gameObject);
    }

    bool CanStepForward()
    {
        Vector2Int headCell = cells[cells.Count - 1];
        Vector2Int next = headCell + DirToVec(direction);

        if (!grid.IsInside(next)) return true;

        if (grid.IsBlocked(next, this)) return false;

        return true;
    }


    bool AllOutsideGrid()
    {
        for (int i = 0; i < cells.Count; i++)
            if (grid.IsInside(cells[i])) return false;
        return true;
    }

    Vector2Int DirToVec(Dir d) => d switch
    {
        Dir.Up => Vector2Int.up,
        Dir.Down => Vector2Int.down,
        Dir.Left => Vector2Int.left,
        _ => Vector2Int.right
    };

    // ===================== RENDER =====================

    void RenderImmediate()
    {
        if (grid == null || line == null || cells == null || cells.Count < 2) return;
        ApplyStyleOnce();

        var path = BuildRoundedPath(cells);
        RenderWorld(path);
    }

    void RenderWorld(List<Vector3> pts)
    {
        if (pts == null || pts.Count < 2) return;

        line.positionCount = pts.Count;
        for (int i = 0; i < pts.Count; i++) line.SetPosition(i, pts[i]);

        UpdateHead(pts);
        SyncEdgeCollider(pts);
    }

    void UpdateHead(List<Vector3> pts)
    {
        if (head == null || pts == null || pts.Count < 2) return;

        Vector3 pHead = pts[pts.Count - 1];

        Vector3 dir = direction switch
        {
            Dir.Up => Vector3.up,
            Dir.Down => Vector3.down,
            Dir.Left => Vector3.left,
            _ => Vector3.right
        };

        head.position = pHead + dir.normalized * headOffset;

        // Góc Z cố định theo yêu cầu của bạn
        float z = direction switch
        {
            Dir.Up => 0f,
            Dir.Left => 90f,
            Dir.Right => -90f,
            Dir.Down => 180f,
            _ => 0f
        };

        head.rotation = Quaternion.Euler(0f, 0f, z);
    }


    void SyncEdgeCollider(List<Vector3> pts)
    {
        if (hitbox == null || pts == null || pts.Count < 2) return;

        Vector2[] localPts = new Vector2[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 local = hitbox.transform.InverseTransformPoint(pts[i]);
            localPts[i] = local;
        }

        hitbox.points = localPts;
        hitbox.edgeRadius = edgeRadius * (grid != null ? grid.cellSize : 1f);
        hitbox.isTrigger = true;
    }

    // ===================== ROUNDED PATH BUILDER =====================

    List<Vector3> BuildRoundedPath(List<Vector2Int> cs)
    {
        var outPts = new List<Vector3>();
        if (grid == null || cs == null || cs.Count < 2) return outPts;

        float r = Mathf.Clamp(cornerRadiusCells, 0f, 0.5f) * grid.cellSize;
        int seg = Mathf.Max(2, arcSegments);

        List<Vector3> w = new List<Vector3>(cs.Count);
        for (int i = 0; i < cs.Count; i++)
        {
            var p = grid.CellToWorld(cs[i]);
            p.z = 0f;
            w.Add(p);
        }

        outPts.Add(w[0]);

        for (int i = 1; i < w.Count - 1; i++)
        {
            Vector3 prev = w[i - 1];
            Vector3 cur = w[i];
            Vector3 next = w[i + 1];

            Vector3 d0 = (cur - prev);
            Vector3 d1 = (next - cur);

            Vector3 a0 = AxisDir(d0);
            Vector3 a1 = AxisDir(d1);

            if (a0 == a1 || a0 == -a1)
            {
                outPts.Add(cur);
                continue;
            }

            if (r <= 0.0001f)
            {
                outPts.Add(cur);
                continue;
            }

            Vector3 pIn  = cur - a0 * r;
            Vector3 pOut = cur + a1 * r;

            if ((outPts[outPts.Count - 1] - pIn).sqrMagnitude > 1e-6f)
                outPts.Add(pIn);

            Vector3 center = cur - a0 * r + a1 * r;

            Vector3 vStart = (pIn - center);
            Vector3 vEnd   = (pOut - center);

            float cross = Cross2(a0, a1);

            float ang0 = Mathf.Atan2(vStart.y, vStart.x);
            float ang1 = Mathf.Atan2(vEnd.y, vEnd.x);

            float delta = (cross > 0f) ? Mathf.PI * 0.5f : -Mathf.PI * 0.5f;
            ang1 = ang0 + delta;

            for (int s = 1; s <= seg; s++)
            {
                float t = (float)s / seg;
                float a = ang0 + delta * t;
                Vector3 p = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r;
                outPts.Add(p);
            }

        }

        Vector3 last = w[w.Count - 1];
        if ((outPts[outPts.Count - 1] - last).sqrMagnitude > 1e-6f)
            outPts.Add(last);

        RemoveNearDup(outPts, 0.0005f * grid.cellSize);

        return outPts;
    }

    Vector3 AxisDir(Vector3 d)
    {
        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            return new Vector3(Mathf.Sign(d.x), 0f, 0f);
        else
            return new Vector3(0f, Mathf.Sign(d.y), 0f);
    }

    float Cross2(Vector3 a, Vector3 b) => a.x * b.y - a.y * b.x;

    void RemoveNearDup(List<Vector3> pts, float eps)
    {
        if (pts == null || pts.Count < 2) return;
        float eps2 = eps * eps;

        for (int i = pts.Count - 2; i >= 0; i--)
        {
            if ((pts[i + 1] - pts[i]).sqrMagnitude <= eps2)
                pts.RemoveAt(i + 1);
        }
    }

    // ===================== WINDOW EXTRACT (NO DIAGONAL CORNER BUG) =====================

    static float Smooth01(float x) => x * x * (3f - 2f * x);

    float[] BuildCum(List<Vector3> pts)
    {
        float[] cum = new float[pts.Count];
        cum[0] = 0f;
        for (int i = 1; i < pts.Count; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        return cum;
    }

    float PolylineLength(List<Vector3> pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Count; i++)
            len += Vector3.Distance(pts[i - 1], pts[i]);
        return len;
    }

    Vector3 PointAtDist(List<Vector3> pts, float[] cum, float dist)
    {
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (dist <= 0f) return pts[0];

        float total = cum[cum.Length - 1];
        if (dist >= total) return pts[pts.Count - 1];

        // binary search
        int lo = 0, hi = cum.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (cum[mid] < dist) lo = mid;
            else hi = mid;
        }

        Vector3 a = pts[lo];
        Vector3 b = pts[hi];

        float segLen = Mathf.Max(0.0001f, cum[hi] - cum[lo]);
        float t = (dist - cum[lo]) / segLen;
        Vector3 p = Vector3.Lerp(a, b, t);
        p.z = 0f;
        return p;
    }

    List<Vector3> ExtractWindow(List<Vector3> basePts, float[] cum, float startDist, float length)
    {
        float total = cum[cum.Length - 1];
        float endDist = startDist + length;

        startDist = Mathf.Clamp(startDist, 0f, total);
        endDist   = Mathf.Clamp(endDist,   0f, total);

        float eps = 0.0005f * (grid != null ? grid.cellSize : 1f);

        var outPts = new List<Vector3>(basePts.Count);

        outPts.Add(PointAtDist(basePts, cum, startDist));

        for (int i = 1; i < basePts.Count - 1; i++)
        {
            float d = cum[i];
            if (d >= startDist - eps && d <= endDist + eps)
                outPts.Add(basePts[i]);
        }

        outPts.Add(PointAtDist(basePts, cum, endDist));

        RemoveNearDup(outPts, eps);
        if (outPts.Count < 2)
        {
            outPts.Clear();
            outPts.Add(basePts[0]);
            outPts.Add(basePts[basePts.Count - 1]);
        }
        return outPts;
    }

    public void BindGridAndRegister(GridManager g)
    {
        grid = g;
        if (grid == null) return;

        // register initial occupancy
        grid.RegisterPiece(this, cells);
    }

}
