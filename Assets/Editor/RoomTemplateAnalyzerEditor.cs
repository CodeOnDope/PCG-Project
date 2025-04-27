#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Tilemaps;
using UnityEngine;

/// <summary>
/// Custom Inspector for the RoomTemplateAnalyzer with an improved UI layout.
/// Provides buttons to trigger analysis and create optional assets.
/// </summary>
[CustomEditor(typeof(RoomTemplateAnalyzer))]
public class RoomTemplateAnalyzerEditor : Editor
{
    private RoomTemplateAnalyzer analyzer;

    // --- Serialized Properties ---
    #region Serialized Properties
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
    private SerializedProperty analyzedBoundsProp; // To show read-only data
    private SerializedProperty doorwaysProp;     // To show read-only data
    #endregion

    // GUI Styles
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle boxStyle;
    private GUIStyle buttonStyleLarge;


    private void OnEnable()
    {
        // Ensure target is valid before proceeding
        if (target == null)
        {
            // This can happen during script recompilation or if component is missing
            return;
        }
        if (!(target is RoomTemplateAnalyzer))
        {
            Debug.LogError($"RoomTemplateAnalyzerEditor: Target object is not a RoomTemplateAnalyzer, it is a {target.GetType()}.");
            return;
        }

        analyzer = (RoomTemplateAnalyzer)target;
        FindSerializedProperties();
    }

    private void FindSerializedProperties()
    {
        // Check if serializedObject is valid before finding properties
        if (serializedObject == null || !serializedObject.targetObject)
        {
            // Log error or handle appropriately
            // Debug.LogError("SerializedObject is null or invalid in FindSerializedProperties.");
            return;
        }
        serializedObject.ApplyModifiedProperties(); // Ensure it's up-to-date before finding

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
        analyzedBoundsProp = serializedObject.FindProperty("_analyzedBounds");
        doorwaysProp = serializedObject.FindProperty("_doorways");

        // Optional: Add null checks here too if errors persist
        // if (templateNameProp == null) Debug.LogError("Failed to find property: templateName");
        // ... etc ...
    }


    private void InitializeStyles()
    {
        // Initialize styles safely, only if null
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft, margin = new RectOffset(5, 5, 10, 5) };
        }
        if (subHeaderStyle == null)
        {
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, margin = new RectOffset(5, 5, 8, 4) };
        }
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 10, 10), margin = new RectOffset(0, 0, 5, 5) };
        }
        if (buttonStyleLarge == null)
        {
            buttonStyleLarge = new GUIStyle(GUI.skin.button) { padding = new RectOffset(15, 15, 8, 8), fontSize = 13, fontStyle = FontStyle.Bold };
        }
    }

    public override void OnInspectorGUI()
    {
        // Re-acquire target and properties if lost (e.g., after recompile)
        if (analyzer == null || serializedObject == null || !serializedObject.targetObject)
        {
            if (target != null && target is RoomTemplateAnalyzer)
            {
                analyzer = (RoomTemplateAnalyzer)target;
                FindSerializedProperties(); // Re-find properties
            }
            else
            {
                EditorGUILayout.HelpBox("Error: Target analyzer is null or invalid type.", MessageType.Error);
                return;
            }
        }
        // Ensure properties were found
        if (templateNameProp == null)
        {
            FindSerializedProperties(); // Attempt to find again
            if (templateNameProp == null)
            { // Check again after trying
                EditorGUILayout.HelpBox("Error: Could not find serialized properties. Check script names and field declarations.", MessageType.Error);
                return;
            }
        }


        InitializeStyles(); // Initialize styles at the beginning
        serializedObject.Update(); // Start

        DrawHeader();
        EditorGUILayout.Space();

        // --- Configuration Sections ---
        DrawSection("Template Metadata", DrawMetadata);
        DrawSection("Tilemap References", DrawTilemapRefs);
        DrawSection("Doorway Detection", DrawDoorwaySettings);
        DrawSection("Analyzed Data", DrawAnalyzedData);
        DrawSection("Debug Visualization", DrawDebugSettings);

        EditorGUILayout.Space();
        DrawActionButtons();

        serializedObject.ApplyModifiedProperties(); // End
    }

    // --- Main Drawing Sections ---

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Room Template Analyzer", headerStyle);
        EditorGUILayout.HelpBox("Analyzes this prefab's tilemaps to find bounds and doorways. Run 'Analyze Template' then SAVE/APPLY the prefab.", MessageType.Info);
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f); // Greenish tint

        if (GUILayout.Button("Analyze Template (Save Prefab After!)", buttonStyleLarge, GUILayout.Height(35)))
        {
            HandleAnalyzeTemplate();
        }
        GUI.backgroundColor = defaultColor; // Restore color

        EditorGUILayout.Space(5);

        // Optional button to create the ScriptableObject asset
        if (GUILayout.Button("Create Template Asset File (Optional)", GUILayout.Height(25)))
        {
            HandleCreateAsset();
        }
        EditorGUILayout.EndVertical();
    }

    // Helper to draw sections consistently
    private void DrawSection(string title, System.Action drawContent)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(boxStyle);
        drawContent?.Invoke();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    // --- Content Drawing Methods ---

    private void DrawMetadata()
    {
        EditorGUILayout.PropertyField(templateNameProp);
        EditorGUILayout.PropertyField(validRoomTypesProp, true); // Allow list expansion
        EditorGUILayout.PropertyField(selectionWeightProp);
        EditorGUILayout.PropertyField(allowRotationProp);
    }

    private void DrawTilemapRefs()
    {
        EditorGUILayout.PropertyField(floorTilemapProp, new GUIContent("Floor Tilemap (for Bounds)"));
        EditorGUILayout.PropertyField(wallTilemapProp, new GUIContent("Wall Tilemap (for Gap Detect)"));
        EditorGUILayout.PropertyField(doorTilemapProp, new GUIContent("Door Tilemap (Primary Detect)"));
        EditorGUILayout.Space();
        if (GUILayout.Button("Auto-Detect Tilemaps By Name", GUILayout.Width(220)))
        {
            AutoDetectTilemaps();
        }
    }

    private void DrawDoorwaySettings()
    {
        EditorGUILayout.PropertyField(doorwayTilesProp, new GUIContent("Doorway Tiles (on Door Map)"), true);
        EditorGUILayout.PropertyField(detectDoorwaysFromWallGapsProp, new GUIContent("Detect via Wall Gaps (Fallback)"));
    }

    private void DrawAnalyzedData()
    {
        // Use the ReadOnly drawer implicitly by disabling GUI
        bool wasEnabled = GUI.enabled;
        GUI.enabled = false;
        EditorGUILayout.PropertyField(analyzedBoundsProp, new GUIContent("Analyzed Bounds"));
        EditorGUILayout.PropertyField(doorwaysProp, new GUIContent("Detected Doorways"), true);
        GUI.enabled = wasEnabled;
    }

    private void DrawDebugSettings()
    {
        EditorGUILayout.PropertyField(showBoundsGizmoProp, new GUIContent("Show Bounds Gizmo"));
        EditorGUILayout.PropertyField(showDoorwayGizmosProp, new GUIContent("Show Doorway Gizmos"));
        // Only show color options if corresponding gizmo is enabled
        if (showBoundsGizmoProp.boolValue)
        {
            EditorGUILayout.PropertyField(boundsGizmoColorProp);
        }
        if (showDoorwayGizmosProp.boolValue)
        {
            EditorGUILayout.PropertyField(doorwayGizmoColorProp);
            EditorGUILayout.PropertyField(doorwayDirectionColorProp);
        }
    }


    // --- Button Handlers ---

    private void HandleAnalyzeTemplate()
    {
        // Ensure prefab instance context for analysis if possible
        if (analyzer == null) return; // Should be caught earlier, but safety check

        if (PrefabUtility.IsPartOfPrefabAsset(analyzer.gameObject))
        {
            Debug.LogError("Cannot run analysis directly on prefab asset. Please analyze an instance in the scene and then Apply changes.", analyzer.gameObject);
        }
        else
        {
            Undo.RecordObject(analyzer, "Analyze Room Template");
            analyzer.AnalyzeTemplate(); // This will set dirty flag internally
                                        // Scene view should repaint automatically due to internal repaint calls
        }
    }

    private void HandleCreateAsset()
    {
        if (analyzer == null) return;

        if (PrefabUtility.IsPartOfPrefabAsset(analyzer.gameObject))
        {
            Debug.LogError("Cannot create asset directly from prefab asset. Please use an instance in the scene.", analyzer.gameObject);
        }
        else
        {
            analyzer.CreateTemplateAssetFile(); // Handles analysis within
        }
    }


    // --- Helper Methods ---
    private void AutoDetectTilemaps()
    {
        if (analyzer == null) return;

        Tilemap[] tilemaps = analyzer.GetComponentsInChildren<Tilemap>(true);
        bool changed = false;

        foreach (var map in tilemaps)
        {
            string nameLower = map.name.ToLower();
            if (nameLower.Contains("floor") || nameLower.Contains("ground"))
            {
                if (floorTilemapProp?.objectReferenceValue == null) { floorTilemapProp.objectReferenceValue = map; changed = true; }
            }
            else if (nameLower.Contains("wall"))
            {
                if (wallTilemapProp?.objectReferenceValue == null) { wallTilemapProp.objectReferenceValue = map; changed = true; }
            }
            else if (nameLower.Contains("door") || nameLower.Contains("entrance") || nameLower.Contains("exit"))
            {
                if (doorTilemapProp?.objectReferenceValue == null) { doorTilemapProp.objectReferenceValue = map; changed = true; }
            }
        }

        if (changed)
        {
            serializedObject.ApplyModifiedProperties();
            Debug.Log("Attempted to auto-detect tilemaps.", analyzer);
        }
        else
        {
            Debug.Log("No missing tilemaps found to auto-detect by name.", analyzer);
        }
    }
}
#endif // UNITY_EDITOR