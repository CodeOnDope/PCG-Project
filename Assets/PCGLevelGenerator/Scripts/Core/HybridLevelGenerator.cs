using UnityEngine;

using UnityEngine.Tilemaps;

using System.Collections.Generic;

using System; // Required for System.Random

using System.Linq;

using UnityEditor; // Keep for Undo/EditorUtility



// Wall rotation options for the directional wall tiles

public enum TileRotation

{

    None = 0,       // No rotation

    Clockwise90 = 1,    // 90 degrees clockwise

    Clockwise180 = 2,   // 180 degrees

    Clockwise270 = 3,   // 270 degrees clockwise

    FlipHorizontal = 4, // Flip horizontally

    FlipVertical = 5    // Flip vertically

}



public class HybridLevelGenerator : MonoBehaviour

{

    [Header("--- Generation Mode ---")]

    [Tooltip("Select the generation method:\n" +

             "FullyProcedural: BSP splits, random Rect rooms, procedural corridors.\n" +

             "HybridProcedural: BSP splits, random Templates/L-Shapes/Rects, procedural corridors.\n" +

             "UserDefinedLayout: Reads layout from RoomNode components placed in the scene.")]

    public GenerationMode generationMode = GenerationMode.FullyProcedural;



    [Header("Level Dimensions (Max Bounds)")]

    [Tooltip("Maximum width of the generation grid.")]

    [Range(10, 500)] public int levelWidth = 50;

    [Tooltip("Maximum height of the generation grid.")]

    [Range(10, 500)] public int levelHeight = 50;



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



    [System.Serializable]

    public class DirectionalTile

    {

        public TileBase tile;

        public TileRotation rotation = TileRotation.None;



        public Matrix4x4 GetRotationMatrix()

        {

            switch (rotation)

            {

                case TileRotation.Clockwise90:

                    return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 270), Vector3.one);

                case TileRotation.Clockwise180:

                    return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 180), Vector3.one);

                case TileRotation.Clockwise270:

                    return Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90), Vector3.one);

                case TileRotation.FlipHorizontal:

                    return Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1, 1, 1));

                case TileRotation.FlipVertical:

                    return Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, -1, 1));

                default:

                    return Matrix4x4.identity;

            }

        }

    }



    [Header("Directional Wall Tiles")]

    public bool useDirectionalWalls = false;



    [Tooltip("Wall with floor on the left")]

    public DirectionalTile wallTileLeft = new DirectionalTile();



    [Tooltip("Wall with floor on the right")]

    public DirectionalTile wallTileRight = new DirectionalTile();



    [Tooltip("Wall with floor above")]

    public DirectionalTile wallTileTop = new DirectionalTile();



    [Tooltip("Wall with floor below")]

    public DirectionalTile wallTileBottom = new DirectionalTile();



    [Tooltip("Inner corner with floor on top and left")]

    public DirectionalTile wallTileInnerTopLeft = new DirectionalTile();



    [Tooltip("Inner corner with floor on top and right")]

    public DirectionalTile wallTileInnerTopRight = new DirectionalTile();



    [Tooltip("Inner corner with floor on bottom and left")]

    public DirectionalTile wallTileInnerBottomLeft = new DirectionalTile();



    [Tooltip("Inner corner with floor on bottom and right")]

    public DirectionalTile wallTileInnerBottomRight = new DirectionalTile();



    [Tooltip("Outer corner at top-left edge of room")]

    public DirectionalTile wallTileOuterTopLeft = new DirectionalTile();



    [Tooltip("Outer corner at top-right edge of room")]

    public DirectionalTile wallTileOuterTopRight = new DirectionalTile();



    [Tooltip("Outer corner at bottom-left edge of room")]

    public DirectionalTile wallTileOuterBottomLeft = new DirectionalTile();



    [Tooltip("Outer corner at bottom-right edge of room")]

    public DirectionalTile wallTileOuterBottomRight = new DirectionalTile();



    [Header("Tile Variations")]

    [Tooltip("Additional floor tiles to randomly use during generation.")]

    public List<TileBase> floorTileVariants = new List<TileBase>();

    [Tooltip("Additional wall tiles to randomly use during generation.")]

    public List<TileBase> wallTileVariants = new List<TileBase>();

    [Range(0f, 1f)]

    [Tooltip("Chance to use a variant tile instead of the main tile (0-1).")]

    public float variantTileChance = 0.4f;



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

    private Dictionary<TileBase, TileType> tileTypeCache = new Dictionary<TileBase, TileType>();

    private HashSet<string> reportedUnknownTiles = new HashSet<string>();



    // Parent Transform References

    private Transform playerHolder;

    private Transform enemiesHolder;

    private Transform decorationsHolder;



    // --- Wall Type Enum (13 types total) ---

    private enum WallType

    {

        Default,

        Bottom,    // Sprite #1

        Top,       // Sprite #2 

        Right,     // Sprite #3

        Left,      // Sprite #4

        InnerTopLeft,     // Sprite #5

        InnerTopRight,    // Sprite #6

        InnerBottomLeft,  // Sprite #7

        InnerBottomRight, // Sprite #8

        OuterTopLeft,     // Sprite #9

        OuterTopRight,    // Sprite #10

        OuterBottomLeft,  // Sprite #11

        OuterBottomRight  // Sprite #12

    }



    #region Public Methods



    [ContextMenu("Generate Level")]

    public void GenerateLevel()

    {

        Debug.Log("CUSTOM LOG: Generate Level called with tile variants!");

        GenerateLevel(false); // Default context menu call clears based on mode

    }



    // Helper method for clearing only generated content (Tiles, Entities, internal data)

    private void RemoveExistingOuterCornerTiles()

    {

        for (int x = 0; x < levelWidth; x++)

        {

            for (int y = 0; y < levelHeight; y++)

            {

                Vector3Int pos = new Vector3Int(x, y, 0);

                TileBase tile = wallTilemap.GetTile(pos);



                // Remove any tiles that match outer corner tiles

                if (tile == wallTileOuterTopLeft.tile ||

                    tile == wallTileOuterTopRight.tile ||

                    tile == wallTileOuterBottomLeft.tile ||

                    tile == wallTileOuterBottomRight.tile)

                {

                    wallTilemap.SetTile(pos, null);

                }

            }

        }

    }

    public void AddMissingCornerTiles()

    {

        Debug.Log("AddMissingCornerTiles: Starting room corner tile placement...");



        // First remove any existing outer corner tiles

        RemoveExistingOuterCornerTiles();



        if (wallTileOuterTopLeft.tile == null || wallTileOuterTopRight.tile == null ||

            wallTileOuterBottomLeft.tile == null || wallTileOuterBottomRight.tile == null)

        {

            Debug.LogWarning("AddMissingCornerTiles: Some outer corner tiles are not assigned!");

            return;

        }



        if (placedRoomBounds == null || placedRoomBounds.Count == 0)

        {

            Debug.LogWarning("AddMissingCornerTiles: No room bounds found!");

            return;

        }



        int cornersPlaced = 0;



        // For each room, properly place corner tiles

        foreach (var roomBounds in placedRoomBounds.Values)

        {

            // Calculate wall corner positions (outside the floor area)

            // These are the correct positions as shown by the arrows in your image

            Vector3Int topLeft = new Vector3Int(roomBounds.xMin - 1, roomBounds.yMax, 0);

            Vector3Int topRight = new Vector3Int(roomBounds.xMax, roomBounds.yMax, 0);

            Vector3Int bottomLeft = new Vector3Int(roomBounds.xMin - 1, roomBounds.yMin - 1, 0);

            Vector3Int bottomRight = new Vector3Int(roomBounds.xMax, roomBounds.yMin - 1, 0);



            // Place corner tiles at these exact positions

            if (IsCoordInBounds(topLeft.x, topLeft.y))

            {

                wallTilemap.SetTile(topLeft, wallTileOuterTopLeft.tile);

                if (wallTileOuterTopLeft.rotation != TileRotation.None)

                    wallTilemap.SetTransformMatrix(topLeft, wallTileOuterTopLeft.GetRotationMatrix());

                cornersPlaced++;

            }



            if (IsCoordInBounds(topRight.x, topRight.y))

            {

                wallTilemap.SetTile(topRight, wallTileOuterTopRight.tile);

                if (wallTileOuterTopRight.rotation != TileRotation.None)

                    wallTilemap.SetTransformMatrix(topRight, wallTileOuterTopRight.GetRotationMatrix());

                cornersPlaced++;

            }



            if (IsCoordInBounds(bottomLeft.x, bottomLeft.y))

            {

                wallTilemap.SetTile(bottomLeft, wallTileOuterBottomLeft.tile);

                if (wallTileOuterBottomLeft.rotation != TileRotation.None)

                    wallTilemap.SetTransformMatrix(bottomLeft, wallTileOuterBottomLeft.GetRotationMatrix());

                cornersPlaced++;

            }



            if (IsCoordInBounds(bottomRight.x, bottomRight.y))

            {

                wallTilemap.SetTile(bottomRight, wallTileOuterBottomRight.tile);

                if (wallTileOuterBottomRight.rotation != TileRotation.None)

                    wallTilemap.SetTransformMatrix(bottomRight, wallTileOuterBottomRight.GetRotationMatrix());

                cornersPlaced++;

            }

        }



        Debug.Log($"AddMissingCornerTiles: Placed {cornersPlaced} corner tiles at room corners");

    }





    private bool IsRoomCorner(int x, int y, bool isTop, bool isLeft)

    {

        // This should only be called for actual room corners, so we're checking

        // if this position is actually a floor tile (inside the room)

        if (!IsCoordInBounds(x, y) || grid[x, y] != TileType.Floor)

            return false;



        // Get the positions we need to check

        int wallX = isLeft ? x - 1 : x + 1;

        int wallY = isTop ? y + 1 : y - 1;

        int cornerX = isLeft ? x - 1 : x + 1;

        int cornerY = isTop ? y + 1 : y - 1;



        // Check if we have walls in the appropriate directions

        bool hasWallSide1 = IsCoordInBounds(wallX, y) && grid[wallX, y] == TileType.Wall;

        bool hasWallSide2 = IsCoordInBounds(x, wallY) && grid[x, wallY] == TileType.Wall;



        // A valid room corner has walls on both adjacent sides

        return hasWallSide1 && hasWallSide2;

    }





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



    #endregion



    #region Initialization and Setup



    private void Initialize()

    {

        if (useRandomSeed || seed == 0)

        {

            // Use current time + a random offset to ensure different seeds each time

            seed = Environment.TickCount + UnityEngine.Random.Range(1, 10000);

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



    #endregion



    #region Generation Pipelines



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



    #endregion



    #region Room Creation Helpers



    private void CreateRoomsInLeaves_Procedural()

    {

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

    {

        if (bspLeaves == null) { Debug.LogError("BSP leaves not generated!"); return; }

        if (placedRoomBounds == null) placedRoomBounds = new Dictionary<string, RectInt>();

        placedRoomBounds.Clear();

        for (int i = 0; i < bspLeaves.Count; i++)

        {

            RectInt leaf = bspLeaves[i]; RectInt roomBounds = RectInt.zero; bool roomCreated = false; string roomId = $"Room_{i}";

            float effectiveTemplateProb = Mathf.Min(1.0f, roomTemplateProbability * 1.5f); // Boost by 50%

            if (roomTemplatePrefabs != null && roomTemplatePrefabs.Count > 0 && pseudoRandom.NextDouble() < effectiveTemplateProb)

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

    {

        roomRect = RectInt.zero; int padding = Mathf.Max(0, (int)roomPadding); int maxW = leaf.width - (2 * padding); int maxH = leaf.height - (2 * padding);

        if (maxW < minRoomSize || maxH < minRoomSize) return false;

        int w = pseudoRandom.Next(minRoomSize, maxW + 1); int h = pseudoRandom.Next(minRoomSize, maxH + 1);

        int x = leaf.x + padding + pseudoRandom.Next(0, maxW - w + 1); int y = leaf.y + padding + pseudoRandom.Next(0, maxH - h + 1);

        roomRect = new RectInt(x, y, w, h); return IsRectWithinBounds(roomRect);

    }



    private bool TryCreateLShapeInLeaf(RectInt leaf, out RectInt stemRect, out RectInt legRect, out RectInt overallBounds)

    {

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



    // Located at line 614 in your code
    private bool TryPlaceRoomTemplate(RectInt leaf, string roomId, out RectInt placedBounds)
    {
        placedBounds = RectInt.zero;

        // Initial validation
        if (roomTemplatePrefabs == null || roomTemplatePrefabs.Count == 0)
        {
            Debug.LogWarning($"No room template prefabs assigned or prefab list is empty.");
            return false;
        }

        // Get available templates sorted by size (smallest to largest) to try smaller ones first
        List<GameObject> sortedTemplates = new List<GameObject>(roomTemplatePrefabs);
        sortedTemplates.RemoveAll(p => p == null); // Filter out any null entries

        if (sortedTemplates.Count == 0)
        {
            Debug.LogWarning($"All room template prefabs are null references.");
            return false;
        }

        // Get dimensions of available templates and sort by area
        List<(GameObject prefab, int width, int height, int area)> templateSizes = new List<(GameObject, int, int, int)>();
        foreach (var prefab in sortedTemplates)
        {
            if (GetTemplateDimensions(prefab, out int tW, out int tH))
            {
                templateSizes.Add((prefab, tW, tH, tW * tH));
            }
        }

        // Sort by area (smallest first)
        templateSizes.Sort((a, b) => a.area.CompareTo(b.area));

        // Calculate available space in leaf with padding
        int padding = Mathf.Max(0, (int)roomPadding);
        int availW = leaf.width - (2 * padding);
        int availH = leaf.height - (2 * padding);

        // Try to place each template in order of size
        foreach (var template in templateSizes)
        {
            // Skip if template is too large for the leaf
            if (template.width > availW || template.height > availH)
                continue;

            // Calculate possible placement positions
            int possibleX = availW - template.width + 1;
            int possibleY = availH - template.height + 1;

            // If there are valid positions, try to place the template
            if (possibleX > 0 && possibleY > 0)
            {
                int offX = pseudoRandom.Next(0, possibleX);
                int offY = pseudoRandom.Next(0, possibleY);
                int startX = leaf.x + padding + offX;
                int startY = leaf.y + padding + offY;

                // Attempt placement
                if (PlaceSpecificRoomTemplate(template.prefab, new Vector2Int(startX, startY), out placedBounds))
                {
                    // THIS IS THE CRITICAL LINE THAT WAS MISSING:
                    placedRoomBounds[roomId] = placedBounds;

                    Debug.Log($"Successfully placed template '{template.prefab.name}' in room {roomId}. Size: {template.width}x{template.height}");
                    return true;
                }
            }
        }

        Debug.LogWarning($"Failed to place any template in room {roomId}. Available space: {availW}x{availH}");
        return false;
    }





    private bool PlaceSpecificRoomTemplate(GameObject templatePrefab, Vector2Int targetGridBottomLeft, out RectInt placedBounds)

    {

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

    {

        width = 0; height = 0; if (templatePrefab == null) return false; GameObject temp = null;

        try

        {

            temp = Instantiate(templatePrefab); temp.SetActive(false); Tilemap tm = temp.GetComponentInChildren<Tilemap>(true); if (tm == null) return false; tm.CompressBounds(); BoundsInt bounds = tm.cellBounds; width = bounds.size.x; height = bounds.size.y; return width > 0 && height > 0;

        }

        catch { return false; }

        finally { if (temp != null) { if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp); } }

    }



    private bool TryCreateLShapeAt(Vector2Int centerPos, Vector2Int size, out RectInt stemRect, out RectInt legRect, out RectInt overallBounds)

    {

        stemRect = legRect = overallBounds = RectInt.zero; int minD = 1; if (size.x < minD * 2 || size.y < minD * 2) return false;

        int sW = size.x; int sH = Mathf.Max(minD, Mathf.RoundToInt(size.y * 0.7f)); int lH = Mathf.Max(minD, size.y - sH); int lW = Mathf.Max(minD, Mathf.RoundToInt(size.x * 0.6f));

        int sX = centerPos.x - sW / 2; int sY = centerPos.y - size.y / 2; stemRect = new RectInt(sX, sY, sW, sH);

        int lX = stemRect.xMax - lW; int lY = stemRect.yMax; legRect = new RectInt(lX, lY, lW, lH);

        if (!IsRectWithinBounds(stemRect) || !IsRectWithinBounds(legRect)) return false;

        int minX = Mathf.Min(stemRect.xMin, legRect.xMin); int minY = Mathf.Min(stemRect.yMin, legRect.yMin); int maxX = Mathf.Max(stemRect.xMax, legRect.xMax); int maxY = Mathf.Max(stemRect.yMax, legRect.yMax); overallBounds = new RectInt(minX, minY, maxX - minX, maxY - minY); return true;

    }



    #endregion



    #region Corridor Connection



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

    {

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

    {

        Vector2Int cA = Vector2Int.RoundToInt(roomA.center); Vector2Int cB = Vector2Int.RoundToInt(roomB.center);

        if (pseudoRandom.Next(0, 2) == 0) { CarveCorridorSegment(cA.x, cB.x, cA.y, true); CarveCorridorSegment(cA.y, cB.y, cB.x, false); }

        else { CarveCorridorSegment(cA.y, cB.y, cA.x, false); CarveCorridorSegment(cA.x, cB.x, cB.y, true); }

    }



    private void CarveCorridorSegment(int startCoord, int endCoord, int fixedCoord, bool isHorizontal)

    {

        if (grid == null) return; int min = Mathf.Min(startCoord, endCoord); int max = Mathf.Max(startCoord, endCoord); int halfW = corridorWidth <= 1 ? 0 : (corridorWidth - 1) / 2;

        for (int i = min; i <= max; i++) { for (int w = -halfW; w <= halfW; w++) { int x, y; if (isHorizontal) { x = i; y = fixedCoord + w; } else { x = fixedCoord + w; y = i; } if (IsCoordInBounds(x, y)) { grid[x, y] = TileType.Floor; } } }

    }



    #endregion



    #region Tile Placement & Grid Utilities



    private void CarveRectangle(RectInt rect, TileType tile)

    {

        if (grid == null) return; for (int x = rect.xMin; x < rect.xMax; x++) { for (int y = rect.yMin; y < rect.yMax; y++) { if (IsCoordInBounds(x, y)) { grid[x, y] = tile; } } }

    }



    private List<Vector2Int> GetFloorTilesInRect(RectInt rect)

    {

        List<Vector2Int> tiles = new List<Vector2Int>(); if (grid == null) return tiles; int sX = Mathf.Max(rect.xMin, 0); int eX = Mathf.Min(rect.xMax, levelWidth); int sY = Mathf.Max(rect.yMin, 0); int eY = Mathf.Min(rect.yMax, levelHeight);

        for (int x = sX; x < eX; x++) { for (int y = sY; y < eY; y++) { if (grid[x, y] == TileType.Floor) { tiles.Add(new Vector2Int(x, y)); } } }

        return tiles;

    }



    private Vector3 GetWorldPosition(Vector2Int gridPos)

    {

        if (groundTilemap != null) { return groundTilemap.CellToWorld((Vector3Int)gridPos) + groundTilemap.cellSize * 0.5f; }

        else { Debug.LogWarning("Ground Tilemap missing, using fallback world pos calc."); return new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0); }

    }



    private bool IsCoordInBounds(int x, int y)

    {

        return x >= 0 && x < levelWidth && y >= 0 && y < levelHeight;

    }



    private bool IsRectWithinBounds(RectInt rect)

    {

        return rect.xMin >= 0 && rect.xMax <= levelWidth && rect.yMin >= 0 && rect.yMax <= levelHeight;

    }



    // --- Improved Wall Type Detection Method that matches the sprite numbering ---

    // Replace ONLY this method in your HybridLevelGenerator.cs file



    // Update the DetermineWallType method in your HybridLevelGenerator.cs file

    // This improves the outer corner detection logic



    // Replace the DetermineWallType method in your HybridLevelGenerator.cs file

    // This provides more precise corner detection



    // Replace the DetermineWallType method in your HybridLevelGenerator.cs file

    // This provides the most accurate corner detection



    // First, improve the wall type detection to better handle corridor junctions

    // First, improve the wall type detection to better handle corridor junctions

    private WallType DetermineWallType(int x, int y)

    {

        // Check surrounding cells for floor and wall tiles

        bool hasFloorLeft = IsCoordInBounds(x - 1, y) && grid[x - 1, y] == TileType.Floor;

        bool hasFloorRight = IsCoordInBounds(x + 1, y) && grid[x + 1, y] == TileType.Floor;

        bool hasFloorTop = IsCoordInBounds(x, y + 1) && grid[x, y + 1] == TileType.Floor;

        bool hasFloorBottom = IsCoordInBounds(x, y - 1) && grid[x, y - 1] == TileType.Floor;



        // Count adjacent floor tiles for junction detection

        int floorCount = 0;

        if (hasFloorLeft) floorCount++;

        if (hasFloorRight) floorCount++;

        if (hasFloorTop) floorCount++;

        if (hasFloorBottom) floorCount++;



        // INNER CORNERS - Two adjacent floors in an L shape

        if (hasFloorLeft && hasFloorTop && !hasFloorRight && !hasFloorBottom)

            return WallType.InnerTopLeft;

        if (hasFloorRight && hasFloorTop && !hasFloorLeft && !hasFloorBottom)

            return WallType.InnerTopRight;

        if (hasFloorLeft && hasFloorBottom && !hasFloorRight && !hasFloorTop)

            return WallType.InnerBottomLeft;

        if (hasFloorRight && hasFloorBottom && !hasFloorLeft && !hasFloorTop)

            return WallType.InnerBottomRight;



        // STRAIGHT WALLS - Floor on one side

        if (hasFloorLeft && !hasFloorRight && !hasFloorTop && !hasFloorBottom)

            return WallType.Left;

        if (hasFloorRight && !hasFloorLeft && !hasFloorTop && !hasFloorBottom)

            return WallType.Right;

        if (hasFloorTop && !hasFloorLeft && !hasFloorRight && !hasFloorBottom)

            return WallType.Top;

        if (hasFloorBottom && !hasFloorLeft && !hasFloorRight && !hasFloorTop)

            return WallType.Bottom;



        // CORRIDOR JUNCTIONS - Handle T-junctions and other multi-floor cases

        if (floorCount >= 2)

        {

            // T-junction cases

            if (hasFloorLeft && hasFloorRight && hasFloorBottom && !hasFloorTop)

                return WallType.Bottom;

            if (hasFloorLeft && hasFloorRight && hasFloorTop && !hasFloorBottom)

                return WallType.Top;

            if (hasFloorLeft && hasFloorTop && hasFloorBottom && !hasFloorRight)

                return WallType.Left;

            if (hasFloorRight && hasFloorTop && hasFloorBottom && !hasFloorLeft)

                return WallType.Right;



            // Determine best match for other cases with multiple floors

            if (!hasFloorTop) return WallType.Bottom;

            if (!hasFloorBottom) return WallType.Top;

            if (!hasFloorLeft) return WallType.Right;

            if (!hasFloorRight) return WallType.Left;

        }



        // Use simple outer corner detection from surrounding walls

        bool hasWallLeft = IsCoordInBounds(x - 1, y) && grid[x - 1, y] == TileType.Wall;

        bool hasWallRight = IsCoordInBounds(x + 1, y) && grid[x + 1, y] == TileType.Wall;

        bool hasWallTop = IsCoordInBounds(x, y + 1) && grid[x, y + 1] == TileType.Wall;

        bool hasWallBottom = IsCoordInBounds(x, y - 1) && grid[x, y - 1] == TileType.Wall;



        // OUTER CORNERS - Detect based on adjacent walls    

        if (hasWallLeft && hasWallTop) return WallType.OuterTopLeft;

        if (hasWallRight && hasWallTop) return WallType.OuterTopRight;

        if (hasWallLeft && hasWallBottom) return WallType.OuterBottomLeft;

        if (hasWallRight && hasWallBottom) return WallType.OuterBottomRight;



        // Default case

        return WallType.Default;

    }





    private void FixCorridorJunctions()

    {

        for (int x = 1; x < levelWidth - 1; x++)

        {

            for (int y = 1; y < levelHeight - 1; y++)

            {

                if (grid[x, y] == TileType.Wall)

                {

                    // Count adjacent floor tiles

                    int floorCount = 0;

                    bool hasFloorLeft = IsCoordInBounds(x - 1, y) && grid[x - 1, y] == TileType.Floor;

                    bool hasFloorRight = IsCoordInBounds(x + 1, y) && grid[x + 1, y] == TileType.Floor;

                    bool hasFloorTop = IsCoordInBounds(x, y + 1) && grid[x, y + 1] == TileType.Floor;

                    bool hasFloorBottom = IsCoordInBounds(x, y - 1) && grid[x, y - 1] == TileType.Floor;



                    if (hasFloorLeft) floorCount++;

                    if (hasFloorRight) floorCount++;

                    if (hasFloorTop) floorCount++;

                    if (hasFloorBottom) floorCount++;



                    // Only fix walls with 2 or more adjacent floor tiles

                    if (floorCount >= 2)

                    {

                        // Get the proper wall type for this junction

                        WallType wallType = DetermineWallType(x, y);

                        TileBase tile;

                        Matrix4x4 transform;

                        GetDirectionalTileWithTransform(wallType, out tile, out transform);



                        // Apply the correct tile

                        Vector3Int pos = new Vector3Int(x, y, 0);

                        wallTilemap.SetTile(pos, tile);

                        if (transform != Matrix4x4.identity)

                        {

                            wallTilemap.SetTransformMatrix(pos, transform);

                        }

                    }

                }

            }

        }

    }



    private bool IsCorridorJunction(int x, int y)

    {

        // Count adjacent floor tiles

        int floorCount = 0;

        if (IsCoordInBounds(x - 1, y) && grid[x - 1, y] == TileType.Floor) floorCount++;

        if (IsCoordInBounds(x + 1, y) && grid[x + 1, y] == TileType.Floor) floorCount++;

        if (IsCoordInBounds(x, y - 1) && grid[x, y - 1] == TileType.Floor) floorCount++;

        if (IsCoordInBounds(x, y + 1) && grid[x, y + 1] == TileType.Floor) floorCount++;



        // Junction typically has 2 or 3 adjacent floor tiles

        return floorCount >= 2 && grid[x, y] == TileType.Wall;

    }



    private void ApplyJunctionWallTile(int x, int y)

    {

        // Determine the type of junction

        WallType wallType = DetermineWallType(x, y);

        TileBase tile;

        Matrix4x4 transform;



        GetDirectionalTileWithTransform(wallType, out tile, out transform);



        // Apply the tile

        Vector3Int pos = new Vector3Int(x, y, 0);

        wallTilemap.SetTile(pos, tile);



        if (transform != Matrix4x4.identity)

        {

            wallTilemap.SetTransformMatrix(pos, transform);

        }

    }



    // --- Get Directional Tile with Rotation Support ---

    // Replace this method in your HybridLevelGenerator.cs file



    // --- Get Directional Tile with Rotation Support ---

    // Replace this method in HybridLevelGenerator.cs

    // This fixes the inverted mapping between detected wall types and assigned tiles



    // Replace this method in HybridLevelGenerator.cs

    // This fixes the inverted mapping for ALL tile types



    // Update the GetDirectionalTileWithTransform method in your HybridLevelGenerator.cs file

    // This ensures all corner types have the correct mapping



    private void GetDirectionalTileWithTransform(WallType wallType, out TileBase tile, out Matrix4x4 transform)

    {

        transform = Matrix4x4.identity;

        tile = wallTile;



        // IMPORTANT: Direct mapping based on your inspector setup

        // DO NOT invert the mappings anymore

        switch (wallType)

        {

            // Basic Wall Directions - DIRECT MAPPING

            case WallType.Left:

                tile = wallTileLeft.tile;

                transform = wallTileLeft.GetRotationMatrix();

                Debug.Log($"LEFT wall - using LEFT tile: {tile?.name ?? "null"}");

                break;

            case WallType.Right:

                tile = wallTileRight.tile;

                transform = wallTileRight.GetRotationMatrix();

                Debug.Log($"RIGHT wall - using RIGHT tile: {tile?.name ?? "null"}");

                break;

            case WallType.Top:

                tile = wallTileTop.tile;

                transform = wallTileTop.GetRotationMatrix();

                Debug.Log($"TOP wall - using TOP tile: {tile?.name ?? "null"}");

                break;

            case WallType.Bottom:

                tile = wallTileBottom.tile;

                transform = wallTileBottom.GetRotationMatrix();

                Debug.Log($"BOTTOM wall - using BOTTOM tile: {tile?.name ?? "null"}");

                break;



            // Inner Corners - DIRECT MAPPING

            case WallType.InnerTopLeft:

                tile = wallTileInnerTopLeft.tile;

                transform = wallTileInnerTopLeft.GetRotationMatrix();

                break;

            case WallType.InnerTopRight:

                tile = wallTileInnerTopRight.tile;

                transform = wallTileInnerTopRight.GetRotationMatrix();

                break;

            case WallType.InnerBottomLeft:

                tile = wallTileInnerBottomLeft.tile;

                transform = wallTileInnerBottomLeft.GetRotationMatrix();

                break;

            case WallType.InnerBottomRight:

                tile = wallTileInnerBottomRight.tile;

                transform = wallTileInnerBottomRight.GetRotationMatrix();

                break;



            // Outer Corners - DIRECT MAPPING

            case WallType.OuterTopLeft:

                tile = wallTileOuterTopLeft.tile;

                transform = wallTileOuterTopLeft.GetRotationMatrix();

                break;

            case WallType.OuterTopRight:

                tile = wallTileOuterTopRight.tile;

                transform = wallTileOuterTopRight.GetRotationMatrix();

                break;

            case WallType.OuterBottomLeft:

                tile = wallTileOuterBottomLeft.tile;

                transform = wallTileOuterBottomLeft.GetRotationMatrix();

                break;

            case WallType.OuterBottomRight:

                tile = wallTileOuterBottomRight.tile;

                transform = wallTileOuterBottomRight.GetRotationMatrix();

                break;



            default:

                // Default to standard wall tile

                Debug.LogWarning($"No specific mapping for wall type {wallType}, using default wall tile");

                break;

        }



        // Print wall tile name for debugging

        if (tile != null)

        {

            Debug.Log($"Applied tile {tile.name} for wall type {wallType}");

        }

        else

        {

            Debug.LogWarning($"Null tile for wall type {wallType}, using default wall tile");

            tile = wallTile;

        }

    }



    #endregion



    #region Tilemap Application



    private void PlaceDirectionalCornerTiles()

    {

        if (wallTileOuterTopLeft.tile == null || wallTileOuterTopRight.tile == null ||

            wallTileOuterBottomLeft.tile == null || wallTileOuterBottomRight.tile == null)

        {

            Debug.LogWarning("Cannot place corner tiles: some outer corner tiles are not assigned!");

            return;

        }



        int cornersPlaced = 0;



        foreach (var roomBounds in placedRoomBounds.Values)

        {

            // Define exact corner positions

            Vector3Int topLeft = new Vector3Int(roomBounds.xMin - 1, roomBounds.yMax, 0);

            Vector3Int topRight = new Vector3Int(roomBounds.xMax, roomBounds.yMax, 0);

            Vector3Int bottomLeft = new Vector3Int(roomBounds.xMin - 1, roomBounds.yMin - 1, 0);

            Vector3Int bottomRight = new Vector3Int(roomBounds.xMax, roomBounds.yMin - 1, 0);



            // Place top-left corner

            if (IsCoordInBounds(topLeft.x, topLeft.y))

            {

                wallTilemap.SetTile(topLeft, wallTileOuterTopLeft.tile);

                if (wallTileOuterTopLeft.rotation != TileRotation.None)

                    wallTilemap.SetTransformMatrix(topLeft, wallTileOuterTopLeft.GetRotationMatrix());

                cornersPlaced++;

            }



            // Place top-right corner

            if (IsCoordInBounds(topRight.x, topRight.y))

            {

                wallTilemap.SetTile(topRight, wallTileOuterTopRight.tile);

                if (wallTileOuterTopRight.rotation != TileRotation.None)

                    wallTilemap.SetTransformMatrix(topRight, wallTileOuterTopRight.GetRotationMatrix());

                cornersPlaced++;

            }



            // Place bottom-left corner

            if (IsCoordInBounds(bottomLeft.x, bottomLeft.y))

            {

                wallTilemap.SetTile(bottomLeft, wallTileOuterBottomLeft.tile);

                if (wallTileOuterBottomLeft.rotation != TileRotation.None)

                    wallTilemap.SetTransformMatrix(bottomLeft, wallTileOuterBottomLeft.GetRotationMatrix());

                cornersPlaced++;

            }



            // Place bottom-right corner

            if (IsCoordInBounds(bottomRight.x, bottomRight.y))

            {

                wallTilemap.SetTile(bottomRight, wallTileOuterBottomRight.tile);

                if (wallTileOuterBottomRight.rotation != TileRotation.None)

                    wallTilemap.SetTransformMatrix(bottomRight, wallTileOuterBottomRight.GetRotationMatrix());

                cornersPlaced++;

            }

        }



        Debug.Log($"Placed {cornersPlaced} directional corner tiles");

    }



    private void PlaceRegularCornerTiles()

    {

        int cornersPlaced = 0;



        foreach (var roomBounds in placedRoomBounds.Values)

        {

            // Define exact corner positions

            Vector3Int topLeft = new Vector3Int(roomBounds.xMin - 1, roomBounds.yMax, 0);

            Vector3Int topRight = new Vector3Int(roomBounds.xMax, roomBounds.yMax, 0);

            Vector3Int bottomLeft = new Vector3Int(roomBounds.xMin - 1, roomBounds.yMin - 1, 0);

            Vector3Int bottomRight = new Vector3Int(roomBounds.xMax, roomBounds.yMin - 1, 0);



            // Array of corner positions

            Vector3Int[] cornerPositions = { topLeft, topRight, bottomLeft, bottomRight };



            foreach (var cornerPos in cornerPositions)

            {

                if (IsCoordInBounds(cornerPos.x, cornerPos.y))

                {

                    // Use regular wall tile or variant

                    TileBase selectedTile = wallTile;

                    if (wallTileVariants != null && wallTileVariants.Count > 0 &&

                        pseudoRandom.NextDouble() < variantTileChance)

                    {

                        selectedTile = wallTileVariants[pseudoRandom.Next(wallTileVariants.Count)];

                    }



                    wallTilemap.SetTile(cornerPos, selectedTile);

                    cornersPlaced++;

                }

            }

        }



        Debug.Log($"Placed {cornersPlaced} regular wall tiles at corners");

    }





    private void ApplyTilesToTilemap()

    {

        // Early validation checks

        if (groundTilemap == null || wallTilemap == null || floorTile == null || wallTile == null)

        {

            Debug.LogError("Required Tilemaps or Tiles are not assigned!");

            return;

        }



        // Clearing existing tiles

        groundTilemap.ClearAllTiles();

        wallTilemap.ClearAllTiles();



        // Create dictionaries for optimized batching

        Dictionary<Vector3Int, TileBase> floorTilesToPlace = new Dictionary<Vector3Int, TileBase>();

        Dictionary<Vector3Int, TileBase> wallTilesToPlace = new Dictionary<Vector3Int, TileBase>();

        Dictionary<Vector3Int, Matrix4x4> wallTileTransforms = new Dictionary<Vector3Int, Matrix4x4>();



        // First pass - identify wall types

        Dictionary<Vector2Int, WallType> wallTypes = new Dictionary<Vector2Int, WallType>();

        for (int x = 0; x < levelWidth; x++)

        {

            for (int y = 0; y < levelHeight; y++)

            {

                if (grid[x, y] == TileType.Wall && IsWallCandidate(x, y))

                {

                    Vector2Int pos = new Vector2Int(x, y);

                    WallType type = DetermineWallType(x, y);

                    wallTypes[pos] = type;

                }

            }

        }



        // Second pass - prepare tiles with transforms

        Vector3Int currentPos = Vector3Int.zero;

        for (int x = 0; x < levelWidth; x++)

        {

            for (int y = 0; y < levelHeight; y++)

            {

                currentPos.x = x;

                currentPos.y = y;

                TileType tileType = grid[x, y];



                // Skip level borders

                if (x <= 0 || x >= levelWidth - 1 || y <= 0 || y >= levelHeight - 1)

                    continue;



                if (tileType == TileType.Floor)

                {

                    // Floor tile selection

                    TileBase selectedTile = floorTile;

                    if (floorTileVariants != null && floorTileVariants.Count > 0 &&

                        pseudoRandom.NextDouble() < variantTileChance)

                    {

                        selectedTile = floorTileVariants[pseudoRandom.Next(floorTileVariants.Count)];

                    }

                    floorTilesToPlace[currentPos] = selectedTile;

                }

                else if (tileType == TileType.Wall && IsWallCandidate(x, y))

                {

                    // Skip corners for now if we're using directional walls

                    bool isCorner = false;

                    if (useDirectionalWalls)

                    {

                        foreach (var roomBounds in placedRoomBounds.Values)

                        {

                            // Check if this position is one of the four corner positions

                            if ((x == roomBounds.xMin - 1 && y == roomBounds.yMax) ||    // Top-Left

                                (x == roomBounds.xMax && y == roomBounds.yMax) ||        // Top-Right

                                (x == roomBounds.xMin - 1 && y == roomBounds.yMin - 1) || // Bottom-Left

                                (x == roomBounds.xMax && y == roomBounds.yMin - 1))      // Bottom-Right

                            {

                                isCorner = true;

                                break;

                            }

                        }

                    }



                    if (useDirectionalWalls && isCorner)

                        continue; // Skip corners for now when DWT is ON



                    TileBase selectedTile = wallTile;

                    Matrix4x4 tileTransform = Matrix4x4.identity;



                    // Wall tile selection with directional support

                    if (useDirectionalWalls && wallTypes.TryGetValue(new Vector2Int(x, y), out WallType wallType))

                    {

                        GetDirectionalTileWithTransform(wallType, out selectedTile, out tileTransform);

                    }

                    else if (wallTileVariants != null && wallTileVariants.Count > 0 &&

                             pseudoRandom.NextDouble() < variantTileChance)

                    {

                        selectedTile = wallTileVariants[pseudoRandom.Next(wallTileVariants.Count)];

                    }



                    wallTilesToPlace[currentPos] = selectedTile;



                    // Store transform if not identity

                    if (tileTransform != Matrix4x4.identity)

                    {

                        wallTileTransforms[currentPos] = tileTransform;

                    }

                }

            }

        }



        // Apply tiles to tilemaps

        foreach (var kvp in floorTilesToPlace)

        {

            groundTilemap.SetTile(kvp.Key, kvp.Value);

        }



        foreach (var kvp in wallTilesToPlace)

        {

            wallTilemap.SetTile(kvp.Key, kvp.Value);



            // Apply transform if exists

            if (wallTileTransforms.TryGetValue(kvp.Key, out Matrix4x4 transform))

            {

                wallTilemap.SetTransformMatrix(kvp.Key, transform);

            }

        }



        // Handle corners based on DWT setting

        if (useDirectionalWalls)

        {

            // Place special corner tiles when DWT is ON

            AddMissingCornerTiles();



            // Fix corridor junctions

            FixCorridorJunctions();

        }

        else

        {

            // Place regular wall tiles at corners when DWT is OFF

            PlaceRegularCornerTiles();

        }



        Debug.Log($"Finished applying tiles. Floors: {floorTilesToPlace.Count}, Walls: {wallTilesToPlace.Count}");

    }



    // Update the IsWallCandidate method in your HybridLevelGenerator.cs file

    // This ensures all wall types, including corners, are properly detected



    // Replace the IsWallCandidate method in your HybridLevelGenerator.cs file

    // This uses a simpler approach to avoid detecting too many corners



    // Replace the IsWallCandidate method in your HybridLevelGenerator.cs file



    private bool IsWallCandidate(int x, int y)

    {

        // Skip if not in bounds or not a wall

        if (!IsCoordInBounds(x, y) || grid[x, y] != TileType.Wall)

            return false;



        // Skip level borders

        if (x <= 0 || x >= levelWidth - 1 || y <= 0 || y >= levelHeight - 1)

            return false;



        // Check for adjacent floor tiles

        bool hasAdjacentFloor =

            (IsCoordInBounds(x - 1, y) && grid[x - 1, y] == TileType.Floor) ||

            (IsCoordInBounds(x + 1, y) && grid[x + 1, y] == TileType.Floor) ||

            (IsCoordInBounds(x, y - 1) && grid[x, y - 1] == TileType.Floor) ||

            (IsCoordInBounds(x, y + 1) && grid[x, y + 1] == TileType.Floor);



        // Also include corner positions

        bool isCornerPosition = false;

        foreach (var roomBounds in placedRoomBounds.Values)

        {

            if ((x == roomBounds.xMin - 1 && y == roomBounds.yMax) ||    // Top-Left

                (x == roomBounds.xMax && y == roomBounds.yMax) ||        // Top-Right

                (x == roomBounds.xMin - 1 && y == roomBounds.yMin - 1) || // Bottom-Left

                (x == roomBounds.xMax && y == roomBounds.yMin - 1))      // Bottom-Right

            {

                isCornerPosition = true;

                break;

            }

        }



        return hasAdjacentFloor || isCornerPosition;

    }

    #endregion



    #region Entity Spawning

    private void SpawnEntities()

    {

        if (placedRoomBounds == null || placedRoomBounds.Count == 0)

        {

            Debug.LogWarning("No rooms placed, cannot spawn entities.");

            return;

        }



        Debug.Log("Starting entity spawning...");

        bool playerSpawned = false;



        // First collect all floor tiles for player spawning

        List<Vector2Int> allFloor = new List<Vector2Int>();

        foreach (var kvp in placedRoomBounds)

        {

            allFloor.AddRange(GetFloorTilesInRect(kvp.Value));

        }



        if (allFloor.Count == 0)

        {

            Debug.LogWarning("No floor tiles found, cannot spawn entities.");

            return;

        }



        // Spawn player

        if (playerPrefab != null && playerHolder.childCount == 0)

        {

            int idx = pseudoRandom.Next(allFloor.Count);

            Vector2Int tile = allFloor[idx];

            Vector3 pos = GetWorldPosition(tile);

            Instantiate(playerPrefab, pos, Quaternion.identity, playerHolder);

            playerSpawned = true;

            Debug.Log($"Player spawned at {tile}");

        }



        // Track total entities spawned for debugging

        int totalEnemies = 0;

        int totalDecorations = 0;



        // Spawn enemies and decorations PER ROOM 

        foreach (var kvp in placedRoomBounds)

        {

            string roomId = kvp.Key;

            RectInt roomBounds = kvp.Value;

            List<Vector2Int> roomFloorTiles = GetFloorTilesInRect(roomBounds);



            if (roomFloorTiles.Count == 0)

            {

                Debug.LogWarning($"Room {roomId} has no valid floor tiles for entity spawning.");

                continue;

            }



            // Spawn enemies in this specific room

            int enemiesSpawned = SpawnPrefabsInRoom(enemyPrefab, enemiesPerRoom, roomFloorTiles, enemiesHolder);

            totalEnemies += enemiesSpawned;



            // Spawn decorations in this specific room

            int decorationsSpawned = SpawnPrefabsInRoom(decorationPrefab, decorationsPerRoom, roomFloorTiles, decorationsHolder);

            totalDecorations += decorationsSpawned;

        }



        Debug.Log($"Entity spawning finished. Player Spawned: {playerSpawned}, " +

                  $"Total Enemies: {totalEnemies} (max {enemiesPerRoom}/room), " +

                  $"Total Decorations: {totalDecorations} (max {decorationsPerRoom}/room)");

    }







    private int SpawnPrefabsInRoom(GameObject prefab, int maxCount, List<Vector2Int> roomFloorTiles, Transform parentHolder)

    {

        if (prefab == null || roomFloorTiles == null || parentHolder == null ||

            roomFloorTiles.Count == 0 || maxCount <= 0)

        {

            return 0;

        }



        // Create a copy of the list so we don't modify the original

        List<Vector2Int> availableSpots = new List<Vector2Int>(roomFloorTiles);

        int spawned = 0;



        // Determine how many to spawn (limited by available spots and max count)

        int numToSpawn = Mathf.Min(maxCount, availableSpots.Count);



        for (int i = 0; i < numToSpawn; i++)

        {

            if (availableSpots.Count == 0) break;



            int idx = pseudoRandom.Next(availableSpots.Count);

            Vector2Int tile = availableSpots[idx];

            availableSpots.RemoveAt(idx); // Remove the used tile to prevent duplicates



            Vector3 pos = GetWorldPosition(tile);

            Instantiate(prefab, pos, Quaternion.identity, parentHolder);

            spawned++;

        }



        return spawned;

    }









    private void SpawnPrefabs(GameObject prefab, int maxCount, List<Vector2Int> availableSpots, Transform parentHolder)

    {

        if (prefab == null || availableSpots == null || parentHolder == null || availableSpots.Count == 0 || maxCount <= 0) return; int numSpawn = Mathf.Min(maxCount, availableSpots.Count); int spawned = 0;

        for (int i = 0; i < numSpawn; i++) { if (availableSpots.Count == 0) break; int idx = pseudoRandom.Next(availableSpots.Count); Vector2Int tile = availableSpots[idx]; availableSpots.RemoveAt(idx); Vector3 pos = GetWorldPosition(tile); Instantiate(prefab, pos, Quaternion.identity, parentHolder); spawned++; }

        Debug.Log($"Spawned {spawned} instances of {prefab.name} into {parentHolder.name}.");

    }

    #endregion



    #region BSP Tree Logic

    private void RunBSPSplit()

    {

        if (bspLeaves == null) bspLeaves = new List<RectInt>(); bspLeaves.Clear(); if (pseudoRandom == null) Initialize();

        RectInt root = new RectInt(1, 1, levelWidth - 2, levelHeight - 2); if (root.width <= 0 || root.height <= 0) { Debug.LogError("Level Width/Height too small for BSP."); return; }

        var q = new Queue<KeyValuePair<RectInt, int>>(); q.Enqueue(new KeyValuePair<RectInt, int>(root, 0));

        while (q.Count > 0) { var pair = q.Dequeue(); RectInt node = pair.Key; int iter = pair.Value; if (iter >= maxIterations || ShouldStopSplitting(node)) { bspLeaves.Add(node); continue; } if (TrySplitNode(node, out RectInt nA, out RectInt nB)) { q.Enqueue(new KeyValuePair<RectInt, int>(nA, iter + 1)); q.Enqueue(new KeyValuePair<RectInt, int>(nB, iter + 1)); } else { bspLeaves.Add(node); } }

        Debug.Log($"BSP split complete. Generated {bspLeaves.Count} leaves.");

    }



    private bool ShouldStopSplitting(RectInt node)

    {

        int minSplitSize = minRoomSize + Mathf.Max(1, (int)roomPadding) * 2; if (node.width < minSplitSize * 2 && node.height < minSplitSize * 2) return true;

        float aspect = node.width <= 0 || node.height <= 0 ? 1f : (float)Mathf.Max(node.width, node.height) / Mathf.Max(1, Mathf.Min(node.width, node.height)); if (aspect > 4.0f) return true; return false;

    }



    private bool TrySplitNode(RectInt node, out RectInt nA, out RectInt nB)

    {

        nA = nB = RectInt.zero; bool canH = node.height >= (minRoomSize + (int)roomPadding) * 2 + 1; bool canV = node.width >= (minRoomSize + (int)roomPadding) * 2 + 1; if (!canH && !canV) return false; bool splitH; if (canH && !canV) splitH = true; else if (!canH && canV) splitH = false; else { bool prefH = (node.height > node.width * 1.2f); bool prefV = (node.width > node.height * 1.2f); if (prefH) splitH = true; else if (prefV) splitH = false; else splitH = pseudoRandom.Next(0, 2) == 0; }

        int minSize = minRoomSize + (int)roomPadding; if (splitH) { int minY = node.y + minSize; int maxY = node.yMax - minSize; if (minY >= maxY) return false; int splitY = pseudoRandom.Next(minY, maxY); nA = new RectInt(node.x, node.y, node.width, splitY - node.y); nB = new RectInt(node.x, splitY, node.width, node.yMax - splitY); }

        else { int minX = node.x + minSize; int maxX = node.xMax - minSize; if (minX >= maxX) return false; int splitX = pseudoRandom.Next(minX, maxX); nA = new RectInt(node.x, node.y, splitX - node.x, node.height); nB = new RectInt(splitX, node.y, node.xMax - splitX, node.height); }

        return true;

    }







    #region Utility Functions

    private Transform CreateOrFindParent(string parentName)

    {

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

    #endregion

    #endregion

}