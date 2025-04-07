using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System; // Required for System.Random

public enum TileType { Empty, Floor, Wall }

public class HybridLevelGenerator : MonoBehaviour
{
    [Header("Level Dimensions")]
    public int levelWidth = 100;
    public int levelHeight = 100;

    [Header("BSP Settings")]
    public int minRoomSize = 8; // Minimum width/height for a partition to contain a room
    public int maxIterations = 5; // How many times to split the space (controls room count)
    public float roomPadding = 2f; // Minimum space between room edge and partition edge

    [Header("Corridor Settings")]
    public int corridorWidth = 1; // Width of the corridors (1 = single tile wide)

    [Header("Tilemaps & Tiles")]
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public TileBase floorTile;
    public TileBase wallTile;
    // Add more TileBases for variations or decorations if needed

    [Header("Randomness")]
    public int seed = 0; // Use 0 for random seed each time, or set value for specific layout
    public bool useRandomSeed = true;

    [Header("Entities & Decorations")]
    public GameObject playerPrefab; // Assign the player prefab in the Inspector
    public GameObject enemyPrefab; // Assign the enemy prefab in the Inspector
    public GameObject decorationPrefab; // Assign the decoration prefab in the Inspector
    public int enemiesPerRoom = 2; // Number of enemies per room
    public int decorationsPerRoom = 3; // Number of decorations per room

    // Internal Data
    private TileType[,] grid;
    private List<RectInt> bspLeaves;
    private List<RectInt> rooms;
    private System.Random pseudoRandom;

    // Track dynamically instantiated objects
    private List<GameObject> spawnedObjects = new List<GameObject>();

    // --- Public Methods ---

    [ContextMenu("Generate Level")] // Allows triggering from Inspector
    public void GenerateLevel()
    {
        ClearLevel();
        Initialize();
        RunBSPSplit();
        CreateRoomsInLeaves();
        CreateCorridors(); // Connect rooms based on BSP hierarchy
        ApplyTilesToTilemap();
        // Future steps: Add details (Cellular Automata inside rooms?), place decorations (Noise based?), spawn player/enemies.
        Debug.Log($"Level generation complete. Seed: {seed}. Rooms: {rooms.Count}");
    }

    [ContextMenu("Clear Level")]
    public void ClearLevel()
    {
        // Clear tilemaps
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();

        // Destroy all dynamically spawned objects
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        spawnedObjects.Clear(); // Clear the list after destroying objects

        // Clear internal data
        grid = null;
        bspLeaves = null;
        rooms = new List<RectInt>(); // Ensure rooms list is reset

        Debug.Log("Level cleared.");
    }


    // --- Initialization ---

    private void Initialize()
    {
        // Initialize Random Number Generator
        if (useRandomSeed)
        {
            seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }
        pseudoRandom = new System.Random(seed);

        // Initialize grid - Start with all walls (or empty, depending on approach)
        grid = new TileType[levelWidth, levelHeight];
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                grid[x, y] = TileType.Wall; // Start with walls, then carve out rooms/corridors
            }
        }

        bspLeaves = new List<RectInt>();
        rooms = new List<RectInt>();
    }

    // --- BSP Algorithm ---

    private void RunBSPSplit()
    {
        var rootNode = new RectInt(0, 0, levelWidth, levelHeight);
        var nodeQueue = new Queue<KeyValuePair<RectInt, int>>(); // Use KeyValuePair for Rect + Iteration Depth
        nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(rootNode, 0));

        while (nodeQueue.Count > 0)
        {
            var currentPair = nodeQueue.Dequeue();
            RectInt currentNode = currentPair.Key;
            int currentIteration = currentPair.Value;

            if (currentIteration >= maxIterations || ShouldStopSplitting(currentNode))
            {
                // Reached max depth or node is too small/imbalanced, becomes a leaf
                bspLeaves.Add(currentNode);
                continue;
            }

            RectInt nodeA, nodeB;
            if (TrySplitNode(currentNode, out nodeA, out nodeB))
            {
                nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(nodeA, currentIteration + 1));
                nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(nodeB, currentIteration + 1));
            }
            else
            {
                // Splitting failed (e.g., couldn't find valid split point), treat as leaf
                bspLeaves.Add(currentNode);
            }
        }
        Debug.Log($"BSP Split complete. Leaves: {bspLeaves.Count}");
    }

    private bool ShouldStopSplitting(RectInt node)
    {
        // Stop if node is too small OR if aspect ratio is too extreme (prevents long skinny rooms)
        float aspectRatio = (float)Mathf.Max(node.width, node.height) / Mathf.Min(node.width, node.height);
        return node.width < minRoomSize * 2 || node.height < minRoomSize * 2 || aspectRatio > 2.5f; // Adjust multiplier and ratio as needed
    }

    private bool TrySplitNode(RectInt node, out RectInt nodeA, out RectInt nodeB)
    {
        bool splitHorizontal;
        nodeA = new RectInt();
        nodeB = new RectInt();

        // Decide split direction: prefer splitting wider dimension
        if (node.width > node.height && node.width / (float)node.height >= 1.2f)
        {
            splitHorizontal = false; // Split vertically
        }
        else if (node.height > node.width && node.height / (float)node.width >= 1.2f)
        {
            splitHorizontal = true; // Split horizontally
        }
        else
        {
            // Roughly square, choose randomly
            splitHorizontal = pseudoRandom.Next(0, 2) == 0;
        }

        int minSplitSize = minRoomSize + (int)roomPadding; // Ensure space for padding and room

        if (splitHorizontal)
        {
            // Horizontal Split (along Y axis)
            if (node.height < minSplitSize * 2) return false; // Not enough height to split meaningfully

            // Find a random split point, ensuring both sides are large enough
            int splitY = pseudoRandom.Next(minSplitSize, node.height - minSplitSize);

            nodeA = new RectInt(node.x, node.y, node.width, splitY);
            nodeB = new RectInt(node.x, node.y + splitY, node.width, node.height - splitY);
        }
        else
        {
            // Vertical Split (along X axis)
            if (node.width < minSplitSize * 2) return false; // Not enough width to split meaningfully

            int splitX = pseudoRandom.Next(minSplitSize, node.width - minSplitSize);

            nodeA = new RectInt(node.x, node.y, splitX, node.height);
            nodeB = new RectInt(node.x + splitX, node.y, node.width - splitX, node.height);
        }
        return true;
    }


    // --- Room Creation ---

    private void CreateRoomsInLeaves()
    {
        bool isFirstRoom = true; // Track the first room for player placement

        foreach (RectInt leaf in bspLeaves)
        {
            // Ensure minimum size for the leaf itself before trying to place a room
            if (leaf.width < minRoomSize || leaf.height < minRoomSize) continue;

            // Calculate max possible size for the room within the leaf, considering padding
            int maxRoomWidth = leaf.width - (int)(2 * roomPadding);
            int maxRoomHeight = leaf.height - (int)(2 * roomPadding);

            if (maxRoomWidth < minRoomSize || maxRoomHeight < minRoomSize) continue; // Not enough space even with padding

            // Determine actual room size (randomly within bounds)
            int roomWidth = pseudoRandom.Next(minRoomSize, maxRoomWidth + 1);
            int roomHeight = pseudoRandom.Next(minRoomSize, maxRoomHeight + 1);

            // Determine room position (randomly within the available padded space)
            int roomX = leaf.x + (int)roomPadding + pseudoRandom.Next(0, leaf.width - (int)(2 * roomPadding) - roomWidth + 1);
            int roomY = leaf.y + (int)roomPadding + pseudoRandom.Next(0, leaf.height - (int)(2 * roomPadding) - roomHeight + 1);

            RectInt roomRect = new RectInt(roomX, roomY, roomWidth, roomHeight);
            rooms.Add(roomRect);
            CarveRectangle(roomRect, TileType.Floor); // Carve the room area

            // Place the player in the first room
            if (isFirstRoom && playerPrefab != null)
            {
                Vector3 playerPosition = new Vector3(roomRect.center.x, roomRect.center.y, 0);
                GameObject player = Instantiate(playerPrefab, playerPosition, Quaternion.identity);
                spawnedObjects.Add(player);
                Debug.Log($"Player added to spawnedObjects. Total: {spawnedObjects.Count}");
                isFirstRoom = false;
            }

            // Place enemies in the room
            if (enemyPrefab != null)
            {
                for (int i = 0; i < enemiesPerRoom; i++)
                {
                    Vector3 enemyPosition = new Vector3(
                        pseudoRandom.Next(roomRect.xMin, roomRect.xMax),
                        pseudoRandom.Next(roomRect.yMin, roomRect.yMax),
                        0
                    );
                    GameObject enemy = Instantiate(enemyPrefab, enemyPosition, Quaternion.identity);
                    spawnedObjects.Add(enemy); // Track the enemy object
                }
            }

            // Place decorations in the room
            if (decorationPrefab != null)
            {
                for (int i = 0; i < decorationsPerRoom; i++)
                {
                    Vector3 decorationPosition = new Vector3(
                        pseudoRandom.Next(roomRect.xMin, roomRect.xMax),
                        pseudoRandom.Next(roomRect.yMin, roomRect.yMax),
                        0
                    );
                    GameObject decoration = Instantiate(decorationPrefab, decorationPosition, Quaternion.identity);
                    spawnedObjects.Add(decoration); // Track the decoration object
                }
            }
        }
    }

    // --- Corridor Creation ---
    // This is a simplified approach connecting centers of sibling rooms from the BSP tree.
    // More complex pathfinding (A*) could be used for more organic connections.
    private void CreateCorridors()
    {
        // Need a way to reconstruct the BSP tree structure or relationships.
        // For simplicity here, we'll connect all rooms to their nearest neighbor.
        // A better approach involves storing the BSP tree and connecting siblings.

        // Simple Nearest Neighbor Connection (less structured than pure BSP)
        List<RectInt> connectedRooms = new List<RectInt>();
        if (rooms.Count == 0) return;

        RectInt currentRoom = rooms[pseudoRandom.Next(rooms.Count)];
        connectedRooms.Add(currentRoom);
        rooms.Remove(currentRoom); // Avoid connecting to self

        while (rooms.Count > 0)
        {
            RectInt closestRoom = FindClosestRoom(currentRoom, rooms);
            ConnectRooms(currentRoom, closestRoom);
            connectedRooms.Add(closestRoom);
            rooms.Remove(closestRoom);
            currentRoom = closestRoom; // Move to the newly connected room
        }
        // Add back all rooms to the main list for other potential uses
        rooms.AddRange(connectedRooms);

        // --- // BSP Sibling Connection (Conceptual - Requires Tree Structure) ---
        // If you implemented the BSP tree with node references:
        // Queue<BSPNode> queue = new Queue<BSPNode>();
        // queue.Enqueue(rootBSPNode);
        // while(queue.Count > 0) {
        //    BSPNode node = queue.Dequeue();
        //    if (node.IsLeaf) continue; // Only connect internal nodes
        //
        //    // Find rooms within the left and right children's leaves
        //    RectInt? roomA = FindRoomInNode(node.LeftChild);
        //    RectInt? roomB = FindRoomInNode(node.RightChild);
        //
        //    if(roomA.HasValue && roomB.HasValue) {
        //        ConnectRooms(roomA.Value, roomB.Value);
        //    }
        //
        //    // Add children to queue
        //    if(node.LeftChild != null) queue.Enqueue(node.LeftChild);
        //    if(node.RightChild != null) queue.Enqueue(node.RightChild);
        // }
        // --- // End Conceptual BSP Connection ---
    }

    private RectInt FindClosestRoom(RectInt sourceRoom, List<RectInt> targetRooms)
    {
        RectInt closest = targetRooms[0];
        float minDistance = Vector2.Distance(sourceRoom.center, closest.center);

        for (int i = 1; i < targetRooms.Count; i++)
        {
            float distance = Vector2.Distance(sourceRoom.center, targetRooms[i].center);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = targetRooms[i];
            }
        }
        return closest;
    }

    private void ConnectRooms(RectInt roomA, RectInt roomB)
    {
        // Simple L-shaped corridor: Horizontal then Vertical (or vice versa)
        Vector2Int centerA = Vector2Int.RoundToInt(roomA.center);
        Vector2Int centerB = Vector2Int.RoundToInt(roomB.center);

        // Randomly choose start: Horizontal or Vertical first
        if (pseudoRandom.Next(0, 2) == 0)
        {
            // Horizontal first
            CarveCorridorSegment(centerA.x, centerB.x, centerA.y, true);
            CarveCorridorSegment(centerA.y, centerB.y, centerB.x, false);
        }
        else
        {
            // Vertical first
            CarveCorridorSegment(centerA.y, centerB.y, centerA.x, false);
            CarveCorridorSegment(centerA.x, centerB.x, centerB.y, true);
        }
    }


    private void CarveCorridorSegment(int startCoord, int endCoord, int fixedCoord, bool isHorizontal)
    {
        int min = Mathf.Min(startCoord, endCoord);
        int max = Mathf.Max(startCoord, endCoord);
        int halfWidth = corridorWidth / 2; // Integer division, handles odd/even

        for (int i = min; i <= max; i++)
        {
            for (int w = -halfWidth; w <= corridorWidth - 1 - halfWidth; w++) // Loop accounts for width
            {
                int x, y;
                if (isHorizontal)
                {
                    x = i;
                    y = fixedCoord + w;
                }
                else
                {
                    x = fixedCoord + w;
                    y = i;
                }

                // Check bounds before carving
                if (x >= 0 && x < levelWidth && y >= 0 && y < levelHeight)
                {
                    grid[x, y] = TileType.Floor;
                }
            }
        }
    }

    // --- Utility Functions ---

    private void CarveRectangle(RectInt rect, TileType tile)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                // Check bounds (safety, though BSP should keep things within level bounds)
                if (x >= 0 && x < levelWidth && y >= 0 && y < levelHeight)
                {
                    grid[x, y] = tile;
                }
            }
        }
    }

    // --- Tilemap Application ---

    private void ApplyTilesToTilemap()
    {
        if (groundTilemap == null || wallTilemap == null || floorTile == null || wallTile == null)
        {
            Debug.LogError("Tilemaps or Tile assets not assigned!");
            return;
        }

        // Optimized approach: Prepare arrays for SetTilesBlock (Potentially faster for huge maps)
        // For simplicity and often sufficient speed, we use SetTile here.
        // If profiling shows this is slow, switch to SetTilesBlock.

        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                Vector3Int position = new Vector3Int(x, y, 0);
                switch (grid[x, y])
                {
                    case TileType.Floor:
                        groundTilemap.SetTile(position, floorTile);
                        wallTilemap.SetTile(position, null); // Ensure no wall where floor is
                        break;
                    case TileType.Wall:
                        // Optional: Check neighbors to only place walls adjacent to floor?
                        // This simple version places walls everywhere initially marked.
                        if (IsWallCandidate(x, y)) // Only place walls next to floors
                        {
                            wallTilemap.SetTile(position, wallTile);
                            groundTilemap.SetTile(position, null); // Ensure no floor where wall is
                        }
                        else
                        {
                            // Optional: Could place a different "background" tile here
                            wallTilemap.SetTile(position, null);
                            groundTilemap.SetTile(position, null);
                        }
                        break;
                    case TileType.Empty:
                    default:
                        groundTilemap.SetTile(position, null);
                        wallTilemap.SetTile(position, null);
                        break;
                }
            }
        }
        // Force Tilemaps to refresh if needed (usually automatic)
        // groundTilemap.RefreshAllTiles();
        // wallTilemap.RefreshAllTiles();
    }

    // Helper to only draw walls adjacent to floor tiles
    private bool IsWallCandidate(int x, int y)
    {
        if (grid[x, y] != TileType.Wall) return false; // Only consider original walls

        // Check 8 neighbours
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // Skip self

                int nx = x + dx;
                int ny = y + dy;

                // Check bounds
                if (nx >= 0 && nx < levelWidth && ny >= 0 && ny < levelHeight)
                {
                    if (grid[nx, ny] == TileType.Floor)
                    {
                        return true; // Found adjacent floor, this is a valid wall position
                    }
                }
            }
        }
        return false; // No adjacent floor found
    }

    // --- Potential Future Additions (Hybrid Elements) ---

    // private void AddCellularAutomataDetails(RectInt area) { ... }
    // private void PlaceDecorationsWithPerlinNoise() { ... }
    // private void SpawnEntities() { ... }

}

// --- Optional Helper Class for full BSP Tree (More Complex Corridor Logic) ---
/*public class BSPNode {
    public BSPNode LeftChild;
    public BSPNode RightChild;
    public RectInt? Room; // Room within this node (only if it's a leaf)

    public bool IsLeaf => LeftChild == null && RightChild == null;

    public BSPNode(RectInt partition) {
        Partition = partition;
        LeftChild = null;
        RightChild = null;
        Room = null;
    }
}*/