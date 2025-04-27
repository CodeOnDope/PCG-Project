using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Graph-based level generator that creates procedural levels using a connection graph and room templates.
/// </summary>
public class GraphBasedLevelGenerator : MonoBehaviour
{
    #region Enums and Data Structures
    [System.Serializable]
    public enum RoomType
    {
        Start, Normal, Treasure, Boss, Shop, Secret, Exit
    }

    [System.Serializable]
    public enum GenerationAlgorithm
    {
        GraphBased,
        ConstraintBased, // Note: These other algorithms are not implemented in this example
        AgentBased,
        MixedApproach
    }

    // REMOVED the extra RoomTemplateList class definition

    [System.Serializable]
    public class RoomTemplate
    {
        public string name;
        public GameObject prefab; // Must have RoomTemplateAnalyzer attached
        public RoomType[] validRoomTypes = new RoomType[1] { RoomType.Normal };
        [Range(1, 100)]
        public int weight = 50;
        public bool allowRotation = true;

        // Data fetched from RoomTemplateAnalyzer
        [HideInInspector] public Vector2Int size;
        [HideInInspector] public TilemapData tilemapData = new TilemapData(); // Ensure TilemapData class is defined below
        [HideInInspector] public bool dataFetched = false;
    }

    [System.Serializable]
    public class DoorwayData
    {
        public Vector3Int position; // Local position relative to room pivot (center of doorway tile/group)
        public Vector3Int direction; // Normalized direction vector pointing OUT of the room
        public int width = 1; // Width in tiles
        [HideInInspector] public bool connected = false; // Runtime flag during connection phase
    }

    [System.Serializable]
    public class RoomNode
    {
        public int id;
        public RoomType type;
        public List<int> connections = new List<int>();
        public RoomTemplate template;
        public Vector3 position; // World position
        public int rotation; // 0, 90, 180, 270 (degrees Z)
        public bool isPlaced = false;
        public bool isMainPath = false;
        public GameObject instance; // Reference to the instantiated room prefab
    }

    [System.Serializable]
    public class LevelGraph
    {
        public List<RoomNode> nodes = new List<RoomNode>();
        public Dictionary<int, RoomNode> nodeMap = new Dictionary<int, RoomNode>();

        public RoomNode GetNode(int id) => nodeMap.TryGetValue(id, out var node) ? node : null;

        public int AddNode(RoomNode node)
        {
            if (nodeMap.ContainsKey(node.id))
            {
                Debug.LogWarning($"Graph already contains node with ID {node.id}. Overwriting might cause issues if references exist elsewhere.");
            }
            nodes.Add(node);
            nodeMap[node.id] = node;
            return node.id;
        }

        public void AddConnection(int nodeA, int nodeB, bool bidirectional = true)
        {
            if (nodeMap.TryGetValue(nodeA, out var roomA) && nodeMap.TryGetValue(nodeB, out var roomB))
            {
                if (!roomA.connections.Contains(nodeB)) roomA.connections.Add(nodeB);
                if (bidirectional && !roomB.connections.Contains(nodeA)) roomB.connections.Add(nodeA);
            }
            else
            {
                Debug.LogError($"Failed to add connection: Node ID {(nodeMap.ContainsKey(nodeA) ? nodeB : nodeA)} not found in nodeMap.");
            }
        }

        public void Clear()
        {
            // Destroy previous instances before clearing graph data
            foreach (var node in nodes)
            {
                if (node.instance != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) UnityEditor.Undo.DestroyObjectImmediate(node.instance); // Allow Undo
                    else Destroy(node.instance);
#else
                    Destroy(node.instance);
#endif
                }
            }
            nodes.Clear();
            nodeMap.Clear();
        }
    }
    #endregion

    #region Inspector Properties
    [Header("Generation Settings")]
    [Tooltip("Main algorithm to use for level generation")]
    public GenerationAlgorithm algorithm = GenerationAlgorithm.GraphBased;

    [Tooltip("Random seed for generation. 0 means random every time.")]
    public int seed = 0;

    [Tooltip("Whether to use a new random seed each time")]
    public bool useRandomSeed = true;

    [Header("Level Structure")]
    [Tooltip("Minimum number of rooms to generate")]
    [Range(5, 50)]
    public int minRooms = 8;

    [Tooltip("Maximum number of rooms to generate")]
    [Range(5, 100)]
    public int maxRooms = 15;

    [Tooltip("Minimum number of rooms on the main critical path (Start -> ... -> Boss/Exit)")]
    [Range(3, 20)]
    public int minMainPathRooms = 5;

    [Tooltip("Chance of adding a side branch room when possible (0-1)")]
    [Range(0f, 1f)]
    public float branchProbability = 0.3f;

    [Tooltip("Maximum length of a side branch")]
    [Range(1, 10)]
    public int maxBranchLength = 3;

    [Tooltip("Minimum distance between placed room bounds during fallback placement")]
    [Range(0, 5)]
    public int minRoomDistance = 1;

    [Header("Room Templates")]
    // REMOVED the public RoomTemplateList roomTemplateList field.
    [Tooltip("Generic room template prefabs available for generation (Must have RoomTemplateAnalyzer)")]
    public List<RoomTemplate> roomTemplates = new List<RoomTemplate>(); // << USE THIS DIRECT LIST

    [Tooltip("Dedicated starting room template prefab (optional, overrides generic)")]
    public RoomTemplate startRoomTemplate;

    [Tooltip("Dedicated boss room template prefab (optional, overrides generic)")]
    public RoomTemplate bossRoomTemplate;

    [Tooltip("Dedicated exit room template prefab (optional, overrides generic)")]
    public RoomTemplate exitRoomTemplate;

    [Header("Tilemaps")]
    [Tooltip("Tilemap for drawing corridors (Floor)")]
    public Tilemap floorTilemap;

    [Tooltip("Tilemap for drawing corridors (Walls)")]
    public Tilemap wallTilemap;

    [Header("Corridor Settings")]
    [Tooltip("Whether to generate corridors between connected rooms")]
    public bool generateCorridors = true;

    [Tooltip("Width of corridors in tiles")]
    [Range(1, 5)]
    public int corridorWidth = 2;

    [Tooltip("Default floor tile for corridors")]
    public TileBase defaultCorridorFloorTile;

    [Tooltip("Default wall tile for corridors (optional)")]
    public TileBase defaultCorridorWallTile;

    [Tooltip("Whether to add corner turns in corridors (L-shape fallback)")]
    public bool useCornerCorridors = true;

    [Tooltip("Whether to add decoration to corridors (Not implemented)")]
    public bool decorateCorridors = false; // Currently not implemented

    [Header("Entity Spawning")]
    [Tooltip("Player prefab to spawn at start")]
    public GameObject playerPrefab;

    [Tooltip("Enemy prefabs to potentially spawn in rooms")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Tooltip("Decoration prefabs to potentially place in rooms/corridors (Not implemented for corridors)")]
    public List<GameObject> decorationPrefabs = new List<GameObject>();

    [Tooltip("Treasure prefabs to potentially spawn in certain rooms")]
    public List<GameObject> treasurePrefabs = new List<GameObject>(); // Ensure this is assigned in Inspector

    [Range(0, 10)] public int enemiesPerRoom = 3; // Max enemies per Normal room
    [Range(0, 20)] public int decorationsPerRoom = 5; // Max decorations per room
    #endregion

    #region Private Fields
    private System.Random random;
    private LevelGraph levelGraph = new LevelGraph();
    private Dictionary<RoomType, List<RoomTemplate>> templatesByType = new Dictionary<RoomType, List<RoomTemplate>>();
    private List<Bounds> placedRoomBounds = new List<Bounds>(); // Used for overlap checks
    private int nextNodeId = 0;
    private RoomNode startRoomNode;
    private RoomNode bossRoomNode;
    private RoomNode exitRoomNode;

    // Parent transforms for organization
    private Transform roomContainer;
    private Transform corridorContainer;
    private Transform entityContainer;

    private bool isGenerating = false;
    #endregion

    #region Unity Lifecycle Methods
    private void OnValidate()
    {
        maxRooms = Mathf.Max(maxRooms, minRooms);
        minMainPathRooms = Mathf.Clamp(minMainPathRooms, 3, maxRooms);
    }
    #endregion

    #region Public Methods
    [ContextMenu("Generate Level")]
    public void GenerateLevel()
    {
        if (isGenerating)
        {
            Debug.LogWarning("Already generating level.");
            return;
        }
        isGenerating = true;

        try
        {
            ClearLevel();
            InitializeGenerator();

            if (!ValidatePrerequisites())
            {
                isGenerating = false;
                return;
            }

            Debug.Log($"Starting Level Generation (Seed: {seed})");

            // 1. Create Graph Topology
            switch (algorithm)
            {
                case GenerationAlgorithm.GraphBased:
                    CreateLevelGraph();
                    break;
                default:
                    Debug.LogWarning($"Algorithm '{algorithm}' not fully implemented. Using GraphBased.");
                    CreateLevelGraph();
                    break;
            }

            if (levelGraph.nodes.Count == 0)
            {
                Debug.LogError("Level graph creation failed. No nodes generated.");
                isGenerating = false;
                return;
            }

            // 2. Assign Templates
            if (!AssignAndValidateTemplates())
            {
                Debug.LogError("Failed to assign templates to graph nodes.");
                isGenerating = false;
                return;
            }

            // 3. Place Rooms
            PlaceRooms();
            if (placedRoomBounds.Count == 0)
            {
                Debug.LogError("Room placement failed. No rooms were placed.");
                isGenerating = false;
                return;
            }

            // 4. Generate Corridors
            if (generateCorridors)
            {
                GenerateCorridors();
            }

            // 5. Spawn Entities
            SpawnEntities();

            Debug.Log($"Level Generation Complete. Placed {placedRoomBounds.Count} / {levelGraph.nodes.Count} rooms.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Level generation failed with exception: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            isGenerating = false;
        }
    }

    [ContextMenu("Clear Level")]
    public void ClearLevel()
    {
        Debug.Log("Clearing level...");

        DestroyContainerChildren("Rooms");
        DestroyContainerChildren("Corridors");
        DestroyContainerChildren("Entities");

        if (floorTilemap != null) floorTilemap.ClearAllTiles();
        if (wallTilemap != null) wallTilemap.ClearAllTiles();

        levelGraph.Clear();
        placedRoomBounds.Clear();
        templatesByType.Clear();
        nextNodeId = 0;
        startRoomNode = null;
        bossRoomNode = null;
        exitRoomNode = null;

#if UNITY_EDITOR
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
    #endregion

    #region Initialization and Validation
    private void InitializeGenerator()
    {
        if (useRandomSeed || seed == 0)
        {
            seed = UnityEngine.Random.Range(1, int.MaxValue);
        }
        random = new System.Random(seed);

        roomContainer = SetupContainer("Rooms");
        corridorContainer = SetupContainer("Corridors");
        entityContainer = SetupContainer("Entities");

        if (roomTemplates == null) roomTemplates = new List<RoomTemplate>();

        // Ensure template data is fetched/refreshed
        templatesByType.Clear();
        // Use the direct roomTemplates list here
        FetchDataFromTemplateList(roomTemplates);
        FetchDataFromTemplate(startRoomTemplate, isSpecial: true);
        FetchDataFromTemplate(bossRoomTemplate, isSpecial: true);
        FetchDataFromTemplate(exitRoomTemplate, isSpecial: true);
    }

    private bool ValidatePrerequisites()
    {
        // Use the direct roomTemplates list
        bool hasGenericTemplates = roomTemplates != null && roomTemplates.Any(t => t != null && t.prefab != null && t.dataFetched);
        bool hasStartTemplate = startRoomTemplate != null && startRoomTemplate.prefab != null && startRoomTemplate.dataFetched;

        if (!hasGenericTemplates && !hasStartTemplate)
        {
            Debug.LogError("No VALID Room Templates assigned or analyzed (neither Generic nor Start). Cannot generate level. Ensure prefabs are assigned and analyzed via RoomTemplateAnalyzer.");
            return false;
        }
        if (!hasStartTemplate && !templatesByType.ContainsKey(RoomType.Start) && !templatesByType.ContainsKey(RoomType.Normal))
        {
            Debug.LogError("No valid Start Room Template assigned and no suitable generic templates found (ensure they are analyzed and valid for Start/Normal).");
            return false;
        }
        if (generateCorridors)
        {
            if (floorTilemap == null) { Debug.LogError("Corridor generation requires 'Floor Tilemap'."); return false; }
            if (defaultCorridorFloorTile == null) { Debug.LogError("Corridor generation requires 'Default Corridor Floor Tile'."); return false; }
            if (wallTilemap == null && defaultCorridorWallTile != null) { Debug.LogWarning("Default Corridor Wall Tile assigned, but Wall Tilemap is not. Walls won't be drawn."); }
        }
        if (playerPrefab == null) Debug.LogWarning("Player Prefab not assigned. Player will not be spawned.");
        return true;
    }

    // Updated to use the direct roomTemplates list
    private void FetchDataFromTemplateList(List<RoomTemplate> templates)
    {
        if (templates == null) return;
        foreach (var template in templates)
        {
            FetchDataFromTemplate(template, isSpecial: false);
        }
    }

    private void FetchDataFromTemplate(RoomTemplate template, bool isSpecial)
    {
        if (template == null || template.prefab == null)
        {
            if (!isSpecial && template != null && template.prefab == null) Debug.LogWarning($"Generic Room Template entry has missing prefab reference.");
            if (!isSpecial && template == null) Debug.LogWarning($"Generic Room Templates list contains a null entry.");
            return;
        }
        if (template.dataFetched && template.size != Vector2Int.zero) return;

        RoomTemplateAnalyzer analyzer = template.prefab.GetComponent<RoomTemplateAnalyzer>();
        if (analyzer == null)
        {
            Debug.LogError($"Template '{template.prefab.name}' missing RoomTemplateAnalyzer component!", template.prefab);
            template.dataFetched = false; return;
        }

        BoundsInt bounds = analyzer.GetAnalyzedBounds();
        List<DoorwayData> doorways = analyzer.GetDoorways();

        if (bounds.size == Vector3Int.zero)
        {
            Tilemap floor = analyzer.floorTilemap;
            if (floor != null && floor.GetUsedTilesCount() > 0)
            {
                Debug.LogError($"Template '{template.prefab.name}' not analyzed (zero bounds). Run 'Analyze Template' on prefab and Apply/Save.", template.prefab);
            }
            else
            {
                Debug.LogWarning($"Template '{template.prefab.name}' has zero bounds. Is it empty?", template.prefab);
            }
            template.dataFetched = false; return;
        }

        template.size = new Vector2Int(bounds.size.x, bounds.size.y);
        template.tilemapData.doorways = doorways != null ? new List<DoorwayData>(doorways) : new List<DoorwayData>();
        template.allowRotation = analyzer.allowRotation;
        if (string.IsNullOrEmpty(template.name)) template.name = analyzer.templateName;
        if (template.validRoomTypes == null || template.validRoomTypes.Length == 0)
        {
            template.validRoomTypes = analyzer.validRoomTypes ?? new RoomType[] { RoomType.Normal };
        }
        template.dataFetched = true;

        if (!isSpecial && template.validRoomTypes != null)
        {
            foreach (var type in template.validRoomTypes)
            {
                if (!templatesByType.ContainsKey(type)) templatesByType[type] = new List<RoomTemplate>();
                if (!templatesByType[type].Contains(template)) templatesByType[type].Add(template);
            }
        }
    }

    private Transform SetupContainer(string name)
    {
        Transform container = transform.Find(name);
        if (container == null)
        {
            GameObject containerObj = new GameObject(name);
            containerObj.transform.SetParent(transform, false);
            container = containerObj.transform;
        }
        return container;
    }

    private void DestroyContainerChildren(string name)
    {
        Transform container = transform.Find(name);
        if (container != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    if (container.GetChild(i) != null)
                    {
                        Undo.DestroyObjectImmediate(container.GetChild(i).gameObject);
                    }
                }
            }
            else
            {
                foreach (Transform child in container) Destroy(child.gameObject);
            }
#else
             foreach (Transform child in container) Destroy(child.gameObject);
#endif
        }
    }
    #endregion

    #region Graph Generation
    // (CreateLevelGraph, GetRandomBranchType - Keep as is from your provided script)
    // --- Paste the CreateLevelGraph and GetRandomBranchType functions from your script here ---
    private void CreateLevelGraph()
    {
        levelGraph.Clear();
        placedRoomBounds.Clear();
        nextNodeId = 0;

        int targetRoomCount = random.Next(minRooms, maxRooms + 1);
        Debug.Log($"Targeting {targetRoomCount} rooms.");

        List<int> criticalPathIds = new List<int>();

        // 1. Create Start Room Node
        startRoomNode = new RoomNode { id = nextNodeId++, type = RoomType.Start, isMainPath = true };
        levelGraph.AddNode(startRoomNode);
        criticalPathIds.Add(startRoomNode.id);

        // 2. Create Main Path Nodes (Normal)
        int mainPathNodesNeeded = Mathf.Max(0, minMainPathRooms - 2); // Subtract Start and Boss/Exit
        RoomNode lastMainNode = startRoomNode;
        for (int i = 0; i < mainPathNodesNeeded && criticalPathIds.Count < targetRoomCount; i++)
        {
            RoomNode newNode = new RoomNode { id = nextNodeId++, type = RoomType.Normal, isMainPath = true };
            levelGraph.AddNode(newNode);
            levelGraph.AddConnection(lastMainNode.id, newNode.id);
            criticalPathIds.Add(newNode.id);
            lastMainNode = newNode;
        }

        // 3. Create Boss Room Node
        if (criticalPathIds.Count < targetRoomCount)
        {
            bossRoomNode = new RoomNode { id = nextNodeId++, type = RoomType.Boss, isMainPath = true };
            levelGraph.AddNode(bossRoomNode);
            levelGraph.AddConnection(lastMainNode.id, bossRoomNode.id);
            criticalPathIds.Add(bossRoomNode.id);
            lastMainNode = bossRoomNode;
        }
        else if (lastMainNode.type != RoomType.Start)
        {
            // Not enough room for a separate boss room, convert last normal room
            lastMainNode.type = RoomType.Boss;
            bossRoomNode = lastMainNode;
            Debug.LogWarning("Not enough rooms for dedicated Boss room, converting last main path node.");
        }
        else
        {
            bossRoomNode = null;
            Debug.LogWarning("Not enough rooms for Boss room.");
        }


        // 4. Create Exit Room Node
        if (bossRoomNode != null && criticalPathIds.Count < targetRoomCount)
        {
            exitRoomNode = new RoomNode { id = nextNodeId++, type = RoomType.Exit, isMainPath = true };
            levelGraph.AddNode(exitRoomNode);
            levelGraph.AddConnection(bossRoomNode.id, exitRoomNode.id);
            criticalPathIds.Add(exitRoomNode.id);
            lastMainNode = exitRoomNode;
        }
        else if (bossRoomNode != null)
        {
            exitRoomNode = null;
            Debug.LogWarning("Not enough rooms for dedicated Exit room.");
        }
        else
        {
            exitRoomNode = null;
        }


        // 5. Add Branches
        int currentRoomCount = levelGraph.nodes.Count;
        int roomsToAdd = targetRoomCount - currentRoomCount;
        List<int> potentialBranchStarts = levelGraph.nodes
                .Where(n => n.type != RoomType.Boss && n.type != RoomType.Exit)
                .Select(n => n.id).ToList();

        int branchAttempts = 0;
        int maxBranchAttempts = targetRoomCount * 5; // Increase safety limit

        while (roomsToAdd > 0 && branchAttempts < maxBranchAttempts)
        {
            branchAttempts++;
            if (potentialBranchStarts.Count == 0) break;

            int startNodeId = potentialBranchStarts[random.Next(potentialBranchStarts.Count)];
            RoomNode branchStartNode = levelGraph.GetNode(startNodeId);

            if (branchStartNode.connections.Count >= 4)
            { // Limit connections
                potentialBranchStarts.Remove(startNodeId);
                continue;
            }

            int branchLength = random.Next(1, Mathf.Min(roomsToAdd, maxBranchLength) + 1);
            RoomType branchEndType = GetRandomBranchType();
            RoomNode lastBranchNode = branchStartNode;
            bool branchAdded = false;

            for (int i = 0; i < branchLength; i++)
            {
                if (levelGraph.nodes.Count >= targetRoomCount) { roomsToAdd = 0; break; }

                RoomNode newNode = new RoomNode
                {
                    id = nextNodeId++,
                    type = (i == branchLength - 1) ?
                           (branchEndType == RoomType.Start || branchEndType == RoomType.Boss || branchEndType == RoomType.Exit ? RoomType.Normal : branchEndType)
                           : RoomType.Normal,
                    isMainPath = false
                };
                levelGraph.AddNode(newNode);
                levelGraph.AddConnection(lastBranchNode.id, newNode.id);
                lastBranchNode = newNode;
                roomsToAdd--;
                branchAdded = true;

                if (newNode.type != RoomType.Boss && newNode.type != RoomType.Exit && newNode.connections.Count < 4)
                {
                    potentialBranchStarts.Add(newNode.id);
                }
            }
            if (!branchAdded) potentialBranchStarts.Remove(startNodeId);
        }
        if (branchAttempts >= maxBranchAttempts) Debug.LogWarning("Branching attempts reached limit.");

        Debug.Log($"Created level graph with {levelGraph.nodes.Count} nodes.");
    }

    private RoomType GetRandomBranchType()
    {
        double value = random.NextDouble();
        if (value < 0.4) return RoomType.Treasure;
        if (value < 0.6) return RoomType.Shop;
        if (value < 0.7) return RoomType.Secret;
        return RoomType.Normal;
    }
    // ------------------------------------------------------------------------------------

    private bool AssignAndValidateTemplates()
    {
        Debug.Log("Assigning templates to graph nodes...");
        bool success = true;
        RoomTemplate fallbackTemplate = SelectTemplateByWeight(templatesByType.ContainsKey(RoomType.Normal) ? templatesByType[RoomType.Normal] : null);
        if (fallbackTemplate == null && startRoomTemplate != null && startRoomTemplate.dataFetched) fallbackTemplate = startRoomTemplate;
        if (fallbackTemplate == null) fallbackTemplate = SelectTemplateByWeight(roomTemplates.Where(t => t != null && t.dataFetched).ToList());

        foreach (var node in levelGraph.nodes)
        {
            RoomTemplate chosenTemplate = null;
            if (node.type == RoomType.Start && startRoomTemplate != null && startRoomTemplate.dataFetched) chosenTemplate = startRoomTemplate;
            else if (node.type == RoomType.Boss && bossRoomTemplate != null && bossRoomTemplate.dataFetched) chosenTemplate = bossRoomTemplate;
            else if (node.type == RoomType.Exit && exitRoomTemplate != null && exitRoomTemplate.dataFetched) chosenTemplate = exitRoomTemplate;

            if (chosenTemplate == null && templatesByType.TryGetValue(node.type, out var specificTemplates) && specificTemplates.Count > 0) chosenTemplate = SelectTemplateByWeight(specificTemplates);

            if (chosenTemplate == null && node.type != RoomType.Normal)
            {
                bool usedDedicatedButFailed = (node.type == RoomType.Start && startRoomTemplate != null && !startRoomTemplate.dataFetched) || (node.type == RoomType.Boss && bossRoomTemplate != null && !bossRoomTemplate.dataFetched) || (node.type == RoomType.Exit && exitRoomTemplate != null && !exitRoomTemplate.dataFetched);
                if (!usedDedicatedButFailed && templatesByType.TryGetValue(RoomType.Normal, out var normalTemplates) && normalTemplates.Count > 0) chosenTemplate = SelectTemplateByWeight(normalTemplates);
            }
            if (chosenTemplate == null) chosenTemplate = fallbackTemplate;

            if (chosenTemplate == null || !chosenTemplate.dataFetched || chosenTemplate.prefab == null)
            {
                Debug.LogError($"Failed to find a VALID/ANALYZED template for node {node.id} ({node.type}). Check assignments & analysis.", this); success = false; node.template = null; continue;
            }
            node.template = chosenTemplate;
            node.rotation = (node.template.allowRotation) ? random.Next(4) * 90 : 0;
        }
        if (success) Debug.Log("Successfully assigned templates.");
        return success;
    }

    private RoomTemplate SelectTemplateByWeight(List<RoomTemplate> options)
    {
        if (options == null || options.Count == 0) return null;
        var validOptions = options.Where(t => t != null && t.dataFetched && t.prefab != null).ToList();
        if (validOptions.Count == 0) return null;
        int totalWeight = validOptions.Sum(t => Mathf.Max(1, t.weight));
        if (totalWeight <= 0) return validOptions[random.Next(validOptions.Count)];
        int randomWeight = random.Next(0, totalWeight);
        int cumulativeWeight = 0;
        foreach (var template in validOptions)
        {
            cumulativeWeight += Mathf.Max(1, template.weight);
            if (randomWeight < cumulativeWeight) return template;
        }
        return validOptions[validOptions.Count - 1]; // Fallback
    }
    #endregion

    #region Room Placement
    // (PlaceRooms, TryFindAndSetValidRoomPosition, CalculateWorldBounds, CheckOverlap, PlaceRoom, GetRotatedSize, GetRotatedDoorwayDirection, TransformPoint - Keep as is from previous fixed version)
    // --- Paste the Room Placement functions from the previous fixed script here ---
    private void PlaceRooms()
    {
        Debug.Log("Placing rooms...");
        placedRoomBounds.Clear();

        if (levelGraph.nodes.Count == 0) return;

        RoomNode currentStartNode = startRoomNode;
        if (currentStartNode == null || currentStartNode.template == null || !currentStartNode.template.dataFetched)
        {
            currentStartNode = levelGraph.nodes.FirstOrDefault(n => n.type == RoomType.Start && n.template != null && n.template.dataFetched);
            if (currentStartNode == null)
            {
                Debug.LogError("No valid Start room node found in the graph for placement. Ensure a Start template is assigned and analyzed.");
                return;
            }
            startRoomNode = currentStartNode;
        }

        Queue<int> placementQueue = new Queue<int>();
        HashSet<int> placedOrQueued = new HashSet<int>();

        currentStartNode.position = Vector3.zero;
        if (PlaceRoom(currentStartNode))
        {
            placementQueue.Enqueue(currentStartNode.id);
            placedOrQueued.Add(currentStartNode.id);
        }
        else
        {
            Debug.LogError("Failed to place the initial Start room! Check template prefab and analysis.");
            return;
        }

        while (placementQueue.Count > 0)
        {
            int currentId = placementQueue.Dequeue();
            RoomNode currentRoom = levelGraph.GetNode(currentId);
            if (currentRoom == null || !currentRoom.isPlaced) continue;

            foreach (int connectedId in currentRoom.connections)
            {
                if (placedOrQueued.Contains(connectedId)) continue;

                RoomNode connectedRoom = levelGraph.GetNode(connectedId);
                if (connectedRoom == null) { Debug.LogError($"Graph connection error: Node {currentId} connects to non-existent ID {connectedId}."); continue; }
                if (connectedRoom.template == null || !connectedRoom.template.dataFetched)
                { Debug.LogError($"Cannot place room {connectedId} ({connectedRoom.type}): Missing valid or analyzed template."); placedOrQueued.Add(connectedId); continue; }

                if (TryFindAndSetValidRoomPosition(currentRoom, connectedRoom))
                {
                    if (!PlaceRoom(connectedRoom)) Debug.LogError($"Instantiation or bounds calculation failed for room {connectedId}.");
                }
                else
                { Debug.LogWarning($"Could not find valid placement position for room {connectedRoom.id}."); }
                placedOrQueued.Add(connectedId);
            }
        }

        int unplacedCount = levelGraph.nodes.Count(n => !n.isPlaced);
        if (unplacedCount > 0) Debug.LogWarning($"{unplacedCount} rooms could not be placed.");
    }

    private bool TryFindAndSetValidRoomPosition(RoomNode sourceRoom, RoomNode targetRoom)
    {
        if (sourceRoom?.template == null || targetRoom?.template == null) return false;
        Vector2Int targetSize = GetRotatedSize(targetRoom);
        if (targetSize == Vector2Int.zero) { Debug.LogWarning($"Target room {targetRoom.id} has zero size. Skipping."); return false; }
        Vector2Int sourceSize = GetRotatedSize(sourceRoom);

        var sourceDoorways = sourceRoom.template.tilemapData.doorways;
        var targetDoorways = targetRoom.template.tilemapData.doorways;
        var availableSourceDoors = sourceDoorways?.Where(d => !d.connected).ToList() ?? new List<DoorwayData>();
        var availableTargetDoors = targetDoorways?.Where(d => !d.connected).ToList() ?? new List<DoorwayData>();
        ShuffleList(availableSourceDoors);
        ShuffleList(availableTargetDoors);

        if (availableSourceDoors.Count > 0 && availableTargetDoors.Count > 0)
        {
            foreach (var sourceDoor in availableSourceDoors)
            {
                Vector3Int sourceDoorDir = GetRotatedDoorwayDirection(sourceDoor.direction, sourceRoom.rotation);
                Vector3Int requiredTargetDir = -sourceDoorDir;
                foreach (var targetDoor in availableTargetDoors)
                {
                    Vector3Int targetDoorDir = GetRotatedDoorwayDirection(targetDoor.direction, targetRoom.rotation);
                    if (targetDoorDir == requiredTargetDir)
                    {
                        Vector3 sourceDoorWorldPos = TransformPoint(sourceDoor.position + new Vector3(0.5f, 0.5f, 0), sourceRoom.position, sourceRoom.rotation);
                        Vector3 targetDoorDesiredWorldPos = sourceDoorWorldPos + (Vector3)sourceDoorDir;
                        Vector3 targetDoorLocalPosRotated = TransformPoint(targetDoor.position + new Vector3(0.5f, 0.5f, 0), Vector3.zero, targetRoom.rotation);
                        Vector3 targetRoomPos = targetDoorDesiredWorldPos - targetDoorLocalPosRotated;
                        Bounds potentialBounds = CalculateWorldBounds(targetRoomPos, targetSize);
                        if (!CheckOverlap(potentialBounds))
                        { targetRoom.position = Vector3Int.RoundToInt(targetRoomPos); return true; }
                    }
                }
            }
        }

        float maxSourceExtentX = sourceSize.x / 2f; float maxSourceExtentY = sourceSize.y / 2f;
        float maxTargetExtentX = targetSize.x / 2f; float maxTargetExtentY = targetSize.y / 2f;
        float spacing = minRoomDistance + 1.0f;
        Vector3[] directions = { Vector3.up, Vector3.right, Vector3.down, Vector3.left };
        float[] distancesX = { 0, maxSourceExtentX + maxTargetExtentX + spacing, 0, -(maxSourceExtentX + maxTargetExtentX + spacing) };
        float[] distancesY = { maxSourceExtentY + maxTargetExtentY + spacing, 0, -(maxSourceExtentY + maxTargetExtentY + spacing), 0 };
        ShuffleArray(directions, distancesX, distancesY);
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 potentialPosition = sourceRoom.position + new Vector3(distancesX[i], distancesY[i], 0);
            potentialPosition = new Vector3(Mathf.Round(potentialPosition.x), Mathf.Round(potentialPosition.y), 0);
            Bounds potentialBounds = CalculateWorldBounds(potentialPosition, targetSize);
            if (!CheckOverlap(potentialBounds)) { targetRoom.position = potentialPosition; return true; }
        }
        return false;
    }

    private Bounds CalculateWorldBounds(Vector3 center, Vector2Int size) { return new Bounds(center, new Vector3(size.x, size.y, 1)); }

    private bool CheckOverlap(Bounds potentialBounds, int ignoreRoomId = -1)
    {
        foreach (Bounds placedBound in placedRoomBounds)
        {
            Bounds inflatedPotential = potentialBounds;
            inflatedPotential.Expand(minRoomDistance * 2.0f); // Check spacing
            if (inflatedPotential.Intersects(placedBound)) return true;
        }
        return false;
    }

    private bool PlaceRoom(RoomNode room)
    {
        if (room == null || room.template == null || room.template.prefab == null || !room.template.dataFetched) return false;
        if (room.isPlaced) { Debug.LogWarning($"Room {room.id} already placed."); return true; }
        try
        {
            GameObject roomInstance = Instantiate(room.template.prefab, room.position, Quaternion.Euler(0, 0, room.rotation), roomContainer);
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(roomInstance, "Place Room");
#endif
            roomInstance.name = $"Room_{room.id}_{room.type}_{room.template.name}";
            room.instance = roomInstance; room.isPlaced = true;
            Vector2Int size = GetRotatedSize(room);
            Bounds worldBounds = CalculateWorldBounds(room.position, size);
            placedRoomBounds.Add(worldBounds); return true;
        }
        catch (Exception e) { Debug.LogError($"Instantiate failed for room {room.id} ('{room.template.name}'): {e.Message}\n{e.StackTrace}"); room.isPlaced = false; return false; }
    }

    private Vector2Int GetRotatedSize(RoomNode room)
    { if (room?.template == null) return Vector2Int.zero; Vector2Int size = room.template.size; return (room.rotation == 90 || room.rotation == 270) ? new Vector2Int(size.y, size.x) : size; }

    private Vector3Int GetRotatedDoorwayDirection(Vector3Int localDirection, int roomRotation)
    { if (localDirection == Vector3Int.zero) return Vector3Int.zero; Vector3 rotatedDir = Quaternion.Euler(0, 0, roomRotation) * localDirection; return Vector3Int.RoundToInt(rotatedDir); }

    private Vector3 TransformPoint(Vector3 localPoint, Vector3 roomWorldPosition, int roomRotation)
    { Vector3 rotatedLocalPoint = Quaternion.Euler(0, 0, roomRotation) * localPoint; return roomWorldPosition + rotatedLocalPoint; }
    // ------------------------------------------------------------------------------------
    #endregion

    #region Corridor Generation
    // (GenerateCorridors, FindBestDoorwayForConnection, DrawLShapedCorridor, DrawCorridorSegment, DrawCorridorTileAndWidth, AddCorridorDecoration - Keep as is from previous fixed version)
    // --- Paste the Corridor Generation functions from the previous fixed script here ---
    private void GenerateCorridors()
    {
        if (!generateCorridors) return;
        if (floorTilemap == null || defaultCorridorFloorTile == null) { Debug.LogWarning("Corridor Gen requires Floor Tilemap & Tile."); return; }

        Debug.Log("Generating corridors...");
        HashSet<string> processedConnections = new HashSet<string>();

        foreach (var roomA in levelGraph.nodes)
        {
            if (!roomA.isPlaced) continue;
            foreach (int connectedId in roomA.connections)
            {
                RoomNode roomB = levelGraph.GetNode(connectedId);
                if (roomB == null || !roomB.isPlaced) continue;

                string connectionId = roomA.id < roomB.id ? $"{roomA.id}_{roomB.id}" : $"{roomB.id}_{roomA.id}";
                if (processedConnections.Contains(connectionId)) continue;

                DoorwayData doorwayA = FindBestDoorwayForConnection(roomA, roomB);
                DoorwayData doorwayB = FindBestDoorwayForConnection(roomB, roomA);

                if (doorwayA != null && doorwayB != null)
                {
                    Vector3 worldDoorA_Center = TransformPoint(doorwayA.position + new Vector3(0.5f, 0.5f, 0), roomA.position, roomA.rotation);
                    Vector3 worldDoorB_Center = TransformPoint(doorwayB.position + new Vector3(0.5f, 0.5f, 0), roomB.position, roomB.rotation);
                    Vector3Int worldDirA = GetRotatedDoorwayDirection(doorwayA.direction, roomA.rotation);
                    Vector3Int worldDirB = GetRotatedDoorwayDirection(doorwayB.direction, roomB.rotation);
                    Vector3Int startCell = Vector3Int.FloorToInt(worldDoorA_Center) + worldDirA;
                    Vector3Int endCell = Vector3Int.FloorToInt(worldDoorB_Center) + worldDirB;
                    DrawLShapedCorridor(startCell, endCell);
                    var originalDoorA = roomA.template?.tilemapData?.doorways?.FirstOrDefault(d => d.position == doorwayA.position);
                    var originalDoorB = roomB.template?.tilemapData?.doorways?.FirstOrDefault(d => d.position == doorwayB.position);
                    if (originalDoorA != null) originalDoorA.connected = true; // Mark original as connected
                    if (originalDoorB != null) originalDoorB.connected = true;
                }
                else
                {
                    Debug.LogWarning($"No matching doorways between Room {roomA.id} & {roomB.id}. Using center fallback.");
                    Vector3Int startCell = Vector3Int.RoundToInt(roomA.position);
                    Vector3Int endCell = Vector3Int.RoundToInt(roomB.position);
                    DrawLShapedCorridor(startCell, endCell);
                }
                processedConnections.Add(connectionId);
            }
        }
        Debug.Log($"Processed {processedConnections.Count} corridor connections.");
    }

    private DoorwayData FindBestDoorwayForConnection(RoomNode fromRoom, RoomNode toRoom)
    {
        if (fromRoom?.template?.tilemapData?.doorways == null || toRoom == null) return null;
        Vector3 targetDirection = (toRoom.position - fromRoom.position).normalized;
        DoorwayData bestDoorway = null; float bestAlignment = -1.0f;
        var availableDoorways = fromRoom.template.tilemapData.doorways.Where(d => !d.connected); // Use only unconnected doorways for matching
        foreach (var doorway in availableDoorways)
        {
            if (doorway.direction == Vector3Int.zero) continue;
            Vector3Int worldDoorDir = GetRotatedDoorwayDirection(doorway.direction, fromRoom.rotation);
            float alignment = Vector3.Dot(((Vector3)worldDoorDir).normalized, targetDirection);
            if (alignment > bestAlignment) { bestAlignment = alignment; bestDoorway = doorway; }
        }
        return bestDoorway;
    }

    private void DrawLShapedCorridor(Vector3Int startCell, Vector3Int endCell)
    {
        if (startCell == endCell) return;
        Vector3Int cornerCell;
        if (random.NextDouble() < 0.5)
        { // Go X then Y
            cornerCell = new Vector3Int(endCell.x, startCell.y, 0);
            DrawCorridorSegment(startCell, cornerCell); DrawCorridorSegment(cornerCell, endCell);
        }
        else
        { // Go Y then X
            cornerCell = new Vector3Int(startCell.x, endCell.y, 0);
            DrawCorridorSegment(startCell, cornerCell); DrawCorridorSegment(cornerCell, endCell);
        }
    }

    private void DrawCorridorSegment(Vector3Int start, Vector3Int end)
    {
        if (start == end) return;
        Vector3Int current = start; Vector3Int direction = Vector3Int.zero; bool movingX = false;
        if (Mathf.Abs(end.x - start.x) >= Mathf.Abs(end.y - start.y)) { if (start.x != end.x) { direction.x = (end.x > start.x) ? 1 : -1; movingX = true; } else if (start.y != end.y) { direction.y = (end.y > start.y) ? 1 : -1; } else return; } else { if (start.y != end.y) { direction.y = (end.y > start.y) ? 1 : -1; } else if (start.x != end.x) { direction.x = (end.x > start.x) ? 1 : -1; movingX = true; } else return; }
        int safetyBreak = 0; int maxDist = Mathf.Abs(start.x - end.x) + Mathf.Abs(start.y - end.y); int maxSteps = maxDist + 5;
        while (safetyBreak++ <= maxSteps)
        {
            DrawCorridorTileAndWidth(current, direction);
            if (movingX) { if ((direction.x > 0 && current.x >= end.x) || (direction.x < 0 && current.x <= end.x)) break; } else { if ((direction.y > 0 && current.y >= end.y) || (direction.y < 0 && current.y <= end.y)) break; }
            current += direction;
        }
        if (safetyBreak <= maxSteps) DrawCorridorTileAndWidth(end, direction); else Debug.LogError($"Corridor segment drawing exceeded safety break: {start} to {end}");
    }

    private void DrawCorridorTileAndWidth(Vector3Int centerCell, Vector3Int segmentDirection)
    {
        int floorRadius = corridorWidth / 2; int floorStartOffset = -(corridorWidth - 1) / 2; int wallOffset = floorRadius + 1;
        Vector3Int perpendicular = (segmentDirection.x != 0) ? Vector3Int.up : Vector3Int.right;
        for (int w = 0; w < corridorWidth; w++)
        {
            Vector3Int tilePos = centerCell + perpendicular * (floorStartOffset + w);
            if (floorTilemap.GetTile(tilePos) == null) { floorTilemap.SetTile(tilePos, defaultCorridorFloorTile); /* if (decorateCorridors && random.NextDouble() < 0.05) AddCorridorDecoration(tilePos); */ } // Decoration disabled for now
        }
        if (wallTilemap != null && defaultCorridorWallTile != null)
        {
            Vector3Int wallPos1 = centerCell + perpendicular * (-wallOffset); Vector3Int wallPos2 = centerCell + perpendicular * (wallOffset);
            if (floorTilemap.GetTile(wallPos1) == null) wallTilemap.SetTile(wallPos1, defaultCorridorWallTile);
            if (floorTilemap.GetTile(wallPos2) == null) wallTilemap.SetTile(wallPos2, defaultCorridorWallTile);
        }
    }

    private void AddCorridorDecoration(Vector3Int tilePos)
    { /* Keep as is */ }
    // ------------------------------------------------------------------------------------
    #endregion

    #region Entity Spawning
    // (SpawnEntities, SpawnPlayer, SpawnEnemiesAndDecorationsInRooms, TryFindSpawnPositionInRoom, SpawnTreasure - Keep robustness checks added previously)
    // --- Paste the Entity Spawning functions from the previous fixed script here ---
    private void SpawnEntities()
    {
        Debug.Log("Spawning entities...");
        SpawnPlayer();
        SpawnEnemiesAndDecorationsInRooms(); // Combined for efficiency
        SpawnTreasure();
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null) { Debug.LogWarning("Player Prefab not assigned."); return; }
        RoomNode start = startRoomNode;
        if (start == null || !start.isPlaced) { start = levelGraph.nodes.FirstOrDefault(n => n.isPlaced); if (start == null) { Debug.LogError("Cannot spawn player: No placed rooms found."); return; } Debug.LogWarning("Designated Start room not found/placed, spawning player in first available room."); }
        if (TryFindSpawnPositionInRoom(start, out Vector3 spawnPos, 0.5f))
        {
            GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity, entityContainer); player.name = "Player"; Debug.Log($"Spawned player at {spawnPos} in Room {start.id}");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(player, "Spawn Player");
#endif
        }
        else Debug.LogError($"Could not find valid spawn position for player in room {start.id}.");
    }

    private void SpawnEnemiesAndDecorationsInRooms()
    {
        var validEnemyPrefabs = enemyPrefabs?.Where(p => p != null).ToList() ?? new List<GameObject>();
        var validDecorationPrefabs = decorationPrefabs?.Where(p => p != null).ToList() ?? new List<GameObject>();
        bool canSpawnEnemies = validEnemyPrefabs.Count > 0; bool canSpawnDecorations = validDecorationPrefabs.Count > 0;
        if (!canSpawnEnemies) Debug.LogWarning("No valid enemy prefabs. Skipping enemy spawning.");
        if (!canSpawnDecorations) Debug.LogWarning("No valid decoration prefabs. Skipping decoration spawning.");
        if (!canSpawnEnemies && !canSpawnDecorations) return;

        foreach (var room in levelGraph.nodes)
        {
            if (!room.isPlaced || room.instance == null) continue;
            if (canSpawnEnemies)
            {
                int enemyCount = 0; bool noEnemyZone = (room.type == RoomType.Start || room.type == RoomType.Exit || room.type == RoomType.Shop);
                if (!noEnemyZone) { if (room.type == RoomType.Boss) enemyCount = random.Next(1, enemiesPerRoom + 1); else enemyCount = random.Next(0, enemiesPerRoom + 1); }
                for (int i = 0; i < enemyCount; i++)
                {
                    if (TryFindSpawnPositionInRoom(room, out Vector3 spawnPos))
                    {
                        GameObject prefab = validEnemyPrefabs[random.Next(validEnemyPrefabs.Count)]; GameObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity, entityContainer); enemy.name = $"Enemy_{room.id}_{i}";
#if UNITY_EDITOR
                        if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(enemy, "Spawn Enemy");
#endif
                    }
                }
            }
            if (canSpawnDecorations)
            {
                int decorCount = random.Next(0, decorationsPerRoom + 1);
                for (int i = 0; i < decorCount; i++)
                {
                    if (TryFindSpawnPositionInRoom(room, out Vector3 spawnPos))
                    {
                        GameObject prefab = validDecorationPrefabs[random.Next(validDecorationPrefabs.Count)]; GameObject decor = Instantiate(prefab, spawnPos, Quaternion.identity, entityContainer); decor.transform.rotation = Quaternion.Euler(0, 0, random.Next(4) * 90); decor.name = $"Decor_{room.id}_{i}";
#if UNITY_EDITOR
                        if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(decor, "Spawn Decoration");
#endif
                    }
                }
            }
        }
    }

    private bool TryFindSpawnPositionInRoom(RoomNode room, out Vector3 position, float radiusMultiplier = 0.8f)
    {
        position = room.position; if (room.template == null || !room.isPlaced || room.instance == null) return false;
        Vector2Int size = GetRotatedSize(room); float halfWidth = Mathf.Max(0, size.x / 2f - 1f); float halfHeight = Mathf.Max(0, size.y / 2f - 1f);
        radiusMultiplier = Mathf.Clamp01(radiusMultiplier); halfWidth *= radiusMultiplier; halfHeight *= radiusMultiplier;
        if (halfWidth <= 0 && halfHeight <= 0 && size.x > 0 && size.y > 0) { return true; } // Use center for small rooms
        float randomX = (float)(random.NextDouble() * 2 * halfWidth - halfWidth); float randomY = (float)(random.NextDouble() * 2 * halfHeight - halfHeight);
        position = room.position + new Vector3(randomX, randomY, 0); return true;
    }

    private void SpawnTreasure()
    {
        var validTreasurePrefabs = treasurePrefabs?.Where(p => p != null).ToList() ?? new List<GameObject>();
        if (validTreasurePrefabs.Count == 0) { Debug.LogWarning("No valid Treasure Prefabs assigned. Skipping."); return; }
        foreach (var room in levelGraph.nodes)
        {
            if (!room.isPlaced) continue;
            int treasureCount = 0;
            if (room.type == RoomType.Treasure) treasureCount = random.Next(2, 5);
            else if (room.type == RoomType.Boss) treasureCount = random.Next(1, 3);
            else if (room.type == RoomType.Secret) treasureCount = random.Next(1, 4);
            else if (random.NextDouble() < 0.05) treasureCount = 1;
            for (int i = 0; i < treasureCount; i++)
            {
                if (TryFindSpawnPositionInRoom(room, out Vector3 spawnPos))
                {
                    GameObject prefab = validTreasurePrefabs[random.Next(validTreasurePrefabs.Count)]; GameObject treasure = Instantiate(prefab, spawnPos, Quaternion.identity, entityContainer); treasure.name = $"Treasure_{room.id}_{i}";
#if UNITY_EDITOR
                    if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(treasure, "Spawn Treasure");
#endif
                }
            }
        }
    }
    // ------------------------------------------------------------------------------------
    #endregion

    #region Helper Methods
    // (ShuffleArray, ShuffleList - Keep as is from previous fixed version)
    // --- Paste the Helper functions from the previous fixed script here ---
    private void ShuffleArray<T>(T[] array, params Array[] otherArrays) { if (array == null || array.Length <= 1) return; int n = array.Length; while (n > 1) { n--; int k = random.Next(n + 1); T value = array[k]; array[k] = array[n]; array[n] = value; foreach (Array other in otherArrays) { if (other != null && other.Length == array.Length) { object otherValue = other.GetValue(k); other.SetValue(other.GetValue(n), k); other.SetValue(otherValue, n); } } } }
    private void ShuffleList<T>(List<T> list) { if (list == null || list.Count <= 1) return; int n = list.Count; while (n > 1) { n--; int k = random.Next(n + 1); T value = list[k]; list[k] = list[n]; list[n] = value; } }
    // ------------------------------------------------------------------------------------
    #endregion

    #region Editor Visualization
#if UNITY_EDITOR
    // (VisualizeGraph, GetColorForRoomType - Keep as is from previous fixed version)
    // --- Paste the Editor Visualization functions from the previous fixed script here ---
    [ContextMenu("Visualize Graph")] private void VisualizeGraph() { if (levelGraph.nodes.Count == 0) { Debug.LogWarning("No level graph to visualize."); return; } GameObject visualParent = GameObject.Find("GraphVisualization"); if (visualParent != null) DestroyImmediate(visualParent); visualParent = new GameObject("GraphVisualization"); Material nodeMat = new Material(Shader.Find("Unlit/Color")); Material lineMat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")); foreach (var node in levelGraph.nodes) { GameObject nodeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere); nodeObj.transform.SetParent(visualParent.transform); nodeObj.transform.position = node.isPlaced ? node.position : new Vector3(node.id % 10, node.id / 10, 0); nodeObj.transform.localScale = Vector3.one * 0.5f; nodeObj.name = $"Node_{node.id}_{node.type}"; Renderer renderer = nodeObj.GetComponent<Renderer>(); if (renderer != null) { Material instanceMat = new Material(nodeMat); instanceMat.color = GetColorForRoomType(node.type); renderer.material = instanceMat; } var textObj = new GameObject("Label"); textObj.transform.SetParent(nodeObj.transform, false); textObj.transform.localPosition = Vector3.up * 0.5f; var textMesh = textObj.AddComponent<TextMesh>(); textMesh.text = $"{node.id}:{node.type}"; textMesh.fontSize = 10; textMesh.characterSize = 0.1f; textMesh.anchor = TextAnchor.LowerCenter; textMesh.color = Color.black; } HashSet<string> processedConnections = new HashSet<string>(); foreach (var nodeA in levelGraph.nodes) { foreach (int connectedId in nodeA.connections) { string connectionId = nodeA.id < connectedId ? $"{nodeA.id}_{connectedId}" : $"{connectedId}_{nodeA.id}"; if (!processedConnections.Contains(connectionId) && levelGraph.nodeMap.ContainsKey(connectedId)) { RoomNode nodeB = levelGraph.nodeMap[connectedId]; GameObject lineObj = new GameObject($"Line_{nodeA.id}_{nodeB.id}"); lineObj.transform.SetParent(visualParent.transform); LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>(); lineRenderer.material = lineMat; lineRenderer.startColor = GetColorForRoomType(nodeA.type) * 0.8f; lineRenderer.endColor = GetColorForRoomType(nodeB.type) * 0.8f; lineRenderer.startWidth = 0.1f; lineRenderer.endWidth = 0.1f; lineRenderer.positionCount = 2; lineRenderer.SetPosition(0, nodeA.isPlaced ? nodeA.position : new Vector3(nodeA.id % 10, nodeA.id / 10, 0)); lineRenderer.SetPosition(1, nodeB.isPlaced ? nodeB.position : new Vector3(nodeB.id % 10, nodeB.id / 10, 0)); processedConnections.Add(connectionId); } } } Debug.Log("Graph visualization created/updated."); }
    private Color GetColorForRoomType(RoomType type) { switch (type) { case RoomType.Start: return Color.green; case RoomType.Normal: return Color.blue; case RoomType.Treasure: return Color.yellow; case RoomType.Boss: return Color.red; case RoomType.Shop: return Color.cyan; case RoomType.Secret: return Color.magenta; case RoomType.Exit: return Color.gray; default: return Color.white; } }
    // ------------------------------------------------------------------------------------
#endif
    #endregion
}

// Simple data class for TilemapData - Moved outside main class
[System.Serializable]
public class TilemapData
{
    public List<GraphBasedLevelGenerator.DoorwayData> doorways = new List<GraphBasedLevelGenerator.DoorwayData>();
    // Add other relevant tilemap data here if needed
}