using UnityEngine;

public class VectorSpawner : MonoBehaviour
{
    public GridManager grid;
    public VectorPiece vectorPrefab;
    public Transform root;

    [Header("Spawn")]
    public int tries = 500;
    public int minLen = 2;
    public int maxLen = 5;

    void Start()
    {
        SpawnRandomVectors();
    }

    [ContextMenu("SpawnRandomVectors")]
    public void SpawnRandomVectors()
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);

        for (int t = 0; t < tries; t++)
        {
            var head = new Vector2Int(Random.Range(0, grid.width), Random.Range(0, grid.height));
            var dir = (Dir)Random.Range(0, 4);
            int len = Random.Range(minLen, maxLen + 1);

            if (!grid.CanPlace(head, dir, len)) continue;

            var v = Instantiate(vectorPrefab, root);
            v.Init(grid, head, dir, len);
        }
    }
}
