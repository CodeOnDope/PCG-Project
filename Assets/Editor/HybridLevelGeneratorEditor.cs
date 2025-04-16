/*using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

// Note: Assumes GenerationMode enum is defined in a separate shared file.
[CustomEditor(typeof(HybridLevelGenerator))]
public class HybridLevelGeneratorEditor : Editor
{
    // --- Serialized Properties ---
    SerializedProperty generationModeProp;
    SerializedProperty levelWidthProp;
    SerializedProperty levelHeightProp;
    SerializedProperty minRoomSizeProp;
    SerializedProperty maxIterationsProp;
    SerializedProperty roomPaddingProp;
    SerializedProperty lShapeProbabilityProp;
    SerializedProperty minLLegRatioProp;
    SerializedProperty maxLLegRatioProp;
    SerializedProperty roomTemplatePrefabsProp;
    SerializedProperty roomTemplateProbabilityProp;
    SerializedProperty corridorWidthProp;
    SerializedProperty groundTilemapProp;
    SerializedProperty wallTilemapProp;
    SerializedProperty floorTileProp;
    SerializedProperty wallTileProp;
    SerializedProperty seedProp;
    SerializedProperty useRandomSeedProp;
    SerializedProperty playerPrefabProp;
    SerializedProperty enemyPrefabProp;
    SerializedProperty decorationPrefabProp;
    SerializedProperty enemiesPerRoomProp;
    SerializedProperty decorationsPerRoomProp;
    SerializedProperty levelPreviewTextureProp;
    SerializedProperty levelPreviewCameraProp; // Added

    // --- Foldout States ---
    bool levelDimensionsFoldout = true;
    bool bspSettingsFoldout = true;
    bool hybridSettingsFoldout = true;
    bool commonSettingsFoldout = true;
    bool tileSettingsFoldout = true;
    bool randomnessSettingsFoldout = true;
    bool entitySettingsFoldout = true;
    bool editorPreviewFoldout = true; // Foldout for preview settings

    // --- Style & Color ---
    Color dimensionsBgColor = new Color(0.85f, 0.9f, 1f);
    Color bspBgColor = new Color(0.85f, 1f, 0.85f);
    Color hybridBgColor = new Color(1f, 0.9f, 0.85f);
    Color commonBgColor = new Color(0.9f, 0.9f, 0.9f);
    GUIStyle foldoutHeaderStyle;
    bool stylesInitialized = false;


    private void OnEnable()
    {
        // Find properties
        generationModeProp = serializedObject.FindProperty("generationMode");
        levelWidthProp = serializedObject.FindProperty("levelWidth");
        levelHeightProp = serializedObject.FindProperty("levelHeight");
        minRoomSizeProp = serializedObject.FindProperty("minRoomSize");
        maxIterationsProp = serializedObject.FindProperty("maxIterations");
        roomPaddingProp = serializedObject.FindProperty("roomPadding");
        lShapeProbabilityProp = serializedObject.FindProperty("lShapeProbability");
        minLLegRatioProp = serializedObject.FindProperty("minLLegRatio");
        maxLLegRatioProp = serializedObject.FindProperty("maxLLegRatio");
        roomTemplatePrefabsProp = serializedObject.FindProperty("roomTemplatePrefabs");
        roomTemplateProbabilityProp = serializedObject.FindProperty("roomTemplateProbability");
        corridorWidthProp = serializedObject.FindProperty("corridorWidth");
        groundTilemapProp = serializedObject.FindProperty("groundTilemap");
        wallTilemapProp = serializedObject.FindProperty("wallTilemap");
        floorTileProp = serializedObject.FindProperty("floorTile");
        wallTileProp = serializedObject.FindProperty("wallTile");
        seedProp = serializedObject.FindProperty("seed");
        useRandomSeedProp = serializedObject.FindProperty("useRandomSeed");
        playerPrefabProp = serializedObject.FindProperty("playerPrefab");
        enemyPrefabProp = serializedObject.FindProperty("enemyPrefab");
        decorationPrefabProp = serializedObject.FindProperty("decorationPrefab");
        enemiesPerRoomProp = serializedObject.FindProperty("enemiesPerRoom");
        decorationsPerRoomProp = serializedObject.FindProperty("decorationsPerRoom");
        levelPreviewTextureProp = serializedObject.FindProperty("levelPreviewTexture");
        levelPreviewCameraProp = serializedObject.FindProperty("levelPreviewCamera"); // Added

        InitializeStyles();
    }

    private void InitializeStyles()
    {
        if (stylesInitialized) return;
        foldoutHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader) { fontStyle = FontStyle.Bold, fontSize = 13 };
        stylesInitialized = true;
    }

    public override void OnInspectorGUI()
    {
        if (!stylesInitialized) InitializeStyles(); // Ensure styles ready

        serializedObject.Update();
        Color defaultBgColor = GUI.backgroundColor;

        // --- Generation Mode ---
        EditorGUILayout.LabelField("Generation Control", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(generationModeProp);
        bool modeChanged = EditorGUI.EndChangeCheck();
        EditorGUILayout.Space(5);

        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;
        switch (currentMode)
        { // Help text
            case GenerationMode.FullyProcedural: EditorGUILayout.HelpBox("BSP + Random Rect Rooms + MST Corridors.", MessageType.Info); break;
            case GenerationMode.HybridProcedural: EditorGUILayout.HelpBox("BSP + Random Templates/L-Shapes/Rects + MST Corridors.", MessageType.Info); break;
            case GenerationMode.UserDefinedLayout: EditorGUILayout.HelpBox("Uses RoomNode components in the scene. Design layout using the Visual Level Designer window.", MessageType.Info); break;
        }
        EditorGUILayout.Space(5);

        // --- Open Editor Button (Conditional) --- // *** ADDED ***
        if (currentMode == GenerationMode.UserDefinedLayout)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            if (GUILayout.Button("Open Visual Level Designer", GUILayout.Height(25)))
            {
                VisualLevelDesignEditor.ShowWindow(); // Call static method to open/focus window
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        // --- Settings Sections ---
        levelDimensionsFoldout = EditorGUILayout.Foldout(levelDimensionsFoldout, "Level Dimensions", true, foldoutHeaderStyle);
        if (levelDimensionsFoldout) { GUI.backgroundColor = dimensionsBgColor; EditorGUILayout.BeginVertical(GUI.skin.box); GUI.backgroundColor = defaultBgColor; EditorGUILayout.PropertyField(levelWidthProp); EditorGUILayout.PropertyField(levelHeightProp); EditorGUILayout.EndVertical(); }
        EditorGUILayout.Space(5);

        // Show BSP only if relevant
        if (currentMode == GenerationMode.FullyProcedural || currentMode == GenerationMode.HybridProcedural)
        {
            bspSettingsFoldout = EditorGUILayout.Foldout(bspSettingsFoldout, "BSP Settings", true, foldoutHeaderStyle);
            if (bspSettingsFoldout) { GUI.backgroundColor = bspBgColor; EditorGUILayout.BeginVertical(GUI.skin.box); GUI.backgroundColor = defaultBgColor; EditorGUILayout.PropertyField(minRoomSizeProp, new GUIContent("Min Room Size (BSP)")); EditorGUILayout.PropertyField(maxIterationsProp, new GUIContent("BSP Max Iterations")); EditorGUILayout.PropertyField(roomPaddingProp, new GUIContent("BSP Room Padding")); EditorGUILayout.EndVertical(); }
            EditorGUILayout.Space(5);
        }

        // Show Hybrid only if relevant
        if (currentMode == GenerationMode.HybridProcedural)
        {
            hybridSettingsFoldout = EditorGUILayout.Foldout(hybridSettingsFoldout, "Hybrid Room Settings", true, foldoutHeaderStyle);
            if (hybridSettingsFoldout) { GUI.backgroundColor = hybridBgColor; EditorGUILayout.BeginVertical(GUI.skin.box); GUI.backgroundColor = defaultBgColor; EditorGUILayout.LabelField("Shape Probabilities", EditorStyles.miniBoldLabel); EditorGUILayout.PropertyField(lShapeProbabilityProp); EditorGUILayout.PropertyField(roomTemplateProbabilityProp); EditorGUILayout.Space(5); EditorGUILayout.LabelField("L-Shape Ratios", EditorStyles.miniBoldLabel); EditorGUILayout.PropertyField(minLLegRatioProp); EditorGUILayout.PropertyField(maxLLegRatioProp); EditorGUILayout.Space(5); EditorGUILayout.LabelField("Templates", EditorStyles.miniBoldLabel); EditorGUILayout.PropertyField(roomTemplatePrefabsProp, true); EditorGUILayout.EndVertical(); }
            EditorGUILayout.Space(5);
        }
        // Common Settings
        commonSettingsFoldout = EditorGUILayout.Foldout(commonSettingsFoldout, "Common Settings", true, foldoutHeaderStyle);
        if (commonSettingsFoldout)
        {
            GUI.backgroundColor = commonBgColor; EditorGUILayout.BeginVertical(GUI.skin.box); GUI.backgroundColor = defaultBgColor;
            EditorGUILayout.PropertyField(corridorWidthProp); EditorGUILayout.Space(10);
            tileSettingsFoldout = EditorGUILayout.Foldout(tileSettingsFoldout, "Tiles & Tilemaps", true); if (tileSettingsFoldout) { EditorGUI.indentLevel++; EditorGUILayout.PropertyField(groundTilemapProp, EditorGUIUtility.IconContent("Tilemap Icon")); EditorGUILayout.PropertyField(wallTilemapProp, EditorGUIUtility.IconContent("Tilemap Icon")); EditorGUILayout.PropertyField(floorTileProp); EditorGUILayout.PropertyField(wallTileProp); EditorGUI.indentLevel--; }
            EditorGUILayout.Space(5);
            randomnessSettingsFoldout = EditorGUILayout.Foldout(randomnessSettingsFoldout, "Randomness", true); if (randomnessSettingsFoldout) { EditorGUI.indentLevel++; EditorGUILayout.PropertyField(useRandomSeedProp); if (!useRandomSeedProp.boolValue) { EditorGUILayout.PropertyField(seedProp); } EditorGUI.indentLevel--; }
            EditorGUILayout.Space(5);
            entitySettingsFoldout = EditorGUILayout.Foldout(entitySettingsFoldout, "Entities & Decorations", true); if (entitySettingsFoldout) { EditorGUI.indentLevel++; EditorGUILayout.PropertyField(playerPrefabProp); EditorGUILayout.PropertyField(enemyPrefabProp); EditorGUILayout.PropertyField(decorationPrefabProp); EditorGUILayout.PropertyField(enemiesPerRoomProp); EditorGUILayout.PropertyField(decorationsPerRoomProp); EditorGUI.indentLevel--; }
            EditorGUILayout.Space(5);
            // Show Templates list here if Hybrid or UserDefined
            if (currentMode == GenerationMode.HybridProcedural || currentMode == GenerationMode.UserDefinedLayout)
            {
                EditorGUILayout.PropertyField(roomTemplatePrefabsProp, new GUIContent("Room Template Prefabs"), true);
            }
            // Moved Editor Preview to its own foldout
            // EditorGUILayout.PropertyField(levelPreviewTextureProp);
            // EditorGUILayout.PropertyField(levelPreviewCameraProp);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space(5);

        // Editor Preview Section (Always shown, but only relevant if setup)
        editorPreviewFoldout = EditorGUILayout.Foldout(editorPreviewFoldout, "Editor Preview Settings", true, foldoutHeaderStyle);
        if (editorPreviewFoldout)
        {
            GUI.backgroundColor = commonBgColor * 0.95f; // Slightly different grey
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = defaultBgColor;
            EditorGUILayout.HelpBox("Assign a Render Texture and a Camera that renders to it to see a preview in the Visual Level Designer window.", MessageType.Info);
            EditorGUILayout.PropertyField(levelPreviewTextureProp);
            EditorGUILayout.PropertyField(levelPreviewCameraProp);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space(15);
        // --- Draw Buttons ---
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 30, fontStyle = FontStyle.Bold };
        GUI.backgroundColor = new Color(0.7f, 1.0f, 0.7f);
        if (GUILayout.Button("Generate Level", buttonStyle)) { HybridLevelGenerator generator = (HybridLevelGenerator)target; Undo.RecordObject(generator, "Generate Level"); generator.GenerateLevel(); MarkSceneDirty(generator); }
        GUI.backgroundColor = defaultBgColor; EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
        if (GUILayout.Button("Clear Level", buttonStyle)) { HybridLevelGenerator generator = (HybridLevelGenerator)target; if (EditorUtility.DisplayDialog("Confirm Clear", "Clear generated level AND scene design nodes?", "Clear All", "Cancel")) { Undo.RecordObject(generator, "Clear Level"); generator.ClearLevel(); MarkSceneDirty(generator); } }
        GUI.backgroundColor = defaultBgColor;

        serializedObject.ApplyModifiedProperties();
    }
    private void MarkSceneDirty(HybridLevelGenerator generator) { if (!Application.isPlaying && generator != null && generator.gameObject != null) { try { if (generator.gameObject.scene.IsValid()) { UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene); } } catch (Exception e) { Debug.LogWarning($"Could not mark scene dirty: {e.Message}"); } } }
}*/







/*using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;

// Note: Assumes GenerationMode enum is defined in a separate shared file.
[CustomEditor(typeof(HybridLevelGenerator))]
public class HybridLevelGeneratorEditor : Editor
{
    // --- Serialized Properties ---
    SerializedProperty generationModeProp;
    SerializedProperty levelWidthProp;
    SerializedProperty levelHeightProp;
    SerializedProperty minRoomSizeProp;
    SerializedProperty maxIterationsProp;
    SerializedProperty roomPaddingProp;
    SerializedProperty lShapeProbabilityProp;
    SerializedProperty minLLegRatioProp;
    SerializedProperty maxLLegRatioProp;
    SerializedProperty roomTemplatePrefabsProp;
    SerializedProperty roomTemplateProbabilityProp;
    SerializedProperty corridorWidthProp;
    SerializedProperty groundTilemapProp;
    SerializedProperty wallTilemapProp;
    SerializedProperty floorTileProp;
    SerializedProperty wallTileProp;
    SerializedProperty seedProp;
    SerializedProperty useRandomSeedProp;
    SerializedProperty playerPrefabProp;
    SerializedProperty enemyPrefabProp;
    SerializedProperty decorationPrefabProp;
    SerializedProperty enemiesPerRoomProp;
    SerializedProperty decorationsPerRoomProp;
    SerializedProperty levelPreviewTextureProp;
    SerializedProperty levelPreviewCameraProp;

    // --- Foldout States ---
    bool levelDimensionsFoldout = true;
    bool bspSettingsFoldout = true;
    bool hybridSettingsFoldout = true;
    bool commonSettingsFoldout = true;
    bool tileSettingsFoldout = true;
    bool randomnessSettingsFoldout = true;
    bool entitySettingsFoldout = true;
    bool editorPreviewFoldout = true;

    // --- Style & Color ---
    // Modern, more vibrant color scheme
    Color headerBgColor = new Color(0.2f, 0.2f, 0.22f);
    Color headerTextColor = new Color(1f, 1f, 1f);
    Color dimensionsBgColor = new Color(0.24f, 0.48f, 0.85f, 0.2f);
    Color bspBgColor = new Color(0.26f, 0.76f, 0.34f, 0.2f);
    Color hybridBgColor = new Color(0.87f, 0.5f, 0.24f, 0.2f);
    Color commonBgColor = new Color(0.4f, 0.4f, 0.4f, 0.2f);
    Color previewBgColor = new Color(0.65f, 0.35f, 0.75f, 0.2f);
    Color generateButtonColor = new Color(0.26f, 0.76f, 0.34f);
    Color clearButtonColor = new Color(0.87f, 0.36f, 0.36f);

    // UI Elements
    GUIStyle headerStyle;
    GUIStyle subHeaderStyle;
    GUIStyle boxHeaderStyle;
    GUIStyle foldoutHeaderStyle;
    GUIStyle buttonStyle;
    GUIStyle separatorStyle;
    GUIContent generateIcon;
    GUIContent clearIcon;
    GUIContent previewIcon;
    GUIContent dimensionsIcon;
    GUIContent bspIcon;
    GUIContent hybridIcon;
    GUIContent settingsIcon;
    GUIContent designerIcon;
    Texture2D sectionHeaderTexture;
    Texture2D lineTexture;
    bool stylesInitialized = false;

    private void OnEnable()
    {
        // Find properties
        generationModeProp = serializedObject.FindProperty("generationMode");
        levelWidthProp = serializedObject.FindProperty("levelWidth");
        levelHeightProp = serializedObject.FindProperty("levelHeight");
        minRoomSizeProp = serializedObject.FindProperty("minRoomSize");
        maxIterationsProp = serializedObject.FindProperty("maxIterations");
        roomPaddingProp = serializedObject.FindProperty("roomPadding");
        lShapeProbabilityProp = serializedObject.FindProperty("lShapeProbability");
        minLLegRatioProp = serializedObject.FindProperty("minLLegRatio");
        maxLLegRatioProp = serializedObject.FindProperty("maxLLegRatio");
        roomTemplatePrefabsProp = serializedObject.FindProperty("roomTemplatePrefabs");
        roomTemplateProbabilityProp = serializedObject.FindProperty("roomTemplateProbability");
        corridorWidthProp = serializedObject.FindProperty("corridorWidth");
        groundTilemapProp = serializedObject.FindProperty("groundTilemap");
        wallTilemapProp = serializedObject.FindProperty("wallTilemap");
        floorTileProp = serializedObject.FindProperty("floorTile");
        wallTileProp = serializedObject.FindProperty("wallTile");
        seedProp = serializedObject.FindProperty("seed");
        useRandomSeedProp = serializedObject.FindProperty("useRandomSeed");
        playerPrefabProp = serializedObject.FindProperty("playerPrefab");
        enemyPrefabProp = serializedObject.FindProperty("enemyPrefab");
        decorationPrefabProp = serializedObject.FindProperty("decorationPrefab");
        enemiesPerRoomProp = serializedObject.FindProperty("enemiesPerRoom");
        decorationsPerRoomProp = serializedObject.FindProperty("decorationsPerRoom");
        levelPreviewTextureProp = serializedObject.FindProperty("levelPreviewTexture");
        levelPreviewCameraProp = serializedObject.FindProperty("levelPreviewCamera");

        InitializeStyles();
    }

    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        // Create textures for headers and separators
        sectionHeaderTexture = new Texture2D(1, 1);
        sectionHeaderTexture.SetPixel(0, 0, headerBgColor);
        sectionHeaderTexture.Apply();

        lineTexture = new Texture2D(1, 1);
        lineTexture.SetPixel(0, 0, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        lineTexture.Apply();

        // Header styles
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = headerTextColor },
            fontStyle = FontStyle.Bold,
            fixedHeight = 28,
            margin = new RectOffset(0, 0, 8, 8)
        };

        subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            margin = new RectOffset(0, 0, 6, 6)
        };

        boxHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            margin = new RectOffset(0, 0, 0, 4)
        };

        // Main foldout style
        foldoutHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 13,
            margin = new RectOffset(0, 0, 6, 0)
        };

        // Button style
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            fixedHeight = 32,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(10, 10, 5, 5)
        };

        // Separator style
        separatorStyle = new GUIStyle
        {
            normal = { background = lineTexture },
            margin = new RectOffset(0, 0, 8, 8),
            fixedHeight = 2
        };

        // Icons (using built-in editor icons)
        generateIcon = EditorGUIUtility.IconContent("d_Refresh");
        generateIcon.text = " Generate Level";

        clearIcon = EditorGUIUtility.IconContent("d_TreeEditor.Trash");
        clearIcon.text = " Clear Level";

        previewIcon = EditorGUIUtility.IconContent("d_ViewToolOrbit");
        previewIcon.text = " Editor Preview Settings";

        dimensionsIcon = EditorGUIUtility.IconContent("d_RectTool");
        dimensionsIcon.text = " Level Dimensions";

        bspIcon = EditorGUIUtility.IconContent("d_GridLayoutGroup Icon");
        bspIcon.text = " BSP Settings";

        hybridIcon = EditorGUIUtility.IconContent("d_Prefab Icon");
        hybridIcon.text = " Hybrid Room Settings";

        settingsIcon = EditorGUIUtility.IconContent("d_Settings");
        settingsIcon.text = " Common Settings";

        designerIcon = EditorGUIUtility.IconContent("d_SceneViewCamera");
        designerIcon.text = " Open Visual Level Designer";

        stylesInitialized = true;
    }

    public override void OnInspectorGUI()
    {
        if (!stylesInitialized) InitializeStyles();

        serializedObject.Update();
        Color defaultBgColor = GUI.backgroundColor;
        Color defaultContentColor = GUI.contentColor;

        // Draw the stylish header
        EditorGUILayout.Space(5);
        DrawHeader("Hybrid Level Generator", headerBgColor);
        EditorGUILayout.Space(5);

        // --- Generation Mode ---
        EditorGUILayout.LabelField("Generation Control", subHeaderStyle);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(generationModeProp);
        bool modeChanged = EditorGUI.EndChangeCheck();

        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;

        // Help text with improved styling
        DrawColoredBox(() => {
            switch (currentMode)
            {
                case GenerationMode.FullyProcedural:
                    EditorGUILayout.HelpBox("BSP + Random Rect Rooms + MST Corridors.", MessageType.Info);
                    break;
                case GenerationMode.HybridProcedural:
                    EditorGUILayout.HelpBox("BSP + Random Templates/L-Shapes/Rects + MST Corridors.", MessageType.Info);
                    break;
                case GenerationMode.UserDefinedLayout:
                    EditorGUILayout.HelpBox("Uses RoomNode components in the scene. Design layout using the Visual Level Designer window.", MessageType.Info);
                    break;
            }
        }, new Color(0.3f, 0.3f, 0.3f, 0.1f));

        // --- Open Editor Button (Conditional) ---
        if (currentMode == GenerationMode.UserDefinedLayout)
        {
            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
            if (GUILayout.Button(designerIcon, buttonStyle))
            {
                VisualLevelDesignEditor.ShowWindow();
            }
            GUI.backgroundColor = defaultBgColor;
        }

        DrawSeparator();

        // --- Level Dimensions Section ---
        levelDimensionsFoldout = EditorGUILayout.Foldout(levelDimensionsFoldout, dimensionsIcon, true, foldoutHeaderStyle);
        if (levelDimensionsFoldout)
        {
            DrawColoredSection(() => {
                EditorGUILayout.PropertyField(levelWidthProp, new GUIContent("Level Width", "Width of the level in grid units"));
                EditorGUILayout.PropertyField(levelHeightProp, new GUIContent("Level Height", "Height of the level in grid units"));

                // Add visual size preview
                EditorGUILayout.Space(5);
                Rect previewRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.4f, 0.8f, 0.5f));
                GUI.Label(previewRect, $"Visual Size Preview ({levelWidthProp.intValue} x {levelHeightProp.intValue})",
                    new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
            }, dimensionsBgColor);
        }

        // --- BSP Settings Section ---
        if (currentMode == GenerationMode.FullyProcedural || currentMode == GenerationMode.HybridProcedural)
        {
            bspSettingsFoldout = EditorGUILayout.Foldout(bspSettingsFoldout, bspIcon, true, foldoutHeaderStyle);
            if (bspSettingsFoldout)
            {
                DrawColoredSection(() => {
                    EditorGUILayout.PropertyField(minRoomSizeProp, new GUIContent("Min Room Size", "Minimum size of BSP rooms"));
                    EditorGUILayout.PropertyField(maxIterationsProp, new GUIContent("Max Iterations", "Maximum BSP tree depth"));
                    EditorGUILayout.PropertyField(roomPaddingProp, new GUIContent("Room Padding", "Space between rooms and BSP partition"));

                    // Add visual explanation
                    EditorGUILayout.Space(5);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("BSP Process:", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();

                    // BSP mini visualization
                    Rect bspVisualizationRect = EditorGUILayout.GetControlRect(false, 60);
                    EditorGUI.DrawRect(bspVisualizationRect, new Color(0.22f, 0.22f, 0.22f));

                    // Draw simple BSP division illustration
                    float halfWidth = bspVisualizationRect.width / 2;
                    float halfHeight = bspVisualizationRect.height / 2;
                    Color lineColor = new Color(0.8f, 0.8f, 0.8f);

                    // Draw first partition
                    Rect vertLine = new Rect(bspVisualizationRect.x + halfWidth, bspVisualizationRect.y, 1, bspVisualizationRect.height);
                    EditorGUI.DrawRect(vertLine, lineColor);

                    // Draw second tier partitions
                    Rect horizLine1 = new Rect(bspVisualizationRect.x, bspVisualizationRect.y + halfHeight / 2, halfWidth, 1);
                    Rect horizLine2 = new Rect(bspVisualizationRect.x + halfWidth, bspVisualizationRect.y + 3 * halfHeight / 2, halfWidth, 1);
                    EditorGUI.DrawRect(horizLine1, lineColor);
                    EditorGUI.DrawRect(horizLine2, lineColor);

                    // Draw room rects with padding
                    float padding = 5;
                    Rect room1 = new Rect(bspVisualizationRect.x + padding, bspVisualizationRect.y + padding,
                        halfWidth - 2 * padding, halfHeight / 2 - 2 * padding);
                    Rect room2 = new Rect(bspVisualizationRect.x + padding, bspVisualizationRect.y + halfHeight / 2 + padding,
                        halfWidth - 2 * padding, halfHeight / 2 - 2 * padding);
                    Rect room3 = new Rect(bspVisualizationRect.x + halfWidth + padding, bspVisualizationRect.y + padding,
                        halfWidth - 2 * padding, 3 * halfHeight / 2 - 2 * padding);
                    Rect room4 = new Rect(bspVisualizationRect.x + halfWidth + padding, bspVisualizationRect.y + 3 * halfHeight / 2 + padding,
                        halfWidth - 2 * padding, halfHeight / 2 - 2 * padding);

                    EditorGUI.DrawRect(room1, new Color(0.26f, 0.76f, 0.34f, 0.6f));
                    EditorGUI.DrawRect(room2, new Color(0.26f, 0.76f, 0.34f, 0.6f));
                    EditorGUI.DrawRect(room3, new Color(0.26f, 0.76f, 0.34f, 0.6f));
                    EditorGUI.DrawRect(room4, new Color(0.26f, 0.76f, 0.34f, 0.6f));
                }, bspBgColor);
            }
        }

        // --- Hybrid Settings Section ---
        if (currentMode == GenerationMode.HybridProcedural)
        {
            hybridSettingsFoldout = EditorGUILayout.Foldout(hybridSettingsFoldout, hybridIcon, true, foldoutHeaderStyle);
            if (hybridSettingsFoldout)
            {
                DrawColoredSection(() => {
                    // Room shape distribution visualization
                    EditorGUILayout.LabelField("Room Shape Distribution", boxHeaderStyle);

                    float lProb = lShapeProbabilityProp.floatValue;
                    float templateProb = roomTemplateProbabilityProp.floatValue;
                    float rectProb = 1 - lProb - templateProb;

                    // Prevent negative probability for rectangles
                    if (rectProb < 0)
                    {
                        rectProb = 0;
                        EditorGUILayout.HelpBox("Warning: L-shape + Template probabilities exceed 100%", MessageType.Warning);
                    }

                    // Draw probability bar
                    Rect probabilityBarRect = EditorGUILayout.GetControlRect(false, 22);
                    float fullWidth = probabilityBarRect.width;

                    // L-shape segment
                    Rect lRect = new Rect(probabilityBarRect.x, probabilityBarRect.y, fullWidth * lProb, probabilityBarRect.height);
                    EditorGUI.DrawRect(lRect, new Color(0.8f, 0.5f, 0.2f));

                    // Template segment
                    Rect templateRect = new Rect(lRect.x + lRect.width, probabilityBarRect.y, fullWidth * templateProb, probabilityBarRect.height);
                    EditorGUI.DrawRect(templateRect, new Color(0.2f, 0.6f, 0.8f));

                    // Rectangle segment
                    Rect rectRect = new Rect(templateRect.x + templateRect.width, probabilityBarRect.y, fullWidth * rectProb, probabilityBarRect.height);
                    EditorGUI.DrawRect(rectRect, new Color(0.6f, 0.3f, 0.7f));

                    // Labels
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("L-Shapes", EditorStyles.miniLabel, GUILayout.Width(fullWidth * lProb));
                    GUILayout.Label("Templates", EditorStyles.miniLabel, GUILayout.Width(fullWidth * templateProb));
                    GUILayout.Label("Rectangles", EditorStyles.miniLabel, GUILayout.Width(fullWidth * rectProb));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(5);

                    // Room shape probabilities
                    EditorGUILayout.LabelField("Shape Probabilities", boxHeaderStyle);
                    EditorGUILayout.PropertyField(lShapeProbabilityProp);
                    EditorGUILayout.PropertyField(roomTemplateProbabilityProp);
                    EditorGUILayout.LabelField($"Rectangle Probability: {rectProb:P0}", EditorStyles.miniLabel);

                    EditorGUILayout.Space(8);

                    // L-Shape settings
                    EditorGUILayout.LabelField("L-Shape Configuration", boxHeaderStyle);
                    EditorGUILayout.PropertyField(minLLegRatioProp);
                    EditorGUILayout.PropertyField(maxLLegRatioProp);

                    // L-shape visualization
                    Rect lShapeVisualization = EditorGUILayout.GetControlRect(false, 80);
                    Color lShapeColor = new Color(0.8f, 0.5f, 0.2f, 0.8f);
                    EditorGUI.DrawRect(lShapeVisualization, new Color(0.2f, 0.2f, 0.2f));

                    // Draw L-shape based on leg ratio
                    float ratio = (minLLegRatioProp.floatValue + maxLLegRatioProp.floatValue) / 2; // Average for visualization
                    float padding = 10;
                    float maxDim = Mathf.Min(lShapeVisualization.width, lShapeVisualization.height) - (2 * padding);
                    float legWidth = maxDim * ratio;
                    float legHeight = maxDim;

                    Rect lBase = new Rect(
                        lShapeVisualization.x + padding,
                        lShapeVisualization.y + padding,
                        legWidth, legHeight - legWidth);

                    Rect lHoriz = new Rect(
                        lShapeVisualization.x + padding,
                        lShapeVisualization.y + padding + legHeight - legWidth,
                        maxDim, legWidth);

                    EditorGUI.DrawRect(lBase, lShapeColor);
                    EditorGUI.DrawRect(lHoriz, lShapeColor);

                    EditorGUILayout.Space(8);

                    // Template settings
                    EditorGUILayout.LabelField("Room Templates", boxHeaderStyle);
                    EditorGUILayout.PropertyField(roomTemplatePrefabsProp, true);

                }, hybridBgColor);
            }
        }

        // --- Common Settings Section ---
        commonSettingsFoldout = EditorGUILayout.Foldout(commonSettingsFoldout, settingsIcon, true, foldoutHeaderStyle);
        if (commonSettingsFoldout)
        {
            DrawColoredSection(() => {
                // Corridor settings with visualization
                EditorGUILayout.LabelField("Corridor Settings", boxHeaderStyle);
                EditorGUILayout.PropertyField(corridorWidthProp);

                // Corridor width visualization
                int corridorWidth = corridorWidthProp.intValue;
                Rect corridorVisRect = EditorGUILayout.GetControlRect(false, 40);
                EditorGUI.DrawRect(corridorVisRect, new Color(0.2f, 0.2f, 0.2f));

                float padding = 10;
                float maxHeight = corridorVisRect.height - (2 * padding);
                float corridorWidthPixels = Mathf.Min(maxHeight, corridorWidth * 8); // Scale corridor width for visualization

                Rect corridorRect = new Rect(
                    corridorVisRect.x + padding,
                    corridorVisRect.y + (corridorVisRect.height - corridorWidthPixels) / 2,
                    corridorVisRect.width - (2 * padding),
                    corridorWidthPixels);

                EditorGUI.DrawRect(corridorRect, new Color(0.4f, 0.4f, 0.8f, 0.8f));

                // Label in center of corridor
                GUI.Label(corridorRect, $"Width: {corridorWidth}",
                    new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.white }
                    });

                EditorGUILayout.Space(10);

                // Sub-sections organized with nested foldouts
                tileSettingsFoldout = EditorGUILayout.Foldout(tileSettingsFoldout, "Tiles & Tilemaps", true);
                if (tileSettingsFoldout)
                {
                    EditorGUI.indentLevel++;

                    // Tilemap icons
                    EditorGUILayout.PropertyField(groundTilemapProp, EditorGUIUtility.IconContent("Tilemap Icon"));
                    EditorGUILayout.PropertyField(wallTilemapProp, EditorGUIUtility.IconContent("Tilemap Icon"));

                    // Tile previews with colors
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Floor Tile");
                    Rect floorPreviewRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    Rect floorColorRect = new Rect(floorPreviewRect.x, floorPreviewRect.y, 20, floorPreviewRect.height);
                    EditorGUI.DrawRect(floorColorRect, new Color(0.4f, 0.8f, 0.4f));
                    Rect floorFieldRect = new Rect(
                        floorPreviewRect.x + 25,
                        floorPreviewRect.y,
                        floorPreviewRect.width - 25,
                        floorPreviewRect.height);
                    EditorGUI.PropertyField(floorFieldRect, floorTileProp, GUIContent.none);
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel("Wall Tile");
                    Rect wallPreviewRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    Rect wallColorRect = new Rect(wallPreviewRect.x, wallPreviewRect.y, 20, wallPreviewRect.height);
                    EditorGUI.DrawRect(wallColorRect, new Color(0.6f, 0.3f, 0.3f));
                    Rect wallFieldRect = new Rect(
                        wallPreviewRect.x + 25,
                        wallPreviewRect.y,
                        wallPreviewRect.width - 25,
                        wallPreviewRect.height);
                    EditorGUI.PropertyField(wallFieldRect, wallTileProp, GUIContent.none);
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                randomnessSettingsFoldout = EditorGUILayout.Foldout(randomnessSettingsFoldout, "Randomness", true);
                if (randomnessSettingsFoldout)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(useRandomSeedProp);

                    // Only show seed field if not using random seed
                    if (!useRandomSeedProp.boolValue)
                    {
                        EditorGUILayout.PropertyField(seedProp);

                        // Seed quick buttons
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Random Seed", EditorStyles.miniButton))
                        {
                            seedProp.intValue = UnityEngine.Random.Range(1, 9999);
                        }
                        if (GUILayout.Button("Copy Seed", EditorStyles.miniButton))
                        {
                            EditorGUIUtility.systemCopyBuffer = seedProp.intValue.ToString();
                        }
                        if (GUILayout.Button("Paste", EditorStyles.miniButton))
                        {
                            if (int.TryParse(EditorGUIUtility.systemCopyBuffer, out int pastedSeed))
                            {
                                seedProp.intValue = pastedSeed;
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                entitySettingsFoldout = EditorGUILayout.Foldout(entitySettingsFoldout, "Entities & Decorations", true);
                if (entitySettingsFoldout)
                {
                    EditorGUI.indentLevel++;

                    // Player settings with icon
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(EditorGUIUtility.IconContent("d_AnimatorController Icon"), GUILayout.Width(20), GUILayout.Height(20));
                    EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.PropertyField(playerPrefabProp);

                    EditorGUILayout.Space(5);

                    // Enemy settings with icon and count
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(EditorGUIUtility.IconContent("d_PreMatCube"), GUILayout.Width(20), GUILayout.Height(20));
                    EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.PropertyField(enemyPrefabProp);
                    EditorGUILayout.PropertyField(enemiesPerRoomProp);

                    // Enemy count visualization
                    int enemyCount = enemiesPerRoomProp.intValue;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(" ");
                    for (int i = 0; i < Mathf.Min(enemyCount, 10); i++)
                    {
                        GUILayout.Label(EditorGUIUtility.IconContent("d_PreMatCube"), GUILayout.Width(16), GUILayout.Height(16));
                    }
                    if (enemyCount > 10)
                    {
                        GUILayout.Label($"+{enemyCount - 10} more", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(5);

                    // Decoration settings with icon and count
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(EditorGUIUtility.IconContent("d_TerrainInspector.TerrainToolSplat"), GUILayout.Width(20), GUILayout.Height(20));
                    EditorGUILayout.LabelField("Decorations", EditorStyles.boldLabel);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.PropertyField(decorationPrefabProp);
                    EditorGUILayout.PropertyField(decorationsPerRoomProp);

                    EditorGUI.indentLevel--;
                }

                // Show Templates list if needed
                if (currentMode == GenerationMode.HybridProcedural || currentMode == GenerationMode.UserDefinedLayout)
                {
                    if (!hybridSettingsFoldout) // Only show here if not already shown in Hybrid section
                    {
                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField("Room Templates", boxHeaderStyle);
                        EditorGUILayout.PropertyField(roomTemplatePrefabsProp, true);
                    }
                }

            }, commonBgColor);
        }

        // --- Editor Preview Section ---
        editorPreviewFoldout = EditorGUILayout.Foldout(editorPreviewFoldout, previewIcon, true, foldoutHeaderStyle);
        if (editorPreviewFoldout)
        {
            DrawColoredSection(() => {
                // Info box with icon
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label(EditorGUIUtility.IconContent("d_console.infoicon"), GUILayout.Width(32), GUILayout.Height(32));
                EditorGUILayout.LabelField("Assign a Render Texture and a Camera that renders to it to see a preview in the Visual Level Designer window.",
                    new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 });
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // Render texture preview
                EditorGUILayout.PropertyField(levelPreviewTextureProp);
                if (levelPreviewTextureProp.objectReferenceValue != null)
                {
                    // Draw a preview of the render texture
                    Rect previewRect = EditorGUILayout.GetControlRect(false, 100);
                    GUI.Box(previewRect, GUIContent.none);

                    if (levelPreviewTextureProp.objectReferenceValue != null)
                    {
                        GUI.DrawTexture(
                            new Rect(previewRect.x + 2, previewRect.y + 2, previewRect.width - 4, previewRect.height - 4),
                            (Texture)levelPreviewTextureProp.objectReferenceValue,
                            ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        GUI.Label(previewRect, "No Render Texture Assigned",
                            new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 });
                    }
                }

                EditorGUILayout.PropertyField(levelPreviewCameraProp);

                // Quick setup button
                if (levelPreviewTextureProp.objectReferenceValue == null || levelPreviewCameraProp.objectReferenceValue == null)
                {
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Quick Setup Preview Camera", EditorStyles.miniButton))
                    {
                        // This would create a render texture and set up a camera in a real implementation
                        EditorUtility.DisplayDialog("Setup Preview",
                            "This would create a new Render Texture and configure a camera in your scene to use for preview.\n\n" +
                            "Implementation would create assets and configure components.",
                            "OK");
                    }
                }

            }, previewBgColor);
        }

        DrawSeparator();

        // --- Actions ---
        EditorGUILayout.LabelField("Generator Controls", subHeaderStyle);

        EditorGUILayout.BeginHorizontal();

        // Generate Button
        GUI.backgroundColor = generateButtonColor;
        if (GUILayout.Button(generateIcon, buttonStyle, GUILayout.Height(40)))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            Undo.RecordObject(generator, "Generate Level");
            generator.GenerateLevel();
            MarkSceneDirty(generator);
        }

        // Clear Button
        GUI.backgroundColor = clearButtonColor;
        if (GUILayout.Button(clearIcon, buttonStyle, GUILayout.Height(40)))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            if (EditorUtility.DisplayDialog("Confirm Clear",
                "Clear generated level AND scene design nodes?",
                "Clear All", "Cancel"))
            {
                Undo.RecordObject(generator, "Clear Level");
                generator.ClearLevel();
                MarkSceneDirty(generator);
            }
        }
        GUI.backgroundColor = defaultBgColor;
        EditorGUILayout.EndHorizontal();

        // History info
        EditorGUILayout.Space(5);
        if (seedProp != null && !useRandomSeedProp.boolValue)
        {
            EditorGUILayout.LabelField($"Current Seed: {seedProp.intValue}", EditorStyles.miniLabel);
        }

        serializedObject.ApplyModifiedProperties();
    }

    // Helper method to draw a colored section
    private void DrawColoredSection(Action drawContent, Color backgroundColor)
    {
        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = backgroundColor;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = defaultColor;

        if (drawContent != null)
        {
            drawContent();
        }

        EditorGUILayout.EndVertical();
    }

    // Helper method to draw a colored box
    private void DrawColoredBox(Action drawContent, Color backgroundColor)
    {
        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = backgroundColor;
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = defaultColor;

        if (drawContent != null)
        {
            drawContent();
        }

        EditorGUILayout.EndVertical();
    }

    // Helper method to draw a header
    private void DrawHeader(string title, Color backgroundColor)
    {
        Rect headerRect = EditorGUILayout.GetControlRect(false, 26);
        EditorGUI.DrawRect(headerRect, backgroundColor);

        GUI.color = Color.white;
        EditorGUI.LabelField(headerRect, title,
            new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                normal = { textColor = Color.white }
            });
        GUI.color = Color.white;
    }

    // Helper method to draw a separator
    private void DrawSeparator()
    {
        EditorGUILayout.Space(8);
        Rect separatorRect = EditorGUILayout.GetControlRect(false, 2);
        EditorGUI.DrawRect(separatorRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(8);
    }

    private void MarkSceneDirty(HybridLevelGenerator generator)
    {
        if (!Application.isPlaying && generator != null && generator.gameObject != null)
        {
            try
            {
                if (generator.gameObject.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not mark scene dirty: {e.Message}");
            }
        }
    }
}*/




















using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using UnityEditorInternal;

// Note: Assumes GenerationMode enum is defined in a separate shared file like LevelGenerationData.cs
// Example: public enum GenerationMode { FullyProcedural, HybridProcedural, UserDefinedLayout }

// Note: Assumes VisualLevelDesignEditor class with ShowWindow() exists if using UserDefinedLayout mode.
// Example: public static class VisualLevelDesignEditor { public static void ShowWindow() { /* ... */ } }


[CustomEditor(typeof(HybridLevelGenerator))]
public class HybridLevelGeneratorEditor : Editor
{
    // --- Serialized Properties ---
    SerializedProperty generationModeProp;
    SerializedProperty levelWidthProp;
    SerializedProperty levelHeightProp;
    SerializedProperty minRoomSizeProp;
    SerializedProperty maxIterationsProp;
    SerializedProperty roomPaddingProp; // Declared as float in target script
    SerializedProperty lShapeProbabilityProp;
    SerializedProperty minLLegRatioProp;
    SerializedProperty maxLLegRatioProp;
    SerializedProperty roomTemplatePrefabsProp;
    SerializedProperty roomTemplateProbabilityProp;
    SerializedProperty corridorWidthProp;
    SerializedProperty groundTilemapProp;
    SerializedProperty wallTilemapProp;
    SerializedProperty floorTileProp;
    SerializedProperty wallTileProp;
    SerializedProperty seedProp;
    SerializedProperty useRandomSeedProp;
    SerializedProperty playerPrefabProp;
    SerializedProperty enemyPrefabProp;
    SerializedProperty decorationPrefabProp;
    SerializedProperty enemiesPerRoomProp;
    SerializedProperty decorationsPerRoomProp;
    // Removed Preview Properties

    // --- UI States ---
    private int selectedTab = 0;
    // Adjusted Tab Names
    private string[] tabNames = new string[] { "Setup", "Room Generation", "Entities" };
    // Removed Auto-Preview states
    private bool isInitialSetup = true;

    // Reorderable lists
    private ReorderableList roomTemplatesList;

    // --- Style & Color ---
    // Modern, vibrant color scheme
    private readonly Color TAB_BAR_COLOR = new Color(0.15f, 0.15f, 0.17f);
    private readonly Color ACTIVE_TAB_COLOR = new Color(0.22f, 0.51f, 0.89f);
    private readonly Color INACTIVE_TAB_COLOR = new Color(0.25f, 0.25f, 0.27f);
    private readonly Color HEADER_COLOR = new Color(0.22f, 0.51f, 0.89f);
    private readonly Color PANEL_COLOR = new Color(0.18f, 0.18f, 0.2f, 0.8f);
    private readonly Color SECTION_COLOR = new Color(0.22f, 0.22f, 0.24f, 0.8f);
    private readonly Color FIELD_BG_COLOR = new Color(0.2f, 0.2f, 0.22f);
    private readonly Color FIELD_ACCENT_COLOR = new Color(0.3f, 0.6f, 1f);
    private readonly Color GENERATE_COLOR = new Color(0.15f, 0.75f, 0.5f);
    private readonly Color CLEAR_COLOR = new Color(0.85f, 0.25f, 0.25f);
    private readonly Color TEXT_COLOR = new Color(0.9f, 0.9f, 0.9f);
    private readonly Color HIGHLIGHT_COLOR = new Color(0.95f, 0.95f, 0.95f);
    private readonly Color SUCCESS_COLOR = new Color(0.15f, 0.75f, 0.5f, 0.8f);

    // UI Elements
    private GUIStyle tabStyle;
    private GUIStyle activeTabStyle;
    private GUIStyle headerStyle;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle fieldHeaderStyle;
    private GUIStyle panelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle bigButtonStyle;
    private GUIStyle labelStyle;
    private GUIStyle richTextStyle;
    private GUIStyle sliderThumbStyle; // Currently unused with simplified sliders
    private GUIStyle tooltipStyle; // Currently unused
    private GUIStyle dimensionStyle; // Declared

    // Icons
    private GUIContent generateIcon;
    private GUIContent clearIcon;
    // Removed previewIcon, autoPreviewIcon
    private GUIContent settingsIcon;
    private GUIContent designerIcon;
    private GUIContent infoIcon;
    private GUIContent warningIcon;
    // Adjusted size
    private GUIContent[] tabIcons = new GUIContent[3];

    // Textures
    private Texture2D panelTexture;
    private Texture2D tabBarTexture;
    private Texture2D sliderBackTexture; // Currently unused with simplified sliders
    private Texture2D sliderFillTexture; // Currently unused with simplified sliders
    private Texture2D headerTexture;
    // Removed previewRenderTexture

    // Animation
    private float pulseAnimation = 0f;
    private bool stylesInitialized = false;

    // Quick-help feature
    private int quickHelpStep = -1;
    // Adjusted messages
    private string[] quickHelpMessages = new string[] {
         "Start by selecting a <b>Generation Mode</b>. Fully Procedural uses pure BSP, while Hybrid allows for template rooms.",
         "Configure your <b>Level Dimensions</b> (Width/Height) which define the maximum generation area.",
         "Adjust <b>BSP Settings</b> (Min Size, Iterations, Padding) to control how the level is initially partitioned.",
         "In Hybrid Mode, adjust <b>Room Type Distribution</b> probabilities and configure L-Shapes or Templates.",
         "Customize <b>Corridors</b> to connect your rooms.",
         "Assign Tilemaps, Tiles and Prefabs for <b>Players</b>, <b>Enemies</b>, and <b>Decorations</b>.",
         "When ready, use the controls at the bottom to <b>Generate</b> or <b>Clear</b> the level!"
     };

    // OnEnable Method (Corrected)
    private void OnEnable()
    {
        // Find properties
        generationModeProp = serializedObject.FindProperty("generationMode");
        levelWidthProp = serializedObject.FindProperty("levelWidth");
        levelHeightProp = serializedObject.FindProperty("levelHeight");
        minRoomSizeProp = serializedObject.FindProperty("minRoomSize");
        maxIterationsProp = serializedObject.FindProperty("maxIterations");
        roomPaddingProp = serializedObject.FindProperty("roomPadding");
        lShapeProbabilityProp = serializedObject.FindProperty("lShapeProbability");
        minLLegRatioProp = serializedObject.FindProperty("minLLegRatio");
        maxLLegRatioProp = serializedObject.FindProperty("maxLLegRatio");
        roomTemplatePrefabsProp = serializedObject.FindProperty("roomTemplatePrefabs");
        roomTemplateProbabilityProp = serializedObject.FindProperty("roomTemplateProbability");
        corridorWidthProp = serializedObject.FindProperty("corridorWidth");
        groundTilemapProp = serializedObject.FindProperty("groundTilemap");
        wallTilemapProp = serializedObject.FindProperty("wallTilemap");
        floorTileProp = serializedObject.FindProperty("floorTile");
        wallTileProp = serializedObject.FindProperty("wallTile");
        seedProp = serializedObject.FindProperty("seed");
        useRandomSeedProp = serializedObject.FindProperty("useRandomSeed");
        playerPrefabProp = serializedObject.FindProperty("playerPrefab");
        enemyPrefabProp = serializedObject.FindProperty("enemyPrefab");
        decorationPrefabProp = serializedObject.FindProperty("decorationPrefab");
        enemiesPerRoomProp = serializedObject.FindProperty("enemiesPerRoom");
        decorationsPerRoomProp = serializedObject.FindProperty("decorationsPerRoom");
        // Removed preview property finds

        // Set up reorderable lists
        SetupRoomTemplatesList();

        // Removed CreateMiniMapTexture();

        // Register for editor updates (for animations)
        EditorApplication.update += OnEditorUpdate;

        stylesInitialized = false; // Ensure flag is reset here
    }

    private void OnDisable()
    {
        // Unregister from editor updates
        EditorApplication.update -= OnEditorUpdate;
    }

    private void SetupRoomTemplatesList()
    {
        roomTemplatesList = new ReorderableList(
            serializedObject,
            roomTemplatePrefabsProp,
            true, true, true, true);

        roomTemplatesList.drawHeaderCallback = (Rect rect) => { EditorGUI.LabelField(rect, "Room Template Prefabs", EditorStyles.boldLabel); };

        roomTemplatesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            rect.y += 2; rect.height = EditorGUIUtility.singleLineHeight;
            SerializedProperty element = roomTemplatesList.serializedProperty.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        };

        roomTemplatesList.onAddCallback = (ReorderableList list) =>
        {
            int index = list.serializedProperty.arraySize; list.serializedProperty.arraySize++; list.index = index;
            SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
            element.objectReferenceValue = null;
            // Removed needsPreviewUpdate = true;
        };

        roomTemplatesList.onRemoveCallback = (ReorderableList list) =>
        {
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            // Removed needsPreviewUpdate = true;
        };
    }

    // Removed CreateMiniMapTexture, UpdateMiniMapPreview, GenerateBspRooms (Preview), Set/Draw MiniMap methods

    private void OnEditorUpdate()
    {
        // Update animation state
        pulseAnimation = (float)(EditorApplication.timeSinceStartup * 2.0f % (2.0f * Math.PI));

        // Repaint constantly for smooth animation - remove if performance is an issue
        Repaint();

        // Removed Auto-Preview logic
    }

    private Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    public override void OnInspectorGUI()
    {
        // Complete ALL GUI style, texture, and icon initialization inside OnGUI
        if (!stylesInitialized)
        {
            // --- ALL INITIALIZATION CODE MOVED HERE ---
            try // Added try-catch for safety
            {
                // Basic styles
                headerStyle = new GUIStyle { fontSize = 16, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fixedHeight = 30, margin = new RectOffset(0, 0, 5, 10), padding = new RectOffset(5, 5, 5, 5), normal = { textColor = TEXT_COLOR } };
                sectionHeaderStyle = new GUIStyle { fontSize = 14, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 10, 5), padding = new RectOffset(5, 0, 0, 0), normal = { textColor = TEXT_COLOR } };
                fieldHeaderStyle = new GUIStyle { fontSize = 12, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 5, 2), normal = { textColor = TEXT_COLOR } };
                dimensionStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }; // Initialize dimensionStyle

                // Create textures
                panelTexture = CreateColorTexture(PANEL_COLOR);
                tabBarTexture = CreateColorTexture(TAB_BAR_COLOR);
                sliderBackTexture = CreateColorTexture(new Color(0.2f, 0.2f, 0.22f));
                sliderFillTexture = CreateColorTexture(FIELD_ACCENT_COLOR);
                headerTexture = CreateColorTexture(HEADER_COLOR);

                // Set up icons
                generateIcon = EditorGUIUtility.IconContent("d_Refresh"); generateIcon.text = " Generate Level";
                clearIcon = EditorGUIUtility.IconContent("d_TreeEditor.Trash"); clearIcon.text = " Clear Level";
                // previewIcon removed
                // autoPreviewIcon removed
                settingsIcon = EditorGUIUtility.IconContent("d_Settings"); settingsIcon.text = " Settings";
                designerIcon = EditorGUIUtility.IconContent("d_SceneViewCamera"); designerIcon.text = " Visual Designer";
                infoIcon = EditorGUIUtility.IconContent("d_console.infoicon");
                warningIcon = EditorGUIUtility.IconContent("d_console.warnicon");
                // Adjusted tabIcons size & content
                tabIcons[0] = new GUIContent(" Setup", EditorGUIUtility.IconContent("d_Settings")?.image); // Added null checks for safety
                tabIcons[1] = new GUIContent(" Rooms", EditorGUIUtility.IconContent("d_GridLayoutGroup Icon")?.image);
                tabIcons[2] = new GUIContent(" Entities", EditorGUIUtility.IconContent("d_AnimatorController Icon")?.image);

                // Styles requiring EditorStyles or GUI.skin
                tabStyle = new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleCenter, fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 28, border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(10, 10, 4, 4), margin = new RectOffset(0, 0, 0, 0), normal = { background = CreateColorTexture(INACTIVE_TAB_COLOR), textColor = new Color(0.7f, 0.7f, 0.7f) }, hover = { background = CreateColorTexture(new Color(0.3f, 0.3f, 0.32f)), textColor = TEXT_COLOR } };
                activeTabStyle = new GUIStyle(tabStyle) { normal = { background = CreateColorTexture(ACTIVE_TAB_COLOR), textColor = HIGHLIGHT_COLOR }, hover = { background = CreateColorTexture(ACTIVE_TAB_COLOR), textColor = HIGHLIGHT_COLOR } };
                panelStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 10, 10), margin = new RectOffset(0, 0, 0, 10) };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, margin = new RectOffset(5, 5, 5, 5), padding = new RectOffset(10, 10, 5, 5), fixedHeight = 28 };
                bigButtonStyle = new GUIStyle(buttonStyle) { fontSize = 14, fixedHeight = 40, padding = new RectOffset(15, 15, 8, 8) };
                labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, normal = { textColor = TEXT_COLOR } };
                richTextStyle = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true, fontSize = 12, normal = { textColor = TEXT_COLOR } };

                stylesInitialized = true; // Set flag AFTER initialization
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during editor GUI initialization: {e.Message}\n{e.StackTrace}");
                stylesInitialized = true; // Mark as 'attempted'
            }
        }

        // Safety check
        if (labelStyle == null || buttonStyle == null || headerStyle == null || dimensionStyle == null || sectionHeaderStyle == null || richTextStyle == null || tabIcons[0] == null || tabIcons[1] == null || tabIcons[2] == null)
        {
            EditorGUILayout.HelpBox("Editor styles/icons failed to initialize. Check console.", MessageType.Error);
            DrawDefaultInspector(); // Draw default inspector as fallback
            return;
        }

        // --- Rest of OnInspectorGUI ---
        serializedObject.Update();
        Color originalBgColor = GUI.backgroundColor;
        Color originalContentColor = GUI.contentColor;

        EditorGUI.BeginChangeCheck(); // Start checking for changes

        DrawHeader();
        DrawTabBar(); // Draws 3 tabs now

        EditorGUILayout.BeginVertical(EditorStyles.helpBox); // Main content area
        switch (selectedTab)
        {
            case 0: DrawSetupTab(); break;
            case 1: DrawRoomGenerationTab(); break;
            case 2: DrawEntitiesTab(); break;
                // Case 3 removed
        }
        EditorGUILayout.EndVertical();

        DrawActionControls();

        if (quickHelpStep >= 0 && quickHelpStep < quickHelpMessages.Length)
        { DrawQuickHelpTip(); }

        // Apply changes check - no longer needs to trigger preview
        if (EditorGUI.EndChangeCheck())
        {
            // Property changed
        }

        serializedObject.ApplyModifiedProperties();

        // Simplified initial setup prompt
        if (isInitialSetup)
        {
            isInitialSetup = false;
            bool showHelpPrompt = groundTilemapProp.objectReferenceValue == null || wallTilemapProp.objectReferenceValue == null || floorTileProp.objectReferenceValue == null || wallTileProp.objectReferenceValue == null || playerPrefabProp.objectReferenceValue == null;
            if (showHelpPrompt && EditorUtility.DisplayDialog("Level Generator Setup", "Welcome! Some essential references might be missing.\nEnable the Quick Help Guide to get started?", "Yes, Show Help", "No Thanks")) { quickHelpStep = 0; }
            else if (showHelpPrompt) { Debug.LogWarning("[HybridLevelGenerator] Essential references missing (Tilemaps, Tiles, Player Prefab). Assign these in the Inspector."); }
        }
    }

    // DrawHeader method remains mostly the same
    private new void DrawHeader()
    {
        Rect headerRect = EditorGUILayout.GetControlRect(false, 40);
        EditorGUI.DrawRect(headerRect, HEADER_COLOR);
        Color gradientTop = new Color(HEADER_COLOR.r * 1.2f, HEADER_COLOR.g * 1.2f, HEADER_COLOR.b * 1.2f, 1f); Color gradientBottom = HEADER_COLOR;
        for (int y = 0; y < headerRect.height; y++) { float t = y / headerRect.height; Color gradientColor = Color.Lerp(gradientTop, gradientBottom, t); Rect lineRect = new Rect(headerRect.x, headerRect.y + y, headerRect.width, 1); EditorGUI.DrawRect(lineRect, gradientColor); }
        Rect shadowRect = new Rect(headerRect.x + 1, headerRect.y + 1, headerRect.width, headerRect.height);
        if (headerStyle != null) // Safety check
        { GUI.Label(shadowRect, "HYBRID LEVEL GENERATOR", new GUIStyle(headerStyle) { normal = { textColor = new Color(0, 0, 0, 0.3f) } }); GUI.Label(headerRect, "HYBRID LEVEL GENERATOR", headerStyle); }
        else { GUI.Label(headerRect, "HYBRID LEVEL GENERATOR"); }
    }

    // DrawTabBar method adjusted for 3 tabs
    private void DrawTabBar()
    {
        if (tabNames.Length != 3 || tabIcons.Length != 3) { Debug.LogError("Tab configuration mismatch!"); return; } // Safety check size
        if (tabNames.Length == 0) return;

        Rect tabBarRect = EditorGUILayout.GetControlRect(false, 30);
        EditorGUI.DrawRect(tabBarRect, TAB_BAR_COLOR);
        float tabWidth = tabBarRect.width / tabNames.Length;
        for (int i = 0; i < tabNames.Length; i++)
        {
            Rect tabRect = new Rect(tabBarRect.x + (i * tabWidth), tabBarRect.y, tabWidth, tabBarRect.height);
            GUIStyle style = (selectedTab == i) ? activeTabStyle : tabStyle;
            // Use fallback if styles/icons null
            GUIContent currentIcon = tabIcons[i] ?? new GUIContent(tabNames[i]);
            if (GUI.Button(tabRect, currentIcon, style ?? EditorStyles.toolbarButton)) { selectedTab = i; if (quickHelpStep >= 0) { quickHelpStep = Mathf.Clamp(i, 0, quickHelpMessages.Length - 1); } }
        }
    }

    // DrawSetupTab method (Auto-Preview Removed)
    private void DrawSetupTab()
    {
        EditorGUILayout.Space(5);
        // Generation Mode Section
        EditorGUILayout.LabelField("Generation Mode", sectionHeaderStyle);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUI.BeginChangeCheck(); EditorGUILayout.PropertyField(generationModeProp); bool modeChanged = EditorGUI.EndChangeCheck();
        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox); EditorGUILayout.BeginHorizontal(); if (infoIcon != null) GUILayout.Label(infoIcon, GUILayout.Width(24), GUILayout.Height(24)); EditorGUILayout.BeginVertical();
        GUIStyle helpTextStyle = labelStyle ?? EditorStyles.label; GUIStyle miniHelpTextStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = TEXT_COLOR } };
        switch (currentMode) { case GenerationMode.FullyProcedural: EditorGUILayout.LabelField("BSP + Random Rect Rooms + MST Corridors", helpTextStyle); EditorGUILayout.LabelField("Best for: Roguelike dungeons with rectangular rooms", miniHelpTextStyle); break; case GenerationMode.HybridProcedural: EditorGUILayout.LabelField("BSP + Templates/L-Shapes/Rects + MST Corridors", helpTextStyle); EditorGUILayout.LabelField("Best for: Varied levels with predefined room templates", miniHelpTextStyle); break; case GenerationMode.UserDefinedLayout: EditorGUILayout.LabelField("Uses RoomNode components in the scene", helpTextStyle); EditorGUILayout.LabelField("Best for: Hand-crafted levels with procedural elements", miniHelpTextStyle); break; }
        EditorGUILayout.EndVertical(); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical();
        if (currentMode == GenerationMode.UserDefinedLayout) { EditorGUILayout.Space(5); Color originalBgColor = GUI.backgroundColor; GUI.backgroundColor = FIELD_ACCENT_COLOR; GUIContent designerButtonContent = designerIcon ?? new GUIContent("Visual Designer"); if (GUILayout.Button(designerButtonContent, buttonStyle ?? GUI.skin.button)) { try { VisualLevelDesignEditor.ShowWindow(); } catch (Exception e) { Debug.LogError($"Failed to open Visual Level Designer: {e.Message}"); } } GUI.backgroundColor = originalBgColor; }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // Level Dimensions Section
        EditorGUILayout.LabelField("Level Dimensions", sectionHeaderStyle);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        // Safe int access check for properties used in visualization
        int lw = (levelWidthProp != null && levelWidthProp.propertyType == SerializedPropertyType.Integer) ? levelWidthProp.intValue : 10;
        int lh = (levelHeightProp != null && levelHeightProp.propertyType == SerializedPropertyType.Integer) ? levelHeightProp.intValue : 10;
        DrawPropertyWithSlider(levelWidthProp, "Width", 10, 200); // Increased max range slightly
        DrawPropertyWithSlider(levelHeightProp, "Height", 10, 200);
        EditorGUILayout.Space(5);
        Rect sizePreviewRect = EditorGUILayout.GetControlRect(false, 30);
        float aspectRatio = 1f; if (lh > 0) { aspectRatio = lw / (float)lh; }
        EditorGUI.DrawRect(sizePreviewRect, FIELD_BG_COLOR);
        float previewWidth = 0f, previewHeight = 0f; float padding = 5f; float availableWidth = sizePreviewRect.width - (2 * padding); float availableHeight = sizePreviewRect.height - (2 * padding);
        if (availableWidth > 0 && availableHeight > 0) { if (aspectRatio >= 1) { previewWidth = availableWidth; previewHeight = previewWidth / aspectRatio; if (previewHeight > availableHeight) { previewHeight = availableHeight; previewWidth = previewHeight * aspectRatio; } } else { previewHeight = availableHeight; previewWidth = previewHeight * aspectRatio; if (previewWidth > availableWidth) { previewWidth = availableWidth; previewHeight = previewWidth / aspectRatio; } } }
        Rect levelRect = new Rect(sizePreviewRect.x + ((sizePreviewRect.width - previewWidth) / 2), sizePreviewRect.y + ((sizePreviewRect.height - previewHeight) / 2), previewWidth, previewHeight);
        float pulse = Mathf.Sin(pulseAnimation) * 0.1f + 0.9f; Color levelRectColor = new Color(FIELD_ACCENT_COLOR.r * pulse, FIELD_ACCENT_COLOR.g * pulse, FIELD_ACCENT_COLOR.b * pulse, 0.8f);
        if (levelRect.width > 0 && levelRect.height > 0) { EditorGUI.DrawRect(levelRect, levelRectColor); }
        if (dimensionStyle != null) { GUI.Label(levelRect, $"{lw} x {lh}", dimensionStyle); } else { GUI.Label(levelRect, $"{lw} x {lh}"); }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // Randomness Section
        EditorGUILayout.LabelField("Randomness", sectionHeaderStyle);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.PropertyField(useRandomSeedProp);
        if (!useRandomSeedProp.boolValue)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(seedProp);
            if (GUILayout.Button("Random", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                // Safe access before setting
                if (seedProp != null && seedProp.propertyType == SerializedPropertyType.Integer)
                    seedProp.intValue = UnityEngine.Random.Range(1, 99999);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", EditorStyles.miniButton))
            {
                // Safe access before copying
                if (seedProp != null && seedProp.propertyType == SerializedPropertyType.Integer)
                    EditorGUIUtility.systemCopyBuffer = seedProp.intValue.ToString();
            }
            if (GUILayout.Button("Paste", EditorStyles.miniButton)) { if (int.TryParse(EditorGUIUtility.systemCopyBuffer, out int pastedSeed)) { if (seedProp != null && seedProp.propertyType == SerializedPropertyType.Integer) seedProp.intValue = pastedSeed; } }
            if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(25))) { if (seedProp != null && seedProp.propertyType == SerializedPropertyType.Integer) seedProp.intValue--; }
            if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(25))) { if (seedProp != null && seedProp.propertyType == SerializedPropertyType.Integer) seedProp.intValue++; }
            EditorGUILayout.EndHorizontal();
        }
        else { EditorGUILayout.HelpBox("Using random seed each time.", MessageType.None); }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // --- Removed Auto-Preview Toggle ---
        // Quick Help Button remains
        Color helpBtnColor = (quickHelpStep >= 0) ? SUCCESS_COLOR : GUI.backgroundColor;
        Color defaultBgColorHelp = GUI.backgroundColor;
        GUI.backgroundColor = helpBtnColor;
        if (GUILayout.Button(quickHelpStep >= 0 ? "Disable Quick Help" : "Enable Quick Help", buttonStyle ?? GUI.skin.button, GUILayout.Height(26)))
        { quickHelpStep = (quickHelpStep >= 0) ? -1 : 0; }
        GUI.backgroundColor = defaultBgColorHelp;
    }

    // DrawRoomGenerationTab method (with safe access checks)
    private void DrawRoomGenerationTab()
    {
        EditorGUILayout.Space(5);
        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;
        // BSP Settings Section
        if (currentMode == GenerationMode.FullyProcedural || currentMode == GenerationMode.HybridProcedural)
        {
            EditorGUILayout.LabelField("BSP Algorithm Settings", sectionHeaderStyle);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            DrawPropertyWithSlider(minRoomSizeProp, "Min Room Size", 3, 20);
            DrawPropertyWithSlider(maxIterationsProp, "Max Iterations", 1, 10);
            DrawPropertyWithSlider(roomPaddingProp, "Room Padding", 0f, 5f); // Float
            EditorGUILayout.Space(5); EditorGUILayout.LabelField("BSP Division Visualization", fieldHeaderStyle);
            Rect bspVisualizationRect = EditorGUILayout.GetControlRect(false, 100); EditorGUI.DrawRect(bspVisualizationRect, FIELD_BG_COLOR);
            int iterations = 1; if (maxIterationsProp != null && maxIterationsProp.propertyType == SerializedPropertyType.Integer) iterations = maxIterationsProp.intValue; iterations = Mathf.Clamp(iterations, 1, 5);
            DrawBspVisualization(bspVisualizationRect, iterations);
            EditorGUILayout.EndVertical(); EditorGUILayout.Space(10);
        }
        // Hybrid Settings
        if (currentMode == GenerationMode.HybridProcedural)
        {
            EditorGUILayout.LabelField("Room Type Distribution", sectionHeaderStyle);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            float lProb = (lShapeProbabilityProp != null && lShapeProbabilityProp.propertyType == SerializedPropertyType.Float) ? lShapeProbabilityProp.floatValue : 0f;
            float templateProb = (roomTemplateProbabilityProp != null && roomTemplateProbabilityProp.propertyType == SerializedPropertyType.Float) ? roomTemplateProbabilityProp.floatValue : 0f;
            float rectProb = Mathf.Max(0f, 1f - lProb - templateProb);
            if (lProb + templateProb > 1.0f) { EditorGUILayout.BeginHorizontal(EditorStyles.helpBox); if (warningIcon != null) GUILayout.Label(warningIcon, GUILayout.Width(24), GUILayout.Height(24)); EditorGUILayout.LabelField("Warning: L-Shape + Template probabilities exceed 100%.", new GUIStyle(EditorStyles.wordWrappedLabel) { normal = { textColor = new Color(1f, 0.7f, 0.2f) } }); EditorGUILayout.EndHorizontal(); }
            EditorGUILayout.Space(5); Rect distributionRect = EditorGUILayout.GetControlRect(false, 30); EditorGUI.DrawRect(distributionRect, FIELD_BG_COLOR); float fullWidth = distributionRect.width; float xPos = distributionRect.x;
            if (lProb > 0 && fullWidth > 0) { Rect lRect = new Rect(xPos, distributionRect.y, lProb * fullWidth, distributionRect.height); EditorGUI.DrawRect(lRect, new Color(0.9f, 0.6f, 0.3f)); if (lRect.width > 40) { GUI.Label(lRect, "L-Shapes", new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }); } xPos += lRect.width; }
            if (templateProb > 0 && fullWidth > 0) { Rect tRect = new Rect(xPos, distributionRect.y, templateProb * fullWidth, distributionRect.height); EditorGUI.DrawRect(tRect, new Color(0.3f, 0.6f, 0.9f)); if (tRect.width > 40) { GUI.Label(tRect, "Templates", new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }); } xPos += tRect.width; }
            if (rectProb > 0 && fullWidth > 0) { Rect rRect = new Rect(xPos, distributionRect.y, rectProb * fullWidth, distributionRect.height); EditorGUI.DrawRect(rRect, new Color(0.6f, 0.4f, 0.8f)); if (rRect.width > 40) { GUI.Label(rRect, "Rectangles", new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }); } }
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("L-Shape Probability", fieldHeaderStyle); DrawPropertyWithSlider(lShapeProbabilityProp, "", 0f, 1f);
            EditorGUILayout.LabelField("Template Probability", fieldHeaderStyle); DrawPropertyWithSlider(roomTemplateProbabilityProp, "", 0f, 1f);
            EditorGUILayout.LabelField($"Rectangle Probability: {rectProb:P0}", fieldHeaderStyle);
            EditorGUILayout.Space(8); EditorGUILayout.LabelField("L-Shape Settings", fieldHeaderStyle);
            float minLRatio = (minLLegRatioProp != null && minLLegRatioProp.propertyType == SerializedPropertyType.Float) ? minLLegRatioProp.floatValue : 0.2f;
            DrawPropertyWithSlider(minLLegRatioProp, "Min Leg Ratio", 0.2f, 0.8f); DrawPropertyWithSlider(maxLLegRatioProp, "Max Leg Ratio", minLRatio, 0.8f);
            float maxLRatio = (maxLLegRatioProp != null && maxLLegRatioProp.propertyType == SerializedPropertyType.Float) ? maxLLegRatioProp.floatValue : 0.8f;
            float legRatio = (minLRatio + maxLRatio) / 2f; Rect lShapeVisualizationRect = EditorGUILayout.GetControlRect(false, 100); EditorGUI.DrawRect(lShapeVisualizationRect, FIELD_BG_COLOR);
            float size = Mathf.Min(lShapeVisualizationRect.width, lShapeVisualizationRect.height) - 20; float padding = 10; float vertLegWidth = size * legRatio; float vertLegHeight = size; float horizLegWidth = size; float horizLegHeight = size * legRatio;
            if (size > 0) { Rect vertLeg = new Rect(lShapeVisualizationRect.x + padding, lShapeVisualizationRect.y + padding, vertLegWidth, vertLegHeight); Rect horizLeg = new Rect(lShapeVisualizationRect.x + padding, lShapeVisualizationRect.y + padding + (vertLegHeight - horizLegHeight), horizLegWidth, horizLegHeight); float pulse = Mathf.Sin(pulseAnimation) * 0.1f + 0.9f; Color lShapeColor = new Color(0.9f * pulse, 0.6f * pulse, 0.3f * pulse); EditorGUI.DrawRect(vertLeg, lShapeColor); EditorGUI.DrawRect(horizLeg, lShapeColor); }
            GUI.Label(lShapeVisualizationRect, $"Ratio: {legRatio:F2}", new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.LowerRight, normal = { textColor = Color.white } });
            EditorGUILayout.EndVertical(); EditorGUILayout.Space(10);
        }
        // Corridor Settings
        EditorGUILayout.LabelField("Corridor Settings", sectionHeaderStyle);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        DrawPropertyWithSlider(corridorWidthProp, "Corridor Width", 1, 5);
        Rect corridorVisRect = EditorGUILayout.GetControlRect(false, 50); EditorGUI.DrawRect(corridorVisRect, FIELD_BG_COLOR);
        int corridorWidth = 1; if (corridorWidthProp != null && corridorWidthProp.propertyType == SerializedPropertyType.Integer) corridorWidth = corridorWidthProp.intValue;
        float corridorHeight = corridorVisRect.height - 20; Rect corridorRect = new Rect(corridorVisRect.x + 10, corridorVisRect.y + 10, corridorVisRect.width - 20, corridorHeight);
        float corridorPulse = Mathf.Sin(pulseAnimation) * 0.1f + 0.9f; Color corridorColor = new Color(0.4f * corridorPulse, 0.6f * corridorPulse, 0.9f * corridorPulse);
        if (corridorRect.width > 0 && corridorRect.height > 0) { EditorGUI.DrawRect(corridorRect, corridorColor); }
        if (dimensionStyle != null) { GUI.Label(corridorRect, $"Width: {corridorWidth}", dimensionStyle); } else { GUI.Label(corridorRect, $"Width: {corridorWidth}", new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } }); }
        EditorGUILayout.EndVertical(); EditorGUILayout.Space(10);
        // Template Prefabs
        if (currentMode == GenerationMode.HybridProcedural || currentMode == GenerationMode.UserDefinedLayout)
        {
            EditorGUILayout.LabelField("Room Templates", sectionHeaderStyle); EditorGUILayout.BeginVertical(GUI.skin.box); EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (infoIcon != null) GUILayout.Label(infoIcon, GUILayout.Width(24), GUILayout.Height(24));
            EditorGUILayout.LabelField("Add prefabs used as room templates. Used randomly in Hybrid mode or assigned via RoomNode in User Defined mode.", new GUIStyle(EditorStyles.wordWrappedLabel) { normal = { textColor = TEXT_COLOR } });
            EditorGUILayout.EndHorizontal(); EditorGUILayout.Space(5); if (roomTemplatesList != null) roomTemplatesList.DoLayoutList(); EditorGUILayout.EndVertical();
        }
    }

    // DrawEntitiesTab method (with safe access checks)
    private void DrawEntitiesTab()
    {
        EditorGUILayout.Space(5); EditorGUILayout.LabelField("Tilemap References", sectionHeaderStyle); EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal(); if (EditorGUIUtility.IconContent("Tilemap Icon") != null) GUILayout.Label(EditorGUIUtility.IconContent("Tilemap Icon"), GUILayout.Width(20), GUILayout.Height(20)); EditorGUILayout.PropertyField(groundTilemapProp, new GUIContent("Ground Tilemap")); EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal(); if (EditorGUIUtility.IconContent("Tilemap Icon") != null) GUILayout.Label(EditorGUIUtility.IconContent("Tilemap Icon"), GUILayout.Width(20), GUILayout.Height(20)); EditorGUILayout.PropertyField(wallTilemapProp, new GUIContent("Wall Tilemap")); EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical(); EditorGUILayout.Space(10); EditorGUILayout.LabelField("Tile References", sectionHeaderStyle); EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal(); Rect floorColorRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(20)); EditorGUI.DrawRect(floorColorRect, new Color(0.2f, 0.8f, 0.3f)); EditorGUILayout.PropertyField(floorTileProp); EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal(); Rect wallColorRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(20)); EditorGUI.DrawRect(wallColorRect, new Color(0.8f, 0.4f, 0.3f)); EditorGUILayout.PropertyField(wallTileProp); EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical(); EditorGUILayout.Space(10); EditorGUILayout.LabelField("Player Settings", sectionHeaderStyle); EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal(); if (EditorGUIUtility.IconContent("d_AnimatorController Icon") != null) GUILayout.Label(EditorGUIUtility.IconContent("d_AnimatorController Icon"), GUILayout.Width(20), GUILayout.Height(20)); EditorGUILayout.PropertyField(playerPrefabProp); EditorGUILayout.EndHorizontal();
        if (playerPrefabProp.objectReferenceValue != null) { Rect previewRect = EditorGUILayout.GetControlRect(false, 40); EditorGUI.DrawRect(previewRect, FIELD_BG_COLOR); float radius = 10f; Rect playerRect = new Rect(previewRect.x + 20, previewRect.y + (previewRect.height - radius * 2) / 2, radius * 2, radius * 2); float pulse = Mathf.Sin(pulseAnimation) * 0.1f + 0.9f; Color playerColor = new Color(0.2f * pulse, 0.6f * pulse, 1f * pulse); EditorGUI.DrawRect(playerRect, playerColor); GUI.Label(previewRect, "Player will spawn at a valid room location", new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = TEXT_COLOR } }); }
        EditorGUILayout.EndVertical(); EditorGUILayout.Space(10); EditorGUILayout.LabelField("Enemy Settings", sectionHeaderStyle); EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal(); if (EditorGUIUtility.IconContent("d_PreMatCube") != null) GUILayout.Label(EditorGUIUtility.IconContent("d_PreMatCube"), GUILayout.Width(20), GUILayout.Height(20)); EditorGUILayout.PropertyField(enemyPrefabProp); EditorGUILayout.EndHorizontal();
        DrawPropertyWithSlider(enemiesPerRoomProp, "Enemies Per Room", 0, 10);
        int enemyCount = 0; if (enemiesPerRoomProp != null && enemiesPerRoomProp.propertyType == SerializedPropertyType.Integer) enemyCount = enemiesPerRoomProp.intValue;
        if (enemyPrefabProp.objectReferenceValue != null && enemyCount > 0) { Rect previewRect = EditorGUILayout.GetControlRect(false, 40); EditorGUI.DrawRect(previewRect, FIELD_BG_COLOR); int displayCount = Mathf.Min(enemyCount, 20); float iconSize = 16f; float totalWidth = displayCount * iconSize * 1.2f; float startX = previewRect.x + (previewRect.width - totalWidth) / 2; for (int i = 0; i < displayCount; i++) { Rect enemyRect = new Rect(startX + (i * iconSize * 1.2f), previewRect.y + (previewRect.height - iconSize) / 2, iconSize, iconSize); Color enemyColor = (i % 2 == 0) ? new Color(1f, 0.3f, 0.3f) : new Color(0.8f, 0.2f, 0.2f); EditorGUI.DrawRect(enemyRect, enemyColor); } }
        EditorGUILayout.EndVertical(); EditorGUILayout.Space(10); EditorGUILayout.LabelField("Decoration Settings", sectionHeaderStyle); EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal(); if (EditorGUIUtility.IconContent("d_TerrainInspector.TerrainToolSplat") != null) GUILayout.Label(EditorGUIUtility.IconContent("d_TerrainInspector.TerrainToolSplat"), GUILayout.Width(20), GUILayout.Height(20)); EditorGUILayout.PropertyField(decorationPrefabProp); EditorGUILayout.EndHorizontal();
        DrawPropertyWithSlider(decorationsPerRoomProp, "Decorations Per Room", 0, 15);
        int decorCount = 0; if (decorationsPerRoomProp != null && decorationsPerRoomProp.propertyType == SerializedPropertyType.Integer) decorCount = decorationsPerRoomProp.intValue;
        if (decorationPrefabProp.objectReferenceValue != null && decorCount > 0) { Rect previewRect = EditorGUILayout.GetControlRect(false, 40); EditorGUI.DrawRect(previewRect, FIELD_BG_COLOR); int displayCount = Mathf.Min(decorCount, 30); float iconSize = 12f; float totalWidth = displayCount * iconSize * 1.2f; float startX = previewRect.x + (previewRect.width - totalWidth) / 2; for (int i = 0; i < displayCount; i++) { Rect decorRect = new Rect(startX + (i * iconSize * 1.2f), previewRect.y + (previewRect.height - iconSize) / 2, iconSize, iconSize); Color decorColor = (i % 3 == 0) ? new Color(0.3f, 0.7f, 0.3f) : (i % 3 == 1) ? new Color(0.3f, 0.6f, 0.8f) : new Color(0.8f, 0.7f, 0.3f); EditorGUI.DrawRect(decorRect, decorColor); } }
        EditorGUILayout.EndVertical();
    }

    // Removed DrawPreviewTab

    // DrawActionControls method (Using .text for buttons - Recommended)
    private void DrawActionControls()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();
        Color defaultBgColor = GUI.backgroundColor;
        GUIStyle currentBigButtonStyle = bigButtonStyle ?? GUI.skin.button; // Fallback

        // Generate Button
        GUI.backgroundColor = GENERATE_COLOR;
        if (GUILayout.Button(generateIcon?.text ?? "Generate", currentBigButtonStyle))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            Undo.RecordObject(generator, "Generate Level");
            generator.GenerateLevel(); // Assumes this method exists on HybridLevelGenerator
            MarkSceneDirty(generator);
            ShowNotification(new GUIContent("Level Generated Successfully!"));
        }

        // Clear Button
        GUI.backgroundColor = CLEAR_COLOR;
        if (GUILayout.Button(clearIcon?.text ?? "Clear", currentBigButtonStyle))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            if (EditorUtility.DisplayDialog("Confirm Clear", "Clear generated level AND scene design nodes?", "Clear All", "Cancel"))
            {
                Undo.RecordObject(generator, "Clear Level");
                generator.ClearLevel(); // Assumes this method exists on HybridLevelGenerator
                MarkSceneDirty(generator);
                ShowNotification(new GUIContent("Level Cleared"));
            }
        }
        GUI.backgroundColor = defaultBgColor;
        EditorGUILayout.EndHorizontal();
    }

    // DrawQuickHelpTip method remains the same
    private void DrawQuickHelpTip() { if (quickHelpStep < 0 || quickHelpStep >= quickHelpMessages.Length) return; EditorGUILayout.Space(10); Rect tipRect = EditorGUILayout.GetControlRect(false, 60); EditorGUI.DrawRect(tipRect, new Color(0.2f, 0.2f, 0.22f, 0.95f)); float pulseValue = Mathf.Sin(pulseAnimation) * 0.5f + 0.5f; Color borderColor = new Color(1f, 0.9f, 0.2f, pulseValue); EditorGUI.DrawRect(new Rect(tipRect.x, tipRect.y, tipRect.width, 2), borderColor); EditorGUI.DrawRect(new Rect(tipRect.x, tipRect.y + tipRect.height - 2, tipRect.width, 2), borderColor); EditorGUI.DrawRect(new Rect(tipRect.x, tipRect.y, 2, tipRect.height), borderColor); EditorGUI.DrawRect(new Rect(tipRect.x + tipRect.width - 2, tipRect.y, 2, tipRect.height), borderColor); Rect iconRect = new Rect(tipRect.x + 10, tipRect.y + (tipRect.height - 32) / 2, 32, 32); if (EditorGUIUtility.IconContent("d_LightProbes Icon") != null) GUI.Label(iconRect, EditorGUIUtility.IconContent("d_LightProbes Icon")); Rect textRect = new Rect(tipRect.x + 50, tipRect.y + 5, tipRect.width - 130, tipRect.height - 10); if (richTextStyle != null) GUI.Label(textRect, $"TIP {quickHelpStep + 1}/{quickHelpMessages.Length}: {quickHelpMessages[quickHelpStep]}", richTextStyle); else GUI.Label(textRect, $"TIP {quickHelpStep + 1}/{quickHelpMessages.Length}: {quickHelpMessages[quickHelpStep]}"); Rect buttonRect = new Rect(tipRect.x + tipRect.width - 70, tipRect.y + (tipRect.height - 20) / 2, 60, 20); if (GUI.Button(buttonRect, quickHelpStep < quickHelpMessages.Length - 1 ? "Next ▶" : "Done ✓", EditorStyles.miniButton)) { quickHelpStep++; if (quickHelpStep >= quickHelpMessages.Length) { quickHelpStep = -1; } } }

    // DrawPropertyWithSlider methods remain the same
    private void DrawPropertyWithSlider(SerializedProperty property, string label, int minValue, int maxValue) { if (property.propertyType != SerializedPropertyType.Integer) { EditorGUILayout.LabelField($"Error: Property '{label}' is not an Integer."); return; } EditorGUILayout.BeginHorizontal(); property.intValue = Mathf.Clamp(property.intValue, minValue, maxValue); EditorGUILayout.IntSlider(property, minValue, maxValue, label); EditorGUILayout.EndHorizontal(); }
    private void DrawPropertyWithSlider(SerializedProperty property, string label, float minValue, float maxValue) { if (property.propertyType != SerializedPropertyType.Float) { EditorGUILayout.LabelField($"Error: Property '{label}' is not a Float."); return; } EditorGUILayout.BeginHorizontal(); property.floatValue = Mathf.Clamp(property.floatValue, minValue, maxValue); EditorGUILayout.Slider(property, minValue, maxValue, label); EditorGUILayout.EndHorizontal(); }

    // DrawBspVisualization method remains the same
    private void DrawBspVisualization(Rect rect, int iterations) { System.Random rand = new System.Random(42); List<Rect> partitions = new List<Rect> { rect }; for (int i = 0; i < iterations; i++) { List<Rect> newPartitions = new List<Rect>(); foreach (Rect partition in partitions) { bool splitHorizontal = partition.width > partition.height; if (Mathf.Approximately(partition.width, partition.height)) { splitHorizontal = rand.Next(2) == 0; } float ratio = 0.4f + (float)rand.NextDouble() * 0.2f; if (splitHorizontal) { float splitX = partition.x + (partition.width * ratio); Rect leftPartition = new Rect(partition.x, partition.y, splitX - partition.x, partition.height); Rect rightPartition = new Rect(splitX, partition.y, partition.width - (splitX - partition.x), partition.height); newPartitions.Add(leftPartition); newPartitions.Add(rightPartition); } else { float splitY = partition.y + (partition.height * ratio); Rect topPartition = new Rect(partition.x, partition.y, partition.width, splitY - partition.y); Rect bottomPartition = new Rect(partition.x, splitY, partition.width, partition.height - (splitY - partition.y)); newPartitions.Add(topPartition); newPartitions.Add(bottomPartition); } } partitions = newPartitions; } EditorGUI.DrawRect(rect, FIELD_BG_COLOR); foreach (Rect partition in partitions) { Rect borderRect = new Rect(partition.x, partition.y, partition.width, 1); EditorGUI.DrawRect(borderRect, new Color(0.6f, 0.6f, 0.6f, 0.8f)); borderRect = new Rect(partition.x, partition.y, 1, partition.height); EditorGUI.DrawRect(borderRect, new Color(0.6f, 0.6f, 0.6f, 0.8f)); borderRect = new Rect(partition.x + partition.width - 1, partition.y, 1, partition.height); EditorGUI.DrawRect(borderRect, new Color(0.6f, 0.6f, 0.6f, 0.8f)); borderRect = new Rect(partition.x, partition.y + partition.height - 1, partition.width, 1); EditorGUI.DrawRect(borderRect, new Color(0.6f, 0.6f, 0.6f, 0.8f)); float padding = Mathf.Clamp(Mathf.Min(partition.width, partition.height) * 0.1f, 2f, 10f); Rect roomRect = new Rect(partition.x + padding, partition.y + padding, partition.width - (padding * 2), partition.height - (padding * 2)); float pulse = Mathf.Sin(pulseAnimation + partition.x * 0.1f + partition.y * 0.1f) * 0.2f + 0.8f; Color roomColor = new Color(FIELD_ACCENT_COLOR.r * pulse, FIELD_ACCENT_COLOR.g * pulse, FIELD_ACCENT_COLOR.b * pulse, 0.8f); if (roomRect.width > 0 && roomRect.height > 0) EditorGUI.DrawRect(roomRect, roomColor); } }

    // Removed SetupPreviewCamera

    // MarkSceneDirty method remains the same
    private void MarkSceneDirty(HybridLevelGenerator generator) { if (!Application.isPlaying && generator != null && generator.gameObject != null) { try { if (generator.gameObject.scene.IsValid() && generator.gameObject.scene.isLoaded) { UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene); } } catch (Exception e) { Debug.LogWarning($"Could not mark scene dirty: {e.Message}"); } } }

    // ShowNotification method remains the same
    private void ShowNotification(GUIContent content) { Debug.Log($"Notification: {content.text}"); EditorWindow window = EditorWindow.focusedWindow; if (window != null) { window.ShowNotification(content); } }
}