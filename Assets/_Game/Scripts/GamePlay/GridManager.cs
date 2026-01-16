using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Grid")]
    public int width = 3;
    public int height = 3;
    public float cellSize = 1f;
    public Vector2 origin = new Vector2(-1f, -1f);

    [Header("Visual Grid")]
    public bool drawGizmos = true;
    public bool drawRuntimeGrid = true;
    public float lineWidth = 0.03f;
    public int gridSortingOrder = 0;
    public Material lineMaterial;

    [Header("Runtime Grid Dots")]
    public bool runtimeDotsOnly = true;
    public float dotSize = 0.08f;
    public Sprite dotSprite;                 
    public int dotSortingOrder = -1;
    public Color dotColor = Color.grey;

    private readonly Dictionary<Vector2Int, MonoBehaviour> occupied = new();
    private readonly List<LineRenderer> runtimeLines = new();
    private readonly List<GameObject> runtimeDots = new();

    // ================== GRID API ==================
    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(
            origin.x + (cell.x + 0.5f) * cellSize,
            origin.y + (cell.y + 0.5f) * cellSize,
            0f
        );
    }

    public Vector3 CellCornerToWorld(int x, int y)
    {
        return new Vector3(
            origin.x + x * cellSize,
            origin.y + y * cellSize,
            0f
        );
    }

    public bool IsInside(Vector2Int cell)
        => cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;

    public bool IsOccupied(Vector2Int cell)
        => occupied.ContainsKey(cell);

    public MonoBehaviour GetOccupier(Vector2Int cell)
    {
        occupied.TryGetValue(cell, out var p);
        return p;
    }

    public void RegisterPiece(MonoBehaviour piece, List<Vector2Int> cells)
    {
        if (piece == null || cells == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (!IsInside(c)) continue;
            occupied[c] = piece;
        }
    }

    public void UnregisterPiece(MonoBehaviour piece, List<Vector2Int> cells)
    {
        if (piece == null || cells == null) return;

        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (!IsInside(c)) continue;

            if (occupied.TryGetValue(c, out var cur) && cur == piece)
                occupied.Remove(c);
        }
    }

    public bool IsBlocked(Vector2Int cell, MonoBehaviour requester)
    {
        if (!IsInside(cell)) return false;
        if (!occupied.TryGetValue(cell, out var occ)) return false;
        return occ != null && occ != requester;
    }

    // ================== BUILD FROM SCENE ==================
    public void BuildFromScene()
    {
        occupied.Clear();

        var linePieces = FindObjectsOfType<ArrowLinePiece>(true);
        foreach (var lp in linePieces)
        {
            lp.BindGridAndRegister(this); 
        }
    }

    private void Start()
    {
        BuildFromScene();

        if (drawRuntimeGrid)
        {
            if (runtimeDotsOnly) BuildRuntimeDots();
            else BuildRuntimeGrid();
        }
            
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (drawRuntimeGrid)
        {
            if (runtimeDotsOnly) BuildRuntimeDots();
            else BuildRuntimeGrid();
        }
        else
        {
            ClearRuntimeGrid();
            ClearRuntimeDots();
        }

    }
#endif

    // ================== RUNTIME GRID (GAME VIEW) ==================
    void BuildRuntimeGrid()
    {
        ClearRuntimeGrid();

        Material mat = lineMaterial;
        if (mat == null)
            mat = new Material(Shader.Find("Sprites/Default"));

        for (int x = 0; x <= width; x++)
        {
            Vector3 a = CellCornerToWorld(x, 0);
            Vector3 b = CellCornerToWorld(x, height);
            runtimeLines.Add(CreateLine($"GridLine_V_{x}", a, b, mat));
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 a = CellCornerToWorld(0, y);
            Vector3 b = CellCornerToWorld(width, y);
            runtimeLines.Add(CreateLine($"GridLine_H_{y}", a, b, mat));
        }
    }

    LineRenderer CreateLine(string name, Vector3 a, Vector3 b, Material mat)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.material = mat;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);

        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;

        lr.useWorldSpace = true;
        lr.sortingOrder = gridSortingOrder;
        return lr;
    }

    void ClearRuntimeGrid()
    {
        for (int i = 0; i < runtimeLines.Count; i++)
        {
            if (runtimeLines[i] != null)
                Destroy(runtimeLines[i].gameObject);
        }
        runtimeLines.Clear();
        ClearRuntimeDots();
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.white;

        for (int x = 0; x <= width; x++)
        {
            Vector3 a = new Vector3(origin.x + x * cellSize, origin.y, 0);
            Vector3 b = new Vector3(origin.x + x * cellSize, origin.y + height * cellSize, 0);
            Gizmos.DrawLine(a, b);
        }

        for (int y = 0; y <= height; y++)
        {
            Vector3 a = new Vector3(origin.x, origin.y + y * cellSize, 0);
            Vector3 b = new Vector3(origin.x + width * cellSize, origin.y + y * cellSize, 0);
            Gizmos.DrawLine(a, b);
        }

#if UNITY_EDITOR
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        float r = cellSize * 0.03f;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            Gizmos.DrawSphere(CellToWorld(new Vector2Int(x, y)), r);
#endif
    }

    void BuildRuntimeDots()
        {
            ClearRuntimeGrid();   
            ClearRuntimeDots();

            Sprite spriteToUse = dotSprite != null ? dotSprite : GenerateCircleSprite();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3 p = CellToWorld(new Vector2Int(x, y));
                    var go = new GameObject($"GridDot_{x}_{y}");
                    go.transform.SetParent(transform, false);
                    go.transform.position = p;

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = spriteToUse;
                    sr.color = dotColor;
                    sr.sortingOrder = dotSortingOrder;

                    // scale theo world units
                    go.transform.localScale = Vector3.one * dotSize;

                    runtimeDots.Add(go);
                }
            }
        }

        void ClearRuntimeDots()
        {
            for (int i = 0; i < runtimeDots.Count; i++)
            {
                if (runtimeDots[i] != null)
                    Destroy(runtimeDots[i]);
            }
            runtimeDots.Clear();
        }

        Sprite GenerateCircleSprite(int size = 64)
        {
            // Tạo texture tròn đơn giản
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(1, 1, 1, 0);
            Color white = Color.white;

            float r = (size - 1) * 0.5f;
            float r2 = r * r;
            Vector2 c = new Vector2(r, r);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c.x;
                    float dy = y - c.y;
                    float d2 = dx * dx + dy * dy;
                    tex.SetPixel(x, y, d2 <= r2 ? white : clear);
                }
            }
            tex.Apply();

            // pixelsPerUnit = size để scale bằng transform.localScale dễ
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

}
