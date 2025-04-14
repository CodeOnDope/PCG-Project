using UnityEngine;
using UnityEditor; // Required for Editor scripts
using System.Collections.Generic; // Required for List
using System; // Required for Exception class

// This attribute tells Unity to use this script to draw the Inspector
// for components of type HybridLevelGenerator.
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

    // --- Foldout States ---
    bool levelDimensionsFoldout = true;
    bool bspSettingsFoldout = true;
    bool hybridSettingsFoldout = true;
    bool commonSettingsFoldout = true;
    bool tileSettingsFoldout = true;
    bool randomnessSettingsFoldout = true;
    bool entitySettingsFoldout = true;

    // --- Style & Color ---
    // Define some colors for sections (adjust as needed)
    Color headerBgColor = new Color(0.6f, 0.65f, 0.7f, 1f); // Slightly darker grey for headers (not directly used on foldout)
    Color dimensionsBgColor = new Color(0.85f, 0.9f, 1f); // Light Blueish
    Color bspBgColor = new Color(0.85f, 1f, 0.85f); // Light Greenish
    Color hybridBgColor = new Color(1f, 0.9f, 0.85f); // Light Orangish
    Color commonBgColor = new Color(0.9f, 0.9f, 0.9f); // Light Grey

    GUIStyle foldoutHeaderStyle;
    bool stylesInitialized = false;


    private void OnEnable()
    {
        // Find all the properties we want to draw manually
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

        InitializeStyles(); // Initialize styles here
    }

    // Initialize styles - safe to call multiple times
    private void InitializeStyles()
    {
        if (stylesInitialized) return;

        // Create a richer foldout header style
        foldoutHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 13,
            // normal = { textColor = new Color(0.1f, 0.1f, 0.1f) } // Darker text for header
        };
        // You can add background textures here too if desired

        stylesInitialized = true;
    }


    public override void OnInspectorGUI()
    {
        // Ensure styles are initialized if OnEnable wasn't called properly
        if (!stylesInitialized) InitializeStyles();

        serializedObject.Update();
        HybridLevelGenerator generator = (HybridLevelGenerator)target;
        Color defaultBgColor = GUI.backgroundColor;

        // --- Generation Mode Selection ---
        EditorGUILayout.LabelField("Generation Control", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(generationModeProp);
        bool modeChanged = EditorGUI.EndChangeCheck();
        EditorGUILayout.Space(5);

        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;
        switch (currentMode)
        { /* Help text unchanged */
            case GenerationMode.FullyProcedural: EditorGUILayout.HelpBox("Generates level using BSP subdivision and places standard rectangular rooms randomly within partitions. Connects rooms using MST.", MessageType.Info); break;
            case GenerationMode.HybridProcedural: EditorGUILayout.HelpBox("Generates level using BSP subdivision but attempts to place Templates, L-Shapes, or Rectangles randomly within partitions. Connects rooms using MST.", MessageType.Info); break;
        }
        EditorGUILayout.Space(10);

        // --- Settings Sections ---

        levelDimensionsFoldout = EditorGUILayout.Foldout(levelDimensionsFoldout, "Level Dimensions", true, foldoutHeaderStyle);
        if (levelDimensionsFoldout)
        {
            GUI.backgroundColor = dimensionsBgColor; // Use defined color
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = defaultBgColor;
            EditorGUILayout.PropertyField(levelWidthProp);
            EditorGUILayout.PropertyField(levelHeightProp);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space(5);

        if (currentMode == GenerationMode.FullyProcedural || currentMode == GenerationMode.HybridProcedural)
        {
            bspSettingsFoldout = EditorGUILayout.Foldout(bspSettingsFoldout, "BSP Settings", true, foldoutHeaderStyle);
            if (bspSettingsFoldout)
            {
                GUI.backgroundColor = bspBgColor; // Use defined color
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = defaultBgColor;
                EditorGUILayout.PropertyField(minRoomSizeProp, new GUIContent("Min Room Size (BSP)"));
                EditorGUILayout.PropertyField(maxIterationsProp, new GUIContent("BSP Max Iterations"));
                EditorGUILayout.PropertyField(roomPaddingProp, new GUIContent("BSP Room Padding"));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);
        }

        if (currentMode == GenerationMode.HybridProcedural)
        {
            hybridSettingsFoldout = EditorGUILayout.Foldout(hybridSettingsFoldout, "Hybrid Room Settings", true, foldoutHeaderStyle);
            if (hybridSettingsFoldout)
            {
                GUI.backgroundColor = hybridBgColor; // Use defined color
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = defaultBgColor;
                EditorGUILayout.LabelField("Shape Probabilities", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(lShapeProbabilityProp);
                EditorGUILayout.PropertyField(roomTemplateProbabilityProp);
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("L-Shape Ratios", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(minLLegRatioProp);
                EditorGUILayout.PropertyField(maxLLegRatioProp);
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Templates", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(roomTemplatePrefabsProp, true);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);
        }

        commonSettingsFoldout = EditorGUILayout.Foldout(commonSettingsFoldout, "Common Settings", true, foldoutHeaderStyle);
        if (commonSettingsFoldout)
        {
            GUI.backgroundColor = commonBgColor; // Use defined color
            EditorGUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = defaultBgColor;

            EditorGUILayout.PropertyField(corridorWidthProp);
            EditorGUILayout.Space(10);

            tileSettingsFoldout = EditorGUILayout.Foldout(tileSettingsFoldout, "Tiles & Tilemaps", true);
            if (tileSettingsFoldout) { /* ... unchanged ... */ EditorGUI.indentLevel++; EditorGUILayout.PropertyField(groundTilemapProp, EditorGUIUtility.IconContent("Tilemap Icon")); EditorGUILayout.PropertyField(wallTilemapProp, EditorGUIUtility.IconContent("Tilemap Icon")); EditorGUILayout.PropertyField(floorTileProp); EditorGUILayout.PropertyField(wallTileProp); EditorGUI.indentLevel--; }
            EditorGUILayout.Space(5);

            randomnessSettingsFoldout = EditorGUILayout.Foldout(randomnessSettingsFoldout, "Randomness", true);
            if (randomnessSettingsFoldout) { /* ... unchanged ... */ EditorGUI.indentLevel++; EditorGUILayout.PropertyField(useRandomSeedProp); if (!useRandomSeedProp.boolValue) { EditorGUILayout.PropertyField(seedProp); } EditorGUI.indentLevel--; }
            EditorGUILayout.Space(5);

            entitySettingsFoldout = EditorGUILayout.Foldout(entitySettingsFoldout, "Entities & Decorations", true);
            if (entitySettingsFoldout) { /* ... unchanged ... */ EditorGUI.indentLevel++; EditorGUILayout.PropertyField(playerPrefabProp); EditorGUILayout.PropertyField(enemyPrefabProp); EditorGUILayout.PropertyField(decorationPrefabProp); EditorGUILayout.PropertyField(enemiesPerRoomProp); EditorGUILayout.PropertyField(decorationsPerRoomProp); EditorGUI.indentLevel--; }

            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.Space(15);


        // --- Draw Buttons ---
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 30, fontStyle = FontStyle.Bold };

        GUI.backgroundColor = new Color(0.7f, 1.0f, 0.7f);
        if (GUILayout.Button("Generate Level", buttonStyle)) { /* ... unchanged ... */ Undo.RecordObject(generator, "Generate Level"); generator.GenerateLevel(); MarkSceneDirty(generator); }
        GUI.backgroundColor = defaultBgColor;

        EditorGUILayout.Space(5);

        GUI.backgroundColor = new Color(1.0f, 0.6f, 0.6f);
        if (GUILayout.Button("Clear Level", buttonStyle)) { /* ... unchanged ... */ if (EditorUtility.DisplayDialog("Confirm Clear", "Clear generated level (tilemaps and spawned objects)?", "Clear", "Cancel")) { Undo.RecordObject(generator, "Clear Level"); generator.ClearLevel(); MarkSceneDirty(generator); } }
        GUI.backgroundColor = defaultBgColor;

        serializedObject.ApplyModifiedProperties();
    }

    // Helper to mark scene dirty
    private void MarkSceneDirty(HybridLevelGenerator generator)
    { /* ... unchanged ... */
        if (!Application.isPlaying && generator != null && generator.gameObject != null) { try { if (generator.gameObject.scene.IsValid()) { UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene); } } catch (Exception e) { Debug.LogWarning($"Could not mark scene dirty: {e.Message}"); } }
    }
}
