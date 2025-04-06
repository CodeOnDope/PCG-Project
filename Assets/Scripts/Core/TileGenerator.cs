using UnityEngine;
using UnityEngine.Tilemaps;

public class TileGenerator : MonoBehaviour
{
    [Header("Tile Settings")]
    public TileBase floorTile;
    public TileBase wallTile;
    public Tilemap tilemap;

    public void GenerateTiles(TileType[,] grid)
    {
        if (tilemap == null || floorTile == null || wallTile == null)
        {
            Debug.LogError("Tilemap or tiles not assigned!");
            return;
        }

        tilemap.ClearAllTiles();

        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);
                switch (grid[x, y])
                {
                    case TileType.Floor:
                        tilemap.SetTile(position, floorTile);
                        break;
                    case TileType.Wall:
                        tilemap.SetTile(position, wallTile);
                        break;
                    default:
                        tilemap.SetTile(position, null);
                        break;
                }
            }
        }
    }
}