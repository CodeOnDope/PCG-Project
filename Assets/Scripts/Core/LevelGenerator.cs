using UnityEngine;
using System.Collections.Generic;

public class LevelGenerator : MonoBehaviour
{/*
    [Header("Level Dimensions")]
    public int levelWidth = 100;
    public int levelHeight = 100;

    [Header("BSP Settings")]
    public int minRoomSize = 8;
    public int maxIterations = 5;

    [Header("Tile Prefabs")]
    public GameObject floorTilePrefab;
    public GameObject wallTilePrefab;

    private TileType[,] grid; // 2D grid representing the level
    private List<Room> rooms; // List of generated rooms
    private System.Random random; // Random number generator

    void Start()
    {
        GenerateLevel();
    }

    /// <summary>
    /// Main method to generate the level.
    /// </summary>
    public void GenerateLevel()
    {
        InitializeGrid();
        List<RectInt> partitions = RunBSP();
        rooms = CreateRooms(partitions);
        ConnectRooms(rooms);
        ApplyGridToTiles();
    }

    /// <summary>
    /// Initializes the grid with walls.
    /// </summary>
    private void InitializeGrid()
    {
        grid = new TileType[levelWidth, levelHeight];
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                grid[x, y] = TileType.Wall; // Start with walls
            }
        }
        random = new System.Random();
    }

    /// <summary>
    /// Runs the Binary Space Partitioning (BSP) algorithm to divide the level into partitions.
    /// </summary>
    private List<RectInt> RunBSP()
    {
        List<RectInt> partitions = new List<RectInt>();
        Queue<RectInt> queue = new Queue<RectInt>();
        queue.Enqueue(new RectInt(0, 0, levelWidth, levelHeight));

        for (int i = 0; i < maxIterations; i++)
        {
            if (queue.Count == 0) break;

            RectInt current = queue.Dequeue();
            if (current.width < minRoomSize * 2 || current.height < minRoomSize * 2)
            {
                partitions.Add(current);
                continue;
            }

            bool splitHorizontally = random.Next(0, 2) == 0;
            if (current.width > current.height) splitHorizontally = false;
            if (current.height > current.width) splitHorizontally = true;

            if (splitHorizontally)
            {
                int splitY = random.Next(minRoomSize, current.height - minRoomSize);
                queue.Enqueue(new RectInt(current.x, current.y, current.width, splitY));
                queue.Enqueue(new RectInt(current.x, current.y + splitY, current.width, current.height - splitY));
            }
            else
            {
                int splitX = random.Next(minRoomSize, current.width - minRoomSize);
                queue.Enqueue(new RectInt(current.x, current.y, splitX, current.height));
                queue.Enqueue(new RectInt(current.x + splitX, current.y, current.width - splitX, current.height));
            }
        }

        partitions.AddRange(queue);
        return partitions;
    }

    /// <summary>
    /// Creates rooms within the BSP partitions.
    /// </summary>
    private List<Room> CreateRooms(List<RectInt> partitions)
    {
        List<Room> rooms = new List<Room>();
        foreach (RectInt partition in partitions)
        {
            int roomWidth = random.Next(minRoomSize, partition.width);
            int roomHeight = random.Next(minRoomSize, partition.height);
            int roomX = partition.x + random.Next(0, partition.width - roomWidth);
            int roomY = partition.y + random.Next(0, partition.height - roomHeight);

            Room room = new Room(new Vector2Int(roomX, roomY), roomWidth, roomHeight);
            rooms.Add(room);

            // Mark the room area as floor in the grid
            for (int x = room.Position.x; x < room.Position.x + room.Width; x++)
            {
                for (int y = room.Position.y; y < room.Position.y + room.Height; y++)
                {
                    grid[x, y] = TileType.Floor;
                }
            }
        }
        return rooms;
    }

    /// <summary>
    /// Connects rooms with corridors.
    /// </summary>
    private void ConnectRooms(List<Room> rooms)
    {
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            Room roomA = rooms[i];
            Room roomB = rooms[i + 1];

            Vector2Int pointA = new Vector2Int(random.Next(roomA.Position.x, roomA.Position.x + roomA.Width), random.Next(roomA.Position.y, roomA.Position.y + roomA.Height));
            Vector2Int pointB = new Vector2Int(random.Next(roomB.Position.x, roomB.Position.x + roomB.Width), random.Next(roomB.Position.y, roomB.Position.y + roomB.Height));

            CreateCorridor(pointA, pointB);
        }
    }

    /// <summary>
    /// Creates a corridor between two points.
    /// </summary>
    private void CreateCorridor(Vector2Int pointA, Vector2Int pointB)
    {
        Vector2Int current = pointA;

        while (current.x != pointB.x)
        {
            grid[current.x, current.y] = TileType.Floor;
            current.x += current.x < pointB.x ? 1 : -1;
        }

        while (current.y != pointB.y)
        {
            grid[current.x, current.y] = TileType.Floor;
            current.y += current.y < pointB.y ? 1 : -1;
        }
    }

    /// <summary>
    /// Instantiates tiles based on the grid.
    /// </summary>
    private void ApplyGridToTiles()
    {
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                Vector3 position = new Vector3(x, y, 0);
                if (grid[x, y] == TileType.Floor)
                {
                    Instantiate(floorTilePrefab, position, Quaternion.identity);
                }
                else
                {
                    Instantiate(wallTilePrefab, position, Quaternion.identity);
                }
            }
        }
    }
}

/// <summary>
/// Enum to represent tile types.
/// </summary>
public enum TileType { Wall, Floor }

/// <summary>
/// Class to represent a room.
/// </summary>
public class Room
{
    public Vector2Int Position { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }

    public Room(Vector2Int position, int width, int height)
    {
        Position = position;
        Width = width;
        Height = height;
    }*/
}