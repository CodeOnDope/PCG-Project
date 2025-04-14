using UnityEngine;
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
}