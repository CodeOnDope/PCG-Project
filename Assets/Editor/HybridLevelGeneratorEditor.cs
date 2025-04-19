using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.Tilemaps; // Needed for Tilemap checks

// Assumes GenerationMode enum is defined in LevelGenerationTypes.cs or similar
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
    SerializedProperty defaultSceneNodeSizeProp;

    // --- UI States ---
    private bool showFeedback = false;
    private string feedbackMessage = "";
    private MessageType feedbackType = MessageType.Info;
    private double feedbackExpireTime;

    // Section foldouts
    private bool showLevelDimensions = true;
    private bool showBspSettings = true;
    private bool showHybridSettings = true;
    private bool showCorridorSettings = true;
    private bool showTilemapSettings = true;
    private bool showEntitySettings = true;
    private bool showHelp = false;

    // --- Styles & Content Cache ---
    private GUIStyle headerStyle;
    private GUIStyle foldoutHeaderStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle generateButtonStyle;
    private GUIStyle clearButtonStyle;
    private Color headerColor;
    private Color accentColor;
    private Color generateButtonColor; // Store actual color
    private Color clearButtonColor;    // Store actual color
    private readonly Color[] modeColors = new Color[Enum.GetNames(typeof(GenerationMode)).Length];
    private GUIContent[] modeButtonContents;

    private const float FOLDOUT_ANIM_SPEED = 3.0f;
    private const float FEEDBACK_DURATION = 3.5f;
    private float pulseTime = 0f;
    private Dictionary<string, float> foldoutAnimValues = new Dictionary<string, float>();
    private bool stylesInitialized = false;


    private void InitializeStylesAndColors()
    {
        if (stylesInitialized) return;

        headerColor = EditorGUIUtility.isProSkin ? new Color(0.18f, 0.22f, 0.25f) : new Color(0.8f, 0.82f, 0.85f);
        accentColor = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.6f, 1f) : new Color(0.2f, 0.5f, 0.9f);
        modeColors[0] = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.6f, 0.9f, 0.8f) : new Color(0.5f, 0.7f, 1.0f, 0.8f);
        modeColors[1] = EditorGUIUtility.isProSkin ? new Color(0.9f, 0.6f, 0.3f, 0.8f) : new Color(1.0f, 0.7f, 0.4f, 0.8f);
        modeColors[2] = EditorGUIUtility.isProSkin ? new Color(0.4f, 0.8f, 0.5f, 0.8f) : new Color(0.5f, 0.9f, 0.6f, 0.8f);
        generateButtonColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.7f, 0.5f) : new Color(0.3f, 0.8f, 0.6f);
        clearButtonColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.9f, 0.4f, 0.4f);

        headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.8f, 0.85f, 0.9f) : new Color(0.1f, 0.1f, 0.1f) } };
        foldoutHeaderStyle = new GUIStyle(EditorStyles.foldoutHeader) { fixedHeight = 22, fontSize = 12, fontStyle = FontStyle.Bold, padding = new RectOffset(15, 5, 3, 3) };
        subHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11, margin = new RectOffset(0, 0, 5, 2), normal = { textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.35f, 0.35f, 0.35f) } };
        generateButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 40, alignment = TextAnchor.MiddleCenter };
        clearButtonStyle = new GUIStyle(generateButtonStyle);

        foldoutAnimValues["dimensions"] = showLevelDimensions ? 1f : 0f;
        foldoutAnimValues["bsp"] = showBspSettings ? 1f : 0f;
        foldoutAnimValues["hybrid"] = showHybridSettings ? 1f : 0f;
        foldoutAnimValues["corridor"] = showCorridorSettings ? 1f : 0f;
        foldoutAnimValues["tilemap"] = showTilemapSettings ? 1f : 0f;
        foldoutAnimValues["entity"] = showEntitySettings ? 1f : 0f;

        string[] modeNames = Enum.GetNames(typeof(GenerationMode));
        modeButtonContents = new GUIContent[modeNames.Length];
        for (int i = 0; i < modeNames.Length; i++)
        {
            modeButtonContents[i] = new GUIContent(ObjectNames.NicifyVariableName(modeNames[i]), GetModeTooltip((GenerationMode)i));
        }

        stylesInitialized = true;
    }

    private Texture2D MakeColorTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        tex.SetPixel(0, 0, color); tex.Apply(); return tex;
    }

    private string GetModeTooltip(GenerationMode mode)
    { /* (Same as before) */
        switch (mode)
        {
            case GenerationMode.FullyProcedural: return "Generates level using BSP partitions, rectangular rooms, and MST corridors.";
            case GenerationMode.HybridProcedural: return "Generates level using BSP, mixing Rectangles, L-Shapes, and Room Templates.";
            case GenerationMode.UserDefinedLayout: return "Generates level based on RoomNode components placed manually in the scene via the Visual Level Designer.";
            default: return "";
        }
    }

    private void OnEnable()
    {
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
        defaultSceneNodeSizeProp = serializedObject.FindProperty("defaultSceneNodeSize");

        stylesInitialized = false; // Reinitialize styles

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable() { EditorApplication.update -= OnEditorUpdate; }

    private void OnEditorUpdate()
    { /* (Same as before) */
        bool needsRepaint = false;
        pulseTime = (float)(EditorApplication.timeSinceStartup * 2.5) % (2f * Mathf.PI);
        UpdateFoldoutAnimation("dimensions", showLevelDimensions, ref needsRepaint);
        UpdateFoldoutAnimation("bsp", showBspSettings, ref needsRepaint);
        UpdateFoldoutAnimation("hybrid", showHybridSettings, ref needsRepaint);
        UpdateFoldoutAnimation("corridor", showCorridorSettings, ref needsRepaint);
        UpdateFoldoutAnimation("tilemap", showTilemapSettings, ref needsRepaint);
        UpdateFoldoutAnimation("entity", showEntitySettings, ref needsRepaint);
        if (showFeedback && EditorApplication.timeSinceStartup > feedbackExpireTime) { showFeedback = false; needsRepaint = true; }
        if (needsRepaint) Repaint();
    }
    private void UpdateFoldoutAnimation(string key, bool targetState, ref bool needsRepaint)
    { /* (Same as before) */
        float targetValue = targetState ? 1f : 0f; if (!foldoutAnimValues.ContainsKey(key)) { foldoutAnimValues[key] = targetValue; }
        float currentValue = foldoutAnimValues[key]; if (!Mathf.Approximately(currentValue, targetValue)) { foldoutAnimValues[key] = Mathf.MoveTowards(currentValue, targetValue, Time.deltaTime * FOLDOUT_ANIM_SPEED); needsRepaint = true; }
    }
    private void ShowFeedback(string message, MessageType type = MessageType.Info, float duration = FEEDBACK_DURATION)
    { /* (Same as before) */
        feedbackMessage = message; feedbackType = type; showFeedback = true; feedbackExpireTime = EditorApplication.timeSinceStartup + duration; Repaint();
    }
    private bool AreCoreComponentsAssigned()
    { /* (Same as before) */
        return groundTilemapProp.objectReferenceValue != null && wallTilemapProp.objectReferenceValue != null && floorTileProp.objectReferenceValue != null && wallTileProp.objectReferenceValue != null;
    }

    public override void OnInspectorGUI()
    {
        InitializeStylesAndColors();
        serializedObject.Update();

        Color originalBgColor = GUI.backgroundColor;
        Color originalContentColor = GUI.contentColor;
        GUI.contentColor = EditorGUIUtility.isProSkin ? Color.white * 0.9f : Color.black * 0.9f; // Slightly adjust default text color

        EditorGUI.BeginChangeCheck();

        DrawHeader();
        DrawGenerationModeSelector();

        bool coreComponentsOk = AreCoreComponentsAssigned();
        if (!coreComponentsOk)
        {
            EditorGUILayout.HelpBox("Essential Tilemaps or Tiles are missing! Assign Ground/Wall Tilemaps and Floor/Wall Tiles.", MessageType.Error);
        }

        // --- Main Settings Area ---
        EditorGUILayout.BeginVertical(GUI.skin.box);
        DrawFoldoutSection("dimensions", "Level Dimensions & Seed", ref showLevelDimensions, DrawLevelDimensionsSection);
        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;
        EditorGUI.BeginDisabledGroup(currentMode == GenerationMode.UserDefinedLayout);
        if (currentMode == GenerationMode.FullyProcedural || currentMode == GenerationMode.HybridProcedural) { DrawFoldoutSection("bsp", "BSP Algorithm Settings", ref showBspSettings, DrawBspSection); }
        if (currentMode == GenerationMode.HybridProcedural) { DrawFoldoutSection("hybrid", "Hybrid Room Settings", ref showHybridSettings, DrawHybridSection); }
        EditorGUI.EndDisabledGroup();
        DrawFoldoutSection("corridor", "Corridor Settings", ref showCorridorSettings, DrawCorridorSection);
        DrawFoldoutSection("tilemap", "Tiles & Tilemaps", ref showTilemapSettings, DrawTilemapSection);
        DrawFoldoutSection("entity", "Entities & Decorations", ref showEntitySettings, DrawEntitySection);
        EditorGUILayout.EndVertical();
        // --- End Main Settings Area ---

        // --- Feedback Area ---
        if (showFeedback)
        {
            EditorGUILayout.Space(5); EditorGUILayout.HelpBox(feedbackMessage, feedbackType);
            Rect fbRect = GUILayoutUtility.GetLastRect(); Color cc = GUI.contentColor; GUI.contentColor = Color.grey;
            if (GUI.Button(new Rect(fbRect.xMax - 18, fbRect.y + 1, 16, 16), "x", EditorStyles.miniButton)) { showFeedback = false; }
            GUI.contentColor = cc;
        }
        // --- End Feedback Area ---

        // --- Action Buttons Area ---
        EditorGUILayout.Space(15);
        EditorGUI.BeginDisabledGroup(!coreComponentsOk); // Disable if core refs missing
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = generateButtonColor;
        if (GUILayout.Button(new GUIContent(" Generate Level", EditorGUIUtility.IconContent("d_PlayButton On").image, "Generate the level using current settings"), generateButtonStyle))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target; Undo.RecordObject(generator, "Generate Level Action");
            bool skipClearFlag = (generator.generationMode == GenerationMode.UserDefinedLayout); generator.GenerateLevel(skipClearFlag);
            MarkSceneDirty(generator); ShowFeedback("Level Generation Triggered!", MessageType.Info);
        }

        GUI.backgroundColor = clearButtonColor;
        if (GUILayout.Button(new GUIContent(" Clear Level", EditorGUIUtility.IconContent("d_TreeEditor.Trash").image, "Clear generated tiles, entities, AND scene RoomNodes"), clearButtonStyle))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            if (EditorUtility.DisplayDialog("Confirm Clear", "Clear generated level content AND scene design nodes (LevelDesignRoot)?", "Clear All", "Cancel"))
            {
                Undo.RecordObject(generator, "Clear Level"); generator.ClearLevel(); MarkSceneDirty(generator); ShowFeedback("Level Cleared", MessageType.Info);
            }
        }

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = originalBgColor; EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space(10);
        // --- End Action Buttons Area ---

        // --- Simple Help Toggle ---
        showHelp = EditorGUILayout.ToggleLeft(" Show Basic Help", showHelp);
        if (showHelp) { EditorGUILayout.HelpBox("Workflow:\n1. Select Mode.\n2. Configure Dimensions & Settings.\n3. Assign Tilemaps & Tiles.\n4. Assign Entities (Optional).\n5. Generate Level!\n(Use Visual Designer for User Defined Layout setup).", MessageType.None); }
        // --- End Simple Help Toggle ---

        if (EditorGUI.EndChangeCheck()) { serializedObject.ApplyModifiedProperties(); }

        GUI.backgroundColor = originalBgColor; GUI.contentColor = originalContentColor; // Restore defaults
    }

    // --- Section Drawing Helpers ---
    private void DrawHeader() { /* (Same as before) */ EditorGUILayout.Space(5); Rect r = GUILayoutUtility.GetRect(GUIContent.none, headerStyle, GUILayout.Height(30)); EditorGUI.DrawRect(r, headerColor); GUI.Label(r, "Hybrid Procedural Level Generator", headerStyle); EditorGUILayout.Space(10); }

    private void DrawGenerationModeSelector()
    { /* (Same as before, uses cached GUIContent) */
        EditorGUILayout.LabelField("Generation Mode", EditorStyles.boldLabel);
        int currentModeIndex = generationModeProp.enumValueIndex; float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(pulseTime));
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < modeButtonContents.Length; i++)
        {
            bool isSelected = currentModeIndex == i; GUIStyle btnStyle = new GUIStyle(GUI.skin.button); btnStyle.fixedHeight = 30;
            Color normalBg = modeColors[i] * (EditorGUIUtility.isProSkin ? 0.7f : 1.0f); Color selBg = modeColors[i] * (EditorGUIUtility.isProSkin ? 1.2f : 0.8f); selBg.a = 1.0f;
            Color txtCol = EditorGUIUtility.isProSkin ? Color.white * 0.8f : Color.black * 0.8f; Color selTxtCol = EditorGUIUtility.isProSkin ? Color.white : Color.black;
            if (isSelected) { GUI.backgroundColor = Color.Lerp(selBg, selBg * 1.15f, pulse); btnStyle.normal.textColor = selTxtCol; btnStyle.fontStyle = FontStyle.Bold; } else { GUI.backgroundColor = normalBg; btnStyle.normal.textColor = txtCol; }
            if (GUILayout.Button(modeButtonContents[i], btnStyle)) { if (generationModeProp.enumValueIndex != i) { generationModeProp.enumValueIndex = i; ShowFeedback($"{modeButtonContents[i].text} mode selected.", MessageType.Info); } GUI.FocusControl(null); }
        }
        GUI.backgroundColor = Color.white; EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(GetModeHelpText((GenerationMode)currentModeIndex), MessageType.Info);
        if ((GenerationMode)currentModeIndex == GenerationMode.UserDefinedLayout)
        {
            EditorGUILayout.Space(5); Color obg = GUI.backgroundColor; GUI.backgroundColor = accentColor * (EditorGUIUtility.isProSkin ? 1.0f : 1.3f);
            if (GUILayout.Button(new GUIContent(" Open Visual Level Designer", EditorGUIUtility.IconContent("d_EditCollider").image), GUILayout.Height(30)))
            {
                var win = EditorWindow.GetWindow<VisualLevelDesignEditor>("Visual Level Designer"); win.Show(); win.Focus(); ShowFeedback("Visual Level Designer opened.", MessageType.Info);
            }
            GUI.backgroundColor = obg;
        }
        EditorGUILayout.Space(10);
    }

    // Foldout helper using FadeGroup
    private void DrawFoldoutSection(string key, string title, ref bool foldout, Action drawContent)
    { /* (Same as before) */
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool newState = EditorGUILayout.Foldout(foldout, title, true, foldoutHeaderStyle); if (newState != foldout) { foldout = newState; }
        if (foldoutAnimValues.ContainsKey(key)) { if (EditorGUILayout.BeginFadeGroup(foldoutAnimValues[key])) { EditorGUI.indentLevel++; EditorGUILayout.Space(5); if (drawContent != null) { drawContent(); } EditorGUILayout.Space(5); EditorGUI.indentLevel--; } EditorGUILayout.EndFadeGroup(); }
        EditorGUILayout.EndVertical(); EditorGUILayout.Space(3);
    }

    // --- Specific Section Drawing Methods ---
    private void DrawLevelDimensionsSection()
    { /* (Same as before, uses PropertyField) */
        EditorGUILayout.PropertyField(levelWidthProp, new GUIContent("Level Width", "Max grid width."));
        EditorGUILayout.PropertyField(levelHeightProp, new GUIContent("Level Height", "Max grid height."));
        EditorGUILayout.Space(8); EditorGUILayout.LabelField("Random Seed", subHeaderStyle);
        EditorGUILayout.PropertyField(useRandomSeedProp, new GUIContent("Use Random Seed", "Use time-based seed?"));
        EditorGUI.BeginDisabledGroup(useRandomSeedProp.boolValue); EditorGUILayout.BeginHorizontal(); EditorGUILayout.PropertyField(seedProp, new GUIContent("Seed Value", "Manual seed.")); if (GUILayout.Button("New", EditorStyles.miniButton, GUILayout.Width(50))) { seedProp.intValue = UnityEngine.Random.Range(1, 999999); ShowFeedback($"New seed: {seedProp.intValue}", MessageType.None, 2.0f); }
        EditorGUILayout.EndHorizontal(); EditorGUI.EndDisabledGroup();
    }
    private void DrawBspSection()
    { /* (Same as before, uses PropertyField) */
        EditorGUILayout.PropertyField(minRoomSizeProp, new GUIContent("Min Room Size", "Min width/height for BSP leaves & Rects."));
        EditorGUILayout.PropertyField(maxIterationsProp, new GUIContent("BSP Iterations", "Number of BSP splits."));
        EditorGUILayout.PropertyField(roomPaddingProp, new GUIContent("Room Padding", "Empty cells between procedural rooms."));
    }
    private void DrawHybridSection()
    { /* (Same as before, uses PropertyField/Slider where appropriate) */
        EditorGUILayout.LabelField("Procedural Room Chances", subHeaderStyle);
        EditorGUILayout.PropertyField(lShapeProbabilityProp, new GUIContent("L-Shape Chance", "Chance (0-1) for proc. room = L-Shape."));
        EditorGUILayout.PropertyField(roomTemplateProbabilityProp, new GUIContent("Template Chance", "Chance (0-1) for proc. room = Template."));
        if (lShapeProbabilityProp.floatValue + roomTemplateProbabilityProp.floatValue > 1.01f) { EditorGUILayout.HelpBox("Probabilities exceed 100%.", MessageType.Warning); }
        EditorGUILayout.Space(5); EditorGUILayout.LabelField("L-Shape Leg Ratios", subHeaderStyle);
        minLLegRatioProp.floatValue = EditorGUILayout.Slider(new GUIContent("Min Leg Ratio", "Min size ratio of smaller leg."), minLLegRatioProp.floatValue, 0.2f, 0.8f);
        maxLLegRatioProp.floatValue = EditorGUILayout.Slider(new GUIContent("Max Leg Ratio", "Max size ratio of smaller leg."), maxLLegRatioProp.floatValue, minLLegRatioProp.floatValue, 0.8f);
        EditorGUILayout.Space(8); EditorGUILayout.LabelField("Room Templates List", subHeaderStyle);
        EditorGUILayout.PropertyField(roomTemplatePrefabsProp, true); EditorGUILayout.HelpBox("Assign Room Template Prefabs (must contain Tilemap).", MessageType.None);
        EditorGUILayout.Space(5); EditorGUILayout.LabelField("User Defined Node Default", subHeaderStyle);
        EditorGUILayout.PropertyField(defaultSceneNodeSizeProp, new GUIContent("Default Node Size", "Size for UserDefined nodes if size is zero."));
    }
    private void DrawCorridorSection()
    { /* (Same as before, uses PropertyField) */
        EditorGUILayout.PropertyField(corridorWidthProp, new GUIContent("Corridor Width", "Width of corridors (in tiles)."));
    }
    private void DrawTilemapSection()
    { /* (Same as before, uses PropertyField) */
        EditorGUILayout.LabelField("Required Tilemaps", subHeaderStyle);
        EditorGUILayout.PropertyField(groundTilemapProp, new GUIContent("Ground Tilemap"));
        EditorGUILayout.PropertyField(wallTilemapProp, new GUIContent("Wall Tilemap"));
        EditorGUILayout.Space(8); EditorGUILayout.LabelField("Required Tiles", subHeaderStyle);
        EditorGUILayout.PropertyField(floorTileProp, new GUIContent("Floor Tile"));
        EditorGUILayout.PropertyField(wallTileProp, new GUIContent("Wall Tile"));
    }
    private void DrawEntitySection()
    { /* (Same as before, uses PropertyField/IntSlider) */
        EditorGUILayout.LabelField("Player", subHeaderStyle); EditorGUILayout.PropertyField(playerPrefabProp, new GUIContent("Player Prefab"));
        EditorGUILayout.Space(8); EditorGUILayout.LabelField("Enemies", subHeaderStyle); EditorGUILayout.PropertyField(enemyPrefabProp, new GUIContent("Enemy Prefab")); EditorGUILayout.PropertyField(enemiesPerRoomProp, new GUIContent("Max Enemies/Room")); // Using PropertyField for Range attribute
        EditorGUILayout.Space(8); EditorGUILayout.LabelField("Decorations", subHeaderStyle); EditorGUILayout.PropertyField(decorationPrefabProp, new GUIContent("Decoration Prefab")); EditorGUILayout.PropertyField(decorationsPerRoomProp, new GUIContent("Max Decors/Room"));
    } // Using PropertyField for Range attribute

    // --- Utility ---
    private void MarkSceneDirty(HybridLevelGenerator generator)
    { /* (Same as before) */
        if (!Application.isPlaying && generator != null && generator.gameObject != null) { try { if (generator.gameObject.scene != null && generator.gameObject.scene.IsValid() && generator.gameObject.scene.isLoaded) { UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene); } } catch (Exception e) { Debug.LogWarning($"Could not mark scene dirty: {e.Message}"); } }
    }
    private string GetModeHelpText(GenerationMode mode)
    {
        switch (mode)
        {
            case GenerationMode.FullyProcedural:
                return "BSP partitions + Random Rect rooms + MST corridors.\nGood for classic roguelike dungeons.";
            case GenerationMode.HybridProcedural:
                return "BSP partitions + mix of Rects, L-Shapes, and Room Templates + MST corridors.\nOffers more variety.";
            case GenerationMode.UserDefinedLayout:
                return "Generates layout based on RoomNode components placed in the scene.\nRequires setup using the 'Open Visual Level Designer' window.";
            default:
                return "Unknown Generation Mode Selected.";
        }
    }

} // --- End of HybridLevelGeneratorEditor Class ---