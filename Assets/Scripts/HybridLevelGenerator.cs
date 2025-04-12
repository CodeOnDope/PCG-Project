using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System; // Required for System.Random

public enum TileType { Empty, Floor, Wall } // Keep or replace with your actual tile enum/system

public class HybridLevelGenerator : MonoBehaviour
{
    [Header("Level Dimensions")]
    public int levelWidth = 100;
    public int levelHeight = 100;

    [Header("BSP Settings")]
    public int minRoomSize = 8; // Min size applies to width/height of rects or stem/leg of L-shapes
    public int maxIterations = 5;
    public float roomPadding = 2f; // Padding around rooms within BSP leaves

    [Header("Room Shape Settings")]
    [Tooltip("The chance (0 to 1) of attempting to generate an L-shaped room instead of a rectangle within a BSP leaf.")]
    [Range(0f, 1f)]
    public float lShapeProbability = 0.3f; // 30% chance for L-shape
    [Tooltip("Minimum length of the shorter 'leg' of an L-shape, relative to the stem dimension.")]
    [Range(0.3f, 0.8f)]
    public float minLLegRatio = 0.4f;
    [Tooltip("Maximum length of the shorter 'leg' of an L-shape, relative to the stem dimension.")]
    [Range(0.5f, 1.0f)]
    public float maxLLegRatio = 0.7f;

    // --- Room Template Settings ---
    [Header("Room Template Settings")]
    [Tooltip("Prefabs containing Tilemaps that define pre-designed room layouts.")]
    public List<GameObject> roomTemplatePrefabs;
    [Tooltip("The chance (0 to 1) of attempting to use a room template instead of procedural generation (Rect/L-shape). Checked before L-shape probability.")]
    [Range(0f, 1f)]
    public float roomTemplateProbability = 0.2f; // 20% chance to try a template first

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

    // Internal Data
    private TileType[,] grid;
    private List<RectInt> bspLeaves;
    private List<RectInt> rooms; // Stores the bounds of placed rooms (Rect, L-Shape overall bounds, or Template bounds)
    private System.Random pseudoRandom;

    // Parent Transform References
    private Transform playerHolder;
    private Transform enemiesHolder;
    private Transform decorationsHolder;

    // --- Public Methods ---

    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        Debug.Log("--- Starting Level Generation ---");
        ClearLevel();
        Initialize();

        if (grid == null || pseudoRandom == null || rooms == null || bspLeaves == null || playerHolder == null || enemiesHolder == null || decorationsHolder == null)
        {
            Debug.LogError("Initialization failed! Cannot generate level.", this);
            return;
        }
        if (floorTile == null || wallTile == null)
        {
            Debug.LogError("Floor Tile or Wall Tile is not assigned in the Inspector!", this);
            return;
        }


        RunBSPSplit();
        CreateRoomsInLeaves(); // <-- This method is modified for Templates & L-Shapes
        CreateCorridors();
        ApplyTilesToTilemap();

        Debug.Log($"--- Level Generation Complete --- Seed: {seed}. Rooms defined by bounds: {rooms?.Count ?? 0}.");
    }

    [ContextMenu("Clear Level")]
    public void ClearLevel()
    {
        // Debug.Log("--- Running ClearLevel ---");
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();
        // Debug.Log("Tilemaps cleared.");

        DestroyChildrenOf("PlayerHolder");
        DestroyChildrenOf("EnemiesHolder");
        DestroyChildrenOf("DecorationsHolder");

        grid = null;
        bspLeaves = null;
        if (rooms != null) rooms.Clear(); else rooms = new List<RectInt>();

        // Debug.Log("--- ClearLevel Finished ---");
    }

    // --- Initialization ---
    private void Initialize()
    {
        if (useRandomSeed || seed == 0) { seed = Environment.TickCount; }
        pseudoRandom = new System.Random(seed);
        Debug.Log($"Using Seed: {seed}");

        grid = new TileType[levelWidth, levelHeight];
        for (int x = 0; x < levelWidth; x++) { for (int y = 0; y < levelHeight; y++) { grid[x, y] = TileType.Wall; } }

        bspLeaves = new List<RectInt>();
        rooms = new List<RectInt>();

        playerHolder = CreateOrFindParent("PlayerHolder");
        enemiesHolder = CreateOrFindParent("EnemiesHolder");
        decorationsHolder = CreateOrFindParent("DecorationsHolder");

        if (playerHolder == null || enemiesHolder == null || decorationsHolder == null)
        {
            Debug.LogError("Failed to create necessary holder transforms!");
        }
        // Debug.Log("Initialization complete (Grid, RNG, Holders ready).");
    }

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
            // Debug.Log($"Created parent holder: {parentName}");
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
                    // Use DestroyImmediate in editor mode, Destroy in play mode
                    if (Application.isPlaying) { Destroy(child); }
                    else { DestroyImmediate(child); }
                }
            }
        }
    }

    // --- BSP Algorithm ---
    // (Methods: RunBSPSplit, ShouldStopSplitting, TrySplitNode - Assumed functional from your script)
    private void RunBSPSplit() { if (bspLeaves == null) bspLeaves = new List<RectInt>(); else bspLeaves.Clear(); if (pseudoRandom == null) Initialize(); var rootNode = new RectInt(1, 1, levelWidth - 2, levelHeight - 2); var nodeQueue = new Queue<KeyValuePair<RectInt, int>>(); nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(rootNode, 0)); while (nodeQueue.Count > 0) { var currentPair = nodeQueue.Dequeue(); RectInt currentNode = currentPair.Key; int currentIteration = currentPair.Value; if (currentIteration >= maxIterations || ShouldStopSplitting(currentNode)) { bspLeaves.Add(currentNode); continue; } if (TrySplitNode(currentNode, out RectInt nodeA, out RectInt nodeB)) { nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(nodeA, currentIteration + 1)); nodeQueue.Enqueue(new KeyValuePair<RectInt, int>(nodeB, currentIteration + 1)); } else { bspLeaves.Add(currentNode); } } /*Debug.Log($"BSP Split complete. Leaves: {bspLeaves.Count}");*/ }
    private bool ShouldStopSplitting(RectInt node) { float aspectRatio = node.width <= 0 || node.height <= 0 ? 1f : (float)Mathf.Max(node.width, node.height) / Mathf.Max(1, Mathf.Min(node.width, node.height)); return node.width < minRoomSize * 2 || node.height < minRoomSize * 2 || aspectRatio > 3.0f; } // Basic checks
    private bool TrySplitNode(RectInt node, out RectInt nodeA, out RectInt nodeB) { bool splitHorizontal; nodeA = nodeB = RectInt.zero; bool preferHorizontal = (node.height > node.width && (float)node.height / Mathf.Max(1, node.width) >= 1.25f); bool preferVertical = (node.width > node.height && (float)node.width / Mathf.Max(1, node.height) >= 1.25f); if (preferHorizontal) splitHorizontal = true; else if (preferVertical) splitHorizontal = false; else splitHorizontal = pseudoRandom.Next(0, 2) == 0; int minPartitionSize = minRoomSize + (int)(2 * roomPadding); minPartitionSize = Mathf.Max(minRoomSize, minPartitionSize); if (splitHorizontal) { if (node.height < minPartitionSize * 2) return false; int splitY = pseudoRandom.Next(node.y + minPartitionSize, node.yMax - minPartitionSize + 1); nodeA = new RectInt(node.x, node.y, node.width, splitY - node.y); nodeB = new RectInt(node.x, splitY, node.width, node.yMax - splitY); } else { if (node.width < minPartitionSize * 2) return false; int splitX = pseudoRandom.Next(node.x + minPartitionSize, node.xMax - minPartitionSize + 1); nodeA = new RectInt(node.x, node.y, splitX - node.x, node.height); nodeB = new RectInt(splitX, node.y, node.xMax - splitX, node.height); } return true; }


    // --- Room Creation & Entity Spawning (UPDATED FOR TEMPLATES) ---
    private void CreateRoomsInLeaves()
    {
        bool isFirstRoom = true;
        if (rooms == null) rooms = new List<RectInt>(); else rooms.Clear();
        if (bspLeaves == null) { Debug.LogError("BSP leaves not generated!"); return; }
        if (pseudoRandom == null) { Debug.LogError("RNG not initialized!"); return; }
        if (playerHolder == null || enemiesHolder == null || decorationsHolder == null) { Debug.LogError("Parent holder transforms not initialized properly!"); return; }

        foreach (RectInt leaf in bspLeaves)
        {
            RectInt roomBoundsForSpawning = RectInt.zero; // Holds the bounds used for spawning
            bool roomCreated = false;

            // --- Decide Room Shape ---

            // 1. Try Template First
            if (roomTemplatePrefabs != null && roomTemplatePrefabs.Count > 0 && pseudoRandom.NextDouble() < roomTemplateProbability)
            {
                if (TryPlaceRoomTemplate(leaf, out roomBoundsForSpawning))
                {
                    roomCreated = true;
                    Debug.Log($"SUCCESS: Placed Room Template in leaf {leaf}. Bounds: {roomBoundsForSpawning}");
                }
                // else { Debug.LogWarning($"FAILED: Room Template placement failed in leaf {leaf}."); } // Optional failure log
            }

            // 2. If Template not tried or failed, try L-Shape
            if (!roomCreated && pseudoRandom.NextDouble() < lShapeProbability)
            {
                if (TryCreateLShapeInLeaf(leaf, out RectInt stemRect, out RectInt legRect, out RectInt overallBounds))
                {
                    Debug.Log($"SUCCESS: Created L-Shape: Stem={stemRect}, Leg={legRect}, Bounds={overallBounds}");
                    CarveRectangle(stemRect, TileType.Floor);
                    CarveRectangle(legRect, TileType.Floor);
                    rooms.Add(overallBounds);
                    roomBoundsForSpawning = overallBounds;
                    roomCreated = true;
                }
                // else { // Failure reason logged inside TryCreateLShapeInLeaf }
            }

            // 3. If Template and L-Shape not tried or failed, try Rectangle
            if (!roomCreated)
            {
                if (TryCreateRectangleInLeaf(leaf, out RectInt standardRect))
                {
                    CarveRectangle(standardRect, TileType.Floor);
                    rooms.Add(standardRect);
                    roomBoundsForSpawning = standardRect;
                    roomCreated = true;
                }
                // else { // Leaf too small for even a rectangle }
            }

            // --- Entity Spawning (If any room was successfully created in this leaf) ---
            if (roomCreated)
            {
                List<Vector2Int> floorSpotsInRoom = GetFloorTilesInRect(roomBoundsForSpawning);
                if (floorSpotsInRoom.Count == 0) continue; // Skip spawning if somehow no floor tiles exist

                // Spawn Player
                if (isFirstRoom && playerPrefab != null)
                {
                    Vector2Int centerTile = Vector2Int.RoundToInt(roomBoundsForSpawning.center);
                    Vector2Int chosenTilePos;
                    if (centerTile.x >= 0 && centerTile.x < levelWidth && centerTile.y >= 0 && centerTile.y < levelHeight && grid[centerTile.x, centerTile.y] == TileType.Floor && floorSpotsInRoom.Contains(centerTile))
                    { chosenTilePos = centerTile; }
                    else if (floorSpotsInRoom.Count > 0) { chosenTilePos = floorSpotsInRoom[0]; }
                    else { Debug.LogWarning($"No valid floor spots found in room bounds {roomBoundsForSpawning} to spawn player."); continue; }

                    Vector3 spawnPos = GetWorldPosition(chosenTilePos);
                    floorSpotsInRoom.Remove(chosenTilePos);
                    Instantiate(playerPrefab, spawnPos, Quaternion.identity, playerHolder);
                    isFirstRoom = false;
                }

                // Spawn Enemies
                if (enemyPrefab != null)
                {
                    int enemyCount = Mathf.Min(enemiesPerRoom, floorSpotsInRoom.Count);
                    for (int i = 0; i < enemyCount; i++) { if (floorSpotsInRoom.Count == 0) break; int spotIndex = pseudoRandom.Next(floorSpotsInRoom.Count); Vector2Int spawnTilePos = floorSpotsInRoom[spotIndex]; floorSpotsInRoom.RemoveAt(spotIndex); Vector3 worldPos = GetWorldPosition(spawnTilePos); Instantiate(enemyPrefab, worldPos, Quaternion.identity, enemiesHolder); }
                }

                // Spawn Decorations
                if (decorationPrefab != null)
                {
                    int decorationCount = Mathf.Min(decorationsPerRoom, floorSpotsInRoom.Count);
                    for (int i = 0; i < decorationCount; i++) { if (floorSpotsInRoom.Count == 0) break; int spotIndex = pseudoRandom.Next(floorSpotsInRoom.Count); Vector2Int spawnTilePos = floorSpotsInRoom[spotIndex]; floorSpotsInRoom.RemoveAt(spotIndex); Vector3 worldPos = GetWorldPosition(spawnTilePos); Instantiate(decorationPrefab, worldPos, Quaternion.identity, decorationsHolder); }
                }
            } // end if (roomCreated)
        } // end foreach leaf
    }

    // --- Helper to calculate and validate a standard rectangle within a leaf ---
    private bool TryCreateRectangleInLeaf(RectInt leaf, out RectInt roomRect)
    {
        roomRect = RectInt.zero;
        int padding = (int)roomPadding;
        int maxRoomWidth = leaf.width - (2 * padding);
        int maxRoomHeight = leaf.height - (2 * padding);

        if (maxRoomWidth < minRoomSize || maxRoomHeight < minRoomSize) return false;

        int roomWidth = pseudoRandom.Next(minRoomSize, maxRoomWidth + 1);
        int roomHeight = pseudoRandom.Next(minRoomSize, maxRoomHeight + 1);
        int roomX = leaf.x + padding + pseudoRandom.Next(0, maxRoomWidth - roomWidth + 1);
        int roomY = leaf.y + padding + pseudoRandom.Next(0, maxRoomHeight - roomHeight + 1);

        roomRect = new RectInt(roomX, roomY, roomWidth, roomHeight);
        return true;
    }

    // --- Helper to calculate and validate an L-shape within a leaf ---
    private bool TryCreateLShapeInLeaf(RectInt leaf, out RectInt stemRect, out RectInt legRect, out RectInt overallBounds)
    {
        stemRect = legRect = overallBounds = RectInt.zero;
        int padding = (int)roomPadding;
        int availableWidth = leaf.width - (2 * padding);
        int availableHeight = leaf.height - (2 * padding);
        int minLegSize = 1; // Minimum dimension for a leg part to be valid

        // Initial size check
        if (availableWidth < minRoomSize || availableHeight < minRoomSize) { return false; }

        // Determine stem dimensions first
        int stemW = pseudoRandom.Next(minRoomSize, availableWidth + 1);
        int stemH = pseudoRandom.Next(minRoomSize, availableHeight + 1);

        // Try placing the stem randomly within the padded area
        int maxStemX = availableWidth - stemW;
        int maxStemY = availableHeight - stemH;
        if (maxStemX < 0 || maxStemY < 0) { return false; } // Stem doesn't fit

        int stemRelX = pseudoRandom.Next(0, maxStemX + 1);
        int stemRelY = pseudoRandom.Next(0, maxStemY + 1);
        stemRect = new RectInt(leaf.x + padding + stemRelX, leaf.y + padding + stemRelY, stemW, stemH);

        // --- Check available space adjacent to the placed stem ---
        RectInt paddedLeafBounds = new RectInt(leaf.x + padding, leaf.y + padding, availableWidth, availableHeight);
        int spaceAbove = paddedLeafBounds.yMax - stemRect.yMax;
        int spaceBelow = stemRect.yMin - paddedLeafBounds.yMin;
        int spaceRight = paddedLeafBounds.xMax - stemRect.xMax;
        int spaceLeft = stemRect.xMin - paddedLeafBounds.xMin;

        bool canAttachAbove = spaceAbove >= minLegSize;
        bool canAttachBelow = spaceBelow >= minLegSize;
        bool canAttachRight = spaceRight >= minLegSize;
        bool canAttachLeft = spaceLeft >= minLegSize;

        // Determine possible attachment directions
        List<int> possibleVerticalAttach = new List<int>(); // 0: Below, 1: Above
        if (canAttachBelow) possibleVerticalAttach.Add(0);
        if (canAttachAbove) possibleVerticalAttach.Add(1);

        List<int> possibleHorizontalAttach = new List<int>(); // 0: Left, 1: Right
        if (canAttachLeft) possibleHorizontalAttach.Add(0);
        if (canAttachRight) possibleHorizontalAttach.Add(1);

        // Check if ANY attachment is possible
        if (possibleVerticalAttach.Count == 0 && possibleHorizontalAttach.Count == 0)
        {
            // Debug.LogWarning($"L-Shape Skip (Leaf: {leaf}): No space adjacent to stem {stemRect} within {paddedLeafBounds}");
            return false; // No room to attach leg anywhere
        }

        // Decide orientation: prefer attaching along the stem's longer axis if possible
        bool tryVerticalAttach;
        if (stemW >= stemH) { tryVerticalAttach = possibleVerticalAttach.Count > 0; }
        else { tryVerticalAttach = !(possibleHorizontalAttach.Count > 0); }
        // If preferred direction failed, try the other one if possible
        if (tryVerticalAttach && possibleVerticalAttach.Count == 0) tryVerticalAttach = false;
        if (!tryVerticalAttach && possibleHorizontalAttach.Count == 0) tryVerticalAttach = true;


        // --- Calculate Leg based on chosen attachment direction ---
        int legW, legH, legX, legY;

        if (tryVerticalAttach) // Attach leg Above or Below
        {
            bool attachAbove = possibleVerticalAttach[pseudoRandom.Next(possibleVerticalAttach.Count)] == 1;
            int availableSpace = attachAbove ? spaceAbove : spaceBelow;

            if (availableSpace < minLegSize) { return false; } // Safety check
            legH = pseudoRandom.Next(minLegSize, availableSpace + 1);

            legW = (int)(stemW * pseudoRandom.Next((int)(minLLegRatio * 100), (int)(maxLLegRatio * 100) + 1) / 100f);
            legW = Mathf.Clamp(legW, minLegSize, stemW);

            int maxLegXOffset = Mathf.Max(0, stemW - legW);
            legX = stemRect.x + pseudoRandom.Next(0, maxLegXOffset + 1);
            legY = attachAbove ? stemRect.yMax : stemRect.yMin - legH;
        }
        else // Attach leg Left or Right
        {
            bool attachRight = possibleHorizontalAttach[pseudoRandom.Next(possibleHorizontalAttach.Count)] == 1;
            int availableSpace = attachRight ? spaceRight : spaceLeft;

            if (availableSpace < minLegSize) { return false; } // Safety check
            legW = pseudoRandom.Next(minLegSize, availableSpace + 1);

            legH = (int)(stemH * pseudoRandom.Next((int)(minLLegRatio * 100), (int)(maxLLegRatio * 100) + 1) / 100f);
            legH = Mathf.Clamp(legH, minLegSize, stemH);

            int maxLegYOffset = Mathf.Max(0, stemH - legH);
            legY = stemRect.y + pseudoRandom.Next(0, maxLegYOffset + 1);
            legX = attachRight ? stemRect.xMax : stemRect.xMin - legW;
        }

        // Final check uses calculated legW/legH
        if (legW < minLegSize || legH < minLegSize)
        {
            Debug.LogError($"L-Shape Logic Error: Final calculated leg size invalid ({legW}x{legH}). Min required: {minLegSize}. This should not happen.");
            return false;
        }

        legRect = new RectInt(legX, legY, legW, legH);

        // --- Calculate Overall Bounds ---
        int minX = Mathf.Min(stemRect.xMin, legRect.xMin);
        int minY = Mathf.Min(stemRect.yMin, legRect.yMin);
        int maxX = Mathf.Max(stemRect.xMax, legRect.xMax);
        int maxY = Mathf.Max(stemRect.yMax, legRect.yMax);
        overallBounds = new RectInt(minX, minY, maxX - minX, maxY - minY);

        // Final check: ensure overall bounds don't exceed the original leaf (redundant safety check)
        if (overallBounds.xMin < leaf.xMin || overallBounds.yMin < leaf.yMin || overallBounds.xMax > leaf.xMax || overallBounds.yMax > leaf.yMax)
        {
            Debug.LogError($"L-Shape overall bounds calculation error! Bounds {overallBounds} exceed Leaf {leaf}");
            return false;
        }

        return true; // L-shape successfully calculated
    }

    // --- NEW HELPER: Try placing a room template from prefab (WITH TRY-FINALLY) ---
    private bool TryPlaceRoomTemplate(RectInt leaf, out RectInt placedBounds)
    {
        placedBounds = RectInt.zero;
        if (roomTemplatePrefabs == null || roomTemplatePrefabs.Count == 0) return false;

        // --- Select a random template ---
        GameObject selectedTemplatePrefab = roomTemplatePrefabs[pseudoRandom.Next(roomTemplatePrefabs.Count)];
        if (selectedTemplatePrefab == null)
        {
            Debug.LogWarning("Null entry found in roomTemplatePrefabs list.");
            return false;
        }

        GameObject tempInstance = null; // Declare outside try
        try // Use try-finally to ensure cleanup
        {
            // --- Instantiate temporarily to read data ---
            tempInstance = Instantiate(selectedTemplatePrefab, new Vector3(9999, 9999, 0), Quaternion.identity);
            tempInstance.SetActive(false);

            Tilemap templateTilemap = tempInstance.GetComponentInChildren<Tilemap>();
            if (templateTilemap == null)
            {
                Debug.LogError($"Room template prefab '{selectedTemplatePrefab.name}' is missing a Tilemap component in its children!", selectedTemplatePrefab);
                return false; // Exit before cleanup
            }

            // --- Get Template Dimensions ---
            templateTilemap.CompressBounds();
            BoundsInt templateCellBounds = templateTilemap.cellBounds;
            int templateWidth = templateCellBounds.size.x;
            int templateHeight = templateCellBounds.size.y;

            // --- Check if template fits in the leaf ---
            int padding = (int)roomPadding;
            int availableWidth = leaf.width - (2 * padding);
            int availableHeight = leaf.height - (2 * padding);

            if (templateWidth > availableWidth || templateHeight > availableHeight)
            {
                // Debug.Log($"Template '{selectedTemplatePrefab.name}' ({templateWidth}x{templateHeight}) too large for leaf {leaf} available space ({availableWidth}x{availableHeight}).");
                return false; // Exit before cleanup
            }

            // --- Calculate Placement Position ---
            int placeOffsetX = pseudoRandom.Next(0, availableWidth - templateWidth + 1);
            int placeOffsetY = pseudoRandom.Next(0, availableHeight - templateHeight + 1);
            int gridStartX = leaf.x + padding + placeOffsetX;
            int gridStartY = leaf.y + padding + placeOffsetY;

            // --- Copy Tile Data from Template to Main Grid ---
            bool copiedAnyFloor = false;
            foreach (Vector3Int localPos in templateCellBounds.allPositionsWithin)
            {
                TileBase tile = templateTilemap.GetTile(localPos);
                if (tile != null)
                {
                    int gridX = gridStartX + localPos.x - templateCellBounds.xMin;
                    int gridY = gridStartY + localPos.y - templateCellBounds.yMin;

                    if (gridX >= 0 && gridX < levelWidth && gridY >= 0 && gridY < levelHeight)
                    {
                        TileType typeToPlace = TileType.Empty;
                        if (tile == floorTile) { typeToPlace = TileType.Floor; copiedAnyFloor = true; }
                        else if (tile == wallTile) { typeToPlace = TileType.Wall; }

                        if (typeToPlace != TileType.Empty) { grid[gridX, gridY] = typeToPlace; }
                    }
                }
            }

            // --- Final checks and output ---
            if (!copiedAnyFloor)
            {
                Debug.LogWarning($"Placed template '{selectedTemplatePrefab.name}' but it contained no Floor tiles matching the generator's Floor Tile reference.");
                // return false; // Optional: uncomment to treat as failure if floor is mandatory
            }

            placedBounds = new RectInt(gridStartX, gridStartY, templateWidth, templateHeight);
            rooms.Add(placedBounds);
            return true; // Template successfully placed
        }
        finally // This block ALWAYS executes
        {
            // --- Clean up temporary instance ---
            if (tempInstance != null)
            {
                // Use DestroyImmediate in editor mode, Destroy in play mode
                if (Application.isPlaying) { Destroy(tempInstance); }
                else { DestroyImmediate(tempInstance); }
            }
        }
    }


    // --- Corridor Creation ---
    // (Methods: CreateCorridors, FindClosestRoom, ConnectRooms, CarveCorridorSegment - Assumed functional from your script)
    private void CreateCorridors() { if (rooms == null || rooms.Count < 2 || pseudoRandom == null) return; List<RectInt> connectedRooms = new List<RectInt>(); RectInt currentRoom = rooms[pseudoRandom.Next(rooms.Count)]; List<RectInt> remainingRooms = new List<RectInt>(rooms); connectedRooms.Add(currentRoom); remainingRooms.Remove(currentRoom); while (remainingRooms.Count > 0) { RectInt closestRoom = FindClosestRoom(currentRoom, remainingRooms); ConnectRooms(currentRoom, closestRoom); connectedRooms.Add(closestRoom); remainingRooms.Remove(closestRoom); currentRoom = closestRoom; } /* Debug.Log("Corridors created."); */ } // Simplified MST approach
    private RectInt FindClosestRoom(RectInt sourceRoom, List<RectInt> targetRooms) { if (targetRooms == null || targetRooms.Count == 0) return sourceRoom; RectInt closest = targetRooms[0]; float minDistance = Vector2.Distance(sourceRoom.center, closest.center); for (int i = 1; i < targetRooms.Count; i++) { float distance = Vector2.Distance(sourceRoom.center, targetRooms[i].center); if (distance < minDistance) { minDistance = distance; closest = targetRooms[i]; } } return closest; }
    private void ConnectRooms(RectInt roomA, RectInt roomB) { Vector2Int centerA = Vector2Int.RoundToInt(roomA.center); Vector2Int centerB = Vector2Int.RoundToInt(roomB.center); if (pseudoRandom.Next(0, 2) == 0) { CarveCorridorSegment(centerA.x, centerB.x, centerA.y, true); CarveCorridorSegment(centerA.y, centerB.y, centerB.x, false); } else { CarveCorridorSegment(centerA.y, centerB.y, centerA.x, false); CarveCorridorSegment(centerA.x, centerB.x, centerB.y, true); } } // L-shaped corridor
    private void CarveCorridorSegment(int startCoord, int endCoord, int fixedCoord, bool isHorizontal) { if (grid == null) return; int min = Mathf.Min(startCoord, endCoord); int max = Mathf.Max(startCoord, endCoord); int halfWidth = corridorWidth <= 1 ? 0 : (corridorWidth - 1) / 2; for (int i = min; i <= max; i++) { for (int w = -halfWidth; w <= halfWidth; w++) { int x, y; if (isHorizontal) { x = i; y = fixedCoord + w; } else { x = fixedCoord + w; y = i; } if (x >= 0 && x < levelWidth && y >= 0 && y < levelHeight) { grid[x, y] = TileType.Floor; } } } }


    // --- Utility Functions ---
    private void CarveRectangle(RectInt rect, TileType tile) { if (grid == null) return; for (int x = rect.xMin; x < rect.xMax; x++) { for (int y = rect.yMin; y < rect.yMax; y++) { if (x >= 0 && x < levelWidth && y >= 0 && y < levelHeight) { grid[x, y] = tile; } } } }
    private List<Vector2Int> GetFloorTilesInRect(RectInt rect) { List<Vector2Int> floorTiles = new List<Vector2Int>(); if (grid == null) return floorTiles; for (int x = rect.xMin; x < rect.xMax; x++) { for (int y = rect.yMin; y < rect.yMax; y++) { if (x >= 0 && x < levelWidth && y >= 0 && y < levelHeight && grid[x, y] == TileType.Floor) { floorTiles.Add(new Vector2Int(x, y)); } } } return floorTiles; }
    private Vector3 GetWorldPosition(Vector2Int tilePos) { if (groundTilemap == null) return new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0); return groundTilemap.CellToWorld((Vector3Int)tilePos) + (groundTilemap.cellSize * 0.5f); }


    // --- Tilemap Application ---
    private void ApplyTilesToTilemap() { if (groundTilemap == null || wallTilemap == null || floorTile == null || wallTile == null || grid == null) { Debug.LogError("Cannot apply tiles: Tilemaps, Tiles, or Grid not ready!"); return; } groundTilemap.ClearAllTiles(); wallTilemap.ClearAllTiles(); for (int x = 0; x < levelWidth; x++) { for (int y = 0; y < levelHeight; y++) { Vector3Int position = new Vector3Int(x, y, 0); switch (grid[x, y]) { case TileType.Floor: groundTilemap.SetTile(position, floorTile); wallTilemap.SetTile(position, null); break; case TileType.Wall: if (IsWallCandidate(x, y)) { wallTilemap.SetTile(position, wallTile); groundTilemap.SetTile(position, null); } else { wallTilemap.SetTile(position, null); groundTilemap.SetTile(position, null); } break; default: groundTilemap.SetTile(position, null); wallTilemap.SetTile(position, null); break; } } } /*Debug.Log("Tiles applied to tilemap.");*/ }
    private bool IsWallCandidate(int x, int y) { if (grid == null || x < 0 || x >= levelWidth || y < 0 || y >= levelHeight || grid[x, y] != TileType.Wall) return false; for (int dx = -1; dx <= 1; dx++) { for (int dy = -1; dy <= 1; dy++) { if (dx == 0 && dy == 0) continue; int nx = x + dx; int ny = y + dy; if (nx >= 0 && nx < levelWidth && ny >= 0 && ny < levelHeight && grid[nx, ny] == TileType.Floor) { return true; } } } return false; } // Check neighbors for floor

}