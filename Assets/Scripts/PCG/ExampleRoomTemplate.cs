using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Example room template generator.
/// Attach this to a GameObject to quickly set up a room template.
/// </summary>
[ExecuteInEditMode]
public class ExampleRoomTemplate : MonoBehaviour
{
    [Header("Template Settings")]
    [Tooltip("Type of room to create")]
    public TemplateType templateType = TemplateType.Normal;

    [Tooltip("Size of the template (in grid units)")]
    public Vector2Int size = new Vector2Int(10, 8);

    [Header("Room Features")]
    [Tooltip("Number of doorways to create")]
    [Range(1, 4)]
    public int doorwayCount = 2;

    [Tooltip("Whether to randomly place doorways")]
    public bool randomDoorwayPlacement = false;

    [Tooltip("Whether to add internal walls")]
    public bool addInternalWalls = false;

    [Header("References")]
    [Tooltip("Floor Tile to use")]
    public TileBase floorTile;

    [Tooltip("Wall Tile to use")]
    public TileBase wallTile;

    [Tooltip("Door Tile to use (Optional)")]
    public TileBase doorTile;

    // Cached references
    private Tilemap floorTilemap;
    private Tilemap wallTilemap;
    private Tilemap doorTilemap;

    public enum TemplateType
    {
        Normal, Start, Boss, Treasure, Shop, Secret, Exit
    }

    [ContextMenu("Create Example Template")]
    public void CreateExampleTemplate()
    {
#if !UNITY_EDITOR
        Debug.LogError("CreateExampleTemplate can only run in the Unity Editor.");
        return;
#endif

        InitializeTilemaps();
        ClearTilemaps();
        GenerateRoomLayout();

        // Add or update the analyzer component
        RoomTemplateAnalyzer analyzer = GetComponent<RoomTemplateAnalyzer>();
        if (analyzer == null)
        {
            analyzer = gameObject.AddComponent<RoomTemplateAnalyzer>();
        }

        // Set up analyzer properties
        analyzer.templateName = $"{templateType}Room_{size.x}x{size.y}";
        analyzer.floorTilemap = floorTilemap;
        analyzer.wallTilemap = wallTilemap;
        analyzer.doorTilemap = doorTilemap;
        analyzer.detectDoorwaysFromWallGaps = (doorTile == null);

        // Set valid room types based on template type
        GraphBasedLevelGenerator.RoomType roomType;
        switch (templateType)
        {
            case TemplateType.Start: roomType = GraphBasedLevelGenerator.RoomType.Start; break;
            case TemplateType.Boss: roomType = GraphBasedLevelGenerator.RoomType.Boss; break;
            case TemplateType.Treasure: roomType = GraphBasedLevelGenerator.RoomType.Treasure; break;
            case TemplateType.Shop: roomType = GraphBasedLevelGenerator.RoomType.Shop; break;
            case TemplateType.Secret: roomType = GraphBasedLevelGenerator.RoomType.Secret; break;
            case TemplateType.Exit: roomType = GraphBasedLevelGenerator.RoomType.Exit; break;
            default: roomType = GraphBasedLevelGenerator.RoomType.Normal; break;
        }
        analyzer.validRoomTypes = new GraphBasedLevelGenerator.RoomType[] { roomType };

        Debug.Log($"Example template created for '{analyzer.templateName}'. Now run 'Analyze Template' on the RoomTemplateAnalyzer component.");
    }

    private void InitializeTilemaps()
    {
        // Find or create tilemaps under a "Tilemaps" child object
        Transform tilemapsParent = transform.Find("Tilemaps");
        if (tilemapsParent == null)
        {
            tilemapsParent = new GameObject("Tilemaps").transform;
            tilemapsParent.SetParent(transform);
            tilemapsParent.localPosition = Vector3.zero;
        }

        floorTilemap = FindOrCreateTilemap("FloorTilemap", tilemapsParent, 0);
        wallTilemap = FindOrCreateTilemap("WallTilemap", tilemapsParent, 1);
        doorTilemap = FindOrCreateTilemap("DoorTilemap", tilemapsParent, 2);
    }

    private Tilemap FindOrCreateTilemap(string name, Transform parent, int sortingOrder)
    {
        Transform tilemapTransform = parent.Find(name);
        Tilemap tilemap;

        if (tilemapTransform != null)
        {
            tilemap = tilemapTransform.GetComponent<Tilemap>();
            if (tilemap == null)
            {
                tilemap = tilemapTransform.gameObject.AddComponent<Tilemap>();
                if (tilemapTransform.GetComponent<TilemapRenderer>() == null)
                    tilemapTransform.gameObject.AddComponent<TilemapRenderer>();
            }
        }
        else
        {
            GameObject tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent);
            tilemapObject.transform.localPosition = Vector3.zero;
            tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();
        }

        // Ensure Grid component exists on the root object
        if (transform.GetComponent<Grid>() == null)
        {
            transform.gameObject.AddComponent<Grid>();
        }

        // Set renderer properties
        TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
        }
        return tilemap;
    }

    private void ClearTilemaps()
    {
        if (floorTilemap) floorTilemap.ClearAllTiles();
        if (wallTilemap) wallTilemap.ClearAllTiles();
        if (doorTilemap) doorTilemap.ClearAllTiles();
    }

    private void GenerateRoomLayout()
    {
        if (floorTile == null || wallTile == null)
        {
            Debug.LogWarning("Cannot generate room layout: Floor or Wall Tile not assigned.");
            return;
        }

        size.x = Mathf.Max(5, size.x); // Ensure min size
        size.y = Mathf.Max(5, size.y);

        // Calculate bounds relative to local origin (0,0)
        int startX = -size.x / 2;
        int startY = -size.y / 2;
        int endX = startX + size.x;
        int endY = startY + size.y;

        // Generate floor
        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                floorTilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }

        // Generate walls around the floor
        GenerateWalls(startX, startY, endX, endY);

        // Add doorways
        GenerateDoorways(startX, startY, endX, endY);

        // Add internal features if requested
        if (addInternalWalls)
        {
            GenerateInternalFeatures(startX, startY, endX, endY);
        }
    }

    private void GenerateWalls(int startX, int startY, int endX, int endY)
    {
        // Generate walls just outside the floor area
        for (int x = startX - 1; x <= endX; x++)
        {
            wallTilemap.SetTile(new Vector3Int(x, startY - 1, 0), wallTile); // Bottom wall
            wallTilemap.SetTile(new Vector3Int(x, endY, 0), wallTile);       // Top wall
        }
        for (int y = startY; y < endY; y++)
        {
            wallTilemap.SetTile(new Vector3Int(startX - 1, y, 0), wallTile); // Left wall
            wallTilemap.SetTile(new Vector3Int(endX, y, 0), wallTile);       // Right wall
        }
    }

    private void GenerateDoorways(int startX, int startY, int endX, int endY)
    {
        // Calculate midpoints for doorway placement
        int midX = (startX + endX - 1) / 2; // Midpoint adjusted for wall layer
        int midY = (startY + endY - 1) / 2;

        // Potential doorway positions (on the wall layer coordinates)
        Vector3Int[] potentialPositions = new Vector3Int[4];
        potentialPositions[0] = new Vector3Int(midX, endY, 0);           // Top wall center
        potentialPositions[1] = new Vector3Int(endX, midY, 0);           // Right wall center
        potentialPositions[2] = new Vector3Int(midX, startY - 1, 0);     // Bottom wall center
        potentialPositions[3] = new Vector3Int(startX - 1, midY, 0);     // Left wall center

        // If random placement, choose positions along the walls
        if (randomDoorwayPlacement)
        {
            int padding = 2; // Keep doors away from corners
                             // Top/Bottom
            if (endX - startX > padding * 2 + 1)
            {
                int topBottomX = UnityEngine.Random.Range(startX + padding, endX - padding);
                potentialPositions[0].x = topBottomX;
                potentialPositions[2].x = topBottomX;
            }
            // Left/Right
            if (endY - startY > padding * 2 + 1)
            {
                int leftRightY = UnityEngine.Random.Range(startY + padding, endY - padding);
                potentialPositions[1].y = leftRightY;
                potentialPositions[3].y = leftRightY;
            }
        }

        // Randomly choose which walls get doorways
        List<int> indices = new List<int> { 0, 1, 2, 3 };
        indices = indices.OrderBy(x => UnityEngine.Random.value).ToList(); // Shuffle indices

        // Place requested number of doorways
        for (int i = 0; i < doorwayCount && i < indices.Count; i++)
        {
            Vector3Int pos = potentialPositions[indices[i]];

            // Clear wall tile
            wallTilemap.SetTile(pos, null);

            // Place door tile (if assigned)
            if (doorTile != null)
            {
                doorTilemap.SetTile(pos, doorTile);
            }
        }
    }

    private void GenerateInternalFeatures(int startX, int startY, int endX, int endY)
    {
        // Calculate midpoints to avoid placing obstacles in center lines
        int midX = (startX + endX - 1) / 2;
        int midY = (startY + endY - 1) / 2;

        // Add a few random internal walls or pillars
        int featureCount = UnityEngine.Random.Range(1, 4);
        int roomWidth = endX - startX;
        int roomHeight = endY - startY;

        for (int i = 0; i < featureCount; i++)
        {
            if (UnityEngine.Random.value < 0.6f && roomWidth > 3 && roomHeight > 3)
            { // Add internal wall segment
                bool horizontal = UnityEngine.Random.value > 0.5f;
                if (horizontal)
                {
                    int y = UnityEngine.Random.Range(startY + 1, endY - 1);
                    int xStart = UnityEngine.Random.Range(startX + 1, endX - 2);
                    int length = UnityEngine.Random.Range(2, Mathf.Max(3, roomWidth / 2));

                    for (int l = 0; l < length && xStart + l < endX - 1; l++)
                    {
                        wallTilemap.SetTile(new Vector3Int(xStart + l, y, 0), wallTile);
                    }

                    // Add gap in walls longer than 2 tiles
                    if (length > 2)
                    {
                        int gapPosition = UnityEngine.Random.Range(0, length);
                        wallTilemap.SetTile(new Vector3Int(xStart + gapPosition, y, 0), null);
                    }
                }
                else
                { // Vertical wall
                    int x = UnityEngine.Random.Range(startX + 1, endX - 1);
                    int yStart = UnityEngine.Random.Range(startY + 1, endY - 2);
                    int length = UnityEngine.Random.Range(2, Mathf.Max(3, roomHeight / 2));

                    for (int l = 0; l < length && yStart + l < endY - 1; l++)
                    {
                        wallTilemap.SetTile(new Vector3Int(x, yStart + l, 0), wallTile);
                    }

                    // Add gap in walls longer than 2 tiles
                    if (length > 2)
                    {
                        int gapPosition = UnityEngine.Random.Range(0, length);
                        wallTilemap.SetTile(new Vector3Int(x, yStart + gapPosition, 0), null);
                    }
                }
            }
            else
            { // Add pillar
                int x = UnityEngine.Random.Range(startX + 1, endX - 1);
                int y = UnityEngine.Random.Range(startY + 1, endY - 1);

                // Avoid placing directly in center lines
                if (x == midX && y == midY) continue;

                wallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ExampleRoomTemplate))]
public class ExampleRoomTemplateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // Draw the default fields

        EditorGUILayout.Space(10);
        ExampleRoomTemplate template = (ExampleRoomTemplate)target;

        // Button to generate the layout
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f); // Green button

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Generate Room Layout", GUILayout.Height(30)))
        {
            // Record Undo operations
            Undo.RecordObject(template, "Generate Room Layout");
            if (template.GetComponentInChildren<Tilemap>() != null)
            {
                Undo.RecordObjects(template.GetComponentsInChildren<Tilemap>(), "Generate Room Layout (Tilemaps)");
            }

            template.CreateExampleTemplate();

            // Mark objects as dirty
            EditorUtility.SetDirty(template);
            foreach (var tm in template.GetComponentsInChildren<Tilemap>())
            {
                EditorUtility.SetDirty(tm);
            }

            // Refresh scene view
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Help box with instructions
        if (template.floorTile == null || template.wallTile == null)
        {
            EditorGUILayout.HelpBox("Assign Floor and Wall Tiles to enable layout generation.", MessageType.Warning);
        }

        EditorGUILayout.HelpBox("After generating layout, use the 'Analyze Template' button on the RoomTemplateAnalyzer component to detect doorways and bounds.", MessageType.Info);
    }
}
#endif
