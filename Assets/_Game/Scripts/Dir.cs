using UnityEngine;

public enum Dir { Up, Down, Left, Right }

public static class DirUtil
{
    public static Vector2Int Delta(this Dir d) => d switch
    {
        Dir.Up => new Vector2Int(0, 1),
        Dir.Down => new Vector2Int(0, -1),
        Dir.Left => new Vector2Int(-1, 0),
        Dir.Right => new Vector2Int(1, 0),
        _ => Vector2Int.zero
    };

    // giả sử sprite mũi tên mặc định hướng Right
    public static float ZRot(this Dir d) => d switch
    {
        Dir.Right => 0f,
        Dir.Up => 90f,
        Dir.Left => 180f,
        Dir.Down => -90f,
        _ => 0f
    };
}
