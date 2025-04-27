



using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Analyzes a Room Prefab containing Tilemaps to determine its bounds and doorway locations.
/// </summary>
[ExecuteInEditMode]
public class RoomTemplateAnalyzer : MonoBehaviour
{
    [Header("Template Metadata")]
    [Tooltip("Template name used for identification")]
    public string templateName = "New Template";

    [Tooltip("Room types this template can be used for")]
    public GraphBasedLevelGenerator.RoomType[] validRoomTypes = new GraphBasedLevelGenerator.RoomType[]
    {
        GraphBasedLevelGenerator.RoomType.Normal
    };

    [Tooltip("Weight for random selection")]
    [Range(1, 100)]
    public int selectionWeight = 50;

    [Tooltip("Whether this template can be rotated")]
    public bool allowRotation = true;

    [Header("Tilemap References")]
    [Tooltip("Tilemap containing floor tiles")]
    public Tilemap floorTilemap;

    [Tooltip("Tilemap containing wall tiles")]
    public Tilemap wallTilemap;

    [Tooltip("Tilemap containing door/entrance tiles")]
    public Tilemap doorTilemap;

    [Header("Doorway Detection")]
    [Tooltip("Tiles to consider as doorways")]
    public TileBase[] doorwayTiles;

    [Tooltip("Detect doorways using wall gaps")]
    public bool detectDoorwaysFromWallGaps = false;

    // Analyzed Data (Read by Generator)
    [SerializeField, HideInInspector]
    private BoundsInt _analyzedBounds;

    [SerializeField, HideInInspector]
    private List<GraphBasedLevelGenerator.DoorwayData> _doorways = new List<GraphBasedLevelGenerator.DoorwayData>();

    // Public accessors
    public BoundsInt GetAnalyzedBounds() => _analyzedBounds;
    public List<GraphBasedLevelGenerator.DoorwayData> GetDoorways() => _doorways;

    [Header("Debug Visualization")]
    [Tooltip("Show bounds visualization")]
    public bool showBoundsGizmo = true;

    [Tooltip("Show doorway gizmos")]
    public bool showDoorwayGizmos = true;

    public Color boundsGizmoColor = new Color(0, 0.5f, 1, 0.2f);
    public Color doorwayGizmoColor = new Color(0, 1, 0, 0.5f);
    public Color doorwayDirectionColor = new Color(1, 0.5f, 0, 0.8f);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (showBoundsGizmo && _analyzedBounds.size != Vector3Int.zero)
            {
                DrawBoundsGizmo();
            }

            if (showDoorwayGizmos && _doorways != null && _doorways.Count > 0)
            {
                DrawDoorwayGizmos();
            }
        }
    }

    private void DrawBoundsGizmo()
    {
        Gizmos.color = boundsGizmoColor;
        Vector3 boundsCenter = transform.TransformPoint(new Vector3(
            _analyzedBounds.center.x + 0.5f,
            _analyzedBounds.center.y + 0.5f,
            0
        ));
        Vector3 boundsSize = Vector3.Scale(new Vector3(
            _analyzedBounds.size.x,
            _analyzedBounds.size.y,
            1
        ), transform.lossyScale);

        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        Vector3 localCenter = new Vector3(
            _analyzedBounds.center.x + 0.5f,
            _analyzedBounds.center.y + 0.5f,
            0
        );
        Vector3 localSize = new Vector3(
            _analyzedBounds.size.x,
            _analyzedBounds.size.y,
            1
        );

        Gizmos.DrawCube(localCenter, localSize);
        Gizmos.color = new Color(boundsGizmoColor.r, boundsGizmoColor.g, boundsGizmoColor.b, 0.8f);
        Gizmos.DrawWireCube(localCenter, localSize);

        Gizmos.matrix = originalMatrix;

        Handles.Label(boundsCenter + Vector3.up * (boundsSize.y / 2 + 1),
            $"{templateName} ({_analyzedBounds.size.x}x{_analyzedBounds.size.y})");
    }

    private void DrawDoorwayGizmos()
    {
        foreach (var doorway in _doorways)
        {
            Vector3 localPos = doorway.position + new Vector3(0.5f, 0.5f, 0);
            Vector3 worldPos = transform.TransformPoint(localPos);

            Gizmos.color = doorwayGizmoColor;
            Gizmos.DrawCube(worldPos, Vector3.one * 0.8f);

            if (doorway.direction != Vector3Int.zero)
            {
                Vector3 worldDir = transform.TransformDirection((Vector3)doorway.direction).normalized;
                Gizmos.color = doorwayDirectionColor;
                Vector3 arrowEnd = worldPos + worldDir * 1.0f;
                Gizmos.DrawLine(worldPos, arrowEnd);

                // Simple arrowhead
                Vector3 right = Vector3.Cross(worldDir, Vector3.forward).normalized * 0.3f;
                Gizmos.DrawLine(arrowEnd, arrowEnd - worldDir * 0.5f + right);
                Gizmos.DrawLine(arrowEnd, arrowEnd - worldDir * 0.5f - right);
            }

            // Visualize width
            if (doorway.width > 1)
            {
                Vector3 perpDir = Vector3.Cross((Vector3)doorway.direction, Vector3.forward).normalized;
                Vector3 worldPerp = transform.TransformDirection(perpDir);
                float halfWidth = (doorway.width - 1) * 0.5f;

                Gizmos.color = new Color(doorwayGizmoColor.r, doorwayGizmoColor.g, doorwayGizmoColor.b, 0.5f);
                for (float offset = -halfWidth; offset <= halfWidth; offset += 1.0f)
                {
                    if (Mathf.Approximately(offset, 0f)) continue; // Skip center
                    Gizmos.DrawSphere(worldPos + worldPerp * offset, 0.2f);
                }
            }
        }
    }
#endif



    [ContextMenu("Analyze Template")]
    public void AnalyzeTemplate()
    {
#if !UNITY_EDITOR
        Debug.LogError("AnalyzeTemplate can only be called in the Unity Editor.");
        return;
#endif

        Debug.Log($"Analyzing room template '{gameObject.name}'...");

        // Reset analyzed data
        _analyzedBounds = new BoundsInt(0, 0, 0, 0, 0, 0);
        _doorways.Clear();

        // 1. Calculate Bounds
        List<Tilemap> mapsForBounds = GetTilemapsForAnalysis();
        if (mapsForBounds.Count == 0)
        {
            Debug.LogError("No Tilemaps found or assigned for analysis.", this);
            return;
        }

        _analyzedBounds = CalculateCombinedBounds(mapsForBounds);
        if (_analyzedBounds.size == Vector3Int.zero)
        {
            Debug.LogWarning("Template appears empty. Analyzed bounds have zero size.", this);
        }

        // 2. Detect Doorways
        List<GraphBasedLevelGenerator.DoorwayData> detected = new List<GraphBasedLevelGenerator.DoorwayData>();

        if (doorTilemap != null)
        {
            DetectDoorwaysFromTilemap(doorTilemap, doorwayTiles, detected);
        }

        if (detected.Count == 0 && detectDoorwaysFromWallGaps && wallTilemap != null && floorTilemap != null)
        {
            Debug.Log("Attempting detection via Wall Gaps.");
            DetectDoorwaysFromWallGaps(detected);
        }

        // 3. Process and Store Doorways
        _doorways = MergeAdjacentDoorways(detected);

        Debug.Log($"Analysis complete: Size={_analyzedBounds.size}, Detected Doorways={_doorways.Count}");

#if UNITY_EDITOR
        // Mark component dirty
        EditorUtility.SetDirty(this);
        EditorApplication.RepaintHierarchyWindow();
        SceneView.RepaintAll();
        Debug.LogWarning("Remember to SAVE the Scene or APPLY changes to the PREFAB!", this);
#endif
    }





    private List<Tilemap> GetTilemapsForAnalysis()
    {
        List<Tilemap> maps = new List<Tilemap>();

        if (floorTilemap != null) maps.Add(floorTilemap);
        if (wallTilemap != null) maps.Add(wallTilemap);
        if (doorTilemap != null) maps.Add(doorTilemap);

        if (maps.Count > 0) return maps; // Use assigned maps if available

        // Fallback: Get all child tilemaps
        Debug.LogWarning("No specific tilemaps assigned, analyzing all child tilemaps.", this);
        maps.AddRange(GetComponentsInChildren<Tilemap>(true));
        return maps;
    }

    // Calculates combined bounds of multiple tilemaps
    private BoundsInt CalculateCombinedBounds(List<Tilemap> maps)
    {
        BoundsInt combinedBounds = new BoundsInt(0, 0, 0, 0, 0, 0);
        bool firstMap = true;

        foreach (var map in maps)
        {
            map.CompressBounds();
            BoundsInt mapBounds = map.cellBounds;

            if (map.GetUsedTilesCount() == 0 && mapBounds.size == Vector3Int.zero) continue; // Skip empty

            if (firstMap)
            {
                combinedBounds = mapBounds;
                firstMap = false;
            }
            else
            {
                // Extend overall bounds to include this tilemap
                int xMin = Mathf.Min(combinedBounds.xMin, mapBounds.xMin);
                int yMin = Mathf.Min(combinedBounds.yMin, mapBounds.yMin);
                int zMin = Mathf.Min(combinedBounds.zMin, mapBounds.zMin);
                int xMax = Mathf.Max(combinedBounds.xMax, mapBounds.xMax);
                int yMax = Mathf.Max(combinedBounds.yMax, mapBounds.yMax);
                int zMax = Mathf.Max(combinedBounds.zMax, mapBounds.zMax);

                combinedBounds = new BoundsInt(
                    xMin, yMin, zMin,
                    xMax - xMin, yMax - yMin, zMax - zMin
                );
            }
        }
        return combinedBounds;
    }

    // Detects doorways based on specific tiles in a given tilemap
    private void DetectDoorwaysFromTilemap(Tilemap map, TileBase[] allowedDoorTiles, List<GraphBasedLevelGenerator.DoorwayData> results)
    {
        bool anyTileIsDoor = (allowedDoorTiles == null || allowedDoorTiles.Length == 0);

        foreach (Vector3Int pos in map.cellBounds.allPositionsWithin)
        {
            TileBase tile = map.GetTile(pos);
            if (tile == null) continue;

            bool isDoorTile = anyTileIsDoor || (allowedDoorTiles != null && allowedDoorTiles.Contains(tile));
            if (isDoorTile)
            {
                Vector3Int direction = DetectDoorwayDirection(pos);
                results.Add(new GraphBasedLevelGenerator.DoorwayData
                {
                    position = pos,
                    direction = direction,
                    width = 1,
                    connected = false
                });
            }
        }
        Debug.Log($"Found {results.Count} potential doorway tiles on '{map.name}'.");
    }

    // Detects doorways by finding gaps in walls
    private void DetectDoorwaysFromWallGaps(List<GraphBasedLevelGenerator.DoorwayData> results)
    {
        if (floorTilemap == null || wallTilemap == null || _analyzedBounds.size == Vector3Int.zero) return;

        Vector3Int[] directions = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };
        BoundsInt floorBounds = floorTilemap.cellBounds;

        foreach (Vector3Int floorPos in floorBounds.allPositionsWithin)
        {
            if (floorTilemap.GetTile(floorPos) == null) continue;

            foreach (Vector3Int dir in directions)
            {
                Vector3Int adjacentPos = floorPos + dir;

                // Check if the adjacent spot lacks both floor and wall (it's a gap)
                bool hasFloor = floorTilemap.GetTile(adjacentPos) != null;
                bool hasWall = wallTilemap.GetTile(adjacentPos) != null;

                if (!hasFloor && !hasWall)
                {
                    // Check if this could be a doorway
                    Vector3Int behindPos = floorPos - dir;
                    bool behindIsSolid = (floorTilemap.GetTile(behindPos) != null || wallTilemap.GetTile(behindPos) != null);

                    if (behindIsSolid)
                    {
                        results.Add(new GraphBasedLevelGenerator.DoorwayData
                        {
                            position = adjacentPos,
                            direction = dir,
                            width = 1,
                            connected = false
                        });
                    }
                }
            }
        }
        Debug.Log($"Found {results.Count} potential wall gap doorways.");
    }

    // Determines doorway direction based on surrounding tiles
    private Vector3Int DetectDoorwayDirection(Vector3Int doorPos)
    {
        Vector3Int[] directions = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };

        // --- MODIFICATION START ---
        // Prioritize checking the tile behind the potential exit direction
        foreach (var dir in directions)
        {
            Vector3Int behindPos = doorPos - dir;
            // Check if the tile 'behind' the door position has a floor tile (indicating inside the room)
            if (floorTilemap != null && floorTilemap.GetTile(behindPos) != null)
            {
                // Now also check if the tile in the exit direction is actually clear
                Vector3Int exitPos = doorPos + dir;
                bool wallBlocks = wallTilemap != null && wallTilemap.GetTile(exitPos) != null;
                bool floorBlocks = floorTilemap != null && floorTilemap.GetTile(exitPos) != null;
                // Optional: Check door tilemap too if it might contain blockers
                // bool doorBlocks = doorTilemap != null && doorTilemap.GetTile(exitPos) != null;

                if (!wallBlocks && !floorBlocks /* && !doorBlocks */)
                {
                    return dir; // Found floor behind, and clear path ahead
                }
            }
        }
        // --- MODIFICATION END ---


        // Original empty space check (can keep as secondary check)
        foreach (var dir in directions)
        {
            Vector3Int checkPos = doorPos + dir;
            bool hasAnyTile = false;
            // ... (rest of original empty check logic) ...
            if (!hasAnyTile)
            {
                return dir;
            }
        }

        // Fallback to bounds (keep as last resort)
        if (_analyzedBounds.size != Vector3Int.zero) // Check if bounds are valid before using edge detection
        {
            if (doorPos.x == _analyzedBounds.xMin) return Vector3Int.left;
            if (doorPos.x == _analyzedBounds.xMax - 1) return Vector3Int.right;
            if (doorPos.y == _analyzedBounds.yMin) return Vector3Int.down;
            if (doorPos.y == _analyzedBounds.yMax - 1) return Vector3Int.up;
        }


        Debug.LogWarning($"Could not determine direction for doorway at {doorPos}", this);
        return Vector3Int.zero; // Couldn't determine direction
    }

    // Merges adjacent doorways to create wider doorways
    private List<GraphBasedLevelGenerator.DoorwayData> MergeAdjacentDoorways(List<GraphBasedLevelGenerator.DoorwayData> detected)
    {
        if (detected.Count <= 1) return detected;

        List<GraphBasedLevelGenerator.DoorwayData> merged = new List<GraphBasedLevelGenerator.DoorwayData>();
        HashSet<int> processedIndices = new HashSet<int>();

        // Sort for consistent processing
        var sortedDoorways = detected.OrderBy(d => d.position.x).ThenBy(d => d.position.y).ToList();

        for (int i = 0; i < sortedDoorways.Count; i++)
        {
            if (processedIndices.Contains(i)) continue;

            var doorway = sortedDoorways[i];
            List<GraphBasedLevelGenerator.DoorwayData> group = new List<GraphBasedLevelGenerator.DoorwayData> { doorway };
            processedIndices.Add(i);

            // Determine perpendicular direction for width checking
            Vector3Int perpDir = Vector3Int.zero;
            if (doorway.direction.x != 0) perpDir = new Vector3Int(0, 1, 0);
            else if (doorway.direction.y != 0) perpDir = new Vector3Int(1, 0, 0);

            // Find adjacent doorways in the group
            for (int j = 0; j < sortedDoorways.Count; j++)
            {
                if (i == j || processedIndices.Contains(j)) continue;

                var other = sortedDoorways[j];

                // Must have same direction or one must be undetermined
                if (doorway.direction != Vector3Int.zero && other.direction != Vector3Int.zero &&
                    doorway.direction != other.direction && doorway.direction != -other.direction)
                    continue;

                // Check if positions are adjacent along the perpendicular axis
                if (perpDir != Vector3Int.zero &&
                    (other.position == doorway.position + perpDir || other.position == doorway.position - perpDir))
                {
                    group.Add(other);
                    processedIndices.Add(j);
                }
            }

            // Create merged doorway from the group
            if (group.Count > 1)
            {
                // Calculate average position
                Vector3 avgPos = Vector3.zero;
                foreach (var d in group) avgPos += (Vector3)d.position;
                avgPos /= group.Count;

                // Use determined direction or try to infer from group
                Vector3Int groupDir = doorway.direction;
                if (groupDir == Vector3Int.zero)
                {
                    groupDir = InferDirectionFromGroup(group);
                }

                merged.Add(new GraphBasedLevelGenerator.DoorwayData
                {
                    position = Vector3Int.RoundToInt(avgPos),
                    direction = groupDir,
                    width = group.Count,
                    connected = false
                });
            }
            else
            {
                // Single doorway, keep as-is
                merged.Add(doorway);
            }
        }

        return merged;
    }



    [ContextMenu("Create Template Asset File")]
    public void CreateTemplateAssetFile()
    {
#if UNITY_EDITOR
        // Run analysis first to ensure data is current
        AnalyzeTemplate();

        if (_analyzedBounds.size == Vector3Int.zero)
        {
            Debug.LogError("Cannot create template asset: Analysis resulted in zero bounds size.", this);
            return;
        }

        // Create a ScriptableObject to store the template data
        var templateAsset = ScriptableObject.CreateInstance<RoomTemplateAsset>();
        templateAsset.templateName = this.templateName;
        templateAsset.validRoomTypes = this.validRoomTypes.ToArray();
        templateAsset.selectionWeight = this.selectionWeight;
        templateAsset.allowRotation = this.allowRotation;
        templateAsset.size = new Vector2Int(_analyzedBounds.size.x, _analyzedBounds.size.y);

        // Create deep copy of doorway data
        templateAsset.doorways = _doorways.Select(d => new GraphBasedLevelGenerator.DoorwayData
        {
            position = d.position,
            direction = d.direction,
            width = d.width,
            connected = false
        }).ToList();

        // Store reference to the template prefab
        templateAsset.sourcePrefab = gameObject;

        // Save the asset
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Template Asset",
            templateName + "_Template",
            "asset",
            "Save room template as asset file"
        );

        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(templateAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Template saved to {path}", templateAsset);
            Selection.activeObject = templateAsset;
        }
#else
    Debug.LogError("CreateTemplateAssetFile can only be called in the Unity Editor.");
#endif
    }






    private Vector3Int InferDirectionFromGroup(List<GraphBasedLevelGenerator.DoorwayData> group)
    {
        // Try to infer direction from other doorways in group that have direction
        foreach (var doorway in group)
        {
            if (doorway.direction != Vector3Int.zero)
                return doorway.direction;
        }

        // Try to infer from bounds position
        var firstPos = group[0].position;
        if (firstPos.x == _analyzedBounds.xMin) return Vector3Int.left;
        if (firstPos.x == _analyzedBounds.xMax - 1) return Vector3Int.right;
        if (firstPos.y == _analyzedBounds.yMin) return Vector3Int.down;
        if (firstPos.y == _analyzedBounds.yMax - 1) return Vector3Int.up;

        return Vector3Int.zero; // Could not determine
    }
}









#if UNITY_EDITOR
[CustomEditor(typeof(RoomTemplateAnalyzer))]
public class RoomTemplateAnalyzerEditor : Editor
{
    private RoomTemplateAnalyzer analyzer;
    private SerializedProperty templateNameProp;
    private SerializedProperty validRoomTypesProp;
    private SerializedProperty selectionWeightProp;
    private SerializedProperty allowRotationProp;
    private SerializedProperty floorTilemapProp;
    private SerializedProperty wallTilemapProp;
    private SerializedProperty doorTilemapProp;
    private SerializedProperty doorwayTilesProp;
    private SerializedProperty detectDoorwaysFromWallGapsProp;
    private SerializedProperty showBoundsGizmoProp;
    private SerializedProperty showDoorwayGizmosProp;
    private SerializedProperty boundsGizmoColorProp;
    private SerializedProperty doorwayGizmoColorProp;
    private SerializedProperty doorwayDirectionColorProp;

    // Foldout states
    private bool showTemplateSettings = true;
    private bool showTilemapReferences = true;
    private bool showDoorwaySettings = true;
    private bool showDebugSettings = true;

    private void OnEnable()
    {
        analyzer = (RoomTemplateAnalyzer)target;

        // Find serialized properties
        templateNameProp = serializedObject.FindProperty("templateName");
        validRoomTypesProp = serializedObject.FindProperty("validRoomTypes");
        selectionWeightProp = serializedObject.FindProperty("selectionWeight");
        allowRotationProp = serializedObject.FindProperty("allowRotation");
        floorTilemapProp = serializedObject.FindProperty("floorTilemap");
        wallTilemapProp = serializedObject.FindProperty("wallTilemap");
        doorTilemapProp = serializedObject.FindProperty("doorTilemap");
        doorwayTilesProp = serializedObject.FindProperty("doorwayTiles");
        detectDoorwaysFromWallGapsProp = serializedObject.FindProperty("detectDoorwaysFromWallGaps");
        showBoundsGizmoProp = serializedObject.FindProperty("showBoundsGizmo");
        showDoorwayGizmosProp = serializedObject.FindProperty("showDoorwayGizmos");
        boundsGizmoColorProp = serializedObject.FindProperty("boundsGizmoColor");
        doorwayGizmoColorProp = serializedObject.FindProperty("doorwayGizmoColor");
        doorwayDirectionColorProp = serializedObject.FindProperty("doorwayDirectionColor");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Room Template Analyzer", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Analyzes room templates for procedural generation", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Template Settings
        showTemplateSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showTemplateSettings, "Template Settings");
        if (showTemplateSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(templateNameProp, new GUIContent("Template Name", "Name for identification"));
            EditorGUILayout.PropertyField(validRoomTypesProp, new GUIContent("Valid Room Types", "Room types this template can be used for"));
            EditorGUILayout.PropertyField(selectionWeightProp, new GUIContent("Selection Weight", "Weight for random selection (higher = more likely)"));
            EditorGUILayout.PropertyField(allowRotationProp, new GUIContent("Allow Rotation", "Whether this template can be rotated"));

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Tilemap References
        showTilemapReferences = EditorGUILayout.BeginFoldoutHeaderGroup(showTilemapReferences, "Tilemap References");
        if (showTilemapReferences)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(floorTilemapProp, new GUIContent("Floor Tilemap", "Tilemap containing floor tiles"));
            EditorGUILayout.PropertyField(wallTilemapProp, new GUIContent("Wall Tilemap", "Tilemap containing wall tiles"));
            EditorGUILayout.PropertyField(doorTilemapProp, new GUIContent("Door Tilemap", "Tilemap containing door/entrance tiles"));

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Auto-Detect Tilemaps"))
            {
                AutoDetectTilemaps();
            }

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Doorway Settings
        showDoorwaySettings = EditorGUILayout.BeginFoldoutHeaderGroup(showDoorwaySettings, "Doorway Detection");
        if (showDoorwaySettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(doorwayTilesProp, new GUIContent("Doorway Tiles", "Tiles to consider as doorways"));
            EditorGUILayout.PropertyField(detectDoorwaysFromWallGapsProp, new GUIContent("Detect via Wall Gaps", "Whether to detect doorways using gaps in walls"));

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Debug Settings
        showDebugSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showDebugSettings, "Debug Settings");
        if (showDebugSettings)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.PropertyField(showBoundsGizmoProp, new GUIContent("Show Bounds Gizmo", "Show bounds visualization"));
            EditorGUILayout.PropertyField(showDoorwayGizmosProp, new GUIContent("Show Doorway Gizmos", "Show debug gizmos for doorways"));
            EditorGUILayout.PropertyField(boundsGizmoColorProp, new GUIContent("Bounds Gizmo Color", "Gizmo color for bounds"));
            EditorGUILayout.PropertyField(doorwayGizmoColorProp, new GUIContent("Doorway Gizmo Color", "Gizmo color for doorways"));
            EditorGUILayout.PropertyField(doorwayDirectionColorProp, new GUIContent("Direction Gizmo Color", "Gizmo color for doorway directions"));

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Action buttons
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Analyze Template", GUILayout.Height(30)))
        {
            analyzer.AnalyzeTemplate();
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();

        // Show analyzed data in read-only format
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Analyzed Data", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.LabelField("Bounds Size", analyzer.GetAnalyzedBounds().size.ToString());
        EditorGUILayout.LabelField("Doorways Found", analyzer.GetDoorways()?.Count.ToString() ?? "0");
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }

    private void AutoDetectTilemaps()
    {
        Tilemap[] tilemaps = analyzer.GetComponentsInChildren<Tilemap>();

        foreach (var tilemap in tilemaps)
        {
            string name = tilemap.name.ToLower();

            if (name.Contains("floor") || name.Contains("ground"))
            {
                floorTilemapProp.objectReferenceValue = tilemap;
            }
            else if (name.Contains("wall"))
            {
                wallTilemapProp.objectReferenceValue = tilemap;
            }
            else if (name.Contains("door") || name.Contains("entrance") || name.Contains("exit"))
            {
                doorTilemapProp.objectReferenceValue = tilemap;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif

#if UNITY_EDITOR
/// <summary>
/// ScriptableObject to store analyzed room template data as an asset file.
/// </summary>
public class RoomTemplateAsset : ScriptableObject
{
    public string templateName;
    public GraphBasedLevelGenerator.RoomType[] validRoomTypes;
    public int selectionWeight;
    public bool allowRotation;
    public Vector2Int size;
    public List<GraphBasedLevelGenerator.DoorwayData> doorways;
    public GameObject sourcePrefab; // Reference back to the original prefab
}
#endif