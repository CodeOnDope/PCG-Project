using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System; // Required for System.Random
using System.Linq;

// --- Enums ---
public enum TileType { Empty, Floor, Wall }

// Mode Selection (Reduced Options)
public enum GenerationMode { FullyProcedural, HybridProcedural }

// Node Type Enum (Kept for LShape/Template logic in Hybrid mode, but not used for JSON)
public enum NodeType { Rect, LShape, Template }


public class HybridLevelGenerator : MonoBehaviour
{
    [Header("--- Generation Mode ---")]
    [Tooltip("Select the generation method:\n" +
             "FullyProcedural: BSP splits, random Rect rooms, procedural corridors.\n" +
             "HybridProcedural: BSP splits, random Templates/L-Shapes/Rects, procedural corridors.")]
    public GenerationMode generationMode = GenerationMode.HybridProcedural;

    // Removed Level Design Input section

    [Header("Level Dimensions (Max Bounds)")]
    public int levelWidth = 100;
    public int levelHeight = 100;

    [Header("BSP Settings")] // Used by both modes
    public int minRoomSize = 8;
    public int maxIterations = 5;
    public float roomPadding = 2f;

    [Header("Room Shape Settings (Hybrid Mode)")]
    [Tooltip("The chance (0-1) a procedural room attempts L-shape (Hybrid Mode).")]
    [Range(0f, 1f)] public float lShapeProbability = 0.3f;
    [Range(0.3f, 0.8f)] public float minLLegRatio = 0.4f;
    [Range(0.5f, 1.0f)] public float maxLLegRatio = 0.7f;
    // Removed defaultSceneNodeSize as it was for SceneDefinedLayout

    [Header("Room Template Settings (Hybrid Mode)")]
    [Tooltip("Prefabs with Tilemaps for room layouts (Used in Hybrid mode).")]
    public List<GameObject> roomTemplatePrefabs;
    [Tooltip("The chance (0-1) a procedural room attempts Template (Hybrid Mode).")]
    [Range(0f, 1f)] public float roomTemplateProbability = 0.2f;

    [Header("Corridor Settings")]
    public int corridorWidth = 1;

    [Header("Tilemaps & Tiles")]
    public Tilemap groundTilemap;
    public Tilemap wallTilemap;
    public TileBase floorTile;
    public TileBase wallTile;

    [Header("Randomness")]
    public int seed = 0;
    public bool useRandomSeed = true;

    [Header("Entities & Decorations")]
    public GameObject playerPrefab;
    public GameObject enemyPrefab;
    public GameObject decorationPrefab;
    public int enemiesPerRoom = 2;
    public int decorationsPerRoom = 3;

    // --- Internal Data ---
    private TileType[,] grid;
    private List<RectInt> bspLeaves;
    private Dictionary<string, RectInt> placedRoomBounds = new Dictionary<string, RectInt>();
    private System.Random pseudoRandom;

    // Parent Transform References
    private Transform playerHolder;
    private Transform enemiesHolder;
    private Transform decorationsHolder;

    // --- Public Methods ---

    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        Debug.Log($"--- Starting Level Generation (Mode: {generationMode}) ---");
        ClearLevel();
        Initialize();

        if (grid == null || pseudoRandom == null || placedRoomBounds == null || playerHolder == null || enemiesHolder == null || decorationsHolder == null) { Debug.LogError("Initialization failed!", this); return; }
        if (floorTile == null || wallTile == null) { Debug.LogError("Floor Tile or Wall Tile is not assigned!", this); return; }

        bool generationSuccess = false;
        switch (generationMode)
        {
            case GenerationMode.FullyProcedural:
                generationSuccess = GenerateProceduralLevel();
                break;
            case GenerationMode.HybridProcedural:
                generationSuccess = GenerateHybridLevel();
                break;
                // Removed UserDefinedDesign and SceneDefinedLayout cases
        }

        if (generationSuccess)
        {
            ApplyTilesToTilemap();
            SpawnEntitiesAndDecorations();
            Debug.Log($"--- Level Generation Complete --- Seed: {seed}. Rooms placed: {placedRoomBounds?.Count ?? 0}.");
        }
        else
        {
            Debug.LogError($"--- Level Generation FAILED (Mode: {generationMode}) ---");
        }
    }

    [ContextMenu("Clear Level")]
    public void ClearLevel()
    {
        if (groundTilemap != null) groundTilemap.ClearAllTiles(); if (wallTilemap != null) wallTilemap.ClearAllTiles();
        DestroyChildrenOf("PlayerHolder"); DestroyChildrenOf("EnemiesHolder"); DestroyChildrenOf("DecorationsHolder");
        grid = null; bspLeaves = null; if (placedRoomBounds != null) placedRoomBounds.Clear(); else placedRoomBounds = new Dictionary<string, RectInt>();
    }
    private void Initialize()
    {
        if (useRandomSeed || seed == 0) { seed = Environment.TickCount; }
        pseudoRandom = new System.Random(seed); Debug.Log($"Using Seed: {seed}");
        grid = new TileType[levelWidth, levelHeight]; for (int x = 0; x < levelWidth; x++) { for (int y = 0; y < levelHeight; y++) { grid[x, y] = TileType.Wall; } }
        bspLeaves = new List<RectInt>(); placedRoomBounds = new Dictionary<string, RectInt>();
        playerHolder = CreateOrFindParent("PlayerHolder"); enemiesHolder = CreateOrFindParent("EnemiesHolder"); decorationsHolder = CreateOrFindParent("DecorationsHolder");
    }

    // --- Mode-Specific Generation Pipelines ---
    private bool GenerateProceduralLevel()
    {
        RunBSPSplit();
        CreateRoomsInLeaves_Procedural();
        if (placedRoomBounds.Count < 2) { Debug.LogWarning("Less than 2 rooms generated, skipping corridor generation."); }
        else { CreateCorridors_Procedural(); }
        return placedRoomBounds.Count > 0;
    }
    private bool GenerateHybridLevel()
    {
        RunBSPSplit();
        CreateRoomsInLeaves_Hybrid();
        if (placedRoomBounds.Count < 2) { Debug.LogWarning("Less than 2 rooms generated, skipping corridor generation."); }
        else { CreateCorridors_Procedural(); }
        return placedRoomBounds.Count > 0;
    }
    // Removed GenerateUserDefinedLevel()
    // Removed GenerateFromSceneLayout()


    // --- Room Creation Logic (Procedural - Rect Only) ---
    private void CreateRoomsInLeaves_Procedural()
    {
        if (bspLeaves == null) { Debug.LogError("BSP leaves not generated!"); return; }
        placedRoomBounds.Clear();
        for (int i = 0; i < bspLeaves.Count; i++)
        {
            RectInt leaf = bspLeaves[i];
            if (TryCreateRectangleInLeaf(leaf, out RectInt roomBounds))
            {
                CarveRectangle(roomBounds, TileType.Floor);
                string roomId = $"Room_{i}";
                placedRoomBounds[roomId] = roomBounds;
            }
        }
        Debug.Log($"Procedural room creation complete. Rooms generated: {placedRoomBounds.Count}");
    }

    // --- Room Creation Logic (Hybrid - Templates, L-Shapes, Rects) ---
    private void CreateRoomsInLeaves_Hybrid()
    {
        if (bspLeaves == null) { Debug.LogError("BSP leaves not generated!"); return; }
        placedRoomBounds.Clear();
        for (int i = 0; i < bspLeaves.Count; i++)
        {
            RectInt leaf = bspLeaves[i];
            RectInt roomBounds = RectInt.zero;
            bool roomCreated = false;
            string roomId = $"Room_{i}";
            // 1. Try Template First
            if (roomTemplatePrefabs != null && roomTemplatePrefabs.Count > 0 && pseudoRandom.NextDouble() < roomTemplateProbability)
            {
                // NOTE: TryPlaceRoomTemplate now needs to add to placedRoomBounds dictionary
                if (TryPlaceRoomTemplate(leaf, roomId, out roomBounds))
                {
                    roomCreated = true;
                    // Debug.Log($"SUCCESS: Placed Room Template in leaf {leaf}. Bounds: {roomBounds}");
                }
            }
            // 2. Try L-Shape
            if (!roomCreated && pseudoRandom.NextDouble() < lShapeProbability)
            {
                if (TryCreateLShapeInLeaf(leaf, out RectInt stemRect, out RectInt legRect, out roomBounds))
                {
                    CarveRectangle(stemRect, TileType.Floor); CarveRectangle(legRect, TileType.Floor);
                    placedRoomBounds[roomId] = roomBounds; // Add L-shape bounds
                    roomCreated = true;
                    // Debug.Log($"SUCCESS: Created L-Shape: Stem={stemRect}, Leg={legRect}, Bounds={roomBounds}");
                }
            }
            // 3. Try Rectangle
            if (!roomCreated)
            {
                if (TryCreateRectangleInLeaf(leaf, out roomBounds))
                {
                    CarveRectangle(roomBounds, TileType.Floor);
                    placedRoomBounds[roomId] = roomBounds; // Add Rect bounds
                    roomCreated = true;
                }
            }
        }
        Debug.Log($"Hybrid room creation complete. Rooms generated: {placedRoomBounds.Count}");
    }

    // Removed PlaceRoomsFromData()

    // --- Corridor Creation (Procedural - MST) ---
    private void CreateCorridors_Procedural()
    {
        if (placedRoomBounds == null || placedRoomBounds.Count < 2) return;
        List<RectInt> roomBoundsList = new List<RectInt>(placedRoomBounds.Values);
        List<RectInt> connectedSet = new List<RectInt>(); List<RectInt> unconnectedSet = new List<RectInt>(roomBoundsList);
        RectInt startRoom = unconnectedSet[pseudoRandom.Next(unconnectedSet.Count)]; connectedSet.Add(startRoom); unconnectedSet.Remove(startRoom);
        while (unconnectedSet.Count > 0)
        {
            RectInt closestUnconnected = default; RectInt closestConnected = default; float minDistance = float.MaxValue;
            foreach (RectInt connectedRoom in connectedSet) { foreach (RectInt unconnectedRoom in unconnectedSet) { float dist = Vector2.Distance(connectedRoom.center, unconnectedRoom.center); if (dist < minDistance) { minDistance = dist; closestUnconnected = unconnectedRoom; closestConnected = connectedRoom; } } }
            if (closestUnconnected != default && closestConnected != default) { ConnectRects(closestConnected, closestUnconnected); connectedSet.Add(closestUnconnected); unconnectedSet.Remove(closestUnconnected); }
            else { Debug.LogError("Failed to find closest room pair for corridor connection. Breaking loop."); break; }
        }
    }

    // Removed CreateCorridorsFromData()

    // --- Spawning Logic ---
    private void SpawnEntitiesAndDecorations()
    {
        if (placedRoomBounds == null || placedRoomBounds.Count == 0) return;
        bool playerSpawned = false;
        foreach (var kvp in placedRoomBounds)
        {
            RectInt currentRoomBounds = kvp.Value; string roomId = kvp.Key; List<Vector2Int> floorSpots = GetFloorTilesInRect(currentRoomBounds); if (floorSpots.Count == 0) continue;
            if (!playerSpawned && playerPrefab != null) { Vector2Int spawnTile = floorSpots[pseudoRandom.Next(floorSpots.Count)]; Vector3 spawnPos = GetWorldPosition(spawnTile); Instantiate(playerPrefab, spawnPos, Quaternion.identity, playerHolder); playerSpawned = true; floorSpots.Remove(spawnTile); Debug.Log($"Player spawned in room {roomId} at {spawnTile}"); }
            SpawnPrefabs(enemyPrefab, enemiesPerRoom, floorSpots, enemiesHolder); SpawnPrefabs(decorationPrefab, decorationsPerRoom, floorSpots, decorationsHolder);
        }
        if (!playerSpawned && playerPrefab != null) { Debug.LogWarning("Player prefab assigned but failed to spawn!"); }
    }
    private void SpawnPrefabs(GameObject prefab, int count, List<Vector2Int> availableSpots, Transform parentHolder)
    {
        if (prefab == null || availableSpots == null || parentHolder == null || availableSpots.Count == 0) return; int numToSpawn = Mathf.Min(count, availableSpots.Count);
        for (int i = 0; i < numToSpawn; i++) { if (availableSpots.Count == 0) break; int spotIndex = pseudoRandom.Next(availableSpots.Count); Vector2Int spawnTilePos = availableSpots[spotIndex]; availableSpots.RemoveAt(spotIndex); Vector3 worldPos = GetWorldPosition(spawnTilePos); Instantiate(prefab, worldPos, Quaternion.identity, parentHolder); }
    }

    // --- Room Generation Helpers ---
    private bool TryCreateRectangleInLeaf(RectInt leaf, out RectInt roomRect)
    { /* ... unchanged ... */
        roomRect = RectInt.zero; int padding = (int)roomPadding; int maxRoomWidth = leaf.width - (2 * padding); int maxRoomHeight = leaf.height - (2 * padding); if (maxRoomWidth < minRoomSize || maxRoomHeight < minRoomSize) return false; int roomWidth = pseudoRandom.Next(minRoomSize, maxRoomWidth + 1); int roomHeight = pseudoRandom.Next(minRoomSize, maxRoomHeight + 1); int roomX = leaf.x + padding + pseudoRandom.Next(0, maxRoomWidth - roomWidth + 1); int roomY = leaf.y + padding + pseudoRandom.Next(0, maxRoomHeight - roomHeight + 1); roomRect = new RectInt(roomX, roomY, roomWidth, roomHeight); return true;
    }
    private bool TryCreateLShapeInLeaf(RectInt leaf, out RectInt stemRect, out RectInt legRect, out RectInt overallBounds)
    { /* ... unchanged ... */
        stemRect = legRect = overallBounds = RectInt.zero; int padding = (int)roomPadding; int availableWidth = leaf.width - (2 * padding); int availableHeight = leaf.height - (2 * padding); int minLegSize = 1; if (availableWidth < minRoomSize || availableHeight < minRoomSize) { return false; }
        int stemW = pseudoRandom.Next(minRoomSize, availableWidth + 1); int stemH = pseudoRandom.Next(minRoomSize, availableHeight + 1); int maxStemX = availableWidth - stemW; int maxStemY = availableHeight - stemH; if (maxStemX < 0 || maxStemY < 0) { return false; }
        int stemRelX = pseudoRandom.Next(0, maxStemX + 1); int stemRelY = pseudoRandom.Next(0, maxStemY + 1); stemRect = new RectInt(leaf.x + padding + stemRelX, leaf.y + padding + stemRelY, stemW, stemH); RectInt paddedLeafBounds = new RectInt(leaf.x + padding, leaf.y + padding, availableWidth, availableHeight); int spaceAbove = paddedLeafBounds.yMax - stemRect.yMax; int spaceBelow = stemRect.yMin - paddedLeafBounds.yMin; int spaceRight = paddedLeafBounds.xMax - stemRect.xMax; int spaceLeft = stemRect.xMin - paddedLeafBounds.xMin; bool canAttachAbove = spaceAbove >= minLegSize; bool canAttachBelow = spaceBelow >= minLegSize; bool canAttachRight = spaceRight >= minLegSize; bool canAttachLeft = spaceLeft >= minLegSize; List<int> possibleVerticalAttach = new List<int>(); if (canAttachBelow) possibleVerticalAttach.Add(0); if (canAttachAbove) possibleVerticalAttach.Add(1); List<int> possibleHorizontalAttach = new List<int>(); if (canAttachLeft) possibleHorizontalAttach.Add(0); if (canAttachRight) possibleHorizontalAttach.Add(1); if (possibleVerticalAttach.Count == 0 && possibleHorizontalAttach.Count == 0) { return false; }
        bool tryVerticalAttach; if (stemW >= stemH) { tryVerticalAttach = possibleVerticalAttach.Count > 0; } else { tryVerticalAttach = !(possibleHorizontalAttach.Count > 0); }
        if (tryVerticalAttach && possibleVerticalAttach.Count == 0) tryVerticalAttach = false; if (!tryVerticalAttach && possibleHorizontalAttach.Count == 0) tryVerticalAttach = true; int legW, legH, legX, legY; if (tryVerticalAttach) { bool attachAbove = possibleVerticalAttach[pseudoRandom.Next(possibleVerticalAttach.Count)] == 1; int availableSpace = attachAbove ? spaceAbove : spaceBelow; if (availableSpace < minLegSize) { return false; } legH = pseudoRandom.Next(minLegSize, availableSpace + 1); legW = (int)(stemW * pseudoRandom.Next((int)(minLLegRatio * 100), (int)(maxLLegRatio * 100) + 1) / 100f); legW = Mathf.Clamp(legW, minLegSize, stemW); int maxLegXOffset = Mathf.Max(0, stemW - legW); legX = stemRect.x + pseudoRandom.Next(0, maxLegXOffset + 1); legY = attachAbove ? stemRect.yMax : stemRect.yMin - legH; } else { bool attachRight = possibleHorizontalAttach[pseudoRandom.Next(possibleHorizontalAttach.Count)] == 1; int availableSpace = attachRight ? spaceRight : spaceLeft; if (availableSpace < minLegSize) { return false; } legW = pseudoRandom.Next(minLegSize, availableSpace + 1); legH = (int)(stemH * pseudoRandom.Next((int)(minLLegRatio * 100), (int)(maxLLegRatio * 100) + 1) / 100f); legH = Mathf.Clamp(legH, minLegSize, stemH); int maxLegYOffset = Mathf.Max(0, stemH - legH); legY = stemRect.y + pseudoRandom.Next(0, maxLegYOffset + 1); legX = attachRight ? stemRect.xMax : stemRect.xMin - legW; }
        if (legW < minLegSize || legH < minLegSize) { return false; }
        legRect = new RectInt(legX, legY, legW, legH); int minX = Mathf.Min(stemRect.xMin, legRect.xMin); int minY = Mathf.Min(stemRect.yMin, legRect.yMin); int maxX = Mathf.Max(stemRect.xMax, legRect.xMax); int maxY = Mathf.Max(stemRect.yMax, legRect.yMax); overallBounds = new RectInt(minX, minY, maxX - minX, maxY - minY); if (overallBounds.xMin < leaf.xMin || overallBounds.yMin < leaf.yMin || overallBounds.xMax > leaf.xMax || overallBounds.yMax > leaf.yMax) { return false; }
        return true;
    }
    // Modified TryPlaceRoomTemplate to take roomId and add to dictionary
    private bool TryPlaceRoomTemplate(RectInt leaf, string roomId, out RectInt placedBounds)
    {
        placedBounds = RectInt.zero;
        if (roomTemplatePrefabs == null || roomTemplatePrefabs.Count == 0) return false;
        GameObject selectedTemplatePrefab = roomTemplatePrefabs[pseudoRandom.Next(roomTemplatePrefabs.Count)];
        if (selectedTemplatePrefab == null) { Debug.LogWarning("Null entry found in roomTemplatePrefabs list."); return false; }
        GameObject tempInstance = null;
        try
        {
            tempInstance = Instantiate(selectedTemplatePrefab, new Vector3(9999, 9999, 0), Quaternion.identity); tempInstance.SetActive(false);
            Tilemap templateTilemap = tempInstance.GetComponentInChildren<Tilemap>();
            if (templateTilemap == null) { Debug.LogError($"Template prefab '{selectedTemplatePrefab.name}' missing Tilemap!", selectedTemplatePrefab); return false; }
            templateTilemap.CompressBounds(); BoundsInt templateCellBounds = templateTilemap.cellBounds;
            int templateWidth = templateCellBounds.size.x; int templateHeight = templateCellBounds.size.y;
            int padding = (int)roomPadding; int availableWidth = leaf.width - (2 * padding); int availableHeight = leaf.height - (2 * padding);
            if (templateWidth > availableWidth || templateHeight > availableHeight) { return false; }
            int placeOffsetX = pseudoRandom.Next(0, availableWidth - templateWidth + 1); int placeOffsetY = pseudoRandom.Next(0, availableHeight - templateHeight + 1);
            int gridStartX = leaf.x + padding + placeOffsetX; int gridStartY = leaf.y + padding + placeOffsetY;
            bool copiedAnyFloor = false;
            foreach (Vector3Int localPos in templateCellBounds.allPositionsWithin)
            {
                TileBase tile = templateTilemap.GetTile(localPos);
                if (tile != null)
                {
                    int gridX = gridStartX + localPos.x - templateCellBounds.xMin; int gridY = gridStartY + localPos.y - templateCellBounds.yMin;
                    if (gridX >= 0 && gridX < levelWidth && gridY >= 0 && gridY < levelHeight)
                    {
                        TileType typeToPlace = TileType.Empty;
                        if (tile == floorTile) { typeToPlace = TileType.Floor; copiedAnyFloor = true; } else if (tile == wallTile) { typeToPlace = TileType.Wall; }
                        if (typeToPlace != TileType.Empty) { grid[gridX, gridY] = typeToPlace; }
                    }
                }
            }
            if (!copiedAnyFloor) { Debug.LogWarning($"Placed template '{selectedTemplatePrefab.name}' contained no Floor tiles matching reference."); }
            placedBounds = new RectInt(gridStartX, gridStartY, templateWidth, templateHeight);
            // *** ADDED: Add to dictionary ***
            placedRoomBounds[roomId] = placedBounds;
            return true;
        }
        finally { if (tempInstance != null) { if (Application.isPlaying) { Destroy(tempInstance); } else { DestroyImmediate(tempInstance); } } }
    }
    // Removed PlaceSpecificRoomTemplate as it's only needed for data-driven modes
    // Removed FindTemplatePrefab as it's only needed for data-driven modes
    // Removed GetTemplateDimensions as it's only needed for scene-layout mode

    // --- Corridor Connection ---
    private void ConnectRects(RectInt roomA, RectInt roomB)
    { /* ... unchanged ... */
        Vector2Int centerA = Vector2Int.RoundToInt(roomA.center); Vector2Int centerB = Vector2Int.RoundToInt(roomB.center); if (pseudoRandom.Next(0, 2) == 0) { CarveCorridorSegment(centerA.x, centerB.x, centerA.y, true); CarveCorridorSegment(centerA.y, centerB.y, centerB.x, false); } else { CarveCorridorSegment(centerA.y, centerB.y, centerA.x, false); CarveCorridorSegment(centerA.x, centerB.x, centerB.y, true); }
    }
    private void CarveCorridorSegment(int startCoord, int endCoord, int fixedCoord, bool isHorizontal)
    { /* ... unchanged ... */
        if (grid == null) return; int min = Mathf.Min(startCoord, endCoord); int max = Mathf.Max(startCoord, endCoord); int halfWidth = corridorWidth <= 1 ? 0 : (corridorWidth - 1) / 2; for (int i = min; i <= max; i++) { for (int w = -halfWidth; w <= halfWidth; w++) { int x, y; if (isHorizontal) { x = i; y = fixedCoord + w; } else { x = fixedCoord + w; y = i; } if (x >= 0 && x < levelWidth && y >= 0 && y < levelHeight) { grid[x, y] = TileType.Floor; } } }
    }

    // --- Utility Functions ---
    private void CarveRectangle(RectInt rect, TileType tile)
    { /* ... unchanged ... */
        if (grid == null) return; for (int x = rect.xMin; x < rect.xMax; x++) { for (int y = rect.yMin; y < rect.yMax; y++) { if (x >= 0 && x < levelWidth && y >= 0 && y < levelHeight) { grid[x, y] = tile; } } }
    }
    private List<Vector2Int> GetFloorTilesInRect(RectInt rect)
    { /* ... unchanged ... */
        List<Vector2Int> floorTiles = new List<Vector2Int>(); if (grid == null) return floorTiles; for (int x = rect.xMin; x < rect.xMax; x++) { for (int y = rect.yMin; y < rect.yMax; y++) { if (x >= 0 && x < levelWidth && y >= 0 && y < levelHeight && grid[x, y] == TileType.Floor) { floorTiles.Add(new Vector2Int(x, y)); } } }
        return floorTiles;
    }
    private Vector3 GetWorldPosition(Vector2Int tilePos)
    { /* ... unchanged ... */
        if (groundTilemap == null) return new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0); return groundTilemap.CellToWorld((Vector3Int)tilePos) + (groundTilemap.cellSize * 0.5f);
    }

    // --- Tilemap Application ---
    private void ApplyTilesToTilemap()
    { /* ... unchanged ... */
        if (groundTilemap == null || wallTilemap == null || floorTile == null || wallTile == null || grid == null) { Debug.LogError("Cannot apply tiles: Tilemaps, Tiles, or Grid not ready!"); return; }
        groundTilemap.ClearAllTiles(); wallTilemap.ClearAllTiles(); for (int x = 0; x < levelWidth; x++) { for (int y = 0; y < levelHeight; y++) { Vector3Int position = new Vector3Int(x, y, 0); switch (grid[x, y]) { case TileType.Floor: groundTilemap.SetTile(position, floorTile); wallTilemap.SetTile(position, null); break; case TileType.Wall: if (IsWallCandidate(x, y)) { wallTilemap.SetTile(position, wallTile); groundTilemap.SetTile(position, null); } else { wallTilemap.SetTile(position, null); groundTilemap.SetTile(position, null); } break; default: groundTilemap.SetTile(position, null); wallTilemap.SetTile(position, null); break; } } }
    }
    private bool IsWallCandidate(int x, int y)
    { /* ... unchanged ... */
        if (grid == null || x < 0 || x >= levelWidth || y < 0 || y >= levelHeight || grid[x, y] != TileType.Wall) return false; for (int dx = -1; dx <= 1; dx++) { for (int dy = -1; dy <= 1; dy++) { if (dx == 0 && dy == 0) continue; int nx = x + dx; int ny = y + dy; if (nx >= 0 && nx < levelWidth && ny >= 0 && ny < levelHeight && grid[nx, ny] == TileType.Floor) { return true; } } }
        return false;
    }

    // --- Helper Functions ---
    private Transform CreateOrFindParent(string parentName)
    { /* ... unchanged ... */
        Transform existingParent = transform.Find(parentName); if (existingParent != null) { return existingParent; } else { GameObject parentObject = new GameObject(parentName); parentObject.transform.SetParent(this.transform); parentObject.transform.localPosition = Vector3.zero; return parentObject.transform; }
    }
    private void DestroyChildrenOf(string parentName)
    { /* ... unchanged ... */
        Transform parent = transform.Find(parentName); if (parent != null) { int childCount = parent.childCount; for (int i = childCount - 1; i >= 0; i--) { GameObject child = parent.GetChild(i).gameObject; if (child != null) { if (Application.isPlaying) { Destroy(child); } else { DestroyImmediate(child); } } } }
    }
    // Removed ParseLevelDesign()
    private void RunBSPSplit()
    { /* ... unchanged ... */
        if (bspLeaves == null) bspLeaves = new List<RectInt>(); else bspLeaves.Clear(); if (pseudoRandom == null) Initialize(); var rootNode = new RectInt(1, 1, levelWidth - 2, levelHeight - 2); var nodeQueue = new Queue<KeyValuePair<RectInt, int>>(); nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(rootNode, 0)); while (nodeQueue.Count > 0) { var currentPair = nodeQueue.Dequeue(); RectInt currentNode = currentPair.Key; int currentIteration = currentPair.Value; if (currentIteration >= maxIterations || ShouldStopSplitting(currentNode)) { bspLeaves.Add(currentNode); continue; } if (TrySplitNode(currentNode, out RectInt nodeA, out RectInt nodeB)) { nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(nodeA, currentIteration + 1)); nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(nodeB, currentIteration + 1)); } else { bspLeaves.Add(currentNode); } }
    }
    private bool ShouldStopSplitting(RectInt node)
    { /* ... unchanged ... */
        if (node.width < minRoomSize * 2 && node.height < minRoomSize * 2) return true; float aspectRatio = node.width <= 0 || node.height <= 0 ? 1f : (float)Mathf.Max(node.width, node.height) / Mathf.Max(1, Mathf.Min(node.width, node.height)); if (aspectRatio > 4.0f) return true; return false;
    }
    private bool TrySplitNode(RectInt node, out RectInt nodeA, out RectInt nodeB)
    { /* ... unchanged ... */
        bool splitHorizontal; nodeA = nodeB = RectInt.zero; bool preferHorizontal = (node.height > node.width && (float)node.height / Mathf.Max(1, node.width) >= 1.2f); bool preferVertical = (node.width > node.height && (float)node.width / Mathf.Max(1, node.height) >= 1.2f); if (preferHorizontal) splitHorizontal = true; else if (preferVertical) splitHorizontal = false; else splitHorizontal = pseudoRandom.Next(0, 2) == 0; int minSizeForSplit = minRoomSize + (int)(roomPadding) + 1; minSizeForSplit = Mathf.Max(minRoomSize + 1, minSizeForSplit); if (splitHorizontal) { if (node.height < minSizeForSplit * 2) return false; int splitY = pseudoRandom.Next(node.y + minSizeForSplit, node.yMax - minSizeForSplit + 1); nodeA = new RectInt(node.x, node.y, node.width, splitY - node.y); nodeB = new RectInt(node.x, splitY, node.width, node.yMax - splitY); } else { if (node.width < minSizeForSplit * 2) return false; int splitX = pseudoRandom.Next(node.x + minSizeForSplit, node.xMax - minSizeForSplit + 1); nodeA = new RectInt(node.x, node.y, splitX - node.x, node.height); nodeB = new RectInt(splitX, node.y, node.xMax - splitX, node.height); }
        return true;
    }

}