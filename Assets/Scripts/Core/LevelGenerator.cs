using UnityEngine;
using System.Collections.Generic;

public class LevelGenerator : MonoBehaviour
{
    [Header("Level Settings")]
    public int numberOfLevels = 1;
    public int numberOfRooms = 10; // Add this
    public int numberOfEnemies = 20; // Add this
    public int numberOfDecorators = 15; // Add this
    public int startLevel = 0; // Add this
    public int endLevel = 1; // Add this
    public int minRoomsPerLevel = 5;
    public int maxRoomsPerLevel = 10;
    public int minRoomSize = 5;
    public int maxRoomSize = 10;

    [Header("Tile Settings")]
    public GameObject floorTilePrefab;
    public GameObject wallTilePrefab;

    [Header("Enemy Settings")]
    public GameObject enemyPrefab;
    public int minEnemiesPerRoom = 1;
    public int maxEnemiesPerRoom = 3;

    [Header("Player Settings")]
    public GameObject playerPrefab;

    [Header("Decorator Settings")]
    public GameObject[] decoratorPrefabs;

    [Header("Triggers")]
    public GameObject startTriggerPrefab;
    public GameObject endTriggerPrefab;

    private List<Room> rooms;

    void Start()
    {
        GenerateLevels();
    }

    public void GenerateLevels()
    {
        for (int i = 0; i < numberOfLevels; i++)
        {
            GenerateLevel(i);
        }
    }

    private void GenerateLevel(int levelIndex)
    {
        rooms = new List<Room>();
        int roomCount = Random.Range(minRoomsPerLevel, maxRoomsPerLevel + 1);

        for (int i = 0; i < roomCount; i++)
        {
            Room room = GenerateRoom();
            rooms.Add(room);
            PlaceRoom(room);
            SpawnEnemies(room);
            PlaceDecorators(room);
        }

        PlaceStartAndEndTriggers(levelIndex);
    }

    private Room GenerateRoom()
    {
        int width = Random.Range(minRoomSize, maxRoomSize + 1);
        int height = Random.Range(minRoomSize, maxRoomSize + 1);
        Vector2 position = new Vector2(Random.Range(0, 100), Random.Range(0, 100)); // Example position logic

        return new Room(position, width, height);
    }

    private void PlaceRoom(Room room)
    {
        for (int x = 0; x < room.Width; x++)
        {
            for (int y = 0; y < room.Height; y++)
            {
                Vector3 tilePosition = new Vector3(room.Position.x + x, room.Position.y + y, 0);
                Instantiate(floorTilePrefab, tilePosition, Quaternion.identity);
            }
        }

        // Place walls around the room
        for (int x = -1; x <= room.Width; x++)
        {
            Instantiate(wallTilePrefab, new Vector3(room.Position.x + x, room.Position.y - 1, 0), Quaternion.identity);
            Instantiate(wallTilePrefab, new Vector3(room.Position.x + x, room.Position.y + room.Height, 0), Quaternion.identity);
        }
        for (int y = 0; y <= room.Height; y++)
        {
            Instantiate(wallTilePrefab, new Vector3(room.Position.x - 1, room.Position.y + y, 0), Quaternion.identity);
            Instantiate(wallTilePrefab, new Vector3(room.Position.x + room.Width, room.Position.y + y, 0), Quaternion.identity);
        }
    }

    private void SpawnEnemies(Room room)
    {
        int enemyCount = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
        for (int i = 0; i < enemyCount; i++)
        {
            Vector2 enemyPosition = new Vector2(room.Position.x + Random.Range(1, room.Width - 1), room.Position.y + Random.Range(1, room.Height - 1));
            Instantiate(enemyPrefab, enemyPosition, Quaternion.identity);
        }
    }

    private void PlaceDecorators(Room room)
    {
        foreach (var decorator in decoratorPrefabs)
        {
            Vector2 decoratorPosition = new Vector2(room.Position.x + Random.Range(1, room.Width - 1), room.Position.y + Random.Range(1, room.Height - 1));
            Instantiate(decorator, decoratorPosition, Quaternion.identity);
        }
    }

    private void PlaceStartAndEndTriggers(int levelIndex)
    {
        Instantiate(startTriggerPrefab, new Vector3(0, 0, 0), Quaternion.identity); // Example position for start trigger
        Instantiate(endTriggerPrefab, new Vector3(99, 99, 0), Quaternion.identity); // Example position for end trigger
    }

    public void ClearLevels() // Add this method
    {
        // Logic to clear all generated levels
        Debug.Log("Levels cleared.");
    }

    public void ClearLevel()
    {
        // Add logic to clear the generated level
        Debug.Log("Clearing the level...");
        // Example: Destroy all child objects of this GameObject
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }
    }
}

[System.Serializable]
public class Room
{
    public Vector2 Position;
    public int Width;
    public int Height;

    public Room(Vector2 position, int width, int height)
    {
        Position = position;
        Width = width;
        Height = height;
    }
}