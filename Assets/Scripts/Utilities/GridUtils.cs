using UnityEngine;

public static class GridUtils
{
    public static bool IsWithinBounds(int x, int y, int width, int height)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public static Vector2Int GetGridPosition(Vector3 worldPosition, float tileSize)
    {
        return new Vector2Int(Mathf.FloorToInt(worldPosition.x / tileSize), Mathf.FloorToInt(worldPosition.y / tileSize));
    }

    public static Vector3 GetWorldPosition(Vector2Int gridPosition, float tileSize)
    {
        return new Vector3(gridPosition.x * tileSize, gridPosition.y * tileSize, 0);
    }

    public static void ClearGrid(int[,] grid)
    {
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                grid[x, y] = 0; // Assuming 0 represents an empty tile
            }
        }
    }
}