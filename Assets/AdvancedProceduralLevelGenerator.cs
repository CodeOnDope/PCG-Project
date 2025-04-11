using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = UnityEngine.Random;

[CreateAssetMenu(fileName = "LevelTheme", menuName = "PCG/Level Theme")]
public class LevelTheme : ScriptableObject
{
    public string themeName;
    public TileBase floorTile;
    public TileBase wallTile;
    public TileBase[] decorationTiles;
    public GameObject[] enemyPrefabs;
    public GameObject[] specialObjectPrefabs;
    public Color ambientColor = Color.white;
    [Range(0, 1)] public float decorationDensity = 0.1f;
    [Range(0, 1)] public float enemyDensity = 0.05f;
}

[CreateAssetMenu(fileName = "RoomTemplate", menuName = "PCG/Room Template")]
public class RoomTemplate : ScriptableObject
{
    public enum RoomType
    {
        Normal,
        Entrance,
        Exit,
        Treasure,
        Shop,
        Boss,
        Challenge,
        Secret
    }

    public RoomType roomType;
    public int minWidth;
    public int minHeight;
    public int maxWidth;
    public int maxHeight;
    public GameObject[] requiredObjects;
    public Vector2Int[] objectPositions; // Relative to room corner
    [Range(0, 1)] public float decorationDensity = 0.1f;
    [Range(0, 1)] public float enemyDensity = 0.05f;
    public int maxEnemies = 5;
    public bool mustBeConnectedToMainPath = true;
    public int minDistanceFromStart = 0;
    public string[] requiredTags;
    public string[] excludedTags;

    // Custom room layout (optional)
    public bool useCustomLayout = false;
    public int[,] layoutGrid; // 0 = empty, 1 = floor, 2 = wall, 3 = special
}

public class AdvancedProceduralLevelGenerator : MonoBehaviour
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
    [SerializeField] private bool generateMinimap = true;
    [SerializeField] private int seed = 0;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private float difficultyMultiplier = 1.0f;
    [SerializeField] private int maxRetries = 5;

    [Header("Corridor Settings")]
    [SerializeField] private CorridorStyle corridorStyle = CorridorStyle.Random;
    [SerializeField] private bool useCorridorDecorations = true;
    [SerializeField][Range(0, 1)] private float corridorWindingFactor = 0.2f;
    [SerializeField] private bool useDoors = true;
    [SerializeField] private TileBase doorTile;

    private enum CorridorStyle { Straight, LShaped, Random, Organic }

    [Header("Room Settings")]
    [SerializeField] private int minRoomsCount = 10;
    [SerializeField] private int maxRoomsCount = 20;
    [SerializeField] private bool enforceMinimumRooms = true;
    [SerializeField] private bool useRoomTemplates = true;
    [SerializeField] private RoomTemplate[] roomTemplates;
    [SerializeField] private int entranceRoomIndex = -1; // -1 = auto-select
    [SerializeField] private int exitRoomIndex = -1; // -1 = auto-select
    [SerializeField] private float secretRoomChance = 0.2f;
    [SerializeField] private float treasureRoomChance = 0.3f;
    [SerializeField] private float shopRoomChance = 0.2f;
    [SerializeField] private bool alwaysIncludeBossRoom = true;

    [Header("Biome Settings")]
    [SerializeField] private bool useBiomes = true;
    [SerializeField] private LevelTheme[] levelThemes;
    [SerializeField] private float biomeSmoothness = 0.5f;
    [SerializeField] private bool useThemeTransitions = true;
    [SerializeField] private int minBiomeSize = 10;

    [Header("Tilemap References")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap decorationTilemap;
    [SerializeField] private Tilemap objectTilemap;
    [SerializeField] private Tilemap minimapTilemap;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private GameObject[] treasurePrefabs;
    [SerializeField] private GameObject[] bossPrefabs;
    [SerializeField] private GameObject[] shopPrefabs;
    [SerializeField] private GameObject exitPrefab;
    [SerializeField] private GameObject doorPrefab;

    [Header("Default Tiles")]
    [SerializeField] private TileBase defaultFloorTile;
    [SerializeField] private TileBase defaultWallTile;
    [SerializeField] private TileBase[] defaultDecorationTiles;
    [SerializeField] private TileBase minimapFloorTile;
    [SerializeField] private TileBase minimapWallTile;
    [SerializeField] private TileBase minimapRoomTile;
    [SerializeField] private TileBase minimapSpecialRoomTile;
    [SerializeField] private TileBase minimapPlayerTile;

    [Header("Advanced Settings")]
    [SerializeField] private bool applyPostProcessing = true;
    [SerializeField] private int smoothingIterations = 3;
    [SerializeField] private bool ensureConnectivity = true;
    [SerializeField] private bool avoidDeadEnds = true;
    [SerializeField] private float waterChance = 0.1f;
    [SerializeField] private float obstacleChance = 0.1f;
    [SerializeField] private bool useKeySystem = true;

    // Level data
    private int[,] levelGrid; // 0 = empty, 1 = floor, 2 = wall, 3 = door, 4 = water, 5 = obstacle
    private int[,] biomeGrid; // Stores biome/theme index for each cell
    private List<Room> rooms = new List<Room>();
    private List<Vector2Int> corridors = new List<Vector2Int>();
    private List<Vector2Int> doors = new List<Vector2Int>();
    private List<Room> criticalPathRooms = new List<Room>();
    private Dictionary<Vector2Int, GameObject> objectsPlaced = new Dictionary<Vector2Int, GameObject>();
    private BSPTree rootLeaf;
    private Room entranceRoom;
    private Room exitRoom;
    private Room bossRoom;
    private List<Room> specialRooms = new List<Room>();
    private bool levelValid = false;
    private List<Vector2Int> keyPositions = new List<Vector2Int>();
    private List<Vector2Int> lockPositions = new List<Vector2Int>();
    private Dictionary<string, List<Vector2Int>> taggedPositions = new Dictionary<string, List<Vector2Int>>();

    private System.Random rng;

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
        public RoomTemplate.RoomType roomType = RoomTemplate.RoomType.Normal;
        public bool isConnected = false;
        public List<Door> doors = new List<Door>();
        public LevelTheme theme;
        public List<Room> connectedRooms = new List<Room>();
        public int distanceFromStart = 0;
        public bool isSecretRoom = false;
        public bool isVisited = false;
        public List<string> tags = new List<string>();
        public RoomTemplate template;
        public List<KeyValuePair<GameObject, Vector2Int>> requiredObjects = new List<KeyValuePair<GameObject, Vector2Int>>();

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

        public bool Contains(Vector2Int position)
        {
            return position.x >= x && position.x < x + width &&
                   position.y >= y && position.y < y + height;
        }

        public List<Vector2Int> GetPerimeterTiles()
        {
            List<Vector2Int> perimeter = new List<Vector2Int>();

            // Top and bottom edges
            for (int i = x; i < x + width; i++)
            {
                perimeter.Add(new Vector2Int(i, y));
                perimeter.Add(new Vector2Int(i, y + height - 1));
            }

            // Left and right edges (excluding corners already added)
            for (int j = y + 1; j < y + height - 1; j++)
            {
                perimeter.Add(new Vector2Int(x, j));
                perimeter.Add(new Vector2Int(x + width - 1, j));
            }

            return perimeter;
        }
    }

    public class Door
    {
        public Vector2Int position;
        public bool isLocked;
        public string keyId;
        public Room connectedRoom;

        public Door(Vector2Int position, bool isLocked = false)
        {
            this.position = position;
            this.isLocked = isLocked;
            this.keyId = Guid.NewGuid().ToString();
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
        InitializeSeed();

        int retryCount = 0;
        do
        {
            ClearLevel();
            InitializeLevelGrid();
            InitializeBiomeGrid();

            // Step 1: Generate rooms using BSP
            GenerateRoomsWithBSP();

            // Step 2: Apply room templates if enabled
            if (useRoomTemplates && roomTemplates != null && roomTemplates.Length > 0)
            {
                ApplyRoomTemplates();
            }

            // Step 3: Connect rooms with corridors
            ConnectRooms();

            // Step 4: Ensure minimum number of rooms if needed
            if (enforceMinimumRooms && rooms.Count < minRoomsCount)
            {
                AddAdditionalRooms(minRoomsCount - rooms.Count);
            }

            // Step 5: Designate special rooms (entrance, exit, boss, etc.)
            DesignateSpecialRooms();

            // Step 6: Apply cellular automata for more organic rooms
            if (applyPostProcessing)
            {
                for (int i = 0; i < smoothingIterations; i++)
                {
                    ApplyCellularAutomata();
                }
            }

            // Step 7: Ensure level connectivity
            if (ensureConnectivity)
            {
                EnsureConnectivity();
            }

            // Step 8: Place walls around floors
            PlaceWalls();

            // Step 9: Add doors between rooms and corridors if enabled
            if (useDoors)
            {
                PlaceDoors();
            }

            // Step 10: Add special features like water and obstacles
            AddSpecialFeatures();

            // Step 11: Apply key-lock system if enabled
            if (useKeySystem)
            {
                ImplementKeyLockSystem();
            }

            // Step 12: Add decorations to rooms and corridors
            PlaceDecorations();

            // Step 13: Place enemies based on difficulty and room type
            PlaceEnemies();

            // Step 14: Place player in entrance room
            PlacePlayer();

            // Step 15: Place special objects (treasures, shop items, etc.)
            PlaceSpecialObjects();

            // Step 16: Validate the level
            levelValid = ValidateLevel();

            retryCount++;
        } while (!levelValid && retryCount < maxRetries);

        // Render the level to tilemaps
        RenderLevel();

        // Generate minimap if enabled
        if (generateMinimap)
        {
            GenerateMinimap();
        }

        Debug.Log($"Level generated successfully after {retryCount} attempts.");
    }

    private void InitializeSeed()
    {
        // Set the random seed
        if (useRandomSeed)
        {
            seed = UnityEngine.Random.Range(0, int.MaxValue);
        }

        UnityEngine.Random.InitState(seed);
        rng = new System.Random(seed);

        Debug.Log($"Using seed: {seed}");
    }

    private void ClearLevel()
    {
        rooms.Clear();
        specialRooms.Clear();
        corridors.Clear();
        doors.Clear();
        criticalPathRooms.Clear();
        objectsPlaced.Clear();
        keyPositions.Clear();
        lockPositions.Clear();
        taggedPositions.Clear();

        entranceRoom = null;
        exitRoom = null;
        bossRoom = null;

        floorTilemap.ClearAllTiles();
        wallTilemap.ClearAllTiles();
        decorationTilemap.ClearAllTiles();
        if (objectTilemap != null) objectTilemap.ClearAllTiles();
        if (minimapTilemap != null) minimapTilemap.ClearAllTiles();

        // Destroy all existing game objects
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject enemy in enemies)
        {
            Destroy(enemy);
        }

        GameObject[] treasures = GameObject.FindGameObjectsWithTag("Treasure");
        foreach (GameObject treasure in treasures)
        {
            Destroy(treasure);
        }

        GameObject[] shopItems = GameObject.FindGameObjectsWithTag("Shop");
        foreach (GameObject item in shopItems)
        {
            Destroy(item);
        }

        GameObject[] doorObjects = GameObject.FindGameObjectsWithTag("Door");
        foreach (GameObject doorObj in doorObjects)
        {
            Destroy(doorObj);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Destroy(player);
        }

        GameObject exit = GameObject.FindGameObjectWithTag("Exit");
        if (exit != null)
        {
            Destroy(exit);
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

    private void InitializeBiomeGrid()
    {
        if (!useBiomes || levelThemes == null || levelThemes.Length == 0)
            return;

        biomeGrid = new int[levelWidth, levelHeight];

        if (levelThemes.Length == 1)
        {
            // If only one theme, fill the grid with it
            for (int x = 0; x < levelWidth; x++)
            {
                for (int y = 0; y < levelHeight; y++)
                {
                    biomeGrid[x, y] = 0;
                }
            }
        }
        else
        {
            // Generate Perlin noise for biome distribution
            float noiseScale = biomeSmoothness;
            int numBiomes = levelThemes.Length;
            float offsetX = Random.value * 100;
            float offsetY = Random.value * 100;

            for (int x = 0; x < levelWidth; x++)
            {
                for (int y = 0; y < levelHeight; y++)
                {
                    float noiseValue = Mathf.PerlinNoise((x + offsetX) * noiseScale, (y + offsetY) * noiseScale);
                    int biomeIndex = Mathf.FloorToInt(noiseValue * numBiomes);
                    biomeIndex = Mathf.Clamp(biomeIndex, 0, numBiomes - 1);
                    biomeGrid[x, y] = biomeIndex;
                }
            }

            // Ensure biome regions are at least minBiomeSize
            SmoothBiomes(minBiomeSize);
        }
    }

    private void SmoothBiomes(int minRegionSize)
    {
        // Identify all biome regions
        Dictionary<int, List<Vector2Int>> regions = new Dictionary<int, List<Vector2Int>>();
        bool[,] visited = new bool[levelWidth, levelHeight];

        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                if (!visited[x, y])
                {
                    List<Vector2Int> region = GetConnectedRegion(x, y, biomeGrid[x, y], visited);

                    // If region is too small, merge it with the largest adjacent region
                    if (region.Count < minRegionSize)
                    {
                        MergeSmallRegion(region);
                    }
                }
            }
        }
    }

    private List<Vector2Int> GetConnectedRegion(int startX, int startY, int biomeType, bool[,] visited)
    {
        List<Vector2Int> region = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int start = new Vector2Int(startX, startY);
        queue.Enqueue(start);
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            region.Add(current);

            // Check all 4 directions
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),  // Up
                new Vector2Int(1, 0),  // Right
                new Vector2Int(0, -1), // Down
                new Vector2Int(-1, 0)  // Left
            };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = new Vector2Int(current.x + dir.x, current.y + dir.y);

                if (IsInMap(next.x, next.y) && !visited[next.x, next.y] && biomeGrid[next.x, next.y] == biomeType)
                {
                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return region;
    }

    private void MergeSmallRegion(List<Vector2Int> region)
    {
        if (region.Count == 0) return;

        // Find the most common adjacent biome type
        Dictionary<int, int> adjacentBiomeCounts = new Dictionary<int, int>();

        foreach (Vector2Int pos in region)
        {
            // Check neighbors
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(1, 0),
                new Vector2Int(0, -1),
                new Vector2Int(-1, 0)
            };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = new Vector2Int(pos.x + dir.x, pos.y + dir.y);

                if (IsInMap(neighbor.x, neighbor.y) && !region.Contains(neighbor))
                {
                    int neighborBiome = biomeGrid[neighbor.x, neighbor.y];

                    if (!adjacentBiomeCounts.ContainsKey(neighborBiome))
                    {
                        adjacentBiomeCounts[neighborBiome] = 0;
                    }

                    adjacentBiomeCounts[neighborBiome]++;
                }
            }
        }

        // Find the most common adjacent biome
        int mostCommonBiome = biomeGrid[region[0].x, region[0].y]; // Default to current biome
        int highestCount = 0;

        foreach (var kvp in adjacentBiomeCounts)
        {
            if (kvp.Value > highestCount)
            {
                highestCount = kvp.Value;
                mostCommonBiome = kvp.Key;
            }
        }

        // Merge the region into the most common biome
        foreach (Vector2Int pos in region)
        {
            biomeGrid[pos.x, pos.y] = mostCommonBiome;
        }
    }

    private void GenerateRoomsWithBSP()
    {
        rootLeaf = new BSPTree(new Rect(0, 0, levelWidth, levelHeight));
        SplitBSP(rootLeaf, 0);
        CreateRoomsFromBSP(rootLeaf);

        // Assign biomes to rooms if using multiple biomes
        if (useBiomes && levelThemes != null && levelThemes.Length > 1)
        {
            AssignBiomesToRooms();
        }
    }

    private void SplitBSP(BSPTree leaf, int depth)
    {
        // Check if the leaf is already split or too small to split
        if (leaf.IsSplit())
            return;

        // Calculate the maximum split depth based on desired room count
        int maxDepth = Mathf.CeilToInt(Mathf.Log(maxRoomsCount, 2));

        // Stop splitting if we've reached the maximum depth
        if (depth >= maxDepth)
            return;

        // Decide whether to split horizontally or vertically based on width/height ratio
        bool splitHorizontally = leaf.rect.width > leaf.rect.height;

        // If the aspect ratio is roughly equal, choose randomly
        if (Mathf.Abs(leaf.rect.width - leaf.rect.height) < 5)
        {
            splitHorizontally = (Random.value > 0.5f);
        }

        int max = (splitHorizontally ? (int)leaf.rect.width : (int)leaf.rect.height);

        // Check if the leaf is too small to split further
        if (max < maxLeafSize * 2)
            return;

        // Determine the split position with some randomness
        int split = Random.Range(maxLeafSize, max - maxLeafSize);

        // Create the child leaves
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

        // Recursively split children with some randomness
        if (Random.value < 0.8f || depth < 2) // Higher levels always split, lower levels have a chance not to
        {
            SplitBSP(leaf.leftChild, depth + 1);
        }

        if (Random.value < 0.8f || depth < 2)
        {
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

    private void AssignBiomesToRooms()
    {
        if (levelThemes == null || levelThemes.Length == 0)
            return;

        foreach (Room room in rooms)
        {
            // Determine the most common biome in this room's area
            Dictionary<int, int> biomeCounts = new Dictionary<int, int>();

            for (int x = room.x; x < room.x + room.width; x++)
            {
                for (int y = room.y; y < room.y + room.height; y++)
                {
                    if (IsInMap(x, y))
                    {
                        int biomeIndex = biomeGrid[x, y];

                        if (!biomeCounts.ContainsKey(biomeIndex))
                        {
                            biomeCounts[biomeIndex] = 0;
                        }

                        biomeCounts[biomeIndex]++;
                    }
                }
            }

            // Assign the most common biome to this room
            int highestCount = 0;
            int dominantBiome = 0;

            foreach (var kvp in biomeCounts)
            {
                if (kvp.Value > highestCount)
                {
                    highestCount = kvp.Value;
                    dominantBiome = kvp.Key;
                }
            }

            room.theme = levelThemes[dominantBiome];

            // Update the biome grid for all tiles in this room to be consistent
            for (int x = room.x; x < room.x + room.width; x++)
            {
                for (int y = room.y; y < room.y + room.height; y++)
                {
                    if (IsInMap(x, y))
                    {
                        biomeGrid[x, y] = dominantBiome;
                    }
                }
            }
        }
    }

    private void ApplyRoomTemplates()
    {
        if (roomTemplates == null || roomTemplates.Length == 0)
            return;

        foreach (Room room in rooms)
        {
            // Skip rooms that already have a template
            if (room.template != null)
                continue;

            // Find appropriate templates for this room
            List<RoomTemplate> validTemplates = new List<RoomTemplate>();

            foreach (RoomTemplate template in roomTemplates)
            {
                if (room.width >= template.minWidth && room.width <= template.maxWidth &&
                    room.height >= template.minHeight && room.height <= template.maxHeight)
                {
                    // Check if the template has tags that match the room
                    bool tagsMatch = true;

                    if (template.requiredTags != null && template.requiredTags.Length > 0)
                    {
                        foreach (string tag in template.requiredTags)
                        {
                            if (!room.tags.Contains(tag))
                            {
                                tagsMatch = false;
                                break;
                            }
                        }
                    }

                    if (!tagsMatch)
                        continue;

                    if (template.excludedTags != null && template.excludedTags.Length > 0)
                    {
                        foreach (string tag in template.excludedTags)
                        {
                            if (room.tags.Contains(tag))
                            {
                                tagsMatch = false;
                                break;
                            }
                        }
                    }

                    if (!tagsMatch)
                        continue;

                    validTemplates.Add(template);
                }
            }

            if (validTemplates.Count > 0)
            {
                // Select a random template from valid ones
                RoomTemplate selectedTemplate = validTemplates[Random.Range(0, validTemplates.Count)];
                room.template = selectedTemplate;
                room.roomType = selectedTemplate.roomType;

                // If using custom layout, apply it
                if (selectedTemplate.useCustomLayout && selectedTemplate.layoutGrid != null)
                {
                    ApplyCustomLayoutToRoom(room, selectedTemplate);
                }

                // Store required objects
                if (selectedTemplate.requiredObjects != null && selectedTemplate.requiredObjects.Length > 0)
                {
                    for (int i = 0; i < selectedTemplate.requiredObjects.Length; i++)
                    {
                        Vector2Int pos;
                        if (selectedTemplate.objectPositions != null && i < selectedTemplate.objectPositions.Length)
                        {
                            // Use specified position
                            pos = new Vector2Int(room.x + selectedTemplate.objectPositions[i].x,
                                               room.y + selectedTemplate.objectPositions[i].y);
                        }
                        else
                        {
                            // Use center of room as default
                            pos = new Vector2Int(room.centerX, room.centerY);
                        }

                        room.requiredObjects.Add(new KeyValuePair<GameObject, Vector2Int>(
                            selectedTemplate.requiredObjects[i], pos));
                    }
                }
            }
        }
    }

    private void ApplyCustomLayoutToRoom(Room room, RoomTemplate template)
    {
        int layoutWidth = template.layoutGrid.GetLength(0);
        int layoutHeight = template.layoutGrid.GetLength(1);

        // Clear existing floor tiles in this room
        for (int x = room.x; x < room.x + room.width; x++)
        {
            for (int y = room.y; y < room.y + room.height; y++)
            {
                if (IsInMap(x, y))
                {
                    levelGrid[x, y] = 0; // Clear to empty
                }
            }
        }

        // Calculate scaling factors
        float scaleX = (float)room.width / layoutWidth;
        float scaleY = (float)room.height / layoutHeight;

        // Apply the custom layout
        for (int lx = 0; lx < layoutWidth; lx++)
        {
            for (int ly = 0; ly < layoutHeight; ly++)
            {
                // Calculate the corresponding position in the actual room
                int rx = Mathf.FloorToInt(room.x + lx * scaleX);
                int ry = Mathf.FloorToInt(room.y + ly * scaleY);

                // Ensure we're within the room bounds
                if (rx >= room.x && rx < room.x + room.width && ry >= room.y && ry < room.y + room.height)
                {
                    int tileType = template.layoutGrid[lx, ly];

                    if (IsInMap(rx, ry))
                    {
                        levelGrid[rx, ry] = tileType;

                        // If this is a special tile, tag it for later use
                        if (tileType == 3)
                        {
                            string tag = $"special_{room.roomType}_{lx}_{ly}";
                            if (!taggedPositions.ContainsKey(tag))
                            {
                                taggedPositions[tag] = new List<Vector2Int>();
                            }
                            taggedPositions[tag].Add(new Vector2Int(rx, ry));
                        }
                    }
                }
            }
        }
    }

    private void ConnectRooms()
    {
        // Connect rooms using minimum spanning tree to ensure all rooms are accessible
        List<Room> connectedRooms = new List<Room>();
        List<Room> unconnectedRooms = new List<Room>(rooms);

        // Start with a random room
        Room startRoom = unconnectedRooms[Random.Range(0, unconnectedRooms.Count)];
        connectedRooms.Add(startRoom);
        unconnectedRooms.Remove(startRoom);
        startRoom.isConnected = true;

        // Prim's algorithm for minimum spanning tree
        while (unconnectedRooms.Count > 0)
        {
            float minDistance = float.MaxValue;
            Room closestUnconnectedRoom = null;
            Room closestConnectedRoom = null;

            foreach (Room connectedRoom in connectedRooms)
            {
                foreach (Room unconnectedRoom in unconnectedRooms)
                {
                    float distance = Vector2.Distance(
                        new Vector2(connectedRoom.centerX, connectedRoom.centerY),
                        new Vector2(unconnectedRoom.centerX, unconnectedRoom.centerY)
                    );

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestUnconnectedRoom = unconnectedRoom;
                        closestConnectedRoom = connectedRoom;
                    }
                }
            }

            if (closestUnconnectedRoom != null && closestConnectedRoom != null)
            {
                CreateCorridor(closestConnectedRoom, closestUnconnectedRoom);
                closestUnconnectedRoom.isConnected = true;
                closestConnectedRoom.connectedRooms.Add(closestUnconnectedRoom);
                closestUnconnectedRoom.connectedRooms.Add(closestConnectedRoom);
                connectedRooms.Add(closestUnconnectedRoom);
                unconnectedRooms.Remove(closestUnconnectedRoom);
            }
        }

        // Add a few extra connections to create loops (if not avoiding loops)
        if (!avoidDeadEnds)
        {
            int extraConnections = Mathf.FloorToInt(rooms.Count * 0.2f); // 20% extra connections

            for (int i = 0; i < extraConnections; i++)
            {
                Room roomA = rooms[Random.Range(0, rooms.Count)];
                Room roomB = rooms[Random.Range(0, rooms.Count)];

                if (roomA != roomB && !roomA.connectedRooms.Contains(roomB))
                {
                    CreateCorridor(roomA, roomB);
                    roomA.connectedRooms.Add(roomB);
                    roomB.connectedRooms.Add(roomA);
                }
            }
        }
    }

    private void CreateCorridor(Room room1, Room room2)
    {
        Vector2Int start = new Vector2Int(room1.centerX, room1.centerY);
        Vector2Int end = new Vector2Int(room2.centerX, room2.centerY);
        List<Vector2Int> path = new List<Vector2Int>();

        // Choose corridor style
        switch (corridorStyle)
        {
            case CorridorStyle.Straight:
                path = CreateStraightCorridor(start, end);
                break;
            case CorridorStyle.LShaped:
                path = CreateLShapedCorridor(start, end);
                break;
            case CorridorStyle.Organic:
                path = CreateOrganicCorridor(start, end);
                break;
            case CorridorStyle.Random:
            default:
                float randValue = Random.value;
                if (randValue < 0.4f)
                    path = CreateStraightCorridor(start, end);
                else if (randValue < 0.8f)
                    path = CreateLShapedCorridor(start, end);
                else
                    path = CreateOrganicCorridor(start, end);
                break;
        }

        // Add the path to corridors list and update the level grid
        foreach (Vector2Int pos in path)
        {
            if (IsInMap(pos.x, pos.y))
            {
                levelGrid[pos.x, pos.y] = 1; // Floor
                corridors.Add(pos);
            }
        }
    }

    private List<Vector2Int> CreateStraightCorridor(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();

        // Choose whether to go horizontal or vertical first
        bool horizontalFirst = Random.value < 0.5f;

        if (horizontalFirst)
        {
            // Horizontal segment
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

            // Vertical segment
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
        }
        else
        {
            // Vertical segment
            int y = start.y;
            int stepY = y < end.y ? 1 : -1;

            while (y != end.y)
            {
                for (int i = -corridorWidth / 2; i <= corridorWidth / 2; i++)
                {
                    int corridorX = start.x + i;
                    if (IsInMap(corridorX, y))
                    {
                        path.Add(new Vector2Int(corridorX, y));
                    }
                }
                y += stepY;
            }

            // Horizontal segment
            int x = start.x;
            int stepX = x < end.x ? 1 : -1;

            while (x != end.x)
            {
                for (int i = -corridorWidth / 2; i <= corridorWidth / 2; i++)
                {
                    int corridorY = end.y + i;
                    if (IsInMap(x, corridorY))
                    {
                        path.Add(new Vector2Int(x, corridorY));
                    }
                }
                x += stepX;
            }
        }

        return path;
    }

    private List<Vector2Int> CreateLShapedCorridor(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();

        // Calculate corner position
        Vector2Int corner = new Vector2Int(end.x, start.y);

        // Start to corner
        int x = start.x;
        int stepX = x < corner.x ? 1 : -1;

        while (x != corner.x)
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

        // Corner to end
        int y = corner.y;
        int stepY = y < end.y ? 1 : -1;

        while (y != end.y)
        {
            for (int i = -corridorWidth / 2; i <= corridorWidth / 2; i++)
            {
                int corridorX = corner.x + i;
                if (IsInMap(corridorX, y))
                {
                    path.Add(new Vector2Int(corridorX, y));
                }
            }
            y += stepY;
        }

        // Add the corner itself
        for (int i = -corridorWidth / 2; i <= corridorWidth / 2; i++)
        {
            for (int j = -corridorWidth / 2; j <= corridorWidth / 2; j++)
            {
                if (IsInMap(corner.x + i, corner.y + j))
                {
                    path.Add(new Vector2Int(corner.x + i, corner.y + j));
                }
            }
        }

        return path;
    }

    private List<Vector2Int> CreateOrganicCorridor(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        List<Vector2Int> centerPath = new List<Vector2Int>();

        // Create a winding path between start and end
        Vector2Int current = start;
        centerPath.Add(current);

        while (current != end)
        {
            Vector2Int direction;

            // Decide direction based on target and random factor
            if (Random.value < (1 - corridorWindingFactor))
            {
                // Move toward the end point
                int dx = end.x - current.x;
                int dy = end.y - current.y;

                if (Mathf.Abs(dx) > Mathf.Abs(dy))
                {
                    direction = new Vector2Int(dx > 0 ? 1 : -1, 0);
                }
                else
                {
                    direction = new Vector2Int(0, dy > 0 ? 1 : -1);
                }
            }
            else
            {
                // Random direction
                int rand = Random.Range(0, 4);
                direction = rand switch
                {
                    0 => new Vector2Int(1, 0),  // Right
                    1 => new Vector2Int(-1, 0), // Left
                    2 => new Vector2Int(0, 1),  // Up
                    _ => new Vector2Int(0, -1)  // Down
                };
            }

            Vector2Int next = new Vector2Int(current.x + direction.x, current.y + direction.y);

            // Stay within map bounds
            if (IsInMap(next.x, next.y))
            {
                current = next;
                centerPath.Add(current);
            }
        }

        // Expand the center path to corridor width
        foreach (Vector2Int pos in centerPath)
        {
            for (int i = -corridorWidth / 2; i <= corridorWidth / 2; i++)
            {
                for (int j = -corridorWidth / 2; j <= corridorWidth / 2; j++)
                {
                    Vector2Int corridorPos = new Vector2Int(pos.x + i, pos.y + j);
                    if (IsInMap(corridorPos.x, corridorPos.y) && !path.Contains(corridorPos))
                    {
                        path.Add(corridorPos);
                    }
                }
            }
        }

        return path;
    }

    private void AddAdditionalRooms(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // Try to place a new room
            int maxAttempts = 50;
            int attempts = 0;
            bool roomPlaced = false;

            while (!roomPlaced && attempts < maxAttempts)
            {
                attempts++;

                int roomWidth = Random.Range(minRoomSize, maxRoomSize);
                int roomHeight = Random.Range(minRoomSize, maxRoomSize);

                int roomX = Random.Range(5, levelWidth - roomWidth - 5);
                int roomY = Random.Range(5, levelHeight - roomHeight - 5);

                // Check if the room overlaps with any existing rooms
                bool overlaps = false;
                Room newRoom = new Room(roomX, roomY, roomWidth, roomHeight);

                foreach (Room existingRoom in rooms)
                {
                    if (newRoom.Intersects(existingRoom, 2))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    // Add the room
                    rooms.Add(newRoom);

                    // Mark the room on the grid
                    for (int x = roomX; x < roomX + roomWidth; x++)
                    {
                        for (int y = roomY; y < roomY + roomHeight; y++)
                        {
                            if (IsInMap(x, y))
                            {
                                levelGrid[x, y] = 1; // Floor
                            }
                        }
                    }

                    // Connect the room to the closest existing room
                    Room closestRoom = null;
                    float minDistance = float.MaxValue;

                    foreach (Room existingRoom in rooms)
                    {
                        if (existingRoom == newRoom) continue;

                        float distance = Vector2.Distance(
                            new Vector2(existingRoom.centerX, existingRoom.centerY),
                            new Vector2(newRoom.centerX, newRoom.centerY)
                        );

                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestRoom = existingRoom;
                        }
                    }

                    if (closestRoom != null)
                    {
                        CreateCorridor(newRoom, closestRoom);
                        newRoom.isConnected = true;
                        newRoom.connectedRooms.Add(closestRoom);
                        closestRoom.connectedRooms.Add(newRoom);
                    }

                    roomPlaced = true;
                }
            }
        }
    }

    private void DesignateSpecialRooms()
    {
        // Sort rooms by size
        List<Room> sortedRooms = new List<Room>(rooms);
        sortedRooms.Sort((a, b) => (b.width * b.height).CompareTo(a.width * a.height));

        // Mark the largest room as the main room
        Room mainRoom = sortedRooms[0];
        mainRoom.isMainRoom = true;
        mainRoom.tags.Add("main");

        // Designate entrance room
        if (entranceRoomIndex >= 0 && entranceRoomIndex < rooms.Count)
        {
            entranceRoom = rooms[entranceRoomIndex];
        }
        else
        {
            // Choose a room that's reasonably sized but not the main room
            List<Room> candidates = sortedRooms.Where(r => !r.isMainRoom && r.width >= 8 && r.height >= 8).ToList();
            if (candidates.Count > 0)
            {
                entranceRoom = candidates[Random.Range(0, Mathf.Min(3, candidates.Count))];
            }
            else
            {
                entranceRoom = mainRoom; // Fallback to main room
            }
        }

        entranceRoom.roomType = RoomTemplate.RoomType.Entrance;
        entranceRoom.tags.Add("entrance");

        // Calculate distance from entrance for each room
        CalculateRoomDistances();

        // Designate exit room - should be far from entrance
        List<Room> exitCandidates = sortedRooms.Where(r =>
            r != entranceRoom &&
            r.distanceFromStart >= rooms.Count / 3 && // At least 1/3 of the way through the level
            r.width >= 8 && r.height >= 8).ToList();

        if (exitRoomIndex >= 0 && exitRoomIndex < rooms.Count)
        {
            exitRoom = rooms[exitRoomIndex];
        }
        else if (exitCandidates.Count > 0)
        {
            exitRoom = exitCandidates[Random.Range(0, Mathf.Min(3, exitCandidates.Count))];
        }
        else
        {
            // Find the furthest room from entrance
            Room furthestRoom = rooms.OrderByDescending(r => r.distanceFromStart).First();
            exitRoom = furthestRoom;
        }

        exitRoom.roomType = RoomTemplate.RoomType.Exit;
        exitRoom.tags.Add("exit");

        // Find the critical path from entrance to exit
        FindCriticalPath();

        // Designate boss room if enabled - should be near exit
        if (alwaysIncludeBossRoom)
        {
            List<Room> bossCandidates = sortedRooms.Where(r =>
                r != entranceRoom &&
                r != exitRoom &&
                r.distanceFromStart >= rooms.Count / 2 && // At least halfway through
                r.width >= 10 && r.height >= 10).ToList();

            if (bossCandidates.Count > 0)
            {
                bossRoom = bossCandidates[0]; // Pick largest suitable room
                bossRoom.roomType = RoomTemplate.RoomType.Boss;
                bossRoom.tags.Add("boss");
            }
        }

        // Add treasure rooms
        int treasureRoomCount = Mathf.FloorToInt(rooms.Count * treasureRoomChance);
        AddSpecialRooms(treasureRoomCount, RoomTemplate.RoomType.Treasure, "treasure", r =>
            r != entranceRoom && r != exitRoom && r != bossRoom &&
            !criticalPathRooms.Contains(r) && r.width >= 6 && r.height >= 6);

        // Add shop rooms
        int shopRoomCount = Mathf.FloorToInt(rooms.Count * shopRoomChance);
        AddSpecialRooms(shopRoomCount, RoomTemplate.RoomType.Shop, "shop", r =>
            r != entranceRoom && r != exitRoom && r != bossRoom &&
            r.roomType != RoomTemplate.RoomType.Treasure && r.width >= 7 && r.height >= 7);

        // Add secret rooms
        int secretRoomCount = Mathf.FloorToInt(rooms.Count * secretRoomChance);
        AddSpecialRooms(secretRoomCount, RoomTemplate.RoomType.Secret, "secret", r =>
            r != entranceRoom && r != exitRoom && r != bossRoom &&
            r.roomType != RoomTemplate.RoomType.Treasure &&
            r.roomType != RoomTemplate.RoomType.Shop &&
            r.distanceFromStart >= 2);
    }

    private void CalculateRoomDistances()
    {
        // Reset distances
        foreach (Room room in rooms)
        {
            room.distanceFromStart = int.MaxValue;
            room.isVisited = false;
        }

        // BFS from entrance room
        Queue<Room> queue = new Queue<Room>();
        entranceRoom.distanceFromStart = 0;
        entranceRoom.isVisited = true;
        queue.Enqueue(entranceRoom);

        while (queue.Count > 0)
        {
            Room current = queue.Dequeue();

            foreach (Room connected in current.connectedRooms)
            {
                if (!connected.isVisited)
                {
                    connected.distanceFromStart = current.distanceFromStart + 1;
                    connected.isVisited = true;
                    queue.Enqueue(connected);
                }
            }
        }

        // Reset isVisited for later use
        foreach (Room room in rooms)
        {
            room.isVisited = false;
        }
    }

    private void FindCriticalPath()
    {
        criticalPathRooms.Clear();

        if (entranceRoom == null || exitRoom == null)
            return;

        // Reset for pathfinding
        foreach (Room room in rooms)
        {
            room.isVisited = false;
        }

        // Use BFS to find shortest path
        Queue<Room> queue = new Queue<Room>();
        Dictionary<Room, Room> cameFrom = new Dictionary<Room, Room>();

        entranceRoom.isVisited = true;
        queue.Enqueue(entranceRoom);

        bool foundPath = false;
        while (queue.Count > 0 && !foundPath)
        {
            Room current = queue.Dequeue();

            if (current == exitRoom)
            {
                foundPath = true;
                break;
            }

            foreach (Room neighbor in current.connectedRooms)
            {
                if (!neighbor.isVisited)
                {
                    neighbor.isVisited = true;
                    queue.Enqueue(neighbor);
                    cameFrom[neighbor] = current;
                }
            }
        }

        // Reconstruct path
        if (foundPath)
        {
            Room current = exitRoom;
            while (current != entranceRoom)
            {
                criticalPathRooms.Add(current);
                current = cameFrom[current];
            }
            criticalPathRooms.Add(entranceRoom);
            criticalPathRooms.Reverse();
        }

        // Tag critical path rooms
        foreach (Room room in criticalPathRooms)
        {
            room.tags.Add("criticalPath");
        }
    }

    private void AddSpecialRooms(int count, RoomTemplate.RoomType roomType, string tag, Func<Room, bool> filter)
    {
        List<Room> candidates = rooms.Where(filter).ToList();

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int index = Random.Range(0, candidates.Count);
            Room room = candidates[index];
            room.roomType = roomType;
            room.tags.Add(tag);
            specialRooms.Add(room);
            candidates.RemoveAt(index);
        }
    }

    private void ApplyCellularAutomata()
    {
        // Make a copy of the level grid
        int[,] newGrid = new int[levelWidth, levelHeight];
        Array.Copy(levelGrid, newGrid, levelGrid.Length);

        // Apply cellular automata to make rooms more organic
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                // Skip corridors and special tiles
                if (corridors.Contains(new Vector2Int(x, y)) || levelGrid[x, y] > 1)
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

    private void EnsureConnectivity()
    {
        bool[,] visited = new bool[levelWidth, levelHeight];
        List<List<Vector2Int>> regions = new List<List<Vector2Int>>();

        // Find all floor regions
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                if (levelGrid[x, y] == 1 && !visited[x, y])
                {
                    List<Vector2Int> region = GetConnectedFloorRegion(x, y, visited);
                    if (region.Count > 0)
                    {
                        regions.Add(region);
                    }
                }
            }
        }

        // If more than one region, connect them
        if (regions.Count > 1)
        {
            // Sort regions by size (largest first)
            regions.Sort((a, b) => b.Count.CompareTo(a.Count));

            List<Vector2Int> mainRegion = regions[0];

            for (int i = 1; i < regions.Count; i++)
            {
                ConnectRegions(mainRegion, regions[i]);
            }
        }
    }

    private List<Vector2Int> GetConnectedFloorRegion(int startX, int startY, bool[,] visited)
    {
        List<Vector2Int> region = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int start = new Vector2Int(startX, startY);
        queue.Enqueue(start);
        visited[startX, startY] = true;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            region.Add(current);

            // Check adjacent tiles
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),  // Up
                new Vector2Int(1, 0),  // Right
                new Vector2Int(0, -1), // Down
                new Vector2Int(-1, 0)  // Left
            };

            foreach (Vector2Int dir in directions)
            {
                Vector2Int next = new Vector2Int(current.x + dir.x, current.y + dir.y);

                if (IsInMap(next.x, next.y) && !visited[next.x, next.y] && levelGrid[next.x, next.y] == 1)
                {
                    visited[next.x, next.y] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return region;
    }

    private void ConnectRegions(List<Vector2Int> regionA, List<Vector2Int> regionB)
    {
        // Find the closest pair of points between regions
        float minDistance = float.MaxValue;
        Vector2Int bestA = Vector2Int.zero;
        Vector2Int bestB = Vector2Int.zero;

        foreach (Vector2Int a in regionA)
        {
            foreach (Vector2Int b in regionB)
            {
                float distance = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));

                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestA = a;
                    bestB = b;
                }
            }
        }

        // Create a corridor between the closest points
        List<Vector2Int> path = CreateOrganicCorridor(bestA, bestB);

        // Add the path to the level grid
        foreach (Vector2Int pos in path)
        {
            if (IsInMap(pos.x, pos.y))
            {
                levelGrid[pos.x, pos.y] = 1; // Floor
                corridors.Add(pos);
            }
        }
    }

    private void PlaceWalls()
    {
        // Make a copy of the level grid
        int[,] newGrid = new int[levelWidth, levelHeight];
        Array.Copy(levelGrid, newGrid, levelGrid.Length);

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

    private void PlaceDoors()
    {
        if (!useDoors)
            return;

        // Find potential door positions between rooms and corridors
        foreach (Room room in rooms)
        {
            List<Vector2Int> perimeter = room.GetPerimeterTiles();
            List<Vector2Int> potentialDoorPositions = new List<Vector2Int>();

            foreach (Vector2Int pos in perimeter)
            {
                // Check if this position is a wall
                if (!IsInMap(pos.x, pos.y) || levelGrid[pos.x, pos.y] != 2)
                    continue;

                // Check if there's a floor on both sides (inside and outside the room)
                Vector2Int[] directions = new Vector2Int[]
                {
                    new Vector2Int(0, 1),  // Up
                    new Vector2Int(1, 0),  // Right
                    new Vector2Int(0, -1), // Down
                    new Vector2Int(-1, 0)  // Left
                };

                bool hasInsideFloor = false;
                bool hasOutsideFloor = false;

                foreach (Vector2Int dir in directions)
                {
                    Vector2Int neighbor = new Vector2Int(pos.x + dir.x, pos.y + dir.y);

                    if (IsInMap(neighbor.x, neighbor.y) && levelGrid[neighbor.x, neighbor.y] == 1)
                    {
                        if (room.Contains(neighbor))
                        {
                            hasInsideFloor = true;
                        }
                        else
                        {
                            hasOutsideFloor = true;
                        }
                    }
                }

                if (hasInsideFloor && hasOutsideFloor)
                {
                    potentialDoorPositions.Add(pos);
                }
            }

            // Place doors at appropriate positions
            if (potentialDoorPositions.Count > 0)
            {
                // For regular rooms, just place one door
                if (room.roomType == RoomTemplate.RoomType.Normal ||
                    room.roomType == RoomTemplate.RoomType.Entrance)
                {
                    // Choose a random door position
                    Vector2Int doorPos = potentialDoorPositions[Random.Range(0, potentialDoorPositions.Count)];
                    PlaceDoorAt(doorPos, room);
                }
                // For special rooms, possibly place a locked door
                else if (room.roomType == RoomTemplate.RoomType.Treasure ||
                         room.roomType == RoomTemplate.RoomType.Shop ||
                         room.roomType == RoomTemplate.RoomType.Boss)
                {
                    // If using key system, 50% chance for a locked door
                    bool isLocked = useKeySystem && Random.value < 0.5f;

                    // Choose a random door position
                    Vector2Int doorPos = potentialDoorPositions[Random.Range(0, potentialDoorPositions.Count)];
                    PlaceDoorAt(doorPos, room, isLocked);
                }
                // For secret rooms, place a hidden door
                else if (room.roomType == RoomTemplate.RoomType.Secret)
                {
                    Vector2Int doorPos = potentialDoorPositions[Random.Range(0, potentialDoorPositions.Count)];
                    PlaceDoorAt(doorPos, room, false, true);
                }
            }
        }
    }

    private void PlaceDoorAt(Vector2Int position, Room room, bool isLocked = false, bool isHidden = false)
    {
        // Mark the door on the level grid
        levelGrid[position.x, position.y] = 3; // Door
        doors.Add(position);

        // Create a door object
        Door door = new Door(position, isLocked);
        room.doors.Add(door);

        // If locked, add to locked positions
        if (isLocked)
        {
            lockPositions.Add(position);
        }

        // If we have a door prefab, instantiate it
        if (doorPrefab != null)
        {
            GameObject doorObj = Instantiate(doorPrefab, new Vector3(position.x, position.y, 0), Quaternion.identity);
            doorObj.tag = "Door";

            // Set door properties - you'll need to implement these in your door script
            DoorController doorController = doorObj.GetComponent<DoorController>();
            if (doorController != null)
            {
                doorController.isLocked = isLocked;
                doorController.isHidden = isHidden;
                doorController.keyId = door.keyId;
                // doorController.Initialize(); // You'd need to implement this method
            }

            // Store the door in the objects dictionary
            objectsPlaced[position] = doorObj;
        }
    }

    private void AddSpecialFeatures()
    {
        // Add water
        if (waterChance > 0)
        {
            AddWaterFeatures();
        }

        // Add obstacles
        if (obstacleChance > 0)
        {
            AddObstacles();
        }
    }

    private void AddWaterFeatures()
    {
        // Select random rooms to add water features
        foreach (Room room in rooms)
        {
            // Skip special rooms
            if (room.roomType != RoomTemplate.RoomType.Normal)
                continue;

            // Random chance based on water chance
            if (Random.value > waterChance)
                continue;

            // Choose a random water feature type
            int featureType = Random.Range(0, 3);

            switch (featureType)
            {
                case 0: // Small pond
                    AddWaterPond(room);
                    break;
                case 1: // Stream
                    AddWaterStream(room);
                    break;
                case 2: // Border lake
                    AddWaterBorder(room);
                    break;
            }
        }
    }

    private void AddWaterPond(Room room)
    {
        // Create a small pond in the room
        int pondSize = Mathf.Min(room.width, room.height) / 3;
        int pondX = room.centerX - pondSize / 2;
        int pondY = room.centerY - pondSize / 2;

        for (int x = 0; x < pondSize; x++)
        {
            for (int y = 0; y < pondSize; y++)
            {
                int worldX = pondX + x;
                int worldY = pondY + y;

                // Create oval shape
                float distanceFromCenter = Vector2.Distance(
                    new Vector2(worldX, worldY),
                    new Vector2(room.centerX, room.centerY)
                );

                if (distanceFromCenter < pondSize / 2 && IsInMap(worldX, worldY) &&
                    levelGrid[worldX, worldY] == 1) // Only replace floor tiles
                {
                    levelGrid[worldX, worldY] = 4; // Water
                }
            }
        }
    }

    private void AddWaterStream(Room room)
    {
        // Create a stream across the room
        bool horizontal = Random.value < 0.5f;
        int streamWidth = Random.Range(1, 3);

        if (horizontal)
        {
            int streamY = Random.Range(room.y + 2, room.y + room.height - 2);

            for (int x = room.x; x < room.x + room.width; x++)
            {
                for (int w = -streamWidth / 2; w <= streamWidth / 2; w++)
                {
                    int worldY = streamY + w;

                    if (IsInMap(x, worldY) && levelGrid[x, worldY] == 1)
                    {
                        levelGrid[x, worldY] = 4; // Water
                    }
                }
            }
        }
        else // Vertical
        {
            int streamX = Random.Range(room.x + 2, room.x + room.width - 2);

            for (int y = room.y; y < room.y + room.height; y++)
            {
                for (int w = -streamWidth / 2; w <= streamWidth / 2; w++)
                {
                    int worldX = streamX + w;

                    if (IsInMap(worldX, y) && levelGrid[worldX, y] == 1)
                    {
                        levelGrid[worldX, y] = 4; // Water
                    }
                }
            }
        }
    }

    private void AddWaterBorder(Room room)
    {
        // Create water along one or more edges of the room
        int numEdges = Random.Range(1, 4);
        bool[] edgesWithWater = new bool[4] { false, false, false, false }; // Top, Right, Bottom, Left

        for (int i = 0; i < numEdges; i++)
        {
            int edge = Random.Range(0, 4);
            edgesWithWater[edge] = true;
        }

        int borderWidth = Random.Range(1, 3);

        // Top edge
        if (edgesWithWater[0])
        {
            for (int x = room.x + 1; x < room.x + room.width - 1; x++)
            {
                for (int y = room.y + 1; y < room.y + 1 + borderWidth; y++)
                {
                    if (IsInMap(x, y) && levelGrid[x, y] == 1)
                    {
                        levelGrid[x, y] = 4; // Water
                    }
                }
            }
        }

        // Right edge
        if (edgesWithWater[1])
        {
            for (int x = room.x + room.width - 1 - borderWidth; x < room.x + room.width - 1; x++)
            {
                for (int y = room.y + 1; y < room.y + room.height - 1; y++)
                {
                    if (IsInMap(x, y) && levelGrid[x, y] == 1)
                    {
                        levelGrid[x, y] = 4; // Water
                    }
                }
            }
        }

        // Bottom edge
        if (edgesWithWater[2])
        {
            for (int x = room.x + 1; x < room.x + room.width - 1; x++)
            {
                for (int y = room.y + room.height - 1 - borderWidth; y < room.y + room.height - 1; y++)
                {
                    if (IsInMap(x, y) && levelGrid[x, y] == 1)
                    {
                        levelGrid[x, y] = 4; // Water
                    }
                }
            }
        }

        // Left edge
        if (edgesWithWater[3])
        {
            for (int x = room.x + 1; x < room.x + 1 + borderWidth; x++)
            {
                for (int y = room.y + 1; y < room.y + room.height - 1; y++)
                {
                    if (IsInMap(x, y) && levelGrid[x, y] == 1)
                    {
                        levelGrid[x, y] = 4; // Water
                    }
                }
            }
        }
    }

    private void AddObstacles()
    {
        // Add obstacles to rooms
        foreach (Room room in rooms)
        {
            // Skip very small rooms and special rooms
            if (room.width < 8 || room.height < 8 || room.roomType != RoomTemplate.RoomType.Normal)
                continue;

            // Random chance based on obstacle chance
            if (Random.value > obstacleChance)
                continue;

            // Choose a random obstacle type
            int obstacleType = Random.Range(0, 3);

            switch (obstacleType)
            {
                case 0: // Pillars
                    AddPillars(room);
                    break;
                case 1: // Central structure
                    AddCentralStructure(room);
                    break;
                case 2: // Random obstacles
                    AddRandomObstacles(room);
                    break;
            }
        }
    }

    private void AddPillars(Room room)
    {
        // Add pillars in a grid pattern
        int pillarSpacing = Random.Range(3, 5);

        for (int x = room.x + 2; x < room.x + room.width - 2; x += pillarSpacing)
        {
            for (int y = room.y + 2; y < room.y + room.height - 2; y += pillarSpacing)
            {
                // Skip center area
                if (Mathf.Abs(x - room.centerX) < 2 && Mathf.Abs(y - room.centerY) < 2)
                    continue;

                if (IsInMap(x, y) && levelGrid[x, y] == 1)
                {
                    levelGrid[x, y] = 5; // Obstacle
                }
            }
        }
    }

    private void AddCentralStructure(Room room)
    {
        // Add a central structure
        int structureSize = Mathf.Min(room.width, room.height) / 3;

        for (int x = room.centerX - structureSize / 2; x <= room.centerX + structureSize / 2; x++)
        {
            for (int y = room.centerY - structureSize / 2; y <= room.centerY + structureSize / 2; y++)
            {
                // Create a hollow structure
                if (x == room.centerX - structureSize / 2 || x == room.centerX + structureSize / 2 ||
                    y == room.centerY - structureSize / 2 || y == room.centerY + structureSize / 2)
                {
                    if (IsInMap(x, y) && levelGrid[x, y] == 1)
                    {
                        levelGrid[x, y] = 5; // Obstacle
                    }
                }
            }
        }
    }

    private void AddRandomObstacles(Room room)
    {
        // Add random obstacles
        int numObstacles = Random.Range(5, 15);

        for (int i = 0; i < numObstacles; i++)
        {
            int x = Random.Range(room.x + 2, room.x + room.width - 2);
            int y = Random.Range(room.y + 2, room.y + room.height - 2);

            // Skip areas near doors
            bool nearDoor = false;
            foreach (Door door in room.doors)
            {
                if (Vector2Int.Distance(new Vector2Int(x, y), door.position) < 3)
                {
                    nearDoor = true;
                    break;
                }
            }

            if (!nearDoor && IsInMap(x, y) && levelGrid[x, y] == 1)
            {
                levelGrid[x, y] = 5; // Obstacle

                // Sometimes make larger obstacles
                if (Random.value < 0.3f)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            // Skip center (already placed)
                            if (dx == 0 && dy == 0)
                                continue;

                            int nx = x + dx;
                            int ny = y + dy;

                            if (IsInMap(nx, ny) && levelGrid[nx, ny] == 1 && Random.value < 0.5f)
                            {
                                levelGrid[nx, ny] = 5; // Obstacle
                            }
                        }
                    }
                }
            }
        }
    }

    private void ImplementKeyLockSystem()
    {
        if (!useKeySystem || lockPositions.Count == 0)
            return;

        // Create a key for each locked door
        foreach (Vector2Int lockPos in lockPositions)
        {
            // Find a suitable room to place the key in
            Room lockRoom = null;

            // Find which room contains this lock
            foreach (Room room in rooms)
            {
                foreach (Door door in room.doors)
                {
                    if (door.position == lockPos)
                    {
                        lockRoom = room;
                        break;
                    }
                }

                if (lockRoom != null)
                    break;
            }

            if (lockRoom == null)
                continue;

            // Find a room that's on the critical path but before this locked room
            List<Room> keyRoomCandidates = new List<Room>();

            foreach (Room room in criticalPathRooms)
            {
                if (room != lockRoom && room.distanceFromStart < lockRoom.distanceFromStart)
                {
                    keyRoomCandidates.Add(room);
                }
            }

            // If no suitable room found, use any room except the locked room
            if (keyRoomCandidates.Count == 0)
            {
                keyRoomCandidates = rooms.Where(r => r != lockRoom).ToList();
            }

            if (keyRoomCandidates.Count == 0)
                continue;

            // Choose a random room to place the key
            Room keyRoom = keyRoomCandidates[Random.Range(0, keyRoomCandidates.Count)];

            // Choose a random position in the room
            int keyX = Random.Range(keyRoom.x + 1, keyRoom.x + keyRoom.width - 1);
            int keyY = Random.Range(keyRoom.y + 1, keyRoom.y + keyRoom.height - 1);

            // Ensure it's on a floor tile
            if (IsInMap(keyX, keyY) && levelGrid[keyX, keyY] == 1)
            {
                Vector2Int keyPos = new Vector2Int(keyX, keyY);
                keyPositions.Add(keyPos);

                // Associate the key with the lock
                // In a real implementation, you'd need to store this relationship
                // and create a key object with the appropriate ID
            }
        }
    }

    private void PlaceDecorations()
    {
        if (decorationTilemap == null)
            return;

        // Place decorations in each room
        foreach (Room room in rooms)
        {
            // Use room-specific decoration density if available
            float roomDecorationDensity = decorationDensity;

            if (room.template != null)
            {
                roomDecorationDensity = room.template.decorationDensity;
            }

            // Adjust density based on room type
            switch (room.roomType)
            {
                case RoomTemplate.RoomType.Entrance:
                case RoomTemplate.RoomType.Exit:
                    roomDecorationDensity *= 1.5f; // More decorations in important rooms
                    break;
                case RoomTemplate.RoomType.Shop:
                case RoomTemplate.RoomType.Treasure:
                    roomDecorationDensity *= 2f; // Even more decorations in special rooms
                    break;
                case RoomTemplate.RoomType.Boss:
                    roomDecorationDensity *= 0.5f; // Fewer decorations in boss rooms (more space to move)
                    break;
            }

            // Get appropriate decoration tiles
            TileBase[] decorTiles;

            if (room.theme != null && room.theme.decorationTiles != null && room.theme.decorationTiles.Length > 0)
            {
                decorTiles = room.theme.decorationTiles;
            }
            else
            {
                decorTiles = defaultDecorationTiles;
            }

            if (decorTiles == null || decorTiles.Length == 0)
                continue;

            // Place decorations
            for (int x = room.x + 1; x < room.x + room.width - 1; x++)
            {
                for (int y = room.y + 1; y < room.y + room.height - 1; y++)
                {
                    // Only place on floor tiles
                    if (IsInMap(x, y) && levelGrid[x, y] == 1 && Random.value < roomDecorationDensity)
                    {
                        // Check if there's enough space (not near walls, enemies or other decorations)
                        bool canPlaceDecoration = true;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dy = -1; dy <= 1; dy++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;

                                if (IsInMap(nx, ny) && levelGrid[nx, ny] != 1)
                                {
                                    canPlaceDecoration = false;
                                    break;
                                }

                                if (IsInMap(nx, ny) && decorationTilemap.GetTile(new Vector3Int(nx, ny, 0)) != null)
                                {
                                    canPlaceDecoration = false;
                                    break;
                                }
                            }

                            if (!canPlaceDecoration)
                                break;
                        }

                        if (canPlaceDecoration)
                        {
                            // Place a random decoration tile
                            TileBase decorTile = decorTiles[Random.Range(0, decorTiles.Length)];
                            decorationTilemap.SetTile(new Vector3Int(x, y, 0), decorTile);
                        }
                    }
                }
            }
        }

        // Place decorations in corridors if enabled
        if (useCorridorDecorations)
        {
            float corridorDecorDensity = decorationDensity * 0.5f; // Less dense in corridors

            foreach (Vector2Int pos in corridors)
            {
                if (Random.value < corridorDecorDensity)
                {
                    // Check if there's enough space
                    bool canPlaceDecoration = true;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int nx = pos.x + dx;
                            int ny = pos.y + dy;

                            if (IsInMap(nx, ny) && levelGrid[nx, ny] != 1)
                            {
                                canPlaceDecoration = false;
                                break;
                            }

                            if (IsInMap(nx, ny) && decorationTilemap.GetTile(new Vector3Int(nx, ny, 0)) != null)
                            {
                                canPlaceDecoration = false;
                                break;
                            }
                        }

                        if (!canPlaceDecoration)
                            break;
                    }

                    if (canPlaceDecoration)
                    {
                        // Choose appropriate decoration tiles based on location
                        TileBase[] decorTiles = defaultDecorationTiles;

                        if (decorTiles != null && decorTiles.Length > 0)
                        {
                            TileBase decorTile = decorTiles[Random.Range(0, decorTiles.Length)];
                            decorationTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), decorTile);
                        }
                    }
                }
            }
        }
    }

    private void PlaceEnemies()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return;

        // Place enemies in each room based on room type and distance from start
        foreach (Room room in rooms)
        {
            // Skip the entrance room and very small rooms
            if (room == entranceRoom || room.width < 5 || room.height < 5)
                continue;

            // Determine enemy density based on room type and template
            float roomEnemyDensity = enemyDensity;
            int maxEnemiesInRoom = Mathf.FloorToInt((room.width * room.height) / 25f); // Default

            if (room.template != null)
            {
                roomEnemyDensity = room.template.enemyDensity;
                maxEnemiesInRoom = room.template.maxEnemies;
            }

            // Adjust based on room type
            switch (room.roomType)
            {
                case RoomTemplate.RoomType.Boss:
                    // Boss rooms have one boss enemy
                    PlaceBossEnemy(room);
                    continue;
                case RoomTemplate.RoomType.Treasure:
                case RoomTemplate.RoomType.Shop:
                    // Fewer enemies in special rooms
                    roomEnemyDensity *= 0.5f;
                    break;
                case RoomTemplate.RoomType.Challenge:
                    // More enemies in challenge rooms
                    roomEnemyDensity *= 2f;
                    break;
                case RoomTemplate.RoomType.Secret:
                    // Fewer enemies in secret rooms
                    roomEnemyDensity *= 0.25f;
                    break;
            }

            // Scale enemy density based on distance from start
            roomEnemyDensity *= 1 + (room.distanceFromStart * 0.1f);

            // Apply difficulty multiplier
            roomEnemyDensity *= difficultyMultiplier;

            // Determine number of enemies to place
            int enemyCount = Mathf.RoundToInt(room.width * room.height * roomEnemyDensity / 100f);
            enemyCount = Mathf.Clamp(enemyCount, 0, maxEnemiesInRoom);

            if (enemyCount == 0)
                continue;

            // Get enemy prefabs appropriate for this room
            GameObject[] availableEnemies;

            if (room.theme != null && room.theme.enemyPrefabs != null && room.theme.enemyPrefabs.Length > 0)
            {
                availableEnemies = room.theme.enemyPrefabs;
            }
            else
            {
                availableEnemies = enemyPrefabs;
            }

            if (availableEnemies.Length == 0)
                continue;

            // Place enemies
            int enemiesPlaced = 0;
            int maxAttempts = enemyCount * 5;
            int attempts = 0;

            while (enemiesPlaced < enemyCount && attempts < maxAttempts)
            {
                attempts++;

                // Choose a random position in the room
                int x = Random.Range(room.x + 2, room.x + room.width - 2);
                int y = Random.Range(room.y + 2, room.y + room.height - 2);

                // Check if position is valid (floor tile and not too close to doors or other enemies)
                if (IsValidEnemyPosition(x, y, room))
                {
                    // Choose a random enemy prefab
                    GameObject enemyPrefab = availableEnemies[Random.Range(0, availableEnemies.Length)];

                    // Instantiate the enemy
                    Vector3 position = new Vector3(x, y, 0);
                    GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
                    enemy.tag = "Enemy";

                    // Store the enemy in the objects dictionary
                    objectsPlaced[new Vector2Int(x, y)] = enemy;

                    enemiesPlaced++;
                }
            }

            // Mark that this room has enemies
            room.hasEnemies = enemiesPlaced > 0;
        }
    }

    private void PlaceBossEnemy(Room room)
    {
        if (bossPrefabs == null || bossPrefabs.Length == 0)
            return;

        // Place a boss enemy in the center of the room
        Vector3 position = new Vector3(room.centerX, room.centerY, 0);

        // Choose a random boss prefab
        GameObject bossPrefab = bossPrefabs[Random.Range(0, bossPrefabs.Length)];

        // Instantiate the boss
        GameObject boss = Instantiate(bossPrefab, position, Quaternion.identity);
        boss.tag = "Enemy";

        // Store the boss in the objects dictionary
        objectsPlaced[new Vector2Int(room.centerX, room.centerY)] = boss;

        // Mark that this room has enemies
        room.hasEnemies = true;
    }

    private bool IsValidEnemyPosition(int x, int y, Room room)
    {
        // Check if position is on a floor tile
        if (!IsInMap(x, y) || levelGrid[x, y] != 1)
            return false;

        // Check if position already has something on it
        if (decorationTilemap.GetTile(new Vector3Int(x, y, 0)) != null)
            return false;

        // Check if position is already occupied by an object
        if (objectsPlaced.ContainsKey(new Vector2Int(x, y)))
            return false;

        // Check if position is too close to doors
        foreach (Door door in room.doors)
        {
            if (Vector2Int.Distance(new Vector2Int(x, y), door.position) < 3)
                return false;
        }

        return true;
    }

    private void PlacePlayer()
    {
        if (playerPrefab == null || entranceRoom == null)
            return;

        // Place player in the center of the entrance room
        Vector3 position = new Vector3(entranceRoom.centerX, entranceRoom.centerY, 0);

        // Instantiate the player
        GameObject player = Instantiate(playerPrefab, position, Quaternion.identity);
        player.tag = "Player";
    }

    private void PlaceSpecialObjects()
    {
        // Place exit
        if (exitPrefab != null && exitRoom != null)
        {
            Vector3 exitPosition = new Vector3(exitRoom.centerX, exitRoom.centerY, 0);
            GameObject exit = Instantiate(exitPrefab, exitPosition, Quaternion.identity);
            exit.tag = "Exit";
            objectsPlaced[new Vector2Int(exitRoom.centerX, exitRoom.centerY)] = exit;
        }

        // Place treasures in treasure rooms
        if (treasurePrefabs != null && treasurePrefabs.Length > 0)
        {
            foreach (Room room in specialRooms)
            {
                if (room.roomType == RoomTemplate.RoomType.Treasure)
                {
                    // Place treasures
                    int treasureCount = Random.Range(1, 4);

                    for (int i = 0; i < treasureCount; i++)
                    {
                        int x = Random.Range(room.x + 2, room.x + room.width - 2);
                        int y = Random.Range(room.y + 2, room.y + room.height - 2);

                        if (IsValidObjectPosition(x, y))
                        {
                            GameObject treasurePrefab = treasurePrefabs[Random.Range(0, treasurePrefabs.Length)];
                            Vector3 position = new Vector3(x, y, 0);
                            GameObject treasure = Instantiate(treasurePrefab, position, Quaternion.identity);
                            treasure.tag = "Treasure";
                            objectsPlaced[new Vector2Int(x, y)] = treasure;
                        }
                    }
                }
            }
        }

        // Place shop items in shop rooms
        if (shopPrefabs != null && shopPrefabs.Length > 0)
        {
            foreach (Room room in specialRooms)
            {
                if (room.roomType == RoomTemplate.RoomType.Shop)
                {
                    // Place shop items
                    int itemCount = Random.Range(2, 5);

                    // Arrange items in a row
                    int startX = room.centerX - (itemCount / 2);
                    int y = room.centerY;

                    for (int i = 0; i < itemCount; i++)
                    {
                        int x = startX + i;

                        if (IsValidObjectPosition(x, y))
                        {
                            GameObject shopPrefab = shopPrefabs[Random.Range(0, shopPrefabs.Length)];
                            Vector3 position = new Vector3(x, y, 0);
                            GameObject shopItem = Instantiate(shopPrefab, position, Quaternion.identity);
                            shopItem.tag = "Shop";
                            objectsPlaced[new Vector2Int(x, y)] = shopItem;
                        }
                    }
                }
            }
        }

        // Place keys
        foreach (Vector2Int keyPos in keyPositions)
        {
            // Here you would instantiate a key object
            // For now, we'll just add something to the decoration tilemap
            if (IsInMap(keyPos.x, keyPos.y) && decorationTilemap != null)
            {
                // Use a special tile to represent keys
                if (defaultDecorationTiles != null && defaultDecorationTiles.Length > 0)
                {
                    TileBase keyTile = defaultDecorationTiles[0]; // Use first decoration tile as key
                    decorationTilemap.SetTile(new Vector3Int(keyPos.x, keyPos.y, 0), keyTile);
                }
            }
        }

        // Place required objects from room templates
        foreach (Room room in rooms)
        {
            if (room.requiredObjects.Count > 0)
            {
                foreach (var obj in room.requiredObjects)
                {
                    GameObject prefab = obj.Key;
                    Vector2Int pos = obj.Value;

                    if (IsValidObjectPosition(pos.x, pos.y) && prefab != null)
                    {
                        Vector3 position = new Vector3(pos.x, pos.y, 0);
                        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
                        objectsPlaced[pos] = instance;
                    }
                }
            }
        }
    }

    private bool IsValidObjectPosition(int x, int y)
    {
        // Check if position is on a floor tile
        if (!IsInMap(x, y) || levelGrid[x, y] != 1)
            return false;

        // Check if position already has something on it
        if (decorationTilemap.GetTile(new Vector3Int(x, y, 0)) != null)
            return false;

        // Check if position is already occupied by an object
        if (objectsPlaced.ContainsKey(new Vector2Int(x, y)))
            return false;

        return true;
    }

    private bool ValidateLevel()
    {
        // Basic validation: ensure we have an entrance and exit
        if (entranceRoom == null || exitRoom == null)
            return false;

        // Ensure there's a valid path from entrance to exit
        if (criticalPathRooms.Count == 0)
            return false;

        // Check for isolated regions
        bool[,] visited = new bool[levelWidth, levelHeight];
        int totalFloorTiles = 0;

        // Count total floor tiles
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                if (levelGrid[x, y] == 1)
                {
                    totalFloorTiles++;
                }
            }
        }

        // Flood fill from entrance room to count connected floor tiles
        List<Vector2Int> connectedRegion = GetConnectedFloorRegion(entranceRoom.centerX, entranceRoom.centerY, visited);

        // If not all floor tiles are connected, the level is invalid
        if (connectedRegion.Count < totalFloorTiles)
            return false;

        // All checks passed
        return true;
    }

    private void RenderLevel()
    {
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);

                // Choose the appropriate tiles based on the cell's biome
                LevelTheme theme = GetThemeForPosition(x, y);
                TileBase floorTile = theme?.floorTile ?? defaultFloorTile;
                TileBase wallTile = theme?.wallTile ?? defaultWallTile;

                switch (levelGrid[x, y])
                {
                    case 1: // Floor
                        floorTilemap.SetTile(position, floorTile);
                        break;
                    case 2: // Wall
                        wallTilemap.SetTile(position, wallTile);
                        break;
                    case 3: // Door
                        // Doors are handled separately through the PlaceDoors method
                        floorTilemap.SetTile(position, floorTile);
                        break;
                    case 4: // Water
                        // Set water tiles (you'd need to add water tiles to your implementation)
                        break;
                    case 5: // Obstacle
                        // Set obstacle tiles (you'd need to add obstacle tiles to your implementation)
                        wallTilemap.SetTile(position, wallTile);
                        break;
                }
            }
        }
    }

    private LevelTheme GetThemeForPosition(int x, int y)
    {
        if (!useBiomes || levelThemes == null || levelThemes.Length == 0)
            return null;

        if (IsInMap(x, y))
        {
            int biomeIndex = biomeGrid[x, y];
            if (biomeIndex >= 0 && biomeIndex < levelThemes.Length)
            {
                return levelThemes[biomeIndex];
            }
        }

        return null;
    }

    private void GenerateMinimap()
    {
        if (minimapTilemap == null)
            return;

        // Clear the minimap
        minimapTilemap.ClearAllTiles();

        // Render the rooms
        foreach (Room room in rooms)
        {
            // Choose the tile based on room type
            TileBase roomTile;

            switch (room.roomType)
            {
                case RoomTemplate.RoomType.Entrance:
                case RoomTemplate.RoomType.Exit:
                case RoomTemplate.RoomType.Boss:
                case RoomTemplate.RoomType.Shop:
                case RoomTemplate.RoomType.Treasure:
                    roomTile = minimapSpecialRoomTile;
                    break;
                default:
                    roomTile = minimapRoomTile;
                    break;
            }

            // Fill the room on the minimap
            for (int x = room.x; x < room.x + room.width; x++)
            {
                for (int y = room.y; y < room.y + room.height; y++)
                {
                    if (IsInMap(x, y))
                    {
                        minimapTilemap.SetTile(new Vector3Int(x, y, 0), roomTile);
                    }
                }
            }
        }

        // Render corridors
        foreach (Vector2Int pos in corridors)
        {
            minimapTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), minimapFloorTile);
        }

        // Render walls
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                if (levelGrid[x, y] == 2) // Wall
                {
                    minimapTilemap.SetTile(new Vector3Int(x, y, 0), minimapWallTile);
                }
            }
        }

        // Mark player position
        if (entranceRoom != null)
        {
            minimapTilemap.SetTile(new Vector3Int(entranceRoom.centerX, entranceRoom.centerY, 0), minimapPlayerTile);
        }
    }

    private bool IsInMap(int x, int y)
    {
        return x >= 0 && x < levelWidth && y >= 0 && y < levelHeight;
    }
}

// This is a dummy implementation of the DoorController class
// You would need to implement this properly in your project
public class DoorController : MonoBehaviour
{
    public bool isLocked;
    public bool isHidden;
    public string keyId;

    public void Initialize()
    {
        // Initialize door based on properties
    }
}