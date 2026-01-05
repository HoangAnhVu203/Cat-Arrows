using UnityEngine;

public class GridManager : MonoBehaviour
{
    public int width = 8;
    public int height = 10;
    public float cellSize = 1f;
    public Vector2 origin = Vector2.zero;

    // mỗi ô đang bị chiếm bởi vector nào
    private VectorPiece[,] occ;

    // tường/vật cản cố định (prototype: có thể để false hết)
    private bool[,] blocked;

    void Awake()
    {
        occ = new VectorPiece[width, height];
        blocked = new bool[width, height];
    }

    public bool InBounds(Vector2Int p)
        => p.x >= 0 && p.x < width && p.y >= 0 && p.y < height;

    public Vector3 GridToWorld(Vector2Int p)
        => new Vector3(origin.x + p.x * cellSize, origin.y + p.y * cellSize, 0);

    public bool IsCellBlocked(Vector2Int p)
    {
        if (!InBounds(p)) return false;           // ngoài map coi như không chặn
        if (blocked[p.x, p.y]) return true;       // tường
        if (occ[p.x, p.y] != null) return true;   // có vector khác
        return false;
    }

    public void SetOcc(Vector2Int p, VectorPiece v)
    {
        if (!InBounds(p)) return;
        occ[p.x, p.y] = v;
    }

    public void ClearOcc(Vector2Int p, VectorPiece v)
    {
        if (!InBounds(p)) return;
        if (occ[p.x, p.y] == v) occ[p.x, p.y] = null;
    }

    public bool CanPlace(Vector2Int head, Dir dir, int len)
    {
        for (int i = 0; i < len; i++)
        {
            var p = head + dir.Delta() * i;
            if (!InBounds(p)) return false;
            if (IsCellBlocked(p)) return false;
        }
        return true;
    }
}
