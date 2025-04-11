using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using Random = UnityEngine.Random;

public class ProceduralLevelGenerator : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private int levelWidth = 100;
    [SerializeField] private int levelHeight = 100;
    [SerializeField] private int minRoomSize = 6;
    [SerializeField] private int maxRoomSize = 15;
    [SerializeField] private int maxLeafSize = 20;
    [SerializeField] private int corridorWidth = 2;
    [SerializeField][Range(0, 1)] private float decorationDensity = 0.1f;
    [SerializeField][Range(0, 1)] private float enemyDensity = 0.05f;

    [Header("Tilemap References")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap decorationTilemap;

    [Header("Tiles")]
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;
    [SerializeField] private TileBase[] decorationTiles;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject[] enemyPrefabs;

    // Level data
    private int[,] levelGrid; // 0 = empty, 1 = floor, 2 = wall
    private List<Room> rooms = new List<Room>();
    private List<Vector2Int> corridors = new List<Vector2Int>();
    private BSPTree rootLeaf;

    private System.Random rng = new System.Random();

    [System.Serializable]
    public class Room
    {
        public int x;
        public int y;
        public int width;
        public int height;
        public int centerX;
        public int centerY;
        public bool isMainRoom = false;
        public bool hasEnemies = false;

        public Room(int x, int y, int width, int height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.centerX = x + width / 2;
            this.centerY = y + height / 2;
        }

        public bool Intersects(Room other, int buffer = 0)
        {
            return !(x + width + buffer < other.x ||
                     other.x + other.width + buffer < x ||
                     y + height + buffer < other.y ||
                     other.y + other.height + buffer < y);
        }
    }

    public class BSPTree
    {
        public BSPTree leftChild;
        public BSPTree rightChild;
        public Rect rect;
        public Room room;

        public BSPTree(Rect rect)
        {
            this.rect = rect;
        }

        public bool IsSplit()
        {
            return leftChild != null || rightChild != null;
        }
    }

    private void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        ClearLevel();
        InitializeLevelGrid();

        // Step 1: Generate rooms using BSP
        rootLeaf = new BSPTree(new Rect(0, 0, levelWidth, levelHeight));
        SplitBSP(rootLeaf, 0);
        CreateRoomsFromBSP(rootLeaf);

        // Step 2: Connect rooms with corridors
        ConnectRooms();

        // Step 3: Apply cellular automata for more organic rooms
        ApplyCellularAutomata();

        // Step 4: Place walls around floors
        PlaceWalls();

        // Step 5: Add decorations
        PlaceDecorations();

        // Step 6: Place player in the largest room
        PlacePlayer();

        // Step 7: Place enemies in other rooms
        PlaceEnemies();

        // Render the level to the tilemap
        RenderLevel();
    }

    private void ClearLevel()
    {
        rooms.Clear();
        corridors.Clear();
        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        decorationTilemap.ClearAllTiles();

        // Destroy all existing enemies
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            Destroy(enemy);
        }

        // Destroy player if it exists
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Destroy(player);
        }
    }

    private void InitializeLevelGrid()
    {
        levelGrid = new int[levelWidth, levelHeight];

        // Initialize with empty space
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                levelGrid[x, y] = 0; // Empty
            }
        }
    }

    private void SplitBSP(BSPTree leaf, int depth)
    {
        // Check if the leaf is already split
        if (leaf.IsSplit())
            return;

        // Decide whether to split horizontally or vertically based on width/height ratio
        bool splitHorizontally = leaf.rect.width > leaf.rect.height;

        // If the aspect ratio is roughly equal, choose randomly
        if (Mathf.Abs(leaf.rect.width - leaf.rect.height) < 5)
        {
            splitHorizontally = (Random.value > 0.5f);
        }

        int max = (splitHorizontally ? (int)leaf.rect.width : (int)leaf.rect.height);

        if (max < maxLeafSize * 2)
            return;

        int split = Random.Range(maxLeafSize, max - maxLeafSize);

        if (splitHorizontally)
        {
            leaf.leftChild = new BSPTree(new Rect(leaf.rect.x, leaf.rect.y, split, leaf.rect.height));
            leaf.rightChild = new BSPTree(new Rect(leaf.rect.x + split, leaf.rect.y, leaf.rect.width - split, leaf.rect.height));
        }
        else
        {
            leaf.leftChild = new BSPTree(new Rect(leaf.rect.x, leaf.rect.y, leaf.rect.width, split));
            leaf.rightChild = new BSPTree(new Rect(leaf.rect.x, leaf.rect.y + split, leaf.rect.width, leaf.rect.height - split));
        }

        // Recursively split children
        if (depth < 4) // Limit recursion depth
        {
            SplitBSP(leaf.leftChild, depth + 1);
            SplitBSP(leaf.rightChild, depth + 1);
        }
    }

    private void CreateRoomsFromBSP(BSPTree leaf)
    {
        if (leaf.leftChild != null || leaf.rightChild != null)
        {
            // This leaf has been split, so process its children
            if (leaf.leftChild != null)
                CreateRoomsFromBSP(leaf.leftChild);
            if (leaf.rightChild != null)
                CreateRoomsFromBSP(leaf.rightChild);

            // If both children have rooms, create a corridor between them
            if (leaf.leftChild?.room != null && leaf.rightChild?.room != null)
            {
                CreateCorridor(leaf.leftChild.room, leaf.rightChild.room);
            }
        }
        else
        {
            // This leaf is ready to make a room in
            int roomWidth = Random.Range(minRoomSize, Mathf.Min(maxRoomSize, (int)leaf.rect.width - 2));
            int roomHeight = Random.Range(minRoomSize, Mathf.Min(maxRoomSize, (int)leaf.rect.height - 2));

            // Place the room randomly within the leaf
            int roomX = (int)leaf.rect.x + Random.Range(1, (int)leaf.rect.width - roomWidth - 1);
            int roomY = (int)leaf.rect.y + Random.Range(1, (int)leaf.rect.height - roomHeight - 1);

            // Create the room
            leaf.room = new Room(roomX, roomY, roomWidth, roomHeight);
            rooms.Add(leaf.room);

            // Mark the room on the grid
            for (int x = roomX; x < roomX + roomWidth; x++)
            {
                for (int y = roomY; y < roomY + roomHeight; y++)
                {
                    levelGrid[x, y] = 1; // Floor
                }
            }
        }
    }

    private void ConnectRooms()
    {
        // Get the rooms in the order of creation
        List<Room> orderedRooms = new List<Room>(rooms);

        // Find the largest room as the main room
        Room mainRoom = orderedRooms.OrderByDescending(r => r.width * r.height).First();
        mainRoom.isMainRoom = true;

        // Connect each room to the main room or nearest connected room
        foreach (Room room in orderedRooms)
        {
            if (room == mainRoom) continue;

            // Find the nearest connected room
            float minDistance = float.MaxValue;
            Room nearestRoom = mainRoom;

            foreach (Room otherRoom in orderedRooms)
            {
                if (otherRoom == room) continue;

                float distance = Vector2.Distance(
                    new Vector2(room.centerX, room.centerY),
                    new Vector2(otherRoom.centerX, otherRoom.centerY)
                );

                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestRoom = otherRoom;
                }
            }

            CreateCorridor(room, nearestRoom);
        }
    }

    private void CreateCorridor(Room room1, Room room2)
    {
        Vector2Int start = new Vector2Int(room1.centerX, room1.centerY);
        Vector2Int end = new Vector2Int(room2.centerX, room2.centerY);

        // L-shaped corridor
        List<Vector2Int> path = new List<Vector2Int>();

        // Horizontal corridor segment
        int x = start.x;
        int stepX = x < end.x ? 1 : -1;

        while (x != end.x)
        {
            for (int i = -corridorWidth / 2; i <= corridorWidth / 2; i++)
            {
                int corridorY = start.y + i;
                if (IsInMap(x, corridorY))
                {
                    path.Add(new Vector2Int(x, corridorY));
                }
            }
            x += stepX;
        }

        // Vertical corridor segment
        int y = start.y;
        int stepY = y < end.y ? 1 : -1;

        while (y != end.y)
        {
            for (int i = -corridorWidth / 2; i <= corridorWidth / 2; i++)
            {
                int corridorX = end.x + i;
                if (IsInMap(corridorX, y))
                {
                    path.Add(new Vector2Int(corridorX, y));
                }
            }
            y += stepY;
        }

        // Add the corridor to the level grid
        foreach (Vector2Int pos in path)
        {
            if (IsInMap(pos.x, pos.y) && levelGrid[pos.x, pos.y] == 0)
            {
                levelGrid[pos.x, pos.y] = 1; // Floor
                corridors.Add(pos);
            }
        }
    }

    private void ApplyCellularAutomata()
    {
        // Make a copy of the level grid
        int[,] newGrid = new int[levelWidth, levelHeight];
        System.Array.Copy(levelGrid, newGrid, levelGrid.Length);

        // Apply cellular automata to make rooms more organic
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                // Skip corridors
                if (corridors.Contains(new Vector2Int(x, y)))
                    continue;

                int neighbors = CountNeighbors(x, y);

                if (levelGrid[x, y] == 1) // Floor
                {
                    // If less than 4 neighbors, change to empty (wall)
                    if (neighbors < 4)
                    {
                        newGrid[x, y] = 0;
                    }
                }
                else // Empty
                {
                    // If more than 4 neighbors, change to floor
                    if (neighbors > 4)
                    {
                        newGrid[x, y] = 1;
                    }
                }
            }
        }

        // Update the level grid
        levelGrid = newGrid;
    }

    private int CountNeighbors(int x, int y)
    {
        int count = 0;

        for (int i = -1; i <= 1; i++)
        {
            for (int j = -1; j <= 1; j++)
            {
                // Skip the center
                if (i == 0 && j == 0)
                    continue;

                int nx = x + i;
                int ny = y + j;

                // Count neighbors on the grid and outside the grid
                if (!IsInMap(nx, ny) || levelGrid[nx, ny] == 1)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void PlaceWalls()
    {
        // Make a copy of the level grid
        int[,] newGrid = new int[levelWidth, levelHeight];
        System.Array.Copy(levelGrid, newGrid, levelGrid.Length);

        // Place walls around floor tiles
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                if (levelGrid[x, y] == 0) // Empty
                {
                    // Check if there's a floor tile nearby
                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            if (i == 0 && j == 0)
                                continue;

                            int nx = x + i;
                            int ny = y + j;

                            if (IsInMap(nx, ny) && levelGrid[nx, ny] == 1)
                            {
                                newGrid[x, y] = 2; // Wall
                                break;
                            }
                        }
                    }
                }
            }
        }

        levelGrid = newGrid;
    }

    private void PlaceDecorations()
    {
        if (decorationTiles.Length == 0) return;

        // Place decorations randomly on floor tiles
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                if (levelGrid[x, y] == 1 && Random.value < decorationDensity)
                {
                    // Check if there's enough space (not near walls)
                    bool nearWall = false;
                    for (int i = -1; i <= 1; i++)
                    {
                        for (int j = -1; j <= 1; j++)
                        {
                            int nx = x + i;
                            int ny = y + j;

                            if (IsInMap(nx, ny) && levelGrid[nx, ny] == 2)
                            {
                                nearWall = true;
                                break;
                            }
                        }
                    }

                    if (!nearWall)
                    {
                        // Place a random decoration tile
                        TileBase decorTile = decorationTiles[Random.Range(0, decorationTiles.Length)];
                        decorationTilemap.SetTile(new Vector3Int(x, y, 0), decorTile);
                    }
                }
            }
        }
    }

    private void PlacePlayer()
    {
        // Find the main room or the largest room
        Room mainRoom = rooms.Find(r => r.isMainRoom);
        if (mainRoom == null)
        {
            mainRoom = rooms.OrderByDescending(r => r.width * r.height).First();
        }

        // Place the player in the center of the main room
        Vector3 playerPosition = new Vector3(mainRoom.centerX, mainRoom.centerY, 0);
        Instantiate(playerPrefab, playerPosition, Quaternion.identity);
    }

    private void PlaceEnemies()
    {
        // Skip if no enemy prefabs
        if (enemyPrefabs.Length == 0) return;

        foreach (Room room in rooms)
        {
            // Skip the main room and rooms that already have enemies
            if (room.isMainRoom || room.hasEnemies)
                continue;

            // Determine if this room should have enemies
            if (Random.value < enemyDensity)
            {
                room.hasEnemies = true;

                // Place enemies based on room size
                int numEnemies = Mathf.RoundToInt((room.width * room.height) / 50f);
                numEnemies = Mathf.Clamp(numEnemies, 1, 5);

                for (int i = 0; i < numEnemies; i++)
                {
                    // Find a valid position within the room
                    int x = Random.Range(room.x + 1, room.x + room.width - 1);
                    int y = Random.Range(room.y + 1, room.y + room.height - 1);

                    // Ensure it's on a floor tile and not on top of another enemy
                    if (levelGrid[x, y] == 1)
                    {
                        // Choose a random enemy prefab
                        GameObject enemyPrefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];

                        // Instantiate the enemy
                        Vector3 enemyPosition = new Vector3(x, y, 0);
                        Instantiate(enemyPrefab, enemyPosition, Quaternion.identity);
                    }
                }
            }
        }
    }

    private void RenderLevel()
    {
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);

                if (levelGrid[x, y] == 1) // Floor
                {
                    floorTilemap.SetTile(position, floorTile);
                }
                else if (levelGrid[x, y] == 2) // Wall
                {
                    wallTilemap.SetTile(position, wallTile);
                }
            }
        }
    }

    private bool IsInMap(int x, int y)
    {
        return x >= 0 && x < levelWidth && y >= 0 && y < levelHeight;
    }
}