using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO; // Needed for Tilemap checks

// ██████╗  ██████╗  ██████╗    ██╗     ███████╗██╗   ██╗███████╗██╗         ██████╗ ███████╗███╗   ██╗
// ██╔══██╗██╔════╝ ██╔════╝    ██║     ██╔════╝██║   ██║██╔════╝██║        ██╔════╝ ██╔════╝████╗  ██║
// ██████╔╝██║      ██║  ███╗   ██║     █████╗  ██║   ██║█████╗  ██║        ██║  ███╗█████╗  ██╔██╗ ██║
// ██╔═══╝ ██║      ██║   ██║   ██║     ██╔══╝  ╚██╗ ██╔╝██╔══╝  ██║        ██║   ██║██╔══╝  ██║╚██╗██║
// ██║     ╚██████╗ ╚██████╔╝   ███████╗███████╗ ╚████╔╝ ███████╗███████╗   ╚██████╔╝███████╗██║ ╚████║
// ╚═╝      ╚═════╝  ╚═════╝    ╚══════╝╚══════╝  ╚═══╝  ╚══════╝╚══════╝    ╚═════╝ ╚══════╝╚═╝  ╚═══╝
//
// PCG Level Generator for Unity
// Copyright (c) 2025 Dineshkumar & Kamalanathan
// Version: 1.0.0


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
    SerializedProperty floorTileVariantsProp;
    SerializedProperty wallTileVariantsProp;
    SerializedProperty variantTileChanceProp;
    SerializedProperty seedProp;
    SerializedProperty useRandomSeedProp;
    SerializedProperty playerPrefabProp;
    SerializedProperty enemyPrefabProp;
    SerializedProperty decorationPrefabProp;
    SerializedProperty enemiesPerRoomProp;
    SerializedProperty decorationsPerRoomProp;
    SerializedProperty defaultSceneNodeSizeProp;

    // Updated directional wall tile properties - matched to sprite numbers
    SerializedProperty useDirectionalWallsProp;
    // Basic Wall Directions (1-4)
    SerializedProperty wallTileBottomProp; // Sprite #1
    SerializedProperty wallTileTopProp;    // Sprite #2
    SerializedProperty wallTileRightProp;  // Sprite #3
    SerializedProperty wallTileLeftProp;   // Sprite #4

    // Inner corner tiles (5-8)
    SerializedProperty wallTileInnerTopLeftProp;     // Sprite #5
    SerializedProperty wallTileInnerTopRightProp;    // Sprite #6
    SerializedProperty wallTileInnerBottomLeftProp;  // Sprite #7
    SerializedProperty wallTileInnerBottomRightProp; // Sprite #8

    // Outer corner tiles (9-12)
    SerializedProperty wallTileOuterTopLeftProp;     // Sprite #9
    SerializedProperty wallTileOuterTopRightProp;    // Sprite #10
    SerializedProperty wallTileOuterBottomLeftProp;  // Sprite #11
    SerializedProperty wallTileOuterBottomRightProp; // Sprite #12

    // --- UI States ---
    private bool showFeedback = false;
    private string feedbackMessage = "";
    private MessageType feedbackType = MessageType.Info;
    private double feedbackExpireTime;

    // For asset initialization state tracking
    private bool hasInitializedAssets = false;

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
    {
        switch (mode)
        {
            case GenerationMode.FullyProcedural: return "Generates level using BSP partitions, rectangular rooms, and MST corridors.";
            case GenerationMode.HybridProcedural: return "Generates level using BSP, mixing Rectangles, L-Shapes, and Room Templates + MST corridors.\nOffers more variety.";
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

        // Update these property findings for directional wall tiles with rotation
        useDirectionalWallsProp = serializedObject.FindProperty("useDirectionalWalls");

        // Basic Wall Directions - matching sprite numbers 1-4
        wallTileBottomProp = serializedObject.FindProperty("wallTileBottom"); // Sprite #1
        wallTileTopProp = serializedObject.FindProperty("wallTileTop");       // Sprite #2
        wallTileRightProp = serializedObject.FindProperty("wallTileRight");   // Sprite #3
        wallTileLeftProp = serializedObject.FindProperty("wallTileLeft");     // Sprite #4

        // Inner corner tiles - matching sprite numbers 5-8
        wallTileInnerTopLeftProp = serializedObject.FindProperty("wallTileInnerTopLeft");         // Sprite #5
        wallTileInnerTopRightProp = serializedObject.FindProperty("wallTileInnerTopRight");       // Sprite #6
        wallTileInnerBottomLeftProp = serializedObject.FindProperty("wallTileInnerBottomLeft");   // Sprite #7
        wallTileInnerBottomRightProp = serializedObject.FindProperty("wallTileInnerBottomRight"); // Sprite #8

        // Outer corner tiles - matching sprite numbers 9-12
        wallTileOuterTopLeftProp = serializedObject.FindProperty("wallTileOuterTopLeft");         // Sprite #9
        wallTileOuterTopRightProp = serializedObject.FindProperty("wallTileOuterTopRight");       // Sprite #10
        wallTileOuterBottomLeftProp = serializedObject.FindProperty("wallTileOuterBottomLeft");   // Sprite #11
        wallTileOuterBottomRightProp = serializedObject.FindProperty("wallTileOuterBottomRight"); // Sprite #12

        // Add the new tile variant properties
        floorTileVariantsProp = serializedObject.FindProperty("floorTileVariants");
        wallTileVariantsProp = serializedObject.FindProperty("wallTileVariants");
        variantTileChanceProp = serializedObject.FindProperty("variantTileChance");

        stylesInitialized = false; // Reinitialize styles

        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable() { EditorApplication.update -= OnEditorUpdate; }

    private void OnEditorUpdate()
    {
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
    {
        float targetValue = targetState ? 1f : 0f;
        if (!foldoutAnimValues.ContainsKey(key))
        {
            foldoutAnimValues[key] = targetValue;
        }
        float currentValue = foldoutAnimValues[key];
        if (!Mathf.Approximately(currentValue, targetValue))
        {
            foldoutAnimValues[key] = Mathf.MoveTowards(currentValue, targetValue, Time.deltaTime * FOLDOUT_ANIM_SPEED);
            needsRepaint = true;
        }
    }

    private void ShowFeedback(string message, MessageType type = MessageType.Info, float duration = FEEDBACK_DURATION)
    {
        feedbackMessage = message;
        feedbackType = type;
        showFeedback = true;
        feedbackExpireTime = EditorApplication.timeSinceStartup + duration;
        Repaint();
    }

    private bool AreCoreComponentsAssigned()
    {
        return groundTilemapProp.objectReferenceValue != null &&
               wallTilemapProp.objectReferenceValue != null &&
               floorTileProp.objectReferenceValue != null &&
               wallTileProp.objectReferenceValue != null;
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
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(feedbackMessage, feedbackType);
            Rect fbRect = GUILayoutUtility.GetLastRect();
            Color cc = GUI.contentColor;
            GUI.contentColor = Color.grey;
            if (GUI.Button(new Rect(fbRect.xMax - 18, fbRect.y + 1, 16, 16), "x", EditorStyles.miniButton))
            {
                showFeedback = false;
            }
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
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            Undo.RecordObject(generator, "Generate Level Action");
            bool skipClearFlag = (generator.generationMode == GenerationMode.UserDefinedLayout);
            generator.GenerateLevel(skipClearFlag);
            MarkSceneDirty(generator);
            ShowFeedback("Level Generation Triggered!", MessageType.Info);
        }

        GUI.backgroundColor = clearButtonColor;
        if (GUILayout.Button(new GUIContent(" Clear Level", EditorGUIUtility.IconContent("d_TreeEditor.Trash").image, "Clear generated tiles, entities, AND scene RoomNodes"), clearButtonStyle))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            if (EditorUtility.DisplayDialog("Confirm Clear", "Clear generated level content AND scene design nodes (LevelDesignRoot)?", "Clear All", "Cancel"))
            {
                Undo.RecordObject(generator, "Clear Level");
                generator.ClearLevel();
                MarkSceneDirty(generator);
                ShowFeedback("Level Cleared", MessageType.Info);
            }
        }

        EditorGUILayout.EndHorizontal();
        GUI.backgroundColor = originalBgColor;
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space(10);
        // --- End Action Buttons Area ---

        // --- Simple Help Toggle ---
        showHelp = EditorGUILayout.ToggleLeft(" Show Basic Help", showHelp);
        if (showHelp)
        {
            EditorGUILayout.HelpBox("Workflow:\n1. Select Mode.\n2. Configure Dimensions & Settings.\n3. Assign Tilemaps & Tiles.\n4. Assign Entities (Optional).\n5. Generate Level!\n(Use Visual Designer for User Defined Layout setup).", MessageType.None);
        }
        // --- End Simple Help Toggle ---

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        GUI.backgroundColor = originalBgColor;
        GUI.contentColor = originalContentColor; // Restore defaults
    }

    // --- Section Drawing Helpers ---
    private void DrawHeader()
    {
        EditorGUILayout.Space(5);
        Rect r = GUILayoutUtility.GetRect(GUIContent.none, headerStyle, GUILayout.Height(30));
        EditorGUI.DrawRect(r, headerColor);
        GUI.Label(r, "Hybrid Procedural Level Generator", headerStyle);
        EditorGUILayout.Space(5);

        // Top action buttons row
        EditorGUILayout.BeginHorizontal();

        // Initialize Assets button
        GUIStyle actionButtonStyle = new GUIStyle(GUI.skin.button);
        actionButtonStyle.fontStyle = FontStyle.Bold;
        actionButtonStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.cyan : new Color(0.0f, 0.5f, 0.7f);

        Color defaultBgColor = GUI.backgroundColor;

        // Initialize Assets Button
        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);
        if (GUILayout.Button(new GUIContent(" Initialize Assets", EditorGUIUtility.IconContent("d_Refresh").image),
                actionButtonStyle, GUILayout.Height(30)))
        {
            bool proceed = EditorUtility.DisplayDialog("Initialize Assets",
         "This will search for and auto-assign assets from standard folders:\n\n" +
         "• Basic Tiles from main Tiles folder (NOT from subfolders)\n" +
         "• Directional Wall Tiles from subfolders\n" +
         "• Entity prefabs from Prefabs folder\n\n" +
         "This WILL overwrite any existing assignments. Continue?",
         "Initialize", "Cancel");

            if (proceed)
            {
                InitializeAssets();
            }
        }

        // Generate Level Button (at top)
        bool coreComponentsOk = AreCoreComponentsAssigned();
        GUI.backgroundColor = generateButtonColor;
        EditorGUI.BeginDisabledGroup(!coreComponentsOk);
        if (GUILayout.Button(new GUIContent(" Generate Level", EditorGUIUtility.IconContent("d_PlayButton On").image),
                            actionButtonStyle, GUILayout.Height(30)))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            Undo.RecordObject(generator, "Generate Level Action");
            bool skipClearFlag = (generator.generationMode == GenerationMode.UserDefinedLayout);
            generator.GenerateLevel(skipClearFlag);
            MarkSceneDirty(generator);
            ShowFeedback("Level Generation Triggered!", MessageType.Info);
        }

        // Clear Level Button (at top)
        GUI.backgroundColor = clearButtonColor;
        if (GUILayout.Button(new GUIContent(" Clear Level", EditorGUIUtility.IconContent("d_TreeEditor.Trash").image),
                            actionButtonStyle, GUILayout.Height(30)))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            if (EditorUtility.DisplayDialog("Confirm Clear", "Clear generated level content AND scene design nodes (LevelDesignRoot)?", "Clear All", "Cancel"))
            {
                Undo.RecordObject(generator, "Clear Level");
                generator.ClearLevel();
                MarkSceneDirty(generator);
                ShowFeedback("Level Cleared", MessageType.Info);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = defaultBgColor;

        // Add developer credit below header
        GUIStyle creditStyle = new GUIStyle(EditorStyles.miniLabel);
        creditStyle.alignment = TextAnchor.MiddleCenter;
        creditStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f);
        EditorGUILayout.LabelField("Developed by Dineshkumar & Kamalanathan", creditStyle);
        EditorGUILayout.Space(5);
    }

    private void DrawGenerationModeSelector()
    {
        EditorGUILayout.LabelField("Generation Mode", EditorStyles.boldLabel);
        int currentModeIndex = generationModeProp.enumValueIndex;
        float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(pulseTime));

        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < modeButtonContents.Length; i++)
        {
            bool isSelected = currentModeIndex == i;
            GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
            btnStyle.fixedHeight = 30;

            Color normalBg = modeColors[i] * (EditorGUIUtility.isProSkin ? 0.7f : 1.0f);
            Color selBg = modeColors[i] * (EditorGUIUtility.isProSkin ? 1.2f : 0.8f);
            selBg.a = 1.0f;

            Color txtCol = EditorGUIUtility.isProSkin ? Color.white * 0.8f : Color.black * 0.8f;
            Color selTxtCol = EditorGUIUtility.isProSkin ? Color.white : Color.black;

            if (isSelected)
            {
                GUI.backgroundColor = Color.Lerp(selBg, selBg * 1.15f, pulse);
                btnStyle.normal.textColor = selTxtCol;
                btnStyle.fontStyle = FontStyle.Bold;
            }
            else
            {
                GUI.backgroundColor = normalBg;
                btnStyle.normal.textColor = txtCol;
            }

            if (GUILayout.Button(modeButtonContents[i], btnStyle))
            {
                if (generationModeProp.enumValueIndex != i)
                {
                    generationModeProp.enumValueIndex = i;
                    ShowFeedback($"{modeButtonContents[i].text} mode selected.", MessageType.Info);
                }
                GUI.FocusControl(null);
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(GetModeHelpText((GenerationMode)currentModeIndex), MessageType.Info);

        if ((GenerationMode)currentModeIndex == GenerationMode.UserDefinedLayout)
        {
            EditorGUILayout.Space(5);
            Color obg = GUI.backgroundColor;
            GUI.backgroundColor = accentColor * (EditorGUIUtility.isProSkin ? 1.0f : 1.3f);

            if (GUILayout.Button(new GUIContent(" Open Visual Level Designer", EditorGUIUtility.IconContent("d_EditCollider").image), GUILayout.Height(30)))
            {
                var win = EditorWindow.GetWindow<VisualLevelDesignEditor>("Visual Level Designer");
                win.Show();
                win.Focus();
                ShowFeedback("Visual Level Designer opened.", MessageType.Info);
            }

            GUI.backgroundColor = obg;
        }

        EditorGUILayout.Space(10);
    }

    // Foldout helper using FadeGroup
    private void DrawFoldoutSection(string key, string title, ref bool foldout, Action drawContent)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        bool newState = EditorGUILayout.Foldout(foldout, title, true, foldoutHeaderStyle);
        if (newState != foldout)
        {
            foldout = newState;
        }

        if (foldoutAnimValues.ContainsKey(key))
        {
            if (EditorGUILayout.BeginFadeGroup(foldoutAnimValues[key]))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);
                if (drawContent != null)
                {
                    drawContent();
                }
                EditorGUILayout.Space(5);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFadeGroup();
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }

    // --- Specific Section Drawing Methods ---
    private void DrawLevelDimensionsSection()
    {
        EditorGUILayout.PropertyField(levelWidthProp, new GUIContent("Level Width", "Max grid width."));
        EditorGUILayout.PropertyField(levelHeightProp, new GUIContent("Level Height", "Max grid height."));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Random Seed", subHeaderStyle);
        EditorGUILayout.PropertyField(useRandomSeedProp, new GUIContent("Use Random Seed", "Use time-based seed?"));

        EditorGUI.BeginDisabledGroup(useRandomSeedProp.boolValue);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(seedProp, new GUIContent("Seed Value", "Manual seed."));

        if (GUILayout.Button("New", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            seedProp.intValue = UnityEngine.Random.Range(1, 999999);
            ShowFeedback($"New seed: {seedProp.intValue}", MessageType.None, 2.0f);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();
    }

    private void DrawBspSection()
    {
        EditorGUILayout.PropertyField(minRoomSizeProp, new GUIContent("Min Room Size", "Min width/height for BSP leaves & Rects."));
        EditorGUILayout.PropertyField(maxIterationsProp, new GUIContent("BSP Iterations", "Number of BSP splits."));
        EditorGUILayout.PropertyField(roomPaddingProp, new GUIContent("Room Padding", "Empty cells between procedural rooms."));
    }

    private void DrawHybridSection()
    {
        EditorGUILayout.LabelField("Procedural Room Chances", subHeaderStyle);
        EditorGUILayout.PropertyField(lShapeProbabilityProp, new GUIContent("L-Shape Chance", "Chance (0-1) for proc. room = L-Shape."));
        EditorGUILayout.PropertyField(roomTemplateProbabilityProp, new GUIContent("Template Chance", "Chance (0-1) for proc. room = Template."));

        if (lShapeProbabilityProp.floatValue + roomTemplateProbabilityProp.floatValue > 1.01f)
        {
            EditorGUILayout.HelpBox("Probabilities exceed 100%.", MessageType.Warning);
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("L-Shape Leg Ratios", subHeaderStyle);
        minLLegRatioProp.floatValue = EditorGUILayout.Slider(new GUIContent("Min Leg Ratio", "Min size ratio of smaller leg."), minLLegRatioProp.floatValue, 0.2f, 0.8f);
        maxLLegRatioProp.floatValue = EditorGUILayout.Slider(new GUIContent("Max Leg Ratio", "Max size ratio of smaller leg."), maxLLegRatioProp.floatValue, minLLegRatioProp.floatValue, 0.8f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Room Templates List", subHeaderStyle);
        EditorGUILayout.PropertyField(roomTemplatePrefabsProp, true);
        EditorGUILayout.HelpBox("Assign Room Template Prefabs (must contain Tilemap).", MessageType.None);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("User Defined Node Default", subHeaderStyle);
        EditorGUILayout.PropertyField(defaultSceneNodeSizeProp, new GUIContent("Default Node Size", "Size for UserDefined nodes if size is zero."));
    }

    private void DrawCorridorSection()
    {
        EditorGUILayout.PropertyField(corridorWidthProp, new GUIContent("Corridor Width", "Width of corridors (in tiles)."));
    }

    private void DrawTilemapSection()
    {
        EditorGUILayout.LabelField("Required Tilemaps", subHeaderStyle);
        EditorGUILayout.PropertyField(groundTilemapProp, new GUIContent("Ground Tilemap"));
        EditorGUILayout.PropertyField(wallTilemapProp, new GUIContent("Wall Tilemap"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Required Tiles", subHeaderStyle);
        EditorGUILayout.PropertyField(floorTileProp, new GUIContent("Floor Tile"));
        EditorGUILayout.PropertyField(wallTileProp, new GUIContent("Wall Tile"));

        // Updated directional walls section with rotation options
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Directional Wall Tiles", subHeaderStyle);
        EditorGUILayout.PropertyField(useDirectionalWallsProp, new GUIContent("Use Directional Walls", "Enable to use different tiles for walls based on their orientation"));

        if (useDirectionalWallsProp.boolValue)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Basic Wall Directions", EditorStyles.boldLabel);
            DrawDirectionalTileField("Bottom Wall Tile", "Wall with floor below (Sprite #1)", wallTileBottomProp);
            DrawDirectionalTileField("Top Wall Tile", "Wall with floor above (Sprite #2)", wallTileTopProp);
            DrawDirectionalTileField("Right Wall Tile", "Wall with floor on the right (Sprite #3)", wallTileRightProp);
            DrawDirectionalTileField("Left Wall Tile", "Wall with floor on the left (Sprite #4)", wallTileLeftProp);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Inner Corner Tiles", EditorStyles.boldLabel);
            DrawDirectionalTileField("Inner Top-Left", "Inner corner with floor on left and top (Sprite #5)", wallTileInnerTopLeftProp);
            DrawDirectionalTileField("Inner Top-Right", "Inner corner with floor on right and top (Sprite #6)", wallTileInnerTopRightProp);
            DrawDirectionalTileField("Inner Bottom-Left", "Inner corner with floor on left and bottom (Sprite #7)", wallTileInnerBottomLeftProp);
            DrawDirectionalTileField("Inner Bottom-Right", "Inner corner with floor on right and bottom (Sprite #8)", wallTileInnerBottomRightProp);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Outer Corner Tiles", EditorStyles.boldLabel);
            // Fixed order of outer corner tiles to match what's shown in the UI
            DrawDirectionalTileField("Outer Top-Left", "Outer corner at top-left edge of room (Sprite #9)", wallTileOuterTopLeftProp);
            DrawDirectionalTileField("Outer Top-Right", "Outer corner at top-right edge of room (Sprite #10)", wallTileOuterTopRightProp);
            DrawDirectionalTileField("Outer Bottom-Left", "Outer corner at bottom-left edge of room (Sprite #11)", wallTileOuterBottomLeftProp);
            DrawDirectionalTileField("Outer Bottom-Right", "Outer corner at bottom-right edge of room (Sprite #12)", wallTileOuterBottomRightProp);

            EditorGUI.indentLevel--;

            bool anyBasicTilesMissing =
                wallTileLeftProp.FindPropertyRelative("tile").objectReferenceValue == null ||
                wallTileRightProp.FindPropertyRelative("tile").objectReferenceValue == null ||
                wallTileTopProp.FindPropertyRelative("tile").objectReferenceValue == null ||
                wallTileBottomProp.FindPropertyRelative("tile").objectReferenceValue == null;

            if (anyBasicTilesMissing)
            {
                EditorGUILayout.HelpBox("Please assign all four basic directional wall tiles for the system to work correctly.", MessageType.Warning);
            }
        }

        // Keep the existing tile variants section
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Tile Variations", subHeaderStyle);
        EditorGUILayout.PropertyField(floorTileVariantsProp, new GUIContent("Floor Tile Variants", "Additional floor tiles to randomly use during generation"));
        EditorGUILayout.PropertyField(wallTileVariantsProp, new GUIContent("Wall Tile Variants", "Additional wall tiles to randomly use during generation"));
        EditorGUILayout.Slider(variantTileChanceProp, 0f, 1f, new GUIContent("Variant Chance", "Chance to use a variant tile instead of the main tile (0-1)"));

        if (floorTileVariantsProp.arraySize > 0 || wallTileVariantsProp.arraySize > 0)
        {
            EditorGUILayout.HelpBox("Tile variants will be randomly used according to the Variant Chance value.", MessageType.Info);
        }

        if (useDirectionalWallsProp.boolValue && (wallTileVariantsProp.arraySize > 0 && variantTileChanceProp.floatValue > 0))
        {
            EditorGUILayout.HelpBox("Note: When both Directional Walls and Wall Tile Variants are enabled, Directional Walls take precedence.", MessageType.Info);
        }
    }

    // Helper method to draw a DirectionalTile field with tile and rotation properties
    private void DrawDirectionalTileField(string label, string tooltip, SerializedProperty directionalTileProp)
    {
        // Begin horizontal for the directional tile row
        EditorGUILayout.BeginHorizontal();

        SerializedProperty tileProp = directionalTileProp.FindPropertyRelative("tile");
        SerializedProperty rotationProp = directionalTileProp.FindPropertyRelative("rotation");

        // Tile field takes 70% of the width
        EditorGUILayout.PropertyField(tileProp, new GUIContent(label, tooltip), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.55f));

        // Rotation dropdown takes 30% of the width
        EditorGUILayout.PropertyField(rotationProp, GUIContent.none, GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.25f));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEntitySection()
    {
        EditorGUILayout.LabelField("Player", subHeaderStyle);
        EditorGUILayout.PropertyField(playerPrefabProp, new GUIContent("Player Prefab"));

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Enemies", subHeaderStyle);
        EditorGUILayout.PropertyField(enemyPrefabProp, new GUIContent("Enemy Prefab"));
        EditorGUILayout.PropertyField(enemiesPerRoomProp, new GUIContent("Max Enemies/Room")); // Using PropertyField for Range attribute

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Decorations", subHeaderStyle);
        EditorGUILayout.PropertyField(decorationPrefabProp, new GUIContent("Decoration Prefab"));
        EditorGUILayout.PropertyField(decorationsPerRoomProp, new GUIContent("Max Decors/Room"));
    }

    // Asset initialization method
    // Asset initialization method
    // Asset initialization method
    // Asset initialization method
    private void InitializeAssets()
    {
        // Get the target reference
        HybridLevelGenerator generator = (HybridLevelGenerator)target;
        Undo.RecordObject(generator, "Initialize Assets");
        bool anyAssetsAssigned = false;

        Debug.Log("=== INITIALIZING ASSETS ===");

        // Clear any existing wall tile if it's a directional tile
        if (generator.wallTile != null &&
           (generator.wallTile.name.Contains("Bottom") ||
            generator.wallTile.name.Contains("Top") ||
            generator.wallTile.name.Contains("Left") ||
            generator.wallTile.name.Contains("Right")))
        {
            Debug.Log("Clearing directional wall tile: " + generator.wallTile.name);
            generator.wallTile = null;
        }

        // 1. First try to find tiles in the Floor and Wall Tiles folder ONLY
        string mainFolderPath = "Assets/PCGLevelGenerator/Tiles/Floor and Wall Tiles";
        if (System.IO.Directory.Exists(Application.dataPath + mainFolderPath.Substring(6)))
        {
            // Get files only from this specific folder
            string[] assetFiles = System.IO.Directory.GetFiles(
                Application.dataPath + mainFolderPath.Substring(6),
                "*.asset",
                System.IO.SearchOption.TopDirectoryOnly);

            // Process all files in this folder
            foreach (string fullPath in assetFiles)
            {
                string assetPath = "Assets" + fullPath.Substring(Application.dataPath.Length).Replace('\\', '/');
                string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);

                // Wall Tile - check it's not a directional tile
                if (generator.wallTile == null &&
                    fileName.ToLower().Contains("wall") &&
                    !fileName.Contains("Bottom") &&
                    !fileName.Contains("Top") &&
                    !fileName.Contains("Left") &&
                    !fileName.Contains("Right"))
                {
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(assetPath);
                    if (tile != null)
                    {
                        generator.wallTile = tile;
                        anyAssetsAssigned = true;
                        Debug.Log("Assigned Wall Tile: " + tile.name);
                    }
                }

                // Floor Tile
                if (generator.floorTile == null && fileName.ToLower().Contains("floor"))
                {
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(assetPath);
                    if (tile != null)
                    {
                        generator.floorTile = tile;
                        anyAssetsAssigned = true;
                        Debug.Log("Assigned Floor Tile: " + tile.name);
                    }
                }
            }
        }

        // 2. Find and assign Tilemaps if needed
        if (generator.groundTilemap == null || generator.wallTilemap == null)
        {
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (Tilemap tilemap in tilemaps)
            {
                if (generator.groundTilemap == null && tilemap.name.ToLower().Contains("ground"))
                {
                    generator.groundTilemap = tilemap;
                    anyAssetsAssigned = true;
                    Debug.Log("Assigned Ground Tilemap: " + tilemap.name);
                }
                else if (generator.wallTilemap == null && tilemap.name.ToLower().Contains("wall"))
                {
                    generator.wallTilemap = tilemap;
                    anyAssetsAssigned = true;
                    Debug.Log("Assigned Wall Tilemap: " + tilemap.name);
                }
            }
        }

        // 3. Only after main tiles are assigned, load directional tiles
        if (generator.wallTile != null)
        {
            LoadDirectionalTilesWithOverwrite(generator, "Basic Wall Directions");
            LoadDirectionalTilesWithOverwrite(generator, "Inner Corner Tiles");
            LoadDirectionalTilesWithOverwrite(generator, "Outer Corner Tiles");
        }

        // 4. Continue with the rest of your existing initialization code
        // (Entity prefabs, etc.)

        // Save changes
        if (anyAssetsAssigned)
        {
            EditorUtility.SetDirty(generator);
            MarkSceneDirty(generator);
            ShowFeedback("Assets initialized successfully!", MessageType.Info);
        }
        else
        {
            ShowFeedback("No assets were found to assign.", MessageType.Info);
        }
    }


    // Add this function to your HybridLevelGeneratorEditor.cs

    private void AssignMainWallTile(HybridLevelGenerator generator)
    {
        // Explicitly look for wall tiles ONLY in the main "Floor and Wall Tiles" folder
        string mainFolderPath = "Assets/PCGLevelGenerator/Tiles/Floor and Wall Tiles";
        string[] guids = AssetDatabase.FindAssets("t:TileBase Wall", new[] { mainFolderPath });

        Debug.Log($"Found {guids.Length} potential wall tiles in main folder");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Skip ANY tiles from directional tile folders
            if (path.Contains("Directional Tiles") ||
                path.Contains("Basic Wall Directions") ||
                path.Contains("Inner Corner Tiles") ||
                path.Contains("Outer Corner Tiles"))
            {
                Debug.Log($"Skipping directional tile: {path}");
                continue;
            }

            // Only process files directly in the target folder
            string directory = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (directory != mainFolderPath)
            {
                Debug.Log($"Skipping tile not in target folder: {path}");
                continue;
            }

            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile != null)
            {
                generator.wallTile = tile;
                Debug.Log($"ASSIGNED MAIN WALL TILE: {tile.name} from {path}");
                return; // Exit after finding first valid tile
            }
        }

        Debug.LogWarning("Could not find a suitable Wall Tile in the Floor and Wall Tiles folder");
    }


    private void SetupCameraFollow()
    {
        // Find or create Main Camera
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            // Create a new camera if none exists
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            mainCamera = cameraObj.AddComponent<Camera>();
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.192f, 0.301f, 0.474f);
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 10f;
            mainCamera.transform.position = new Vector3(0, 0, -10);
            Debug.Log("Created new Main Camera");
        }

        // Check if CameraFollow script is already attached
        CameraFollow existingScript = mainCamera.GetComponent<CameraFollow>();
        if (existingScript == null)
        {
            // Try to load the CameraFollow script
            string scriptPath = "Assets/PCGLevelGenerator/Scripts/Core/CameraFollow.cs";
            MonoScript scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);

            if (scriptAsset != null)
            {
                // Add the script component
                mainCamera.gameObject.AddComponent<CameraFollow>();
                Debug.Log("Added CameraFollow script to Main Camera");
            }
            else
            {
                Debug.LogError("Could not find CameraFollow script at path: " + scriptPath);
            }
        }
        else
        {
            Debug.Log("CameraFollow script already attached to Main Camera");
        }
    }

    // Helper method to load all directional tiles from a specific folder
    private void LoadDirectionalTilesWithOverwrite(HybridLevelGenerator generator, string subfolderName)
    {
        string basePath = $"Assets/PCGLevelGenerator/Tiles/Directional Tiles/{subfolderName}";

        if (!AssetDatabase.IsValidFolder(basePath))
        {
            Debug.LogWarning($"Folder not found: {basePath}");
            return;
        }

        // Map tile names to their respective properties
        Dictionary<string, SerializedProperty> tileMapping = new Dictionary<string, SerializedProperty>();

        // Basic Wall Directions
        if (subfolderName == "Basic Wall Directions")
        {
            tileMapping.Add("Left", serializedObject.FindProperty("wallTileLeft").FindPropertyRelative("tile"));
            tileMapping.Add("Right", serializedObject.FindProperty("wallTileRight").FindPropertyRelative("tile"));
            tileMapping.Add("Top", serializedObject.FindProperty("wallTileTop").FindPropertyRelative("tile"));
            tileMapping.Add("Bottom", serializedObject.FindProperty("wallTileBottom").FindPropertyRelative("tile"));
        }
        // Inner Corner Tiles
        else if (subfolderName == "Inner Corner Tiles")
        {
            tileMapping.Add("InnerTopLeft", serializedObject.FindProperty("wallTileInnerTopLeft").FindPropertyRelative("tile"));
            tileMapping.Add("InnerTopRight", serializedObject.FindProperty("wallTileInnerTopRight").FindPropertyRelative("tile"));
            tileMapping.Add("InnerBottomLeft", serializedObject.FindProperty("wallTileInnerBottomLeft").FindPropertyRelative("tile"));
            tileMapping.Add("InnerBottomRight", serializedObject.FindProperty("wallTileInnerBottomRight").FindPropertyRelative("tile"));
        }
        // Outer Corner Tiles
        else if (subfolderName == "Outer Corner Tiles")
        {
            tileMapping.Add("OuterTopLeft", serializedObject.FindProperty("wallTileOuterTopLeft").FindPropertyRelative("tile"));
            tileMapping.Add("OuterTopRight", serializedObject.FindProperty("wallTileOuterTopRight").FindPropertyRelative("tile"));
            tileMapping.Add("OuterBottomLeft", serializedObject.FindProperty("wallTileOuterBottomLeft").FindPropertyRelative("tile"));
            tileMapping.Add("OuterBottomRight", serializedObject.FindProperty("wallTileOuterBottomRight").FindPropertyRelative("tile"));
        }

        // Get all tile assets in the folder
        string[] guids = AssetDatabase.FindAssets("t:TileBase", new[] { basePath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);

            if (tile == null)
                continue;

            // Try to match the tile name with our mapping
            foreach (var mapping in tileMapping)
            {
                if (tile.name.Contains(mapping.Key))
                {
                    // Always overwrite the value, regardless of whether it's already assigned
                    mapping.Value.objectReferenceValue = tile;
                    Debug.Log($"Assigned {mapping.Key} Tile: {tile.name} from {path}");
                    break;
                }
            }
        }

        // Apply the changes
        serializedObject.ApplyModifiedProperties();
    }

    // --- Utility ---
    private void MarkSceneDirty(HybridLevelGenerator generator)
    {
        if (!Application.isPlaying && generator != null && generator.gameObject != null)
        {
            try
            {
                if (generator.gameObject.scene != null &&
                    generator.gameObject.scene.IsValid() &&
                    generator.gameObject.scene.isLoaded)
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
}