using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class VectorPiece : MonoBehaviour
{
    [Header("Move")]
    public float flySpeed = 12f;
    public float offscreenMargin = 2f;

    [Header("Visual")]
    public Transform visual; 
    public float thicknessScale = 1f;

    GridManager grid;
    Camera cam;

    public Vector2Int head { get; private set; }
    public Dir dir { get; private set; }
    public int len { get; private set; }

    readonly List<Vector2Int> occupied = new();
    bool flying;

    public void Init(GridManager g, Vector2Int headCell, Dir d, int length)
    {
        grid = g;
        cam = Camera.main;

        head = headCell;
        dir = d;
        len = Mathf.Max(1, length);

        // đặt vị trí world tại "tâm" của vector để collider dễ
        // Vector nằm dọc theo dir, bắt đầu từ head
        Vector3 headW = grid.GridToWorld(head);
        Vector3 tailW = grid.GridToWorld(head + dir.Delta() * (len - 1));
        transform.position = (headW + tailW) * 0.5f;

        // xoay theo hướng
        transform.rotation = Quaternion.Euler(0, 0, dir.ZRot());

        // scale visual để dài theo len
        if (visual != null)
        {
            // giả sử sprite mặc định dài 1 ô theo trục X local
            visual.localScale = new Vector3(len, thicknessScale, 1f);
        }
        else
        {
            transform.localScale = new Vector3(len, thicknessScale, 1f);
        }

        // collider phủ toàn bộ chiều dài (local X)
        var col = GetComponent<BoxCollider2D>();
        col.size = new Vector2(len, 0.8f);
        col.offset = Vector2.zero;

        // đăng ký occupancy cho tất cả ô chiếm
        occupied.Clear();
        for (int i = 0; i < len; i++)
        {
            var p = head + dir.Delta() * i;
            occupied.Add(p);
            grid.SetOcc(p, this);
        }
    }

    void OnMouseDown()
    {
        if (flying) return;
        TryFlyOut();
    }

    void TryFlyOut()
    {
        // Ô ngay phía trước đoạn cuối cùng:
        Vector2Int front = head + dir.Delta() * len;

        // nếu front trong map và có vật cản → không bay
        if (grid.InBounds(front) && grid.IsCellBlocked(front))
            return;

        // cho bay: xóa occupancy khỏi grid ngay lập tức
        foreach (var p in occupied)
            grid.ClearOcc(p, this);

        StartCoroutine(FlyOffscreen());
    }

    IEnumerator FlyOffscreen()
    {
        flying = true;

        Vector2 moveDir = ((Vector2)dir.Delta()).normalized;
        Vector3 target = ComputeOffscreenTarget(moveDir);

        while (Vector2.Distance(transform.position, target) > 0.05f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                flySpeed * Time.deltaTime
            );
            yield return null;
        }

        Destroy(gameObject);
    }

    Vector3 ComputeOffscreenTarget(Vector2 worldDir)
    {
        float z = 0f;
        float dist = -(cam.transform.position.z - z);

        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0, 0, dist));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1, 1, dist));

        Vector3 p = transform.position;

        if (Mathf.Abs(worldDir.x) > Mathf.Abs(worldDir.y))
        {
            float x = worldDir.x > 0 ? tr.x + offscreenMargin : bl.x - offscreenMargin;
            return new Vector3(x, p.y, 0);
        }
        else
        {
            float y = worldDir.y > 0 ? tr.y + offscreenMargin : bl.y - offscreenMargin;
            return new Vector3(p.x, y, 0);
        }
    }
}
