using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Spine.Unity;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GridWavyLineMesh : MonoBehaviour
{
    [Header("Refs")]
    public GridManager grid;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    public PolygonCollider2D poly;        // collider theo mesh
    public Transform destroyRoot;

    [Header("Head (Spine at end)")]
    public SkeletonAnimation headPrefab;
    public SkeletonAnimation head;
    [SerializeField] float headForwardOffsetCells = 0.0f;
    [SerializeField] float headSideOffsetCells = 0.0f;
    public bool rotateHeadAlongDirection = true;
    [SerializeField] float headYOffsetWorld = -0.2f;
    public float headDownAlongLineCells = 0.25f;

    [Header("Caps")]
    public bool roundStartCap = true;
    [Range(2, 24)] public int startCapSegments = 10;
    [Range(0.2f, 2f)] public float startCapRadiusMul = 1f;

    [Header("Base path in GRID (turn points)")]
    public List<Vector2Int> turnCells = new();

    [Header("Visual")]
    [Range(0.02f, 1f)] public float lineWidth = 0.18f;
    public Material material;

    [Header("Start Fade (vertex alpha)")]
    public bool startFade = true;
    [Tooltip("Độ dài đoạn mờ ở đầu (CELL).")]
    [Range(0f, 5f)] public float fadeStartLengthCells = 1.2f;
    [Tooltip("Alpha tại đúng điểm đầu (0..1).")]
    [Range(0f, 1f)] public float fadeStartAlpha = 0.15f;
    [Tooltip("Độ cong fade (cao hơn = lên alpha nhanh hơn).")]
    [Range(0.2f, 4f)] public float fadeCurve = 1.6f;

    [Header("Wave Params (CELL units)")]
    [Range(0f, 0.45f)] public float amplitudeCells = 0.22f;
    [Range(0.5f, 6f)] public float wavelengthCells = 2.2f;
    public float phase = 0f;

    [Header("Uniform Wave (global)")]
    public bool uniformWave = true;
    public float uniformPhase = 0f;

    [Header("Sampling")]
    [Range(6, 80)] public int samplesPerCell = 18;

    [Header("Smoothing Corners (base path)")]
    [Range(0f, 0.49f)] public float cornerRadiusCells = 0.35f;
    [Range(2, 16)] public int cornerSegments = 10;

    [Header("Corner Protection (anti-broken corners)")]
    [Range(0f, 2.5f)] public float cornerProtectCells = 1.05f;
    [Range(0.05f, 1f)] public float cornerProtectCurve = 0.35f;

    [Header("Wave Quality")]
    [Range(0, 6)] public int waveSmoothIters = 2;
    [Range(0f, 1f)] public float cornerAmpMul = 0.25f;
    [Range(0.5f, 6f)] public float cornerSharpness = 2.0f;

    [Header("Click Move")]
    public bool moveOnClick = true;
    [Range(0.1f, 20f)] public float moveSpeedCellsPerSec = 6f;

    [Header("Move Mode")]
    public bool moveForever = true;
    [Range(0f, 50f)] public float extraOutCells = 6f;

    [Header("Destroy")]
    public bool destroyAfterMove = false;
    [Range(0.1f, 10f)] public float destroyDelay = 2f;

    [Header("Block Check (prevent move if blocked ahead)")]
    public bool blockIfAhead = true;
    public LayerMask lineLayerMask = ~0;
    [Range(0.05f, 2f)] public float probeAheadCells = 0.35f;
    [Range(0.2f, 1.5f)] public float probeRadiusMul = 0.75f;

    [Header("On Collision")]
    public bool returnToStartOnBlock = true;
    [Range(2f, 30f)] public float returnSpeedCellsPerSec = 12f;
    [Range(0.01f, 0.5f)] public float returnStopEpsCells = 0.02f;

    [Header("Head Rotation Fix")]
    [Tooltip("Góc bù thêm cho head (độ). Dùng để khớp hướng mặc định của Spine.")]
    public float headAngleOffset = 0f;

    [Tooltip("Nếu bật: đi sang trái sẽ dùng flipX thay vì quay 180 (tránh bị lật ngược khó chịu).")]
    public bool flipHeadWhenLeft = true;

    [Tooltip("Nếu Spine mặc định nhìn sang TRÁI, bật cái này để đảo logic.")]
    public bool spineFacesLeftByDefault = false;


    // ===================== runtime =====================

    Camera cam;

    Mesh mesh;
    Vector3[] verts;
    Vector2[] uvs;
    int[] tris;
    Color32[] colors; // <==== FADE ALPHA HERE

    // path (centerline in world)
    List<Vector3> basePts;
    List<float> cum;
    float totalLen;

    // corner positions in arc-length (for protection)
    List<float> cornerS;

    float movingOffset;
    bool isMoving;
    bool endedByBlock;

    Coroutine moveCR;
    Coroutine returnCR;

    // temp arrays to avoid GC
    float[] cumLen;
    float[] waveArr;
    Vector2[] tangents;
    Vector2[] normals;

    // Cached axis for head so offset never flips
    Vector3 headForwardAxisW = Vector3.right; // world
    Vector3 headSideAxisW = Vector3.up;       // world
    bool headAxisReady = false;
    bool registeredToGM = false;
    bool consumedHeartThisClick = false;

    void Awake()
    {
        if (!grid) grid = FindObjectOfType<GridManager>();
        if (!meshFilter) meshFilter = GetComponent<MeshFilter>();
        if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
        if (!destroyRoot) destroyRoot = transform;

        poly = GetComponent<PolygonCollider2D>();
        if (!poly) poly = gameObject.AddComponent<PolygonCollider2D>();
        poly.isTrigger = true;

        cam = Camera.main;

        if (!mesh)
        {
            mesh = new Mesh();
            mesh.name = "GridWavyLineMesh";
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
        }

        if (material) meshRenderer.sharedMaterial = material;
    }
    void OnEnable()
    {
        if (!registeredToGM && GameManager.Instance != null)
        {
            GameManager.Instance.RegisterLine();
            registeredToGM = true;
        }
    }

    void Start()
    {
        if (!grid) return;
        BakeBasePath();
        BuildHead();
        RebuildVisualAndCollider();
    }

    void Update()
    {
        if (!moveOnClick) return;
        if (isMoving) return;
        if (!grid || !cam) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryPick(Mouse.current.position.ReadValue());

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            TryPick(Touchscreen.current.primaryTouch.position.ReadValue());
    }

    void OnDisable()
    {
        if (registeredToGM && GameManager.Instance != null)
        {
            GameManager.Instance.UnregisterLine();
            registeredToGM = false;
        }
    }

    void BuildHead()
    {
        if (!headPrefab) return;

        if (head) Destroy(head.gameObject);
        head = Instantiate(headPrefab, transform);
    }

    void UpdateHeadAtEnd_NoWave()
    {
        if (!head) return;
        if (!grid || basePts == null || basePts.Count < 2) return;
        if (!headAxisReady) CacheHeadAxesFromLastSegment();

        float cs = grid.cellSize;

        float sEnd = totalLen + movingOffset;
        Vector3 p = PointAtExtended(basePts, cum, sEnd);
        p.z = 0f;

        p += headForwardAxisW * (headForwardOffsetCells * cs);
        p += headSideAxisW * (headSideOffsetCells * cs);
        p += headSideAxisW * (headDownAlongLineCells * cs);

        p.y += headYOffsetWorld;

        p.z = 0f;

        Transform ht = head.transform;
        ht.position = p;

        if (rotateHeadAlongDirection)
        {
            Vector3 f = headForwardAxisW.normalized;
            bool goingLeft = (f.x < -0.001f);

            if (flipHeadWhenLeft && Mathf.Abs(f.x) > Mathf.Abs(f.y))
            {
                float angle = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;

                angle = 0f;

                ht.rotation = Quaternion.Euler(0f, 0f, angle + headAngleOffset);

                float sx = Mathf.Abs(head.Skeleton.ScaleX);
                bool needFlip = goingLeft;

                if (spineFacesLeftByDefault) needFlip = !needFlip;

                head.Skeleton.ScaleX = needFlip ? -sx : sx;
            }
            else
            {
                float angle = Mathf.Atan2(f.y, f.x) * Mathf.Rad2Deg;

                if (spineFacesLeftByDefault) angle += 180f;

                ht.rotation = Quaternion.Euler(0f, 0f, angle + headAngleOffset);

                float sx = Mathf.Abs(head.Skeleton.ScaleX);
                head.Skeleton.ScaleX = sx;
            }
        }

    }

    void TryPick(Vector2 screenPos)
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.GamePlay)
            return;

        if (!grid || !cam || !poly)
            return;

        Vector3 w = cam.ScreenToWorldPoint(screenPos);
        w.z = 0f;

        // 1) Phải click trúng line
        if (!poly.OverlapPoint(w))
            return;

        // Click đúng line -> head idle2
        if (head) head.AnimationState.SetAnimation(0, "idle2", true);

        bool canMove = true;

        // 2) Nếu base path không hợp lệ thì coi như không move được
        if (basePts == null || basePts.Count < 2)
            canMove = false;

        // 3) Kiểm tra block ngay phía trước
        if (canMove && blockIfAhead)
        {
            float cs = grid.cellSize;
            float step = Mathf.Max(0.01f, probeAheadCells) * cs;
            bool blocked = IsBlockedStep(movingOffset, movingOffset + step);
            if (blocked) canMove = false;
        }

        // 4) Nếu không thể move -> trừ tim 1 lần cho click này
        if (!canMove)
        {
            EnableCollider();
            if (GameManager.Instance != null)
                GameManager.Instance.LoseHeart();
            return;
        }

        // 5) Có thể move -> chạy
        StartMove();
    }

    public void StartMove()
    {
        if (isMoving) return;
        if (basePts == null || basePts.Count < 2) return;

        DisableCollider();    

        if (moveCR != null) StopCoroutine(moveCR);
        if (returnCR != null) StopCoroutine(returnCR);

        
        moveCR = StartCoroutine(MoveRoutine());
    }

    IEnumerator MoveRoutine()
    {
        isMoving = true;
        if (!destroyAfterMove)
            EnableCollider();
        endedByBlock = false;

        float speed = moveSpeedCellsPerSec * grid.cellSize;
        float targetDist = totalLen + extraOutCells * grid.cellSize;

        while (true)
        {
            float step = speed * Time.deltaTime;
            float nextOffset = movingOffset + step;

            if (blockIfAhead && IsBlockedStep(movingOffset, nextOffset))
            {
                endedByBlock = true;
                if (GameManager.Instance != null)
                    GameManager.Instance.LoseHeart();
                
                if (returnToStartOnBlock)
                {
                    if (returnCR != null) StopCoroutine(returnCR);
                    returnCR = StartCoroutine(ReturnRoutine(movingOffset, 0f));
                    yield return returnCR;
                }
                break;
            }

            movingOffset = nextOffset;
            RebuildVisualAndCollider();

            if (!moveForever && movingOffset >= targetDist) break;
            yield return null;
        }

        isMoving = false;

        if (destroyAfterMove && !endedByBlock)
        {
            yield return new WaitForSeconds(destroyDelay);
            Destroy(destroyRoot ? destroyRoot.gameObject : gameObject);
        }
    }

    IEnumerator ReturnRoutine(float from, float to)
    {
        float cs = grid.cellSize;
        float v = Mathf.Max(0.01f, returnSpeedCellsPerSec) * cs;
        float eps = Mathf.Max(0.0001f, returnStopEpsCells) * cs;

        movingOffset = from;

        while (Mathf.Abs(movingOffset - to) > eps)
        {
            movingOffset = Mathf.MoveTowards(movingOffset, to, v * Time.deltaTime);
            RebuildVisualAndCollider();
            yield return null;
        }

        movingOffset = to;
        RebuildVisualAndCollider();
        EnableCollider();
    }

    // ===================== BUILD PATH =====================

    void BakeBasePath()
    {
        basePts = null;
        cum = null;
        cornerS = null;
        totalLen = 0f;

        if (!grid || turnCells == null || turnCells.Count < 2) return;

        var cells = ExpandCellsByStep(turnCells);
        if (cells.Count < 2) return;

        var raw = new List<Vector3>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 p = grid.CellToWorld(cells[i]);
            p.z = 0f;
            raw.Add(p);
        }

        raw = RemoveConsecutiveDuplicates(raw, 1e-6f);
        if (raw.Count < 2) return;

        float cs = grid.cellSize;
        float r = Mathf.Clamp(cornerRadiusCells, 0f, 0.49f) * cs;

        var smoothed = new List<Vector3>(raw.Count * 3);
        smoothed.Add(raw[0]);

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
                smoothed.Add(B);
                continue;
            }

            d1 /= len1;
            d2 /= len2;

            if (Vector3.Dot(d1, d2) > 0.999f)
            {
                smoothed.Add(B);
                continue;
            }

            float t = Mathf.Min(r, 0.45f * Mathf.Min(len1, len2));
            Vector3 P = B - d1 * t;
            Vector3 Q = B + d2 * t;

            if ((smoothed[smoothed.Count - 1] - P).sqrMagnitude > 1e-10f)
                smoothed.Add(P);

            for (int s = 1; s <= cornerSegments; s++)
            {
                float u = (float)s / cornerSegments;
                Vector3 pt = (1 - u) * (1 - u) * P + 2 * (1 - u) * u * B + u * u * Q;
                smoothed.Add(pt);
            }
        }

        smoothed.Add(raw[raw.Count - 1]);
        smoothed = RemoveConsecutiveDuplicates(smoothed, 1e-6f);
        if (smoothed.Count < 2) return;

        basePts = smoothed;

        cum = BuildCum(basePts);
        totalLen = cum[cum.Count - 1];

        cornerS = new List<float>();
        for (int i = 1; i < raw.Count - 1; i++)
        {
            if (IsTurn(raw[i - 1], raw[i], raw[i + 1]))
            {
                float sCorner = ProjectToArcLength(basePts, cum, raw[i]);
                cornerS.Add(sCorner);
            }
        }

        movingOffset = 0f;
        CacheHeadAxesFromLastSegment();
    }

    static bool IsTurn(Vector3 A, Vector3 B, Vector3 C)
    {
        Vector3 d1 = (B - A);
        Vector3 d2 = (C - B);
        if (d1.sqrMagnitude < 1e-10f || d2.sqrMagnitude < 1e-10f) return false;
        d1.Normalize(); d2.Normalize();
        return Vector3.Dot(d1, d2) < 0.999f;
    }

    // ===================== VISUAL BUILD =====================

    public void RebuildVisualAndCollider()
    {
        if (!grid || basePts == null || basePts.Count < 2)
        {
            ClearMeshAndCollider();
            return;
        }

        float cs = grid.cellSize;
        int sampleCount = Mathf.Max(2, Mathf.CeilToInt((totalLen / cs) * samplesPerCell));

        float amp = amplitudeCells * cs;
        float waveLen = Mathf.Max(0.0001f, wavelengthCells * cs);
        float maxAmp = 0.35f * cs;
        float phi = uniformWave ? uniformPhase : phase;

        Vector3 gridOrigin = grid.CellToWorld(new Vector2Int(0, 0));
        gridOrigin.z = 0f;

        EnsureTempArrays(sampleCount);

        var centerWorld = new Vector3[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (sampleCount == 1) ? 0f : (float)i / (sampleCount - 1);
            float s = t * totalLen;
            float s2 = s + movingOffset;

            Vector3 p = PointAtExtended(basePts, cum, s2);
            p.z = 0f;
            centerWorld[i] = p;
        }

        ComputeSmoothFrames(centerWorld, tangents, normals);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (sampleCount == 1) ? 0f : (float)i / (sampleCount - 1);
            float s = t * totalLen;
            float s2 = s + movingOffset;

            float protectW = CornerProtectWeight_ByCornerS(s2, cs);

            float cw = 0f;
            if (i > 0 && i < sampleCount - 1)
                cw = CornerWeight(tangents[i - 1], tangents[i + 1], cornerSharpness);

            float cornerW = Mathf.Max(protectW, cw);
            float ampUsed = Mathf.Lerp(amp, amp * cornerAmpMul, cornerW);

            float axT = Mathf.Abs(tangents[i].x);
            float ayT = Mathf.Abs(tangents[i].y);
            bool isVertical = (ayT >= axT);

            float waveCoord = isVertical
                ? (centerWorld[i].y - gridOrigin.y)
                : (centerWorld[i].x - gridOrigin.x);

            float w = Mathf.Sin((2f * Mathf.PI / waveLen) * waveCoord + phi) * ampUsed;
            w = Mathf.Clamp(w, -maxAmp, maxAmp);

            waveArr[i] = w;
        }

        if (waveSmoothIters > 0)
            Smooth1D(waveArr, waveSmoothIters);

        var centerLocal = new Vector3[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float axT = Mathf.Abs(tangents[i].x);
            float ayT = Mathf.Abs(tangents[i].y);
            bool isVertical = (ayT >= axT);

            Vector3 nWorld = isVertical ? Vector3.right : Vector3.up;

            Vector3 pW = centerWorld[i] + nWorld * waveArr[i];
            pW.z = 0f;

            Vector3 pL = transform.InverseTransformPoint(pW);
            pL.z = 0f;
            centerLocal[i] = pL;
        }

        BuildRibbonMesh(centerLocal);
        BuildPolygonColliderFromRibbon(centerLocal);

        UpdateHeadAtEnd_NoWave();
    }

    void EnsureTempArrays(int n)
    {
        if (waveArr == null || waveArr.Length != n) waveArr = new float[n];
        if (tangents == null || tangents.Length != n) tangents = new Vector2[n];
        if (normals == null || normals.Length != n) normals = new Vector2[n];
    }

    float CornerProtectWeight_ByCornerS(float s2, float cs)
    {
        if (cornerS == null || cornerS.Count == 0) return 0f;

        float protect = Mathf.Max(0f, cornerProtectCells) * cs;
        if (protect <= 1e-6f) return 0f;

        float best = float.MaxValue;
        for (int i = 0; i < cornerS.Count; i++)
        {
            float d = Mathf.Abs(s2 - cornerS[i]);
            if (d < best) best = d;
        }

        if (best >= protect) return 0f;

        float x = 1f - (best / protect);
        float k = Mathf.Max(0.05f, cornerProtectCurve);
        return Mathf.Pow(x, k);
    }

    // ===================== MESH =====================

    void BuildRibbonMesh(Vector3[] center)
    {
        int n = (center == null) ? 0 : center.Length;
        if (n < 2) { ClearMeshOnly(); return; }

        float halfW = Mathf.Max(0.001f, lineWidth * 0.5f);
        float capR = halfW * Mathf.Max(0.01f, startCapRadiusMul);

        int capSeg = (roundStartCap ? Mathf.Max(2, startCapSegments) : 0);

        int capVert = roundStartCap ? (1 + (capSeg + 1)) : 0;
        int vCount = n * 2 + capVert;

        int tCount = (n - 1) * 6 + (roundStartCap ? capSeg * 3 : 0);

        if (verts == null || verts.Length != vCount) verts = new Vector3[vCount];
        if (uvs == null || uvs.Length != vCount) uvs = new Vector2[vCount];
        if (tris == null || tris.Length != tCount) tris = new int[tCount];
        if (colors == null || colors.Length != vCount) colors = new Color32[vCount];

        // UV along length (ribbon)
        if (cumLen == null || cumLen.Length != n) cumLen = new float[n];
        float total = 0f;
        cumLen[0] = 0f;
        for (int i = 1; i < n; i++)
        {
            total += Vector3.Distance(center[i - 1], center[i]);
            cumLen[i] = total;
        }
        float invTotal = (total > 1e-6f) ? 1f / total : 0f;

        // Fade length in WORLD units (based on grid cell)
        float cs = (grid ? grid.cellSize : 1f);
        float fadeLenW = Mathf.Max(0.0001f, fadeStartLengthCells * cs);

        // Build ribbon verts
        for (int i = 0; i < n; i++)
        {
            Vector3 dir;
            if (i == 0) dir = center[1] - center[0];
            else if (i == n - 1) dir = center[n - 1] - center[n - 2];
            else dir = center[i + 1] - center[i - 1];

            if (dir.sqrMagnitude < 1e-10f) dir = Vector3.right;
            else dir.Normalize();

            Vector3 nrm = new Vector3(-dir.y, dir.x, 0f);

            int vi = i * 2;
            verts[vi + 0] = center[i] - nrm * halfW;
            verts[vi + 1] = center[i] + nrm * halfW;

            float u = cumLen[i] * invTotal;
            uvs[vi + 0] = new Vector2(u, 0f);
            uvs[vi + 1] = new Vector2(u, 1f);

            // ===== START FADE (vertex alpha) =====
            float a01 = 1f;
            if (startFade && fadeStartLengthCells > 0.0001f)
            {
                float x = Mathf.Clamp01(cumLen[i] / fadeLenW);        
                x = Mathf.Pow(x, Mathf.Max(0.01f, fadeCurve));      
                a01 = Mathf.Lerp(fadeStartAlpha, 1f, x);
            }

            byte aByte = (byte)Mathf.Clamp(Mathf.RoundToInt(a01 * 255f), 0, 255);
            colors[vi + 0] = new Color32(255, 255, 255, aByte);
            colors[vi + 1] = new Color32(255, 255, 255, aByte);
        }

        // Ribbon triangles
        int ti = 0;
        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 2;
            int b = a + 1;
            int c = a + 2;
            int d = a + 3;

            tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
            tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
        }

        // Start cap (rounded)
        if (roundStartCap)
        {
            Vector3 t0 = center[1] - center[0];
            if (t0.sqrMagnitude < 1e-10f) t0 = Vector3.right;
            else t0.Normalize();

            Vector3 n0 = new Vector3(-t0.y, t0.x, 0f);

            int capBase = n * 2;

            verts[capBase] = center[0];
            uvs[capBase] = new Vector2(0f, 0.5f);

            // cap alpha = alpha at start (fadeStartAlpha)
            byte capA = (byte)Mathf.Clamp(Mathf.RoundToInt((startFade ? fadeStartAlpha : 1f) * 255f), 0, 255);
            colors[capBase] = new Color32(255, 255, 255, capA);

            for (int s = 0; s <= capSeg; s++)
            {
                float a = Mathf.Lerp(Mathf.PI * 0.5f, -Mathf.PI * 0.5f, (float)s / capSeg);
                Vector3 p = center[0] + (-t0 * Mathf.Cos(a) + n0 * Mathf.Sin(a)) * capR;

                int idx = capBase + 1 + s;
                verts[idx] = p;
                uvs[idx] = new Vector2(0f, 0.5f);
                colors[idx] = new Color32(255, 255, 255, capA);
            }

            for (int s = 0; s < capSeg; s++)
            {
                int c = capBase;
                int p0 = capBase + 1 + s;
                int p1 = capBase + 1 + (s + 1);

                tris[ti++] = c;
                tris[ti++] = p0;
                tris[ti++] = p1;
            }
        }

        // Upload mesh
        mesh.Clear(false);
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.colors32 = colors; 
        mesh.RecalculateBounds();
    }

    void BuildPolygonColliderFromRibbon(Vector3[] center)
    {
        if (!poly) return;

        int n = (center == null) ? 0 : center.Length;
        if (n < 2)
        {
            poly.pathCount = 0;
            return;
        }

        float halfW = Mathf.Max(0.001f, lineWidth * 0.5f);

        EnsureTempArrays(n);
        ComputeSmoothFrames(center, tangents, normals);

        bool cap = roundStartCap;
        int capSeg = cap ? Mathf.Max(2, startCapSegments) : 0;
        float capR = halfW * Mathf.Max(0.01f, startCapRadiusMul);

        int skip0 = cap ? 1 : 0;

        int leftCount = n - skip0;
        int rightCount = n - skip0;
        int capCount = cap ? (capSeg + 1) : 0;

        int total = capCount + leftCount + rightCount;
        if (total < 3)
        {
            poly.pathCount = 0;
            return;
        }

        var path = new Vector2[total];
        int k = 0;

        if (cap)
        {
            Vector2 t0 = tangents[0];
            if (t0.sqrMagnitude < 1e-8f) t0 = (Vector2)(center[1] - center[0]);
            t0 = SafeNormalize(t0);

            Vector2 n0 = normals[0];
            if (n0.sqrMagnitude < 1e-8f) n0 = new Vector2(-t0.y, t0.x);
            n0 = SafeNormalize(n0);

            Vector2 c0 = (Vector2)center[0];

            for (int s = 0; s <= capSeg; s++)
            {
                float a = Mathf.Lerp(Mathf.PI * 0.5f, -Mathf.PI * 0.5f, (float)s / capSeg);
                Vector2 p = c0 + (-t0 * Mathf.Cos(a) + n0 * Mathf.Sin(a)) * capR;
                path[k++] = p;
            }
        }

        for (int i = skip0; i < n; i++)
        {
            Vector3 nrm = new Vector3(normals[i].x, normals[i].y, 0f);
            Vector3 left = center[i] - nrm * halfW;
            path[k++] = new Vector2(left.x, left.y);
        }

        for (int i = n - 1; i >= skip0; i--)
        {
            Vector3 nrm = new Vector3(normals[i].x, normals[i].y, 0f);
            Vector3 right = center[i] + nrm * halfW;
            path[k++] = new Vector2(right.x, right.y);
        }

        if (k != total) System.Array.Resize(ref path, k);

        poly.pathCount = 1;
        poly.SetPath(0, path);
        poly.isTrigger = true;
    }

    void ClearMeshOnly()
    {
        if (mesh) mesh.Clear(false);
    }

    void ClearMeshAndCollider()
    {
        ClearMeshOnly();
        if (poly) poly.pathCount = 0;
    }

    // ===================== BLOCK / COLLISION =====================

    bool IsBlockedStep(float fromOffset, float toOffset)
    {
        if (basePts == null || basePts.Count < 2) return false;

        float cs = grid.cellSize;

        Vector3 p0 = PointAtExtended(basePts, cum, totalLen + fromOffset);
        Vector3 p1 = PointAtExtended(basePts, cum, totalLen + toOffset);

        Vector2 dir = (p1 - p0);
        float dist = dir.magnitude;
        if (dist < 1e-6f) return false;
        dir /= dist;

        float probeDist = Mathf.Max(0.01f, probeAheadCells) * cs;
        float radius = Mathf.Max(0.01f, lineWidth * probeRadiusMul);

        var filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = lineLayerMask;
        filter.useTriggers = true;

        RaycastHit2D[] hits = new RaycastHit2D[16];
        int hitCount = Physics2D.CircleCast(p0, radius, dir, filter, hits, probeDist);

        for (int i = 0; i < hitCount; i++)
        {
            var h = hits[i];
            if (!h.collider) continue;
            if (h.collider == poly) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    // ===================== HELPERS =====================

    static Vector2 SafeNormalize(Vector2 v)
    {
        float m = v.magnitude;
        return (m < 1e-6f) ? Vector2.right : (v / m);
    }

    static void ComputeSmoothFrames(Vector3[] center, Vector2[] tangents, Vector2[] normals)
    {
        int n = center.Length;
        if (n < 2) return;

        Vector2 prevN = Vector2.up;

        for (int i = 0; i < n; i++)
        {
            Vector2 t;
            if (i == 0) t = (Vector2)(center[1] - center[0]);
            else if (i == n - 1) t = (Vector2)(center[n - 1] - center[n - 2]);
            else t = (Vector2)(center[i + 1] - center[i - 1]);

            t = SafeNormalize(t);

            Vector2 nPerp = new Vector2(-t.y, t.x);
            if (Vector2.Dot(nPerp, prevN) < 0f) nPerp = -nPerp;

            tangents[i] = t;
            normals[i] = nPerp;
            prevN = nPerp;
        }
    }

    static float CornerWeight(Vector2 tPrev, Vector2 tNext, float sharpness = 2.0f)
    {
        float d = Mathf.Clamp(Vector2.Dot(tPrev, tNext), -1f, 1f);
        float w = 1f - Mathf.InverseLerp(1f, 0.0f, d);
        return Mathf.Pow(w, Mathf.Max(0.01f, sharpness));
    }

    static void Smooth1D(float[] a, int iters = 2)
    {
        int n = a.Length;
        if (n < 3) return;

        float[] tmp = new float[n];

        for (int k = 0; k < iters; k++)
        {
            tmp[0] = a[0];
            tmp[n - 1] = a[n - 1];

            for (int i = 1; i < n - 1; i++)
                tmp[i] = (a[i - 1] + a[i] * 2f + a[i + 1]) * 0.25f;

            for (int i = 0; i < n; i++) a[i] = tmp[i];
        }
    }

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

    static List<float> BuildCum(List<Vector3> pts)
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

    static Vector3 PointAtExtended(List<Vector3> pts, List<float> c, float dist)
    {
        if (pts == null || pts.Count == 0) return Vector3.zero;
        if (dist <= 0f) return pts[0];

        float total = c[c.Count - 1];
        if (dist >= total)
        {
            Vector3 last = pts[pts.Count - 1];
            Vector3 prev = pts[pts.Count - 2];
            Vector3 dir = (last - prev);
            if (dir.sqrMagnitude < 1e-8f) dir = Vector3.right;
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

    static float ProjectToArcLength(List<Vector3> pts, List<float> cum, Vector3 worldPoint)
    {
        float bestS = 0f;
        float bestD2 = float.MaxValue;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i];
            Vector3 b = pts[i + 1];
            Vector3 ab = b - a;
            float ab2 = ab.sqrMagnitude;
            if (ab2 < 1e-10f) continue;

            float t = Vector3.Dot(worldPoint - a, ab) / ab2;
            t = Mathf.Clamp01(t);
            Vector3 proj = a + ab * t;

            float d2 = (worldPoint - proj).sqrMagnitude;
            if (d2 < bestD2)
            {
                bestD2 = d2;
                bestS = Mathf.Lerp(cum[i], cum[i + 1], t);
            }
        }

        return bestS;
    }

    void CacheHeadAxesFromLastSegment()
    {
        headAxisReady = false;
        if (turnCells == null || turnCells.Count < 2 || grid == null) return;

        Vector2Int a = turnCells[turnCells.Count - 2];
        Vector2Int b = turnCells[turnCells.Count - 1];
        Vector2Int d = b - a;

        if (d == Vector2Int.zero)
        {
            headForwardAxisW = Vector3.right;
            headSideAxisW = Vector3.up;
            headAxisReady = true;
            return;
        }

        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            headForwardAxisW = (d.x >= 0) ? Vector3.right : Vector3.left;
        else
            headForwardAxisW = (d.y >= 0) ? Vector3.up : Vector3.down;

        bool forwardIsVertical = Mathf.Abs(headForwardAxisW.y) > 0.5f;
        headSideAxisW = forwardIsVertical ? Vector3.right : Vector3.up;

        headAxisReady = true;
    }

    void SetColliderEnabled(bool on)
    {
        if (!poly) return;
        poly.enabled = on;
    }

    void EnableCollider()
    {
        if (!poly) return;
        poly.enabled = true;
    }

    void DisableCollider()
    {
        if (!poly) return;
        poly.enabled = false;
    }

}
