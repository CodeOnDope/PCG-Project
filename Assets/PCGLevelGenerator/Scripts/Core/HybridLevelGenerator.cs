using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System; // Required for System.Random
using System.Linq;
using UnityEditor; // Keep for Undo/EditorUtility

// Assumes GenerationMode, NodeType, TileType defined in LevelGenerationTypes.cs or similar

public class HybridLevelGenerator : MonoBehaviour
{
    [Header("--- Generation Mode ---")]
    [Tooltip("Select the generation method:\n" +
             "FullyProcedural: BSP splits, random Rect rooms, procedural corridors.\n" +
             "HybridProcedural: BSP splits, random Templates/L-Shapes/Rects, procedural corridors.\n" +
             "UserDefinedLayout: Reads layout from RoomNode components placed in the scene.")]
    public GenerationMode generationMode = GenerationMode.HybridProcedural;

    [Header("Level Dimensions (Max Bounds)")]
    [Tooltip("Maximum width of the generation grid.")]
    [Range(10, 500)] public int levelWidth = 100;
    [Tooltip("Maximum height of the generation grid.")]
    [Range(10, 500)] public int levelHeight = 100;

    [Header("BSP Settings (Procedural & Hybrid Modes)")]
    [Tooltip("Minimum width or height for BSP partitions and generated rectangular rooms.")]
    [Range(3, 50)] public int minRoomSize = 8;
    [Tooltip("Number of times the space is recursively split by the BSP algorithm. More iterations = potentially smaller, more numerous spaces.")]
    [Range(1, 12)] public int maxIterations = 5;
    [Tooltip("Minimum empty grid units between rooms generated within BSP leaves.")]
    [Range(0f, 5f)] public float roomPadding = 2f;

    [Header("Room Shape Settings (Hybrid & UserDefined Modes)")]
    [Tooltip("The chance (0-1) a procedural room attempts to be L-shaped (Hybrid Mode only).")]
    [Range(0f, 1f)] public float lShapeProbability = 0.3f;
    [Tooltip("Minimum ratio (0.2-0.8) of the smaller leg's dimension relative to the main stem's corresponding dimension for L-shapes.")]
    [Range(0.2f, 0.8f)] public float minLLegRatio = 0.4f;
    [Tooltip("Maximum ratio (minLLegRatio-0.8) of the smaller leg's dimension relative to the main stem's corresponding dimension for L-shapes.")]
    [Range(0.2f, 0.8f)] public float maxLLegRatio = 0.7f; // Max is clamped by editor script
    [Tooltip("Default logical size used for Rect/LShape RoomNodes in UserDefinedLayout mode if the node's size is zero.")]
    public Vector2Int defaultSceneNodeSize = new Vector2Int(10, 10);

    [Header("Room Template Settings (Hybrid & UserDefined Modes)")]
    [Tooltip("Prefabs containing Tilemaps used as room layouts. Assign prefabs here.")]
    public List<GameObject> roomTemplatePrefabs;
    [Tooltip("The chance (0-1) a procedural room attempts to use a template from the list above (Hybrid Mode only).")]
    [Range(0f, 1f)] public float roomTemplateProbability = 0.2f;

    [Header("Corridor Settings")]
    [Tooltip("Width (in grid units) of generated corridors.")]
    [Range(1, 5)] public int corridorWidth = 1;

    [Header("Tilemaps & Tiles")]
    [Tooltip("Required: The Tilemap for placing floor tiles.")]
    public Tilemap groundTilemap;
    [Tooltip("Required: The Tilemap for placing wall tiles.")]
    public Tilemap wallTilemap;
    [Tooltip("Required: The TileBase asset representing the floor.")]
    public TileBase floorTile;
    [Tooltip("Required: The TileBase asset representing walls.")]
    public TileBase wallTile;

    [Header("Randomness")]
    [Tooltip("Seed for the random number generator. 0 or checking 'Use Random Seed' generates a new seed each time.")]
    public int seed = 0;
    [Tooltip("If checked, ignores the Seed value and uses a time-based seed for each generation.")]
    public bool useRandomSeed = true;

    [Header("Entities & Decorations")]
    [Tooltip("Optional: Prefab for the player character. Spawns one instance.")]
    public GameObject playerPrefab;
    [Tooltip("Optional: Prefab for enemy characters.")]
    public GameObject enemyPrefab;
    [Tooltip("Optional: Prefab for decoration objects.")]
    public GameObject decorationPrefab;
    [Tooltip("Maximum number of enemies to attempt spawning per room.")]
    [Range(0, 20)] public int enemiesPerRoom = 2;
    [Tooltip("Maximum number of decorations to attempt spawning per room.")]
    [Range(0, 20)] public int decorationsPerRoom = 3;


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
        GenerateLevel(false); // Default context menu call clears based on mode
    }

    // Helper method for clearing only generated content (Tiles, Entities, internal data)
    private void ClearGeneratedContentOnly()
    {
        Debug.Log("Clearing generated content (Tiles, Entities)...");
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        DestroyChildrenOfHolder("PlayerHolder");
        DestroyChildrenOfHolder("EnemiesHolder");
        DestroyChildrenOfHolder("DecorationsHolder");
        grid = null;
        bspLeaves = null;
        if (placedRoomBounds != null) placedRoomBounds.Clear(); else placedRoomBounds = new Dictionary<string, RectInt>();
    }

    // Main generation method with optional clear skip parameter
    public void GenerateLevel(bool skipClear = false)
    {
        Debug.Log($"--- Starting Level Generation (Mode: {generationMode}, SkipClear: {skipClear}) ---");

        // --- Determine How to Clear Based on Mode and Parameter ---
        if (skipClear)
        {
            // Explicitly told to skip clear (e.g., from Visual Editor after creating nodes)
            Debug.Log("Skipping ClearLevel step (explicitly requested). Performing partial clear...");
            ClearGeneratedContentOnly();
        }
        else // skipClear is false (e.g., called from Inspector button)
        {
            if (generationMode == GenerationMode.UserDefinedLayout)
            {
                // In UserDefinedLayout mode, Inspector button should only clear generated content, NOT the RoomNodes
                Debug.Log($"UserDefinedLayout mode detected with clear request. Performing PARTIAL clear (preserving RoomNodes).");
                ClearGeneratedContentOnly();
            }
            else
            {
                // For other modes (Procedural, Hybrid), perform a FULL clear including scene nodes if any
                Debug.Log($"Mode is {generationMode}. Performing FULL clear (including potential scene nodes).");
                ClearLevel(); // Calls full clear which includes LevelDesignRoot removal
            }
        }
        // --- End Clear Logic ---

        // Initialize generator state
        Initialize();

        // --- Pre-Generation Validation ---
        if (grid == null || pseudoRandom == null || placedRoomBounds == null || playerHolder == null || enemiesHolder == null || decorationsHolder == null)
        {
            Debug.LogError("Initialization failed! Necessary internal structures are null.", this); return;
        }
        if (floorTile == null || wallTile == null)
        {
            Debug.LogError("Floor Tile or Wall Tile is not assigned!", this); // Keep check
        }
        if (groundTilemap == null || wallTilemap == null)
        {
            Debug.LogError("Ground or Wall Tilemap is not assigned!", this); // Keep check
        }
        // --- End Validation ---

        // --- Run Generation Based on Mode ---
        bool generationSuccess = false;
        Debug.Log($"[GenerateLevel] Mode just before switch: {generationMode}");

        switch (generationMode)
        {
            case GenerationMode.FullyProcedural:
                generationSuccess = GenerateProceduralLevel();
                break;
            case GenerationMode.HybridProcedural:
                generationSuccess = GenerateHybridLevel();
                break;
            case GenerationMode.UserDefinedLayout:
                // Use newer FindObjectsByType for potentially better performance
                // FindObjectsSortMode.None is faster if order doesn't matter.
                RoomNode[] nodes = FindObjectsByType<RoomNode>(FindObjectsSortMode.None);
                if (nodes == null || nodes.Length == 0)
                {
                    Debug.LogError("[GenerateLevel] FAILED: No RoomNode components found in the scene! Cannot generate in UserDefinedLayout mode. Use the Visual Level Designer to create scene nodes first.");
                    generationSuccess = false;
                }
                else
                {
                    Debug.Log($"[GenerateLevel] Found {nodes.Length} RoomNode components. Proceeding with UserDefinedLayout generation.");
                    generationSuccess = GenerateFromSceneLayout(nodes); // Pass nodes in
                }
                break;
            default:
                Debug.LogError($"[GenerateLevel] Unknown or unsupported GenerationMode value encountered: {generationMode}");
                generationSuccess = false;
                break;
        }
        // --- End Generation Based on Mode ---

        // --- Final Steps (Tile Application, Entity Spawning) ---
        if (generationSuccess)
        {
            ApplyTilesToTilemap();
            SpawnEntities();
            Debug.Log($"--- Level Generation Complete --- Seed: {seed}. Rooms processed/placed: {placedRoomBounds?.Count ?? 0}.");
        }
        else
        {
            Debug.LogError($"--- Level Generation FAILED (Mode: {generationMode}) --- Check previous logs for specific error details.");
        }
        // --- End Final Steps ---
    }


    // Full Clear method - Clears generated content AND scene nodes
    [ContextMenu("Clear Level")]
    public void ClearLevel()
    {
        Debug.Log("Clearing level (Generated Content & Scene Nodes)...");
        // Clear Tiles, Entities, internal data first
        ClearGeneratedContentOnly();

        // Then explicitly destroy the design root and its children
        DestroySceneObjectAndChildren("LevelDesignRoot");

        Debug.Log("Generated content and scene nodes cleared.");
    }

    // --- Initialization and Setup ---
    private void Initialize()
    {
        if (useRandomSeed || seed == 0)
        {
            seed = Environment.TickCount;
        }
        pseudoRandom = new System.Random(seed);
        Debug.Log($"Using Seed: {seed}");

        // Initialize grid (ensure dimensions are positive)
        levelWidth = Mathf.Max(1, levelWidth);
        levelHeight = Mathf.Max(1, levelHeight);
        grid = new TileType[levelWidth, levelHeight];
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                grid[x, y] = TileType.Wall; // Default to wall
            }
        }

        // Initialize lists/dictionaries
        if (bspLeaves == null) bspLeaves = new List<RectInt>(); else bspLeaves.Clear();
        if (placedRoomBounds == null) placedRoomBounds = new Dictionary<string, RectInt>(); else placedRoomBounds.Clear();

        // Ensure entity holder transforms exist
        playerHolder = CreateOrFindParent("PlayerHolder");
        enemiesHolder = CreateOrFindParent("EnemiesHolder");
        decorationsHolder = CreateOrFindParent("DecorationsHolder");
    }

    // --- Mode-Specific Generation Pipelines ---
    private bool GenerateProceduralLevel()
    {
        RunBSPSplit();
        CreateRoomsInLeaves_Procedural();
        if (placedRoomBounds.Count < 2)
        {
            Debug.LogWarning("[GenerateProceduralLevel] Less than 2 rooms generated, skipping corridor generation.");
        }
        else
        {
            CreateCorridors_Procedural();
        }
        return placedRoomBounds.Count > 0;
    }

    private bool GenerateHybridLevel()
    {
        RunBSPSplit();
        CreateRoomsInLeaves_Hybrid();
        if (placedRoomBounds.Count < 2)
        {
            Debug.LogWarning("[GenerateHybridLevel] Less than 2 rooms generated, skipping corridor generation.");
        }
        else
        {
            CreateCorridors_Procedural();
        }
        return placedRoomBounds.Count > 0;
    }

    // Accepts the already found RoomNodes
    private bool GenerateFromSceneLayout(RoomNode[] roomNodes)
    {
        Debug.Log($"[GenerateFromSceneLayout] Processing {roomNodes.Length} provided RoomNode components.");
        if (placedRoomBounds == null) placedRoomBounds = new Dictionary<string, RectInt>();
        placedRoomBounds.Clear();

        int successfullyPlacedCount = 0;
        Debug.Log("[GenerateFromSceneLayout] Starting Room Placement Phase...");

        // --- Room Placement Loop ---
        foreach (RoomNode node in roomNodes)
        {
            if (node == null || !node.isActiveAndEnabled)
            {
                Debug.LogWarning($"-- Skipping inactive or null RoomNode: {node?.gameObject.name ?? "NULL"}");
                continue;
            }

            Vector3 nodeWorldPos = node.transform.position;
            if (string.IsNullOrEmpty(node.roomId))
            {
                Debug.LogWarning($"-- Skipping Node {node.gameObject.name}: Room Id is missing.", node.gameObject);
                continue;
            }
            if (placedRoomBounds.ContainsKey(node.roomId))
            {
                Debug.LogWarning($"-- Skipping Node {node.gameObject.name}: Duplicate Room Id '{node.roomId}' already exists.", node.gameObject);
                continue;
            }
            Debug.Log($"Processing Node: {node.gameObject.name} (ID: '{node.roomId}', Type: {node.roomType}, ScenePos: {nodeWorldPos:F2})");

            RectInt roomBounds = RectInt.zero;
            bool created = false;
            Vector2Int nodeSize = node.roomSize.x > 0 && node.roomSize.y > 0 ? node.roomSize : defaultSceneNodeSize;
            int centerX = levelWidth / 2 + Mathf.RoundToInt(nodeWorldPos.x);
            int centerY = levelHeight / 2 + Mathf.RoundToInt(nodeWorldPos.y);
            Vector2Int targetCenterPos = new Vector2Int(centerX, centerY);

            // Generate room based on node type
            switch (node.roomType)
            {
                case NodeType.Rect:
                    roomBounds = new RectInt(targetCenterPos.x - nodeSize.x / 2, targetCenterPos.y - nodeSize.y / 2, nodeSize.x, nodeSize.y);
                    if (IsRectWithinBounds(roomBounds)) { CarveRectangle(roomBounds, TileType.Floor); created = true; }
                    else { Debug.LogWarning($"-- Rect bounds {roomBounds} outside grid.", node.gameObject); }
                    break;
                case NodeType.LShape:
                    if (TryCreateLShapeAt(targetCenterPos, nodeSize, out RectInt stemRect, out RectInt legRect, out roomBounds))
                    {
                        if (IsRectWithinBounds(stemRect) && IsRectWithinBounds(legRect)) { CarveRectangle(stemRect, TileType.Floor); CarveRectangle(legRect, TileType.Floor); created = true; }
                        else { Debug.LogWarning($"-- L-Shape part outside grid.", node.gameObject); }
                    }
                    else
                    {
                        Debug.LogWarning($"-- Failed to calculate L-Shape geometry. Placing Rect fallback.", node.gameObject);
                        roomBounds = new RectInt(targetCenterPos.x - nodeSize.x / 2, targetCenterPos.y - nodeSize.y / 2, nodeSize.x, nodeSize.y);
                        if (IsRectWithinBounds(roomBounds)) { CarveRectangle(roomBounds, TileType.Floor); created = true; }
                        else { Debug.LogWarning($"-- Fallback Rect bounds outside grid.", node.gameObject); }
                    }
                    break;
                case NodeType.Template:
                    GameObject templatePrefab = node.roomTemplatePrefab;
                    if (templatePrefab != null)
                    {
                        if (GetTemplateDimensions(templatePrefab, out int tW, out int tH))
                        {
                            Vector2Int bottomLeft = new Vector2Int(targetCenterPos.x - tW / 2, targetCenterPos.y - tH / 2);
                            if (PlaceSpecificRoomTemplate(templatePrefab, bottomLeft, out roomBounds)) { created = true; }
                            else { Debug.LogWarning($"-- Failed to place template '{templatePrefab.name}'.", node.gameObject); }
                        }
                        else { Debug.LogWarning($"-- Could not get dimensions for template prefab.", node.gameObject); }
                    }
                    else { Debug.LogWarning($"-- Template prefab not assigned.", node.gameObject); }
                    break;
            }

            // Record placed bounds if successful
            if (created && roomBounds.size.x > 0 && roomBounds.size.y > 0)
            {
                // Overlap check (optional - currently just warns)
                foreach (var kvp in placedRoomBounds)
                {
                    if (kvp.Key != node.roomId && kvp.Value.Overlaps(roomBounds))
                    {
                        Debug.LogWarning($"-- Overlap detected: {node.roomId} and {kvp.Key}", node.gameObject);
                    }
                }
                placedRoomBounds[node.roomId] = roomBounds;
                successfullyPlacedCount++;
                Debug.Log($"-- Placed room for node {node.roomId}. Bounds: {roomBounds}");
            }
            else if (created)
            {
                Debug.LogWarning($"-- Room creation reported success but bounds were zero for node {node.roomId}. Skipping record.", node.gameObject);
            }
            else
            {
                Debug.LogWarning($"-- Failed to create/place room for node {node.roomId}.", node.gameObject);
            }
        } // End foreach node loop

        if (successfullyPlacedCount < 1) { Debug.LogError("GenerateFromSceneLayout FAILED: No rooms were successfully placed."); return false; }
        Debug.Log($"[GenerateFromSceneLayout] Finished Room Placement. Successfully placed: {successfullyPlacedCount}");

        // --- Connection Phase ---
        if (successfullyPlacedCount >= 2)
        {
            ConnectSceneNodes(roomNodes); // Use helper for connections
        }
        else { Debug.LogWarning("GenerateFromSceneLayout: Only one room placed, skipping corridor generation."); }

        Debug.Log($"[GenerateFromSceneLayout] Finished Successfully.");
        return true;
    }


    // --- Room Creation Helpers ---
    private void CreateRoomsInLeaves_Procedural()
    { /* (Same as before) */
        if (bspLeaves == null) { Debug.LogError("BSP leaves not generated!"); return; }
        if (placedRoomBounds == null) placedRoomBounds = new Dictionary<string, RectInt>();
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
    private void CreateRoomsInLeaves_Hybrid()
    { /* (Same as before) */
        if (bspLeaves == null) { Debug.LogError("BSP leaves not generated!"); return; }
        if (placedRoomBounds == null) placedRoomBounds = new Dictionary<string, RectInt>();
        placedRoomBounds.Clear();
        for (int i = 0; i < bspLeaves.Count; i++)
        {
            RectInt leaf = bspLeaves[i]; RectInt roomBounds = RectInt.zero; bool roomCreated = false; string roomId = $"Room_{i}";
            if (roomTemplatePrefabs != null && roomTemplatePrefabs.Count > 0 && pseudoRandom.NextDouble() < roomTemplateProbability)
            {
                if (TryPlaceRoomTemplate(leaf, roomId, out roomBounds)) { roomCreated = true; }
            }
            if (!roomCreated && pseudoRandom.NextDouble() < lShapeProbability)
            {
                if (TryCreateLShapeInLeaf(leaf, out RectInt stemRect, out RectInt legRect, out roomBounds))
                {
                    CarveRectangle(stemRect, TileType.Floor); CarveRectangle(legRect, TileType.Floor); placedRoomBounds[roomId] = roomBounds; roomCreated = true;
                }
            }
            if (!roomCreated)
            {
                if (TryCreateRectangleInLeaf(leaf, out roomBounds))
                {
                    CarveRectangle(roomBounds, TileType.Floor); placedRoomBounds[roomId] = roomBounds; roomCreated = true;
                }
            }
        }
        Debug.Log($"Hybrid room creation complete. Rooms generated: {placedRoomBounds.Count}");
    }
    private bool TryCreateRectangleInLeaf(RectInt leaf, out RectInt roomRect)
    { /* (Same as before) */
        roomRect = RectInt.zero; int padding = Mathf.Max(0, (int)roomPadding); int maxW = leaf.width - (2 * padding); int maxH = leaf.height - (2 * padding);
        if (maxW < minRoomSize || maxH < minRoomSize) return false;
        int w = pseudoRandom.Next(minRoomSize, maxW + 1); int h = pseudoRandom.Next(minRoomSize, maxH + 1);
        int x = leaf.x + padding + pseudoRandom.Next(0, maxW - w + 1); int y = leaf.y + padding + pseudoRandom.Next(0, maxH - h + 1);
        roomRect = new RectInt(x, y, w, h); return IsRectWithinBounds(roomRect);
    }
    private bool TryCreateLShapeInLeaf(RectInt leaf, out RectInt stemRect, out RectInt legRect, out RectInt overallBounds)
    { /* (Same as before) */
        stemRect = legRect = overallBounds = RectInt.zero; int padding = Mathf.Max(0, (int)roomPadding); int availW = leaf.width - (2 * padding); int availH = leaf.height - (2 * padding); int minL = 1;
        if (availW < minRoomSize || availH < minRoomSize) return false;
        int sW = pseudoRandom.Next(minRoomSize, availW + 1); int sH = pseudoRandom.Next(minRoomSize, availH + 1);
        int maxSX = availW - sW; int maxSY = availH - sH; if (maxSX < 0 || maxSY < 0) return false;
        int sRelX = pseudoRandom.Next(0, maxSX + 1); int sRelY = pseudoRandom.Next(0, maxSY + 1); stemRect = new RectInt(leaf.x + padding + sRelX, leaf.y + padding + sRelY, sW, sH);
        RectInt padLeaf = new RectInt(leaf.x + padding, leaf.y + padding, availW, availH); int spAb = padLeaf.yMax - stemRect.yMax; int spBe = stemRect.yMin - padLeaf.yMin; int spRi = padLeaf.xMax - stemRect.xMax; int spLe = stemRect.xMin - padLeaf.xMin;
        bool canAb = spAb >= minL; bool canBe = spBe >= minL; bool canRi = spRi >= minL; bool canLe = spLe >= minL;
        List<int> vAtt = new List<int>(); if (canBe) vAtt.Add(0); if (canAb) vAtt.Add(1);
        List<int> hAtt = new List<int>(); if (canLe) hAtt.Add(0); if (canRi) hAtt.Add(1);
        if (vAtt.Count == 0 && hAtt.Count == 0) return false;
        bool tryV; if (sW >= sH) tryV = vAtt.Count > 0; else tryV = !(hAtt.Count > 0); if (tryV && vAtt.Count == 0) tryV = false; if (!tryV && hAtt.Count == 0) tryV = true;
        int lW, lH, lX, lY; float ratio = Mathf.Clamp((float)pseudoRandom.Next((int)(minLLegRatio * 100), (int)(maxLLegRatio * 100) + 1) / 100f, minLLegRatio, maxLLegRatio);
        if (tryV) { bool attAb = vAtt[pseudoRandom.Next(vAtt.Count)] == 1; int availSp = attAb ? spAb : spBe; if (availSp < minL) return false; lH = pseudoRandom.Next(minL, availSp + 1); lW = (int)(sW * ratio); lW = Mathf.Clamp(lW, minL, sW); int maxLXOff = Mathf.Max(0, sW - lW); lX = stemRect.x + pseudoRandom.Next(0, maxLXOff + 1); lY = attAb ? stemRect.yMax : stemRect.yMin - lH; }
        else { bool attRi = hAtt[pseudoRandom.Next(hAtt.Count)] == 1; int availSp = attRi ? spRi : spLe; if (availSp < minL) return false; lW = pseudoRandom.Next(minL, availSp + 1); lH = (int)(sH * ratio); lH = Mathf.Clamp(lH, minL, sH); int maxLYOff = Mathf.Max(0, sH - lH); lY = stemRect.y + pseudoRandom.Next(0, maxLYOff + 1); lX = attRi ? stemRect.xMax : stemRect.xMin - lW; }
        if (lW < minL || lH < minL) return false; legRect = new RectInt(lX, lY, lW, lH);
        if (!IsRectWithinBounds(stemRect) || !IsRectWithinBounds(legRect)) return false;
        int minX = Mathf.Min(stemRect.xMin, legRect.xMin); int minY = Mathf.Min(stemRect.yMin, legRect.yMin); int maxX = Mathf.Max(stemRect.xMax, legRect.xMax); int maxY = Mathf.Max(stemRect.yMax, legRect.yMax); overallBounds = new RectInt(minX, minY, maxX - minX, maxY - minY); return true;
    }
    private bool TryPlaceRoomTemplate(RectInt leaf, string roomId, out RectInt placedBounds)
    { /* (Same as before) */
        placedBounds = RectInt.zero; if (roomTemplatePrefabs == null || roomTemplatePrefabs.Count == 0) return false;
        GameObject prefab = roomTemplatePrefabs[pseudoRandom.Next(roomTemplatePrefabs.Count)]; if (prefab == null) return false;
        if (!GetTemplateDimensions(prefab, out int tW, out int tH)) return false;
        int pad = Mathf.Max(0, (int)roomPadding); int availW = leaf.width - (2 * pad); int availH = leaf.height - (2 * pad); if (tW > availW || tH > availH) return false;
        int offX = pseudoRandom.Next(0, availW - tW + 1); int offY = pseudoRandom.Next(0, availH - tH + 1); int startX = leaf.x + pad + offX; int startY = leaf.y + pad + offY;
        if (PlaceSpecificRoomTemplate(prefab, new Vector2Int(startX, startY), out placedBounds)) { if (placedRoomBounds == null) placedRoomBounds = new Dictionary<string, RectInt>(); placedRoomBounds[roomId] = placedBounds; return true; }
        return false;
    }
    private bool PlaceSpecificRoomTemplate(GameObject templatePrefab, Vector2Int targetGridBottomLeft, out RectInt placedBounds)
    { /* (Same as before) */
        placedBounds = RectInt.zero; if (templatePrefab == null) return false; GameObject temp = null;
        try
        {
            temp = Instantiate(templatePrefab); temp.SetActive(false); Tilemap tm = temp.GetComponentInChildren<Tilemap>(); if (tm == null) return false; tm.CompressBounds(); BoundsInt bounds = tm.cellBounds; int tW = bounds.size.x; int tH = bounds.size.y; if (tW <= 0 || tH <= 0) return false; int startX = targetGridBottomLeft.x; int startY = targetGridBottomLeft.y; placedBounds = new RectInt(startX, startY, tW, tH); if (!IsRectWithinBounds(placedBounds)) return false;
            foreach (Vector3Int pos in bounds.allPositionsWithin) { TileBase tile = tm.GetTile(pos); if (tile != null) { int gridX = startX + pos.x - bounds.xMin; int gridY = startY + pos.y - bounds.yMin; if (IsCoordInBounds(gridX, gridY)) { TileType type = TileType.Empty; if (tile == floorTile) type = TileType.Floor; else if (tile == wallTile) type = TileType.Wall; if (type != TileType.Empty) grid[gridX, gridY] = type; } } }
            return true;
        }
        catch (Exception ex) { Debug.LogError($"Template Placement Exception: {ex.Message}"); return false; }
        finally { if (temp != null) { if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp); } }
    }
    private bool GetTemplateDimensions(GameObject templatePrefab, out int width, out int height)
    { /* (Same as before) */
        width = 0; height = 0; if (templatePrefab == null) return false; GameObject temp = null;
        try
        {
            temp = Instantiate(templatePrefab); temp.SetActive(false); Tilemap tm = temp.GetComponentInChildren<Tilemap>(true); if (tm == null) return false; tm.CompressBounds(); BoundsInt bounds = tm.cellBounds; width = bounds.size.x; height = bounds.size.y; return width > 0 && height > 0;
        }
        catch { return false; }
        finally { if (temp != null) { if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp); } }
    }
    private bool TryCreateLShapeAt(Vector2Int centerPos, Vector2Int size, out RectInt stemRect, out RectInt legRect, out RectInt overallBounds)
    { /* (Same as before) */
        stemRect = legRect = overallBounds = RectInt.zero; int minD = 1; if (size.x < minD * 2 || size.y < minD * 2) return false;
        int sW = size.x; int sH = Mathf.Max(minD, Mathf.RoundToInt(size.y * 0.7f)); int lH = Mathf.Max(minD, size.y - sH); int lW = Mathf.Max(minD, Mathf.RoundToInt(size.x * 0.6f));
        int sX = centerPos.x - sW / 2; int sY = centerPos.y - size.y / 2; stemRect = new RectInt(sX, sY, sW, sH);
        int lX = stemRect.xMax - lW; int lY = stemRect.yMax; legRect = new RectInt(lX, lY, lW, lH);
        if (!IsRectWithinBounds(stemRect) || !IsRectWithinBounds(legRect)) return false;
        int minX = Mathf.Min(stemRect.xMin, legRect.xMin); int minY = Mathf.Min(stemRect.yMin, legRect.yMin); int maxX = Mathf.Max(stemRect.xMax, legRect.xMax); int maxY = Mathf.Max(stemRect.yMax, legRect.yMax); overallBounds = new RectInt(minX, minY, maxX - minX, maxY - minY); return true;
    }


    // --- Corridor Connection ---
    private void ConnectSceneNodes(RoomNode[] roomNodes)
    {
        Debug.Log("[ConnectSceneNodes] Starting Connection Phase...");
        HashSet<Tuple<string, string>> createdConnections = new HashSet<Tuple<string, string>>();
        int connectionCount = 0;
        foreach (RoomNode node in roomNodes)
        {
            if (node == null || string.IsNullOrEmpty(node.roomId) || !placedRoomBounds.ContainsKey(node.roomId)) continue;
            RectInt fromBounds = placedRoomBounds[node.roomId];
            if (node.connectedRooms != null)
            {
                foreach (RoomNode connectedNode in node.connectedRooms)
                {
                    if (connectedNode != null && !string.IsNullOrEmpty(connectedNode.roomId) && placedRoomBounds.ContainsKey(connectedNode.roomId))
                    {
                        if (node.roomId == connectedNode.roomId) continue; // Skip self-connection
                        RectInt toBounds = placedRoomBounds[connectedNode.roomId];
                        var pair1 = Tuple.Create(node.roomId, connectedNode.roomId);
                        var pair2 = Tuple.Create(connectedNode.roomId, node.roomId); // Bidirectional check
                        if (!createdConnections.Contains(pair1) && !createdConnections.Contains(pair2))
                        {
                            Debug.Log($"-- Connecting {node.roomId} to {connectedNode.roomId}");
                            ConnectRects(fromBounds, toBounds); // Carve corridor
                            createdConnections.Add(pair1);
                            connectionCount++;
                        }
                    }
                }
            }
        }
        Debug.Log($"[ConnectSceneNodes] Finished Connections. Created {connectionCount} connections.");
    }

    private void CreateCorridors_Procedural()
    { /* (Same as before) */
        if (placedRoomBounds == null || placedRoomBounds.Count < 2) return;
        List<RectInt> boundsList = new List<RectInt>(placedRoomBounds.Values); List<RectInt> connected = new List<RectInt>(); List<RectInt> unconnected = new List<RectInt>(boundsList);
        RectInt start = unconnected[pseudoRandom.Next(unconnected.Count)]; connected.Add(start); unconnected.Remove(start);
        while (unconnected.Count > 0)
        {
            RectInt nearUncon = default; RectInt srcCon = default; float minDistSq = float.MaxValue;
            foreach (RectInt conRoom in connected) { foreach (RectInt unconRoom in unconnected) { float dSq = Vector2.SqrMagnitude(conRoom.center - unconRoom.center); if (dSq < minDistSq) { minDistSq = dSq; nearUncon = unconRoom; srcCon = conRoom; } } }
            if (nearUncon != default && srcCon != default) { ConnectRects(srcCon, nearUncon); connected.Add(nearUncon); unconnected.Remove(nearUncon); } else { Debug.LogError("MST Corridor Error"); break; }
        }
        Debug.Log($"Procedural corridor generation complete.");
    }
    private void ConnectRects(RectInt roomA, RectInt roomB)
    { /* (Same as before) */
        Vector2Int cA = Vector2Int.RoundToInt(roomA.center); Vector2Int cB = Vector2Int.RoundToInt(roomB.center);
        if (pseudoRandom.Next(0, 2) == 0) { CarveCorridorSegment(cA.x, cB.x, cA.y, true); CarveCorridorSegment(cA.y, cB.y, cB.x, false); }
        else { CarveCorridorSegment(cA.y, cB.y, cA.x, false); CarveCorridorSegment(cA.x, cB.x, cB.y, true); }
    }
    private void CarveCorridorSegment(int startCoord, int endCoord, int fixedCoord, bool isHorizontal)
    { /* (Same as before) */
        if (grid == null) return; int min = Mathf.Min(startCoord, endCoord); int max = Mathf.Max(startCoord, endCoord); int halfW = corridorWidth <= 1 ? 0 : (corridorWidth - 1) / 2;
        for (int i = min; i <= max; i++) { for (int w = -halfW; w <= halfW; w++) { int x, y; if (isHorizontal) { x = i; y = fixedCoord + w; } else { x = fixedCoord + w; y = i; } if (IsCoordInBounds(x, y)) { grid[x, y] = TileType.Floor; } } }
    }


    // --- Tile Placement & Grid Utilities ---
    private void CarveRectangle(RectInt rect, TileType tile)
    { /* (Same as before) */
        if (grid == null) return; for (int x = rect.xMin; x < rect.xMax; x++) { for (int y = rect.yMin; y < rect.yMax; y++) { if (IsCoordInBounds(x, y)) { grid[x, y] = tile; } } }
    }
    private List<Vector2Int> GetFloorTilesInRect(RectInt rect)
    { /* (Same as before) */
        List<Vector2Int> tiles = new List<Vector2Int>(); if (grid == null) return tiles; int sX = Mathf.Max(rect.xMin, 0); int eX = Mathf.Min(rect.xMax, levelWidth); int sY = Mathf.Max(rect.yMin, 0); int eY = Mathf.Min(rect.yMax, levelHeight);
        for (int x = sX; x < eX; x++) { for (int y = sY; y < eY; y++) { if (grid[x, y] == TileType.Floor) { tiles.Add(new Vector2Int(x, y)); } } }
        return tiles;
    }
    private Vector3 GetWorldPosition(Vector2Int gridPos)
    { /* (Same as before) */
        if (groundTilemap != null) { return groundTilemap.CellToWorld((Vector3Int)gridPos) + groundTilemap.cellSize * 0.5f; }
        else { Debug.LogWarning("Ground Tilemap missing, using fallback world pos calc."); return new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0); }
    }
    private bool IsCoordInBounds(int x, int y) { /* (Same as before) */ return x >= 0 && x < levelWidth && y >= 0 && y < levelHeight; }
    private bool IsRectWithinBounds(RectInt rect) { /* (Same as before) */ return rect.xMin >= 0 && rect.xMax <= levelWidth && rect.yMin >= 0 && rect.yMax <= levelHeight; }

    // --- Tilemap Application ---
    private void ApplyTilesToTilemap()
    { /* (Same as before using SetTilesBlock) */
        if (groundTilemap == null || wallTilemap == null || floorTile == null || wallTile == null || grid == null) { Debug.LogError("Cannot apply tiles: Missing references or grid."); return; }
        Debug.Log($"Applying grid ({levelWidth}x{levelHeight}) to tilemaps...");
        List<Vector3Int> floorPos = new List<Vector3Int>(); List<TileBase> floorT = new List<TileBase>(); List<Vector3Int> wallPos = new List<Vector3Int>(); List<TileBase> wallT = new List<TileBase>();
        Vector3Int currentPos = Vector3Int.zero;
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                currentPos.x = x; currentPos.y = y; TileType type = grid[x, y];
                if (type == TileType.Floor) { floorPos.Add(currentPos); floorT.Add(floorTile); }
                else if (type == TileType.Wall && IsWallCandidate(x, y)) { wallPos.Add(currentPos); wallT.Add(wallTile); }
            }
        }
        BoundsInt bounds = new BoundsInt(0, 0, 0, levelWidth, levelHeight, 1);
        TileBase[] nullTiles = Enumerable.Repeat<TileBase>(null, levelWidth * levelHeight).ToArray();
        groundTilemap.SetTilesBlock(bounds, nullTiles); wallTilemap.SetTilesBlock(bounds, nullTiles); // Clear first
        if (floorPos.Count > 0) groundTilemap.SetTiles(floorPos.ToArray(), floorT.ToArray());
        if (wallPos.Count > 0) wallTilemap.SetTiles(wallPos.ToArray(), wallT.ToArray());
        Debug.Log($"Finished applying tiles. Floors: {floorPos.Count}, Walls: {wallPos.Count}");
    }
    private bool IsWallCandidate(int x, int y)
    { /* (Same as before) */
        if (grid == null || !IsCoordInBounds(x, y) || grid[x, y] != TileType.Wall) return false;
        for (int dx = -1; dx <= 1; dx++) { for (int dy = -1; dy <= 1; dy++) { if (dx == 0 && dy == 0) continue; int nx = x + dx; int ny = y + dy; if (IsCoordInBounds(nx, ny) && grid[nx, ny] == TileType.Floor) { return true; } } }
        return false;
    }

    // --- Entity Spawning ---
    private void SpawnEntities()
    { /* (Same as before) */
        if (placedRoomBounds == null || placedRoomBounds.Count == 0) return; Debug.Log("Starting entity spawning..."); bool playerSpawned = false; List<Vector2Int> allFloor = new List<Vector2Int>();
        foreach (var kvp in placedRoomBounds) { allFloor.AddRange(GetFloorTilesInRect(kvp.Value)); }
        if (allFloor.Count == 0) { Debug.LogWarning("No floor tiles found, cannot spawn entities."); return; }
        if (playerPrefab != null) { int idx = pseudoRandom.Next(allFloor.Count); Vector2Int tile = allFloor[idx]; Vector3 pos = GetWorldPosition(tile); Instantiate(playerPrefab, pos, Quaternion.identity, playerHolder); playerSpawned = true; allFloor.RemoveAt(idx); Debug.Log($"Player spawned at {tile}"); }
        int enemiesTotal = enemiesPerRoom * placedRoomBounds.Count; int decorsTotal = decorationsPerRoom * placedRoomBounds.Count;
        SpawnPrefabs(enemyPrefab, enemiesTotal, allFloor, enemiesHolder); SpawnPrefabs(decorationPrefab, decorsTotal, allFloor, decorationsHolder); Debug.Log($"Entity spawning finished. Player Spawned: {playerSpawned}");
    }
    private void SpawnPrefabs(GameObject prefab, int maxCount, List<Vector2Int> availableSpots, Transform parentHolder)
    { /* (Same as before) */
        if (prefab == null || availableSpots == null || parentHolder == null || availableSpots.Count == 0 || maxCount <= 0) return; int numSpawn = Mathf.Min(maxCount, availableSpots.Count); int spawned = 0;
        for (int i = 0; i < numSpawn; i++) { if (availableSpots.Count == 0) break; int idx = pseudoRandom.Next(availableSpots.Count); Vector2Int tile = availableSpots[idx]; availableSpots.RemoveAt(idx); Vector3 pos = GetWorldPosition(tile); Instantiate(prefab, pos, Quaternion.identity, parentHolder); spawned++; }
        Debug.Log($"Spawned {spawned} instances of {prefab.name} into {parentHolder.name}.");
    }


    // --- BSP Tree Logic ---
    private void RunBSPSplit()
    { /* (Same as before) */
        if (bspLeaves == null) bspLeaves = new List<RectInt>(); bspLeaves.Clear(); if (pseudoRandom == null) Initialize();
        RectInt root = new RectInt(1, 1, levelWidth - 2, levelHeight - 2); if (root.width <= 0 || root.height <= 0) { Debug.LogError("Level Width/Height too small for BSP."); return; }
        var q = new Queue<KeyValuePair<RectInt, int>>(); q.Enqueue(new KeyValuePair<RectInt, int>(root, 0));
        while (q.Count > 0) { var pair = q.Dequeue(); RectInt node = pair.Key; int iter = pair.Value; if (iter >= maxIterations || ShouldStopSplitting(node)) { bspLeaves.Add(node); continue; } if (TrySplitNode(node, out RectInt nA, out RectInt nB)) { q.Enqueue(new KeyValuePair<RectInt, int>(nA, iter + 1)); q.Enqueue(new KeyValuePair<RectInt, int>(nB, iter + 1)); } else { bspLeaves.Add(node); } }
        Debug.Log($"BSP split complete. Generated {bspLeaves.Count} leaves.");
    }
    private bool ShouldStopSplitting(RectInt node)
    { /* (Same as before) */
        int minSplitSize = minRoomSize + Mathf.Max(1, (int)roomPadding) * 2; if (node.width < minSplitSize * 2 && node.height < minSplitSize * 2) return true;
        float aspect = node.width <= 0 || node.height <= 0 ? 1f : (float)Mathf.Max(node.width, node.height) / Mathf.Max(1, Mathf.Min(node.width, node.height)); if (aspect > 4.0f) return true; return false;
    }
    private bool TrySplitNode(RectInt node, out RectInt nodeA, out RectInt nodeB)
    { /* (Same as before) */
        nodeA = nodeB = RectInt.zero; bool canH = node.height >= (minRoomSize + (int)roomPadding) * 2 + 1; bool canV = node.width >= (minRoomSize + (int)roomPadding) * 2 + 1; if (!canH && !canV) return false; bool splitH; if (canH && !canV) splitH = true; else if (!canH && canV) splitH = false; else { bool prefH = (node.height > node.width * 1.2f); bool prefV = (node.width > node.height * 1.2f); if (prefH) splitH = true; else if (prefV) splitH = false; else splitH = pseudoRandom.Next(0, 2) == 0; }
        int minSize = minRoomSize + (int)roomPadding; if (splitH) { int minY = node.y + minSize; int maxY = node.yMax - minSize; if (minY >= maxY) return false; int splitY = pseudoRandom.Next(minY, maxY); nodeA = new RectInt(node.x, node.y, node.width, splitY - node.y); nodeB = new RectInt(node.x, splitY, node.width, node.yMax - splitY); }
        else { int minX = node.x + minSize; int maxX = node.xMax - minSize; if (minX >= maxX) return false; int splitX = pseudoRandom.Next(minX, maxX); nodeA = new RectInt(node.x, node.y, splitX - node.x, node.height); nodeB = new RectInt(splitX, node.y, node.xMax - splitX, node.height); }
        return true;
    }


    // --- Utility Functions ---
    private Transform CreateOrFindParent(string parentName)
    { /* (Same as before) */
        Transform parent = transform.Find(parentName); if (parent != null) return parent;
        GameObject go = GameObject.Find(parentName); if (go != null) return go.transform;
        go = new GameObject(parentName); go.transform.SetParent(transform); go.transform.localPosition = Vector3.zero; return go.transform;
    }

    // Destroys children of a specific holder object (usually parented to this generator)
    private void DestroyChildrenOfHolder(string holderName)
    {
        Transform parent = transform.Find(holderName); // Assumes holder is direct child
        if (parent != null)
        {
            int childCount = parent.childCount;
            // Debug.Log($"[DestroyChildrenOfHolder] Found '{holderName}'. Destroying {childCount} children..."); // Optional verbose log
            for (int i = childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (child != null)
                {
                    if (Application.isPlaying) Destroy(child);
                    else Undo.DestroyObjectImmediate(child);
                }
            }
        } // No warning if holder not found, might be intentional
    }

    // Destroys a specific scene object AND its children (used for LevelDesignRoot)
    private void DestroySceneObjectAndChildren(string objectName)
    {
        GameObject parentGO = GameObject.Find(objectName); // Finds anywhere in scene
        if (parentGO != null)
        {
            Transform parent = parentGO.transform;
            int childCount = parent.childCount;
            Debug.Log($"[DestroySceneObjectAndChildren] Found '{objectName}'. Destroying {childCount} children first...");
            for (int i = childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (child != null)
                {
                    if (Application.isPlaying) Destroy(child);
                    else Undo.DestroyObjectImmediate(child);
                }
            }
            // Now destroy the parent object itself
            Debug.Log($"[DestroySceneObjectAndChildren] Destroying the '{objectName}' object itself...");
            if (Application.isPlaying) Destroy(parentGO);
            else Undo.DestroyObjectImmediate(parentGO);
        }
        else
        {
            Debug.LogWarning($"[DestroySceneObjectAndChildren] Object named '{objectName}' not found. Cannot destroy.");
        }
    }

} // --- End of HybridLevelGenerator Class ---