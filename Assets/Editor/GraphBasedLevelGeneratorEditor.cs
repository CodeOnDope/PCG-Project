#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Custom Inspector for the GraphBasedLevelGenerator
/// </summary>
[CustomEditor(typeof(GraphBasedLevelGenerator))]
public class GraphBasedLevelGeneratorEditor : Editor
{
    private GraphBasedLevelGenerator generator;

    // --- Serialized Properties ---
    #region Serialized Properties
    private SerializedProperty algorithmProp;
    private SerializedProperty seedProp;
    private SerializedProperty useRandomSeedProp;
    private SerializedProperty minRoomsProp;
    private SerializedProperty maxRoomsProp;
    private SerializedProperty minMainPathRoomsProp;
    private SerializedProperty branchProbabilityProp;
    private SerializedProperty maxBranchLengthProp;
    private SerializedProperty minRoomDistanceProp;
    private SerializedProperty roomTemplatesProp; // Will find the List<RoomTemplate> field now
    private SerializedProperty startRoomTemplateProp;
    private SerializedProperty bossRoomTemplateProp;
    private SerializedProperty exitRoomTemplateProp;
    private SerializedProperty floorTilemapProp;
    private SerializedProperty wallTilemapProp;
    private SerializedProperty defaultCorridorFloorTileProp;
    private SerializedProperty defaultCorridorWallTileProp;
    private SerializedProperty generateCorridorsProp;
    private SerializedProperty corridorWidthProp;
    private SerializedProperty useCornerCorridorsProp;
    private SerializedProperty decorateCorridorsProp;
    private SerializedProperty playerPrefabProp;
    private SerializedProperty enemyPrefabsProp;
    private SerializedProperty decorationPrefabsProp;
    private SerializedProperty treasurePrefabsProp;
    private SerializedProperty enemiesPerRoomProp;
    private SerializedProperty decorationsPerRoomProp;
    #endregion

    // --- State Variables ---
    #region State Variables
    private bool showTemplatePreview = false;
    private int selectedPreviewTemplateIndex = -1;
    private List<GraphBasedLevelGenerator.RoomTemplate> previewTemplatesList = new List<GraphBasedLevelGenerator.RoomTemplate>();
    private List<string> previewTemplateNamesList = new List<string>();
    private bool showGeneralSettings = true;
    private bool showLevelStructure = true;
    private bool showRoomTemplates = true;
    private bool showCorridorSettings = true;
    private bool showEntitySettings = true;
    #endregion

    // --- GUI Styles ---
    #region GUI Styles
    private GUIStyle headerStyle;
    private GUIStyle boxStyle;
    private GUIStyle buttonStyleLarge; // Renamed from buttonStyle for clarity
    private GUIStyle buttonStyleSmall;
    private GUIStyle miniBoldLabelStyle;
    #endregion

    private void OnEnable()
    {
        generator = (GraphBasedLevelGenerator)target;
        FindSerializedProperties();
    }

    private void FindSerializedProperties()
    {
        // Using the names from the corrected GraphBasedLevelGenerator.cs
        algorithmProp = serializedObject.FindProperty("algorithm");
        seedProp = serializedObject.FindProperty("seed");
        useRandomSeedProp = serializedObject.FindProperty("useRandomSeed");
        minRoomsProp = serializedObject.FindProperty("minRooms");
        maxRoomsProp = serializedObject.FindProperty("maxRooms");
        minMainPathRoomsProp = serializedObject.FindProperty("minMainPathRooms");
        branchProbabilityProp = serializedObject.FindProperty("branchProbability");
        maxBranchLengthProp = serializedObject.FindProperty("maxBranchLength");
        minRoomDistanceProp = serializedObject.FindProperty("minRoomDistance");
        roomTemplatesProp = serializedObject.FindProperty("roomTemplates"); // Finds List<RoomTemplate>
        startRoomTemplateProp = serializedObject.FindProperty("startRoomTemplate");
        bossRoomTemplateProp = serializedObject.FindProperty("bossRoomTemplate");
        exitRoomTemplateProp = serializedObject.FindProperty("exitRoomTemplate");
        floorTilemapProp = serializedObject.FindProperty("floorTilemap");
        wallTilemapProp = serializedObject.FindProperty("wallTilemap");
        defaultCorridorFloorTileProp = serializedObject.FindProperty("defaultCorridorFloorTile");
        defaultCorridorWallTileProp = serializedObject.FindProperty("defaultCorridorWallTile");
        generateCorridorsProp = serializedObject.FindProperty("generateCorridors");
        corridorWidthProp = serializedObject.FindProperty("corridorWidth");
        useCornerCorridorsProp = serializedObject.FindProperty("useCornerCorridors");
        decorateCorridorsProp = serializedObject.FindProperty("decorateCorridors");
        playerPrefabProp = serializedObject.FindProperty("playerPrefab");
        enemyPrefabsProp = serializedObject.FindProperty("enemyPrefabs");
        decorationPrefabsProp = serializedObject.FindProperty("decorationPrefabs");
        treasurePrefabsProp = serializedObject.FindProperty("treasurePrefabs");
        enemiesPerRoomProp = serializedObject.FindProperty("enemiesPerRoom");
        decorationsPerRoomProp = serializedObject.FindProperty("decorationsPerRoom");
    }

    private void InitializeStyles()
    {
        // Use slightly different names to avoid conflicts if base Editor has similar styles
        if (headerStyle == null) headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft, margin = new RectOffset(5, 5, 10, 5) };
        if (boxStyle == null) boxStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 10, 10), margin = new RectOffset(0, 0, 5, 5) };
        if (buttonStyleLarge == null) buttonStyleLarge = new GUIStyle(GUI.skin.button) { padding = new RectOffset(15, 15, 8, 8), fontSize = 13, fontStyle = FontStyle.Bold };
        if (buttonStyleSmall == null) buttonStyleSmall = new GUIStyle(GUI.skin.button) { padding = new RectOffset(5, 5, 3, 3), fontSize = 10 };
        if (miniBoldLabelStyle == null) miniBoldLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        InitializeStyles(); // Call here to ensure styles are ready

        // DrawHeader(); // Using DrawHeader() can sometimes conflict, let's use a simple LabelField
        EditorGUILayout.LabelField("Graph-Based Level Generator", headerStyle);
        EditorGUILayout.HelpBox("Configure parameters and assign room templates.", MessageType.Info);
        EditorGUILayout.Space();

        DrawActionButtons();
        EditorGUILayout.Space(10);

        showGeneralSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showGeneralSettings, "Generation Settings");
        if (showGeneralSettings) DrawGeneralSettings();
        EditorGUILayout.EndFoldoutHeaderGroup();

        showLevelStructure = EditorGUILayout.BeginFoldoutHeaderGroup(showLevelStructure, "Level Structure");
        if (showLevelStructure) DrawLevelStructure();
        EditorGUILayout.EndFoldoutHeaderGroup();

        showRoomTemplates = EditorGUILayout.BeginFoldoutHeaderGroup(showRoomTemplates, "Room Templates");
        if (showRoomTemplates) DrawRoomTemplates(); // Fixed function below
        EditorGUILayout.EndFoldoutHeaderGroup();

        showCorridorSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showCorridorSettings, "Corridor Settings");
        if (showCorridorSettings) DrawCorridorSettings();
        EditorGUILayout.EndFoldoutHeaderGroup();

        showEntitySettings = EditorGUILayout.BeginFoldoutHeaderGroup(showEntitySettings, "Entity Spawning");
        if (showEntitySettings) DrawEntitySettings(); // Draws treasure list
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(10);
        DrawBottomButtons();

        serializedObject.ApplyModifiedProperties();
    }

    // DrawHeader is inherited, avoid redefining or use 'new' keyword if intended override
    // private void DrawHeader() { EditorGUILayout.LabelField("Graph-Based Level Generator", headerStyle); EditorGUILayout.HelpBox("Configure parameters and assign room templates.", MessageType.Info); }

    private void DrawActionButtons()
    {
        EditorGUILayout.BeginHorizontal();
        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Generate Level", buttonStyleLarge, GUILayout.Height(35))) HandleGenerateLevel();
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("Clear Level", buttonStyleLarge, GUILayout.Height(35))) HandleClearLevel();
        GUI.backgroundColor = defaultColor;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBottomButtons() { if (GUILayout.Button("Visualize Graph", GUILayout.Height(25))) HandleVisualizeGraph(); }

    private void DrawGeneralSettings() { EditorGUILayout.BeginVertical(boxStyle); EditorGUILayout.PropertyField(algorithmProp); EditorGUILayout.BeginHorizontal(); EditorGUI.BeginDisabledGroup(useRandomSeedProp.boolValue); EditorGUILayout.PropertyField(seedProp, GUILayout.ExpandWidth(true)); EditorGUI.EndDisabledGroup(); EditorGUILayout.PropertyField(useRandomSeedProp, new GUIContent("Random"), GUILayout.Width(80)); if (!useRandomSeedProp.boolValue && GUILayout.Button("New", buttonStyleSmall, GUILayout.Width(40))) seedProp.intValue = UnityEngine.Random.Range(1, int.MaxValue); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); }

    private void DrawLevelStructure() { EditorGUILayout.BeginVertical(boxStyle); EditorGUILayout.BeginHorizontal(); EditorGUILayout.PropertyField(minRoomsProp, new GUIContent("Min Rooms")); EditorGUILayout.PropertyField(maxRoomsProp, new GUIContent("Max Rooms")); EditorGUILayout.EndHorizontal(); EditorGUILayout.PropertyField(minMainPathRoomsProp); EditorGUILayout.PropertyField(branchProbabilityProp); EditorGUILayout.PropertyField(maxBranchLengthProp); EditorGUILayout.PropertyField(minRoomDistanceProp); EditorGUILayout.EndVertical(); }

    // DrawRoomTemplates uses the helper now to avoid errors
    private void DrawRoomTemplates()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Special Templates", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.LabelField("Start Room Template", miniBoldLabelStyle);
        if (startRoomTemplateProp != null) DrawRoomTemplateFields(startRoomTemplateProp); else EditorGUILayout.HelpBox("Prop missing.", MessageType.Warning);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Boss Room Template", miniBoldLabelStyle);
        if (bossRoomTemplateProp != null) DrawRoomTemplateFields(bossRoomTemplateProp); else EditorGUILayout.HelpBox("Prop missing.", MessageType.Warning);
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Exit Room Template", miniBoldLabelStyle);
        if (exitRoomTemplateProp != null) DrawRoomTemplateFields(exitRoomTemplateProp); else EditorGUILayout.HelpBox("Prop missing.", MessageType.Warning);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField("Generic Room Templates", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign prefabs with analyzed RoomTemplateAnalyzer.", MessageType.Info);
        EditorGUILayout.PropertyField(roomTemplatesProp, true); // Draw the direct list
        if (GUILayout.Button("Add Generic Template", GUILayout.Width(160))) roomTemplatesProp.arraySize++;
        EditorGUILayout.Space(5);

        showTemplatePreview = EditorGUILayout.Foldout(showTemplatePreview, "Template Preview", true, EditorStyles.foldoutHeader);
        if (showTemplatePreview) DrawTemplatePreview();
        EditorGUILayout.EndVertical();
    }

    // Helper function to draw fields of a RoomTemplate property
    private void DrawRoomTemplateFields(SerializedProperty roomTemplateProp)
    {
        if (roomTemplateProp == null) return;
        // Find child properties relative to the passed-in property
        SerializedProperty nameProp = roomTemplateProp.FindPropertyRelative("name");
        SerializedProperty prefabProp = roomTemplateProp.FindPropertyRelative("prefab");
        SerializedProperty weightProp = roomTemplateProp.FindPropertyRelative("weight");
        SerializedProperty rotationProp = roomTemplateProp.FindPropertyRelative("allowRotation");
        EditorGUI.indentLevel++; // Indent fields for clarity
                                 // Draw the fields individually
        if (nameProp != null) EditorGUILayout.PropertyField(nameProp); else Debug.LogWarning("Missing 'name' property in RoomTemplate");
        if (prefabProp != null) EditorGUILayout.PropertyField(prefabProp); else Debug.LogWarning("Missing 'prefab' property in RoomTemplate");
        if (weightProp != null) EditorGUILayout.PropertyField(weightProp); else Debug.LogWarning("Missing 'weight' property in RoomTemplate");
        if (rotationProp != null) EditorGUILayout.PropertyField(rotationProp); else Debug.LogWarning("Missing 'allowRotation' property in RoomTemplate");
        EditorGUI.indentLevel--; // Restore indent
    }

    // DrawTemplatePreview and AddTemplateToPreviewLists remain largely the same
    private void DrawTemplatePreview()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        previewTemplatesList.Clear(); previewTemplateNamesList.Clear();
        AddTemplateToPreviewLists(generator.startRoomTemplate, "[Start] ", previewTemplatesList, previewTemplateNamesList);
        AddTemplateToPreviewLists(generator.bossRoomTemplate, "[Boss] ", previewTemplatesList, previewTemplateNamesList);
        AddTemplateToPreviewLists(generator.exitRoomTemplate, "[Exit] ", previewTemplatesList, previewTemplateNamesList);
        if (generator.roomTemplates != null)
        {
            for (int i = 0; i < generator.roomTemplates.Count; i++)
            {
                var template = generator.roomTemplates[i];
                if (template?.prefab != null) AddTemplateToPreviewLists(template, $"[{i}] ", previewTemplatesList, previewTemplateNamesList);
            }
        }
        if (previewTemplatesList.Count == 0) EditorGUILayout.LabelField("No valid templates assigned.");
        else
        {
            selectedPreviewTemplateIndex = Mathf.Clamp(selectedPreviewTemplateIndex, 0, previewTemplatesList.Count - 1);
            selectedPreviewTemplateIndex = EditorGUILayout.Popup("Select Template", selectedPreviewTemplateIndex, previewTemplateNamesList.ToArray());
            if (selectedPreviewTemplateIndex >= 0 && selectedPreviewTemplateIndex < previewTemplatesList.Count)
            {
                var selectedTemplate = previewTemplatesList[selectedPreviewTemplateIndex];
                EditorGUILayout.LabelField("Template Details:", EditorStyles.boldLabel); EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Name", selectedTemplate.name ?? "N/A");
                if (selectedTemplate.dataFetched) { EditorGUILayout.LabelField("Size", selectedTemplate.size.ToString()); EditorGUILayout.LabelField("Doorways", selectedTemplate.tilemapData?.doorways?.Count.ToString() ?? "0"); }
                else { EditorGUILayout.LabelField("Size", "(Not Analyzed)"); EditorGUILayout.LabelField("Doorways", "(Not Analyzed)"); }
                EditorGUILayout.LabelField("Rotation", selectedTemplate.allowRotation ? "Allowed" : "Fixed");
                if (selectedTemplate.validRoomTypes != null && selectedTemplate.validRoomTypes.Length > 0) EditorGUILayout.LabelField("Valid Types", string.Join(", ", selectedTemplate.validRoomTypes));
                if (GUILayout.Button("Select Prefab", GUILayout.Width(120))) { if (selectedTemplate.prefab != null) { Selection.activeObject = selectedTemplate.prefab; EditorGUIUtility.PingObject(selectedTemplate.prefab); } }
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndVertical();
    }
    private void AddTemplateToPreviewLists(GraphBasedLevelGenerator.RoomTemplate template, string prefix, List<GraphBasedLevelGenerator.RoomTemplate> templates, List<string> names)
    {
        if (template?.prefab != null) { string displayName = string.IsNullOrEmpty(template.name) ? template.prefab.name : template.name; names.Add(prefix + displayName); templates.Add(template); }
    }

    // DrawCorridorSettings remains the same
    private void DrawCorridorSettings()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.PropertyField(generateCorridorsProp);
        EditorGUI.BeginDisabledGroup(!generateCorridorsProp.boolValue);
        EditorGUILayout.PropertyField(corridorWidthProp);
        EditorGUILayout.PropertyField(useCornerCorridorsProp);
        // EditorGUILayout.PropertyField(decorateCorridorsProp);
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Corridor Tilemaps & Tiles", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(floorTilemapProp);
        EditorGUILayout.PropertyField(wallTilemapProp);
        EditorGUILayout.PropertyField(defaultCorridorFloorTileProp);
        EditorGUILayout.PropertyField(defaultCorridorWallTileProp);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();
    }

    // DrawEntitySettings draws the treasure list correctly now
    private void DrawEntitySettings()
    {
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.PropertyField(playerPrefabProp); EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(enemyPrefabsProp, true);
        EditorGUILayout.PropertyField(enemiesPerRoomProp); EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Decorations", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(decorationPrefabsProp, true);
        EditorGUILayout.PropertyField(decorationsPerRoomProp); EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Treasures", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(treasurePrefabsProp, true); // Draws the List<GameObject>
        EditorGUILayout.EndVertical();
    }

    // Action Handlers remain the same
    private void HandleGenerateLevel() { if (Application.isPlaying) generator.GenerateLevel(); else { if (EditorUtility.DisplayDialog("Generate Level (Edit Mode)", "Clear previous & generate new level?", "Generate", "Cancel")) { Undo.RegisterCompleteObjectUndo(generator, "Generate Level"); RecordUndoForContainerChildren("Rooms"); RecordUndoForContainerChildren("Corridors"); RecordUndoForContainerChildren("Entities"); if (generator.floorTilemap != null) Undo.RegisterCompleteObjectUndo(generator.floorTilemap, "Generate Level (Tilemap)"); if (generator.wallTilemap != null) Undo.RegisterCompleteObjectUndo(generator.wallTilemap, "Generate Level (Tilemap)"); generator.GenerateLevel(); if (generator.gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene); } } }
    private void HandleClearLevel() { if (Application.isPlaying) generator.ClearLevel(); else { if (EditorUtility.DisplayDialog("Clear Level (Edit Mode)", "Destroy generated content?", "Clear", "Cancel")) { Undo.RegisterCompleteObjectUndo(generator, "Clear Level"); RecordUndoForContainerChildren("Rooms"); RecordUndoForContainerChildren("Corridors"); RecordUndoForContainerChildren("Entities"); if (generator.floorTilemap != null) Undo.RegisterCompleteObjectUndo(generator.floorTilemap, "Clear Level (Tilemap)"); if (generator.wallTilemap != null) Undo.RegisterCompleteObjectUndo(generator.wallTilemap, "Clear Level (Tilemap)"); generator.ClearLevel(); if (generator.gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene); } } }
    private void HandleVisualizeGraph() { var method = typeof(GraphBasedLevelGenerator).GetMethod("VisualizeGraph", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); if (method != null) { Debug.Log("Visualizing graph..."); method.Invoke(generator, null); } else Debug.LogError("VisualizeGraph method not found."); }
    private void RecordUndoForContainerChildren(string name) { Transform container = generator.transform.Find(name); if (container != null) foreach (Transform child in container) if (child != null) Undo.DestroyObjectImmediate(child.gameObject); } // Use DestroyObjectImmediate for Undo
}
#endif