using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System; // Required for System.Random
using System.Linq;

// --- Enums ---
public enum TileType { Empty, Floor, Wall }

// Mode Selection
public enum GenerationMode { FullyProcedural, HybridProcedural, UserDefinedDesign }

// Node Type for User Defined Design JSON
public enum NodeType { Rect, LShape, Template }

// --- Data Structures for JSON Parsing (UserDefined Mode) ---
[System.Serializable]
public class NodeData
{
    public string id;
    public NodeType type;
    public int x; // Represents desired grid bottom-left X position
    public int y; // Represents desired grid bottom-left Y position
    public string templateName; // Optional: Name of template prefab to use if type is Template
    // Optional: Add width/height fields if you want user to specify size for Rect/LShape
    // public int width;
    // public int height;
}
[System.Serializable]
public class ConnectionData
{
    public string from; // Node ID
    public string to; // Node ID
}
[System.Serializable]
public class LevelDesignData
{
    public List<NodeData> nodes;
    public List<ConnectionData> connections;
}


public class HybridLevelGenerator : MonoBehaviour
{
    [Header("--- Generation Mode ---")]
    [Tooltip("Select the generation method:\n" +
             "FullyProcedural: BSP splits, random Rect rooms, procedural corridors.\n" +
             "HybridProcedural: BSP splits, random Templates/L-Shapes/Rects, procedural corridors.\n" +
             "UserDefinedDesign: Reads layout from Level Design JSON file.")]
    public GenerationMode generationMode = GenerationMode.HybridProcedural;

    [Header("Level Design Input (UserDefined Mode)")]
    [Tooltip("Assign a TextAsset (JSON file) containing the level node layout. Used only in UserDefinedDesign mode.")]
    public TextAsset levelDesignJson;

    [Header("Level Dimensions (Max Bounds)")]
    public int levelWidth = 100;
    public int levelHeight = 100;

    [Header("BSP Settings (Procedural & Hybrid Modes)")]
    public int minRoomSize = 8; // Min size for procedural rooms
    public int maxIterations = 5; // BSP depth
    public float roomPadding = 2f; // Padding within BSP leaves

    [Header("Room Shape Settings (Hybrid Mode)")]
    [Tooltip("The chance (0-1) a procedural room attempts L-shape (Hybrid Mode).")]
    [Range(0f, 1f)] public float lShapeProbability = 0.3f;
    [Range(0.3f, 0.8f)] public float minLLegRatio = 0.4f;
    [Range(0.5f, 1.0f)] public float maxLLegRatio = 0.7f;

    [Header("Room Template Settings (Hybrid & UserDefined Modes)")]
    [Tooltip("Prefabs with Tilemaps for room layouts. Used in Hybrid (randomly) and UserDefined (by name/index).")]
    public List<GameObject> roomTemplatePrefabs;
    [Tooltip("The chance (0-1) a procedural room attempts Template (Hybrid Mode). Checked before L-shape.")]
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
    // Add other prefabs if needed (e.g., Boss, Treasure)

    // --- Internal Data ---
    private TileType[,] grid;
    private List<RectInt> bspLeaves; // Used by Procedural/Hybrid
    // Store placed room bounds mapped to a generated ID for corridor connection & spawning
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
        ClearLevel(); // Calls DestroyChildrenOf
        Initialize(); // Calls CreateOrFindParent

        // Basic validation
        if (grid == null || pseudoRandom == null || placedRoomBounds == null || playerHolder == null || enemiesHolder == null || decorationsHolder == null)
        {
            Debug.LogError("Initialization failed!", this); return;
        }
        if (floorTile == null || wallTile == null)
        {
            Debug.LogError("Floor Tile or Wall Tile is not assigned!", this); return;
        }

        // --- Execute selected generation mode ---
        bool generationSuccess = false;
        switch (generationMode)
        {
            case GenerationMode.FullyProcedural:
                generationSuccess = GenerateProceduralLevel(); // Calls RunBSPSplit, CreateRoomsInLeaves_Procedural, CreateCorridors_Procedural
                break;
            case GenerationMode.HybridProcedural:
                generationSuccess = GenerateHybridLevel(); // Calls RunBSPSplit, CreateRoomsInLeaves_Hybrid, CreateCorridors_Procedural
                break;
            case GenerationMode.UserDefinedDesign:
                generationSuccess = GenerateUserDefinedLevel(); // Calls ParseLevelDesign, PlaceRoomsFromData, CreateCorridorsFromData
                break;
        }

        if (generationSuccess)
        {
            ApplyTilesToTilemap();
            SpawnEntitiesAndDecorations(); // Spawns based on placed rooms
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
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        // Ensure helper methods exist before calling them
        DestroyChildrenOf("PlayerHolder");
        DestroyChildrenOf("EnemiesHolder");
        DestroyChildrenOf("DecorationsHolder");
        grid = null;
        bspLeaves = null;
        if (placedRoomBounds != null) placedRoomBounds.Clear(); else placedRoomBounds = new Dictionary<string, RectInt>();
    }

    // --- Initialization ---
    private void Initialize()
    {
        if (useRandomSeed || seed == 0) { seed = Environment.TickCount; }
        pseudoRandom = new System.Random(seed);
        Debug.Log($"Using Seed: {seed}");

        grid = new TileType[levelWidth, levelHeight];
        for (int x = 0; x < levelWidth; x++) { for (int y = 0; y < levelHeight; y++) { grid[x, y] = TileType.Wall; } }

        bspLeaves = new List<RectInt>(); // Initialize for modes that use it
        placedRoomBounds = new Dictionary<string, RectInt>();

        // Ensure helper methods exist before calling them
        playerHolder = CreateOrFindParent("PlayerHolder");
        enemiesHolder = CreateOrFindParent("EnemiesHolder");
        decorationsHolder = CreateOrFindParent("DecorationsHolder");
    }

    // --- Mode-Specific Generation Pipelines ---

    private bool GenerateProceduralLevel()
    {
        RunBSPSplit(); // Needs BSP functions
        CreateRoomsInLeaves_Procedural();
        if (placedRoomBounds.Count < 2) { Debug.LogWarning("Less than 2 rooms generated, skipping corridor generation."); }
        else { CreateCorridors_Procedural(); } // Needs procedural corridor logic
        return placedRoomBounds.Count > 0;
    }

    private bool GenerateHybridLevel()
    {
        RunBSPSplit(); // Needs BSP functions
        CreateRoomsInLeaves_Hybrid();
        if (placedRoomBounds.Count < 2) { Debug.LogWarning("Less than 2 rooms generated, skipping corridor generation."); }
        else { CreateCorridors_Procedural(); } // Needs procedural corridor logic
        return placedRoomBounds.Count > 0;
    }

    private bool GenerateUserDefinedLevel()
    {
        if (levelDesignJson == null) { Debug.LogError("Level Design JSON (TextAsset) is not assigned!", this); return false; }
        LevelDesignData designData = ParseLevelDesign(); // Needs ParseLevelDesign
        if (designData == null) { Debug.LogError("Failed to parse Level Design JSON data."); return false; }

        PlaceRoomsFromData(designData);
        if (placedRoomBounds.Count < 2) { Debug.LogWarning("Less than 2 rooms placed from design, skipping corridor generation."); }
        else { CreateCorridorsFromData(designData); }
        return placedRoomBounds.Count > 0;
    }


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
            if (roomTemplatePrefabs != null && roomTemplatePrefabs.Count > 0 && pseudoRandom.NextDouble() < roomTemplateProbability)
            {
                if (TryPlaceRoomTemplate(leaf, out roomBounds)) { roomCreated = true; placedRoomBounds[roomId] = roomBounds; }
            }
            if (!roomCreated && pseudoRandom.NextDouble() < lShapeProbability)
            {
                if (TryCreateLShapeInLeaf(leaf, out RectInt stemRect, out RectInt legRect, out roomBounds)) { CarveRectangle(stemRect, TileType.Floor); CarveRectangle(legRect, TileType.Floor); placedRoomBounds[roomId] = roomBounds; roomCreated = true; }
            }
            if (!roomCreated)
            {
                if (TryCreateRectangleInLeaf(leaf, out roomBounds)) { CarveRectangle(roomBounds, TileType.Floor); placedRoomBounds[roomId] = roomBounds; roomCreated = true; }
            }
        }
        Debug.Log($"Hybrid room creation complete. Rooms generated: {placedRoomBounds.Count}");
    }

    // --- Room Placement Logic (User Defined) ---
    private void PlaceRoomsFromData(LevelDesignData designData)
    {
        if (designData == null || designData.nodes == null) return;
        placedRoomBounds.Clear();
        foreach (NodeData node in designData.nodes)
        {
            RectInt roomBounds = RectInt.zero; bool created = false; Vector2Int targetPos = new Vector2Int(node.x, node.y);
            switch (node.type)
            {
                case NodeType.Rect:
                    int width = pseudoRandom.Next(minRoomSize, minRoomSize * 2); int height = pseudoRandom.Next(minRoomSize, minRoomSize * 2); roomBounds = new RectInt(targetPos.x, targetPos.y, width, height); CarveRectangle(roomBounds, TileType.Floor); created = true; break;
                case NodeType.LShape:
                    Debug.LogWarning($"L-Shape placement from data not implemented. Placing rectangle for node {node.id}."); int l_width = pseudoRandom.Next(minRoomSize, minRoomSize * 2); int l_height = pseudoRandom.Next(minRoomSize, minRoomSize * 2); roomBounds = new RectInt(targetPos.x, targetPos.y, l_width, l_height); CarveRectangle(roomBounds, TileType.Floor); created = true; break;
                case NodeType.Template:
                    GameObject templatePrefab = FindTemplatePrefab(node.templateName, node.id);
                    if (templatePrefab != null) { if (PlaceSpecificRoomTemplate(templatePrefab, targetPos, out roomBounds)) { created = true; } else { Debug.LogWarning($"Failed to place template '{templatePrefab.name}' for node {node.id} at {targetPos}."); } }
                    else { Debug.LogWarning($"Template prefab '{node.templateName ?? "any"}' not found for node {node.id}."); }
                    break;
            }
            if (created) { foreach (var kvp in placedRoomBounds) { if (kvp.Key != node.id && kvp.Value.Overlaps(roomBounds)) { Debug.LogWarning($"Overlap detected between room {node.id} ({roomBounds}) and room {kvp.Key} ({kvp.Value})"); } } placedRoomBounds[node.id] = roomBounds; }
            else { Debug.LogWarning($"Failed to create/place room for node {node.id} type {node.type}"); }
        }
    }

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

    // --- Corridor Creation (User Defined) ---
    private void CreateCorridorsFromData(LevelDesignData designData)
    {
        if (designData == null || designData.connections == null) return;
        foreach (ConnectionData connection in designData.connections)
        {
            if (placedRoomBounds.TryGetValue(connection.from, out RectInt fromBounds) && placedRoomBounds.TryGetValue(connection.to, out RectInt toBounds)) { ConnectRects(fromBounds, toBounds); }
            else { Debug.LogWarning($"Could not create corridor: Failed to find placed bounds for nodes {connection.from} or {connection.to}"); }
        }
    }

    // --- Spawning Logic ---
    private void SpawnEntitiesAndDecorations()
    { /* ... unchanged ... */
        if (placedRoomBounds == null || placedRoomBounds.Count == 0) return; bool playerSpawned = false;
        foreach (var kvp in placedRoomBounds)
        {
            RectInt currentRoomBounds = kvp.Value; string roomId = kvp.Key; List<Vector2Int> floorSpots = GetFloorTilesInRect(currentRoomBounds); if (floorSpots.Count == 0) continue;
            if (!playerSpawned && playerPrefab != null) { Vector2Int spawnTile = floorSpots[pseudoRandom.Next(floorSpots.Count)]; Vector3 spawnPos = GetWorldPosition(spawnTile); Instantiate(playerPrefab, spawnPos, Quaternion.identity, playerHolder); playerSpawned = true; floorSpots.Remove(spawnTile); Debug.Log($"Player spawned in room {roomId} at {spawnTile}"); }
            SpawnPrefabs(enemyPrefab, enemiesPerRoom, floorSpots, enemiesHolder); SpawnPrefabs(decorationPrefab, decorationsPerRoom, floorSpots, decorationsHolder);
        }
        if (!playerSpawned && playerPrefab != null) { Debug.LogWarning("Player prefab assigned but failed to spawn!"); }
    }
    private void SpawnPrefabs(GameObject prefab, int count, List<Vector2Int> availableSpots, Transform parentHolder)
    { /* ... unchanged ... */
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
    private bool TryPlaceRoomTemplate(RectInt leaf, out RectInt placedBounds)
    { /* ... unchanged ... */
        placedBounds = RectInt.zero; if (roomTemplatePrefabs == null || roomTemplatePrefabs.Count == 0) return false; GameObject selectedTemplatePrefab = roomTemplatePrefabs[pseudoRandom.Next(roomTemplatePrefabs.Count)]; if (selectedTemplatePrefab == null) { Debug.LogWarning("Null entry found in roomTemplatePrefabs list."); return false; }
        GameObject tempInstance = null; try { tempInstance = Instantiate(selectedTemplatePrefab, new Vector3(9999, 9999, 0), Quaternion.identity); tempInstance.SetActive(false); Tilemap templateTilemap = tempInstance.GetComponentInChildren<Tilemap>(); if (templateTilemap == null) { Debug.LogError($"Template prefab '{selectedTemplatePrefab.name}' missing Tilemap!", selectedTemplatePrefab); return false; } templateTilemap.CompressBounds(); BoundsInt templateCellBounds = templateTilemap.cellBounds; int templateWidth = templateCellBounds.size.x; int templateHeight = templateCellBounds.size.y; int padding = (int)roomPadding; int availableWidth = leaf.width - (2 * padding); int availableHeight = leaf.height - (2 * padding); if (templateWidth > availableWidth || templateHeight > availableHeight) { return false; } int placeOffsetX = pseudoRandom.Next(0, availableWidth - templateWidth + 1); int placeOffsetY = pseudoRandom.Next(0, availableHeight - templateHeight + 1); int gridStartX = leaf.x + padding + placeOffsetX; int gridStartY = leaf.y + padding + placeOffsetY; bool copiedAnyFloor = false; foreach (Vector3Int localPos in templateCellBounds.allPositionsWithin) { TileBase tile = templateTilemap.GetTile(localPos); if (tile != null) { int gridX = gridStartX + localPos.x - templateCellBounds.xMin; int gridY = gridStartY + localPos.y - templateCellBounds.yMin; if (gridX >= 0 && gridX < levelWidth && gridY >= 0 && gridY < levelHeight) { TileType typeToPlace = TileType.Empty; if (tile == floorTile) { typeToPlace = TileType.Floor; copiedAnyFloor = true; } else if (tile == wallTile) { typeToPlace = TileType.Wall; } if (typeToPlace != TileType.Empty) { grid[gridX, gridY] = typeToPlace; } } } } if (!copiedAnyFloor) { Debug.LogWarning($"Placed template '{selectedTemplatePrefab.name}' contained no Floor tiles matching reference."); } placedBounds = new RectInt(gridStartX, gridStartY, templateWidth, templateHeight); /* NOTE: Does NOT add to placedRoomBounds dict here, caller must do it */ return true; } finally { if (tempInstance != null) { if (Application.isPlaying) { Destroy(tempInstance); } else { DestroyImmediate(tempInstance); } } }
    }
    private bool PlaceSpecificRoomTemplate(GameObject templatePrefab, Vector2Int targetGridPos, out RectInt placedBounds)
    { /* ... unchanged ... */
        placedBounds = RectInt.zero; if (templatePrefab == null) return false; GameObject tempInstance = null; try { tempInstance = Instantiate(templatePrefab, new Vector3(9999, 9999, 0), Quaternion.identity); tempInstance.SetActive(false); Tilemap templateTilemap = tempInstance.GetComponentInChildren<Tilemap>(); if (templateTilemap == null) { Debug.LogError($"Template prefab '{templatePrefab.name}' missing Tilemap!", templatePrefab); return false; } templateTilemap.CompressBounds(); BoundsInt templateCellBounds = templateTilemap.cellBounds; int templateWidth = templateCellBounds.size.x; int templateHeight = templateCellBounds.size.y; int gridStartX = targetGridPos.x; int gridStartY = targetGridPos.y; if (gridStartX + templateWidth > levelWidth || gridStartY + templateHeight > levelHeight || gridStartX < 0 || gridStartY < 0) { Debug.LogWarning($"Template '{templatePrefab.name}' placement exceeds level bounds."); return false; } bool copiedAnyFloor = false; foreach (Vector3Int localPos in templateCellBounds.allPositionsWithin) { TileBase tile = templateTilemap.GetTile(localPos); if (tile != null) { int gridX = gridStartX + localPos.x - templateCellBounds.xMin; int gridY = gridStartY + localPos.y - templateCellBounds.yMin; if (gridX >= 0 && gridX < levelWidth && gridY >= 0 && gridY < levelHeight) { TileType typeToPlace = TileType.Empty; if (tile == floorTile) { typeToPlace = TileType.Floor; copiedAnyFloor = true; } else if (tile == wallTile) { typeToPlace = TileType.Wall; } if (typeToPlace != TileType.Empty) { grid[gridX, gridY] = typeToPlace; } } } } if (!copiedAnyFloor) { Debug.LogWarning($"Placed template '{templatePrefab.name}' contained no Floor tiles matching reference."); } placedBounds = new RectInt(gridStartX, gridStartY, templateWidth, templateHeight); return true; } finally { if (tempInstance != null) { if (Application.isPlaying) { Destroy(tempInstance); } else { DestroyImmediate(tempInstance); } } }
    }
    private GameObject FindTemplatePrefab(string templateName, string nodeId)
    { /* ... unchanged ... */
        if (string.IsNullOrEmpty(templateName)) { if (roomTemplatePrefabs != null && roomTemplatePrefabs.Count > 0) { return roomTemplatePrefabs[0]; } return null; }
        return roomTemplatePrefabs?.Find(p => p != null && p.name.Equals(templateName, StringComparison.OrdinalIgnoreCase));
    }

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

    // --- MISSING HELPER FUNCTIONS (Restored Below) ---

    // --- Helper to Create/Find Parent Transforms ---
    private Transform CreateOrFindParent(string parentName)
    {
        Transform existingParent = transform.Find(parentName);
        if (existingParent != null) { return existingParent; }
        else
        {
            GameObject parentObject = new GameObject(parentName);
            parentObject.transform.SetParent(this.transform);
            parentObject.transform.localPosition = Vector3.zero;
            return parentObject.transform;
        }
    }

    // --- Helper to Destroy Children of a Named Parent ---
    private void DestroyChildrenOf(string parentName)
    {
        Transform parent = transform.Find(parentName);
        if (parent != null)
        {
            int childCount = parent.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (child != null)
                {
                    if (Application.isPlaying) { Destroy(child); }
                    else { DestroyImmediate(child); }
                }
            }
        }
    }

    // --- JSON Parsing ---
    private LevelDesignData ParseLevelDesign()
    {
        try
        {
            return JsonUtility.FromJson<LevelDesignData>(levelDesignJson.text);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing Level Design JSON: {e.Message}");
            return null;
        }
    }

    // --- BSP Algorithm ---
    private void RunBSPSplit()
    {
        if (bspLeaves == null) bspLeaves = new List<RectInt>(); else bspLeaves.Clear();
        if (pseudoRandom == null) Initialize(); // Ensure RNG is ready
        var rootNode = new RectInt(1, 1, levelWidth - 2, levelHeight - 2); // Start within bounds
        var nodeQueue = new Queue<KeyValuePair<RectInt, int>>();
        nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(rootNode, 0));

        while (nodeQueue.Count > 0)
        {
            var currentPair = nodeQueue.Dequeue();
            RectInt currentNode = currentPair.Key;
            int currentIteration = currentPair.Value;

            if (currentIteration >= maxIterations || ShouldStopSplitting(currentNode))
            {
                bspLeaves.Add(currentNode);
                continue;
            }

            if (TrySplitNode(currentNode, out RectInt nodeA, out RectInt nodeB))
            {
                nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(nodeA, currentIteration + 1));
                nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(nodeB, currentIteration + 1));
            }
            else
            {
                // If split fails (e.g., too small), add the current node as a leaf
                bspLeaves.Add(currentNode);
            }
        }
        // Debug.Log($"BSP Split complete. Leaves: {bspLeaves.Count}");
    }

    private bool ShouldStopSplitting(RectInt node)
    {
        // Check minimum size requirement for further splitting
        if (node.width < minRoomSize * 2 && node.height < minRoomSize * 2) return true; // Too small in both dimensions

        // Optional: Check aspect ratio to prevent very long/thin leaves
        float aspectRatio = node.width <= 0 || node.height <= 0 ? 1f : (float)Mathf.Max(node.width, node.height) / Mathf.Max(1, Mathf.Min(node.width, node.height));
        if (aspectRatio > 4.0f) return true; // Stop splitting if too elongated (adjust ratio as needed)

        return false; // Default: continue splitting if large enough
    }

    private bool TrySplitNode(RectInt node, out RectInt nodeA, out RectInt nodeB)
    {
        bool splitHorizontal;
        nodeA = nodeB = RectInt.zero;

        // Decide split direction (prefer splitting the longer axis, or random if square-ish)
        bool preferHorizontal = (node.height > node.width && (float)node.height / Mathf.Max(1, node.width) >= 1.2f);
        bool preferVertical = (node.width > node.height && (float)node.width / Mathf.Max(1, node.height) >= 1.2f);
        if (preferHorizontal) splitHorizontal = true;
        else if (preferVertical) splitHorizontal = false;
        else splitHorizontal = pseudoRandom.Next(0, 2) == 0; // Random split for near-square nodes

        // Ensure the node is large enough to be split meaningfully
        int minSizeForSplit = minRoomSize + (int)(2 * roomPadding) + 1; // Need space for padding + min room in each part
        minSizeForSplit = Mathf.Max(minRoomSize + 1, minSizeForSplit); // At least min room size + 1

        if (splitHorizontal)
        {
            if (node.height < minSizeForSplit * 2) return false; // Not tall enough to split + pad + have min rooms
            // Find a random split point (ensure parts are at least minSizeForSplit)
            int splitY = pseudoRandom.Next(node.y + minSizeForSplit, node.yMax - minSizeForSplit + 1);
            nodeA = new RectInt(node.x, node.y, node.width, splitY - node.y);
            nodeB = new RectInt(node.x, splitY, node.width, node.yMax - splitY);
        }
        else
        { // Split Vertical
            if (node.width < minSizeForSplit * 2) return false; // Not wide enough
            int splitX = pseudoRandom.Next(node.x + minSizeForSplit, node.xMax - minSizeForSplit + 1);
            nodeA = new RectInt(node.x, node.y, splitX - node.x, node.height);
            nodeB = new RectInt(splitX, node.y, node.xMax - splitX, node.height);
        }
        return true;
    }

}