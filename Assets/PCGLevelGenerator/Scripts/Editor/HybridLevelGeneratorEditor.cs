using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;

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

    // Directional wall tile properties
    SerializedProperty useDirectionalWallsProp;
    SerializedProperty wallTileBottomProp;
    SerializedProperty wallTileTopProp;
    SerializedProperty wallTileRightProp;
    SerializedProperty wallTileLeftProp;
    SerializedProperty wallTileInnerTopLeftProp;
    SerializedProperty wallTileInnerTopRightProp;
    SerializedProperty wallTileInnerBottomLeftProp;
    SerializedProperty wallTileInnerBottomRightProp;
    SerializedProperty wallTileOuterTopLeftProp;
    SerializedProperty wallTileOuterTopRightProp;
    SerializedProperty wallTileOuterBottomLeftProp;
    SerializedProperty wallTileOuterBottomRightProp;

    // --- UI States ---
    private bool showFeedback = false;
    private string feedbackMessage = "";
    private MessageType feedbackType = MessageType.Info;
    private double feedbackExpireTime;

    // Current active tab
    private int currentTab = 0;
    private readonly string[] tabNames = { "1. Mode", "2. Tiles", "3. Rooms", "4. Entities", "5. Generate" };



    // Setup progress tracking

    private bool[] setupComplete = new bool[4]; // Track completion of each step

    // Add these with your other UI state variables (near line 60-70 where you have other UI states)

    private bool showInnerCornersFoldout = false;

    private bool showOuterCornersFoldout = false;

    // Styling elements

    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle tabStyle;
    private GUIStyle activeTabStyle;
    private GUIStyle stepCompletedStyle;
    private GUIStyle boxStyle;
    private GUIStyle warningStyle;
    private GUIStyle successStyle;
    private Texture2D successIconTexture;
    private Texture2D warningIconTexture;
    private Texture2D infoIconTexture;
    private readonly Color accentColor = new Color(0.2f, 0.6f, 0.9f);
    private readonly Color successColor = new Color(0.2f, 0.7f, 0.3f);
    private readonly Color warningColor = new Color(0.9f, 0.6f, 0.1f);
    private readonly Color errorColor = new Color(0.9f, 0.3f, 0.2f);



    // Textures for template validation

    private Texture2D validTexture;
    private Texture2D invalidTexture;



    // Animation

    private float pulseTime = 0f;



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

        // Directional wall tiles
        useDirectionalWallsProp = serializedObject.FindProperty("useDirectionalWalls");
        wallTileBottomProp = serializedObject.FindProperty("wallTileBottom");
        wallTileTopProp = serializedObject.FindProperty("wallTileTop");
        wallTileRightProp = serializedObject.FindProperty("wallTileRight");
        wallTileLeftProp = serializedObject.FindProperty("wallTileLeft");
        wallTileInnerTopLeftProp = serializedObject.FindProperty("wallTileInnerTopLeft");
        wallTileInnerTopRightProp = serializedObject.FindProperty("wallTileInnerTopRight");
        wallTileInnerBottomLeftProp = serializedObject.FindProperty("wallTileInnerBottomLeft");
        wallTileInnerBottomRightProp = serializedObject.FindProperty("wallTileInnerBottomRight");
        wallTileOuterTopLeftProp = serializedObject.FindProperty("wallTileOuterTopLeft");
        wallTileOuterTopRightProp = serializedObject.FindProperty("wallTileOuterTopRight");
        wallTileOuterBottomLeftProp = serializedObject.FindProperty("wallTileOuterBottomLeft");
        wallTileOuterBottomRightProp = serializedObject.FindProperty("wallTileOuterBottomRight");

        // Tile variants
        floorTileVariantsProp = serializedObject.FindProperty("floorTileVariants");
        wallTileVariantsProp = serializedObject.FindProperty("wallTileVariants");
        variantTileChanceProp = serializedObject.FindProperty("variantTileChance");

        // Register for editor updates
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;



        // Set initial tab to first incomplete step

        UpdateSetupProgress();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        pulseTime = (float)(EditorApplication.timeSinceStartup * 2.5) % (2f * Mathf.PI);



        // Clear feedback after timeout

        if (showFeedback && EditorApplication.timeSinceStartup > feedbackExpireTime)
        {
            showFeedback = false;
            Repaint();
        }
    }



    private void InitializeStyles()

    {

        // Only initialize once

        if (headerStyle != null) return;



        // Create header style

        headerStyle = new GUIStyle(EditorStyles.boldLabel);

        headerStyle.fontSize = 16;

        headerStyle.alignment = TextAnchor.MiddleCenter;

        headerStyle.normal.textColor = EditorGUIUtility.isProSkin ?

            new Color(0.8f, 0.85f, 0.9f) : new Color(0.1f, 0.1f, 0.1f);



        // Create subheader style

        subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);

        subHeaderStyle.fontSize = 12;

        subHeaderStyle.margin = new RectOffset(0, 0, 10, 5);

        // Add subtle background for section headers

        subHeaderStyle.normal.background = MakeColorTexture(new Color(0.3f, 0.3f, 0.3f, 0.1f));

        subHeaderStyle.padding = new RectOffset(5, 5, 3, 3);



        // Create tab styles

        tabStyle = new GUIStyle(GUI.skin.button);

        tabStyle.fixedHeight = 35;

        tabStyle.fontSize = 12;

        tabStyle.padding = new RectOffset(5, 5, 8, 8);

        // Enhance button text appearance

        tabStyle.normal.textColor = EditorGUIUtility.isProSkin ?

            new Color(0.8f, 0.8f, 0.8f) : new Color(0.2f, 0.2f, 0.2f);



        // Active tab style

        activeTabStyle = new GUIStyle(tabStyle);

        activeTabStyle.normal.background = MakeColorTexture(accentColor);

        activeTabStyle.normal.textColor = Color.white;

        activeTabStyle.fontStyle = FontStyle.Bold;



        // Step completed style

        stepCompletedStyle = new GUIStyle(EditorStyles.label);

        stepCompletedStyle.fontSize = 11;

        stepCompletedStyle.normal.textColor = successColor;



        // Box style

        boxStyle = new GUIStyle(EditorStyles.helpBox);

        boxStyle.padding = new RectOffset(15, 15, 15, 15);

        // Add margin for better spacing

        boxStyle.margin = new RectOffset(0, 0, 10, 10);



        // Warning style

        warningStyle = new GUIStyle(EditorStyles.helpBox);

        warningStyle.fontSize = 11;

        warningStyle.padding = new RectOffset(30, 10, 10, 10);



        // Success style

        successStyle = new GUIStyle(EditorStyles.helpBox);

        successStyle.fontSize = 11;

        successStyle.padding = new RectOffset(30, 10, 10, 10);



        // Create textures

        validTexture = CreateTexture(successColor);

        invalidTexture = CreateTexture(errorColor);



        // Icon textures

        successIconTexture = EditorGUIUtility.IconContent("d_Collab").image as Texture2D;

        warningIconTexture = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D;

        infoIconTexture = EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image as Texture2D;

    }



    private Texture2D CreateTexture(Color color)
    {
        Texture2D tex = new Texture2D(16, 16);
        Color[] colors = new Color[16 * 16];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = color;
        }
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }



    private Texture2D MakeColorTexture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }



    public override void OnInspectorGUI()
    {
        // Initialize styles
        InitializeStyles();
        serializedObject.Update();



        // Store original colors

        Color originalBgColor = GUI.backgroundColor;
        Color originalContentColor = GUI.contentColor;



        DrawHeader();
        DrawProgressBar();
        DrawTabs();



        // Draw tab content

        EditorGUILayout.BeginVertical(boxStyle);
        switch (currentTab)
        {
            case 0: DrawModeTab(); break;
            case 1: DrawTilesTab(); break;
            case 2: DrawRoomsTab(); break;
            case 3: DrawEntitiesTab(); break;
            case 4: DrawGenerateTab(); break;
        }
        EditorGUILayout.EndVertical();



        // Draw feedback message if any

        if (showFeedback)
        {
            EditorGUILayout.Space(10);
            DrawFeedbackMessage();
        }



        // Apply changes

        if (GUI.changed)
        {
            serializedObject.ApplyModifiedProperties();
            UpdateSetupProgress();
        }



        // Restore original colors

        GUI.backgroundColor = originalBgColor;
        GUI.contentColor = originalContentColor;
    }



    private void DrawHeader()
    {
        EditorGUILayout.Space(5);



        // Header background

        Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, headerStyle, GUILayout.Height(35));
        EditorGUI.DrawRect(headerRect, EditorGUIUtility.isProSkin ?

            new Color(0.18f, 0.22f, 0.25f) : new Color(0.8f, 0.82f, 0.85f));



        // Header title

        GUI.Label(headerRect, "PCG Level Generator", headerStyle);



        // Version and credit info

        GUIStyle creditStyle = new GUIStyle(EditorStyles.miniLabel);
        creditStyle.alignment = TextAnchor.MiddleCenter;
        creditStyle.normal.textColor = EditorGUIUtility.isProSkin ?

            new Color(0.7f, 0.7f, 0.7f) : new Color(0.4f, 0.4f, 0.4f);



        EditorGUILayout.LabelField("v1.0 • Developed by Dineshkumar & Kamalanathan", creditStyle);
        EditorGUILayout.Space(10);
    }



    private void DrawProgressBar()
    {
        EditorGUILayout.BeginHorizontal();



        // Calculate progress

        int completedSteps = 0;
        for (int i = 0; i < setupComplete.Length; i++)
        {
            if (setupComplete[i]) completedSteps++;
        }
        float progress = completedSteps / (float)setupComplete.Length;



        // Draw bar background

        Rect progressRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(5), GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(progressRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));



        // Draw progress fill

        Rect fillRect = new Rect(progressRect.x, progressRect.y, progressRect.width * progress, progressRect.height);
        EditorGUI.DrawRect(fillRect, successColor);



        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(8);
    }



    private void DrawTabs()
    {
        EditorGUILayout.BeginHorizontal();



        GUIStyle guideStyle = new GUIStyle(EditorStyles.miniLabel);
        guideStyle.alignment = TextAnchor.MiddleCenter;



        // Draw tab buttons

        for (int i = 0; i < tabNames.Length; i++)
        {
            // Determine if tab is enabled
            bool isActive = currentTab == i;
            bool isEnabled = (i == 0) || (i < 4 && setupComplete[i - 1]) || (i == 4 && completedAllMandatory());



            // Set color and style

            GUI.backgroundColor = GetTabColor(i, isActive, isEnabled);
            GUIStyle style = isActive ? activeTabStyle : tabStyle;



            // Draw the tab

            EditorGUI.BeginDisabledGroup(!isEnabled);
            if (GUILayout.Button(tabNames[i], style))
            {
                currentTab = i;
                GUI.FocusControl(null);
            }
            EditorGUI.EndDisabledGroup();



            // Show completion status

            if (i < 4 && setupComplete[i])
            {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                Rect checkRect = new Rect(lastRect.x + lastRect.width - 15, lastRect.y + 2, 12, 12);
                GUI.DrawTexture(checkRect, successIconTexture);
            }
        }



        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();



        EditorGUILayout.Space(5);
    }



    private Color GetTabColor(int tabIndex, bool isActive, bool isEnabled)
    {
        if (!isEnabled) return Color.gray;
        if (isActive) return accentColor;



        // Pulse current incomplete tab

        if (tabIndex < 4 && !setupComplete[tabIndex])
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(pulseTime);
            return Color.Lerp(Color.white, new Color(0.9f, 0.9f, 0.6f), pulse * 0.3f);
        }



        return Color.white;
    }



    #region TAB CONTENT METHODS


    private void DrawModeTab()

    {

        // Add mode indicator at the top

        DrawModeIndicator();



        // Add top navigation button (just Next since this is the first tab)

        EditorGUILayout.BeginHorizontal();

        GUILayout.FlexibleSpace();

        Color originalBg = GUI.backgroundColor;

        GUI.backgroundColor = accentColor;

        if (GUILayout.Button("Next: Tiles & Tilemaps →", GUILayout.Width(150)))

        {

            currentTab = 1;

        }

        GUI.backgroundColor = originalBg;

        EditorGUILayout.EndHorizontal();



        EditorGUILayout.Space(10);



        // Original content

        EditorGUILayout.LabelField("Step 1: Choose Generation Mode", headerStyle);

        EditorGUILayout.Space(5);



        EditorGUILayout.HelpBox("Select the type of level generation that best fits your game. Each mode offers different levels of procedural content and control.", MessageType.Info);

        EditorGUILayout.Space(10);



        // Get current mode

        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;



        // Draw mode selection

        EditorGUILayout.BeginVertical();



        // Draw each mode button

        DrawModeButton(GenerationMode.FullyProcedural, "Fully Procedural",

            "Classic procedural dungeons using BSP algorithm with rectangular rooms.",

            currentMode == GenerationMode.FullyProcedural, new Color(0.2f, 0.6f, 0.9f, 0.8f));



        DrawModeButton(GenerationMode.HybridProcedural, "Hybrid Procedural",

            "Mix of BSP generation with L-shapes & room templates for more variety.",

            currentMode == GenerationMode.HybridProcedural, new Color(0.9f, 0.6f, 0.2f, 0.8f));



        DrawModeButton(GenerationMode.UserDefinedLayout, "User Defined Layout",

            "Design your level layout visually using the Visual Level Designer.",

            currentMode == GenerationMode.UserDefinedLayout, new Color(0.2f, 0.7f, 0.4f, 0.8f));



        EditorGUILayout.EndVertical();



        // Special case for User Defined mode - show visual designer button

        if (currentMode == GenerationMode.UserDefinedLayout)

        {

            EditorGUILayout.Space(10);

            Color origBg = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.4f);



            if (GUILayout.Button(new GUIContent(" Open Visual Level Designer", EditorGUIUtility.IconContent("d_EditCollider").image), GUILayout.Height(30)))

            {

                OpenVisualDesigner();

            }



            GUI.backgroundColor = origBg;

        }



        // Navigation buttons (bottom)

        EditorGUILayout.Space(15);

        DrawNavigationButtons(null, "Next: Tiles & Tilemaps →", null, () => { currentTab = 1; });



        // Mark this step complete regardless of selection

        setupComplete[0] = true;

    }




    private void DrawModeButton(GenerationMode mode, string title, string description, bool isSelected, Color color)
    {
        // Store original colors
        Color originalBg = GUI.backgroundColor;



        // Create button style

        GUIStyle buttonStyle = new GUIStyle(EditorStyles.helpBox);
        buttonStyle.padding = new RectOffset(15, 15, 10, 10);
        buttonStyle.margin = new RectOffset(0, 0, 5, 5);



        // Set button color

        if (isSelected)

        {
            float pulse = 0.8f + 0.2f * Mathf.Sin(pulseTime);
            GUI.backgroundColor = Color.Lerp(color, color * 1.2f, pulse);
        }
        else
        {
            GUI.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.2f);
        }



        // Create the button

        EditorGUILayout.BeginVertical(buttonStyle);



        // Mode selection radio button

        EditorGUILayout.BeginHorizontal();
        bool newSelection = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
        if (newSelection != isSelected && newSelection == true)
        {
            generationModeProp.enumValueIndex = (int)mode;
            GUI.FocusControl(null);
        }



        // Mode title

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 13;
        EditorGUILayout.LabelField(title, titleStyle);
        EditorGUILayout.EndHorizontal();



        // Mode description

        GUIStyle descStyle = new GUIStyle(EditorStyles.label);
        descStyle.wordWrap = true;
        EditorGUILayout.LabelField(description, descStyle);



        EditorGUILayout.EndVertical();
        GUI.backgroundColor = originalBg;
    }



    private void DrawTilesTab()

    {

        // Add mode indicator at the top

        DrawModeIndicator();



        // Add top navigation buttons

        EditorGUILayout.BeginHorizontal();

        Color originalBg = GUI.backgroundColor;

        if (GUILayout.Button("← Back: Mode", GUILayout.Width(150)))

        {

            currentTab = 0;

        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = accentColor;

        if (GUILayout.Button("Next: Room Settings →", GUILayout.Width(150)))

        {

            currentTab = 2;

        }

        GUI.backgroundColor = originalBg;

        EditorGUILayout.EndHorizontal();



        EditorGUILayout.Space(10);



        // Original content

        EditorGUILayout.LabelField("Step 2: Configure Tiles & Tilemaps", headerStyle);

        EditorGUILayout.Space(5);



        EditorGUILayout.HelpBox("Assign the required tilemaps and tiles for level generation. These are essential for creating the visual elements of your level.", MessageType.Info);

        EditorGUILayout.Space(10);



        // Check if essential components are assigned

        bool tilemapsAssigned = groundTilemapProp.objectReferenceValue != null &&

                               wallTilemapProp.objectReferenceValue != null;

        bool tilesAssigned = floorTileProp.objectReferenceValue != null &&

                            wallTileProp.objectReferenceValue != null;



        // Auto-assign button (Initialize Assets)

        Color origBg = GUI.backgroundColor;

        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);



        if (GUILayout.Button(new GUIContent(" Initialize Assets",

                                         EditorGUIUtility.IconContent("d_Refresh").image),

                          GUILayout.Height(30)))

        {

            bool proceed = EditorUtility.DisplayDialog("Initialize Assets",

                "This will search for and auto-assign assets from standard folders:\n\n" +

                "• Basic Tiles from main Tiles folder\n" +

                "• Directional Wall Tiles from subfolders\n" +

                "• Entity prefabs from Prefabs folder\n\n" +

                "This WILL overwrite any existing assignments. Continue?",

                "Initialize", "Cancel");



            if (proceed)

            {

                InitializeAssets();

            }

        }



        GUI.backgroundColor = origBg;

        EditorGUILayout.Space(10);



        // Required Tilemaps Section

        DrawSectionHeader("Required Tilemaps",

                        tilemapsAssigned ? successIconTexture : warningIconTexture);



        EditorGUILayout.PropertyField(groundTilemapProp, new GUIContent("Ground Tilemap",

                                                              "Tilemap for floor tiles"));

        EditorGUILayout.PropertyField(wallTilemapProp, new GUIContent("Wall Tilemap",

                                                           "Tilemap for wall tiles"));



        if (!tilemapsAssigned)

        {

            DrawWarning("Both Ground and Wall Tilemaps must be assigned");

        }



        EditorGUILayout.Space(15);



        // Required Tiles Section

        DrawSectionHeader("Required Tiles",

                        tilesAssigned ? successIconTexture : warningIconTexture);



        EditorGUILayout.PropertyField(floorTileProp, new GUIContent("Floor Tile",

                                                         "Basic floor/ground tile"));

        EditorGUILayout.PropertyField(wallTileProp, new GUIContent("Wall Tile",

                                                       "Basic wall tile"));



        if (!tilesAssigned)

        {

            DrawWarning("Both Floor and Wall Tiles must be assigned");

        }



        EditorGUILayout.Space(15);



        // Optional: Directional Walls Section

        DrawSectionHeader("Directional Walls (Optional)", null);



        EditorGUILayout.PropertyField(useDirectionalWallsProp,

                               new GUIContent("Use Directional Walls",

                                           "Enable to use different tiles for walls based on orientation"));



        if (useDirectionalWallsProp.boolValue)

        {

            EditorGUI.indentLevel++;



            // Basic Wall Directions

            EditorGUILayout.LabelField("Basic Wall Directions", EditorStyles.boldLabel);

            DrawDirectionalTileField("Bottom Wall", wallTileBottomProp);

            DrawDirectionalTileField("Top Wall", wallTileTopProp);

            DrawDirectionalTileField("Left Wall", wallTileLeftProp);

            DrawDirectionalTileField("Right Wall", wallTileRightProp);



            // Inner Corners (collapsible)

            showInnerCornersFoldout = EditorGUILayout.Foldout(showInnerCornersFoldout, "Inner Corner Tiles", true);

            if (showInnerCornersFoldout)

            {

                DrawDirectionalTileField("Inner Top-Left", wallTileInnerTopLeftProp);

                DrawDirectionalTileField("Inner Top-Right", wallTileInnerTopRightProp);

                DrawDirectionalTileField("Inner Bottom-Left", wallTileInnerBottomLeftProp);

                DrawDirectionalTileField("Inner Bottom-Right", wallTileInnerBottomRightProp);

            }



            // Outer Corners (collapsible)

            showOuterCornersFoldout = EditorGUILayout.Foldout(showOuterCornersFoldout, "Outer Corner Tiles", true);

            if (showOuterCornersFoldout)

            {

                DrawDirectionalTileField("Outer Top-Left", wallTileOuterTopLeftProp);

                DrawDirectionalTileField("Outer Top-Right", wallTileOuterTopRightProp);

                DrawDirectionalTileField("Outer Bottom-Left", wallTileOuterBottomLeftProp);

                DrawDirectionalTileField("Outer Bottom-Right", wallTileOuterBottomRightProp);

            }



            EditorGUI.indentLevel--;

        }



        EditorGUILayout.Space(15);



        // Optional: Tile Variants Section

        DrawSectionHeader("Tile Variants (Optional)", null);



        EditorGUILayout.PropertyField(floorTileVariantsProp,

                               new GUIContent("Floor Variants",

                                           "Additional floor tiles to use randomly"));



        EditorGUILayout.PropertyField(wallTileVariantsProp,

                               new GUIContent("Wall Variants",

                                           "Additional wall tiles to use randomly"));



        EditorGUILayout.Slider(variantTileChanceProp, 0f, 1f,

                            new GUIContent("Variant Chance",

                                         "Chance to use variant tiles (0-1)"));



        // Navigation buttons (bottom)

        EditorGUILayout.Space(15);

        DrawNavigationButtons("← Back: Mode", "Next: Room Settings →",

                            () => { currentTab = 0; }, () => { currentTab = 2; });



        // Mark completion status

        setupComplete[1] = tilemapsAssigned && tilesAssigned;

    }



    private void DrawRoomsTab()

    {

        // Add mode indicator at the top

        DrawModeIndicator();



        // Add top navigation buttons

        EditorGUILayout.BeginHorizontal();

        Color topNavBgColor = GUI.backgroundColor;

        if (GUILayout.Button("← Back: Tiles", GUILayout.Width(150)))

        {

            currentTab = 1;

        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = accentColor;

        if (GUILayout.Button("Next: Entities →", GUILayout.Width(150)))

        {

            currentTab = 3;

        }

        GUI.backgroundColor = topNavBgColor;

        EditorGUILayout.EndHorizontal();



        EditorGUILayout.Space(10);



        // Original content

        EditorGUILayout.LabelField("Step 3: Configure Room Settings", headerStyle);

        EditorGUILayout.Space(5);



        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;



        string helpText = "Configure how rooms are generated and connected. ";

        if (currentMode == GenerationMode.FullyProcedural)

            helpText += "In Fully Procedural mode, these settings control the BSP algorithm.";

        else if (currentMode == GenerationMode.HybridProcedural)

            helpText += "In Hybrid mode, these control BSP, L-shapes, and room templates.";

        else

            helpText += "In User Defined mode, these settings affect corridor generation.";



        EditorGUILayout.HelpBox(helpText, MessageType.Info);

        EditorGUILayout.Space(10);



        // Level Dimensions Section

        DrawSectionHeader("Level Dimensions", null);



        EditorGUILayout.PropertyField(levelWidthProp,

                               new GUIContent("Level Width", "Width of level grid in cells"));

        EditorGUILayout.PropertyField(levelHeightProp,

                               new GUIContent("Level Height", "Height of level grid in cells"));



        EditorGUILayout.Space(10);



        // Random Seed Section

        DrawSectionHeader("Random Seed", null);



        EditorGUILayout.PropertyField(useRandomSeedProp,

                               new GUIContent("Use Random Seed", "Generate new seed on each run"));



        EditorGUI.BeginDisabledGroup(useRandomSeedProp.boolValue);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.PropertyField(seedProp, new GUIContent("Seed Value", "Manual seed value"));



        if (GUILayout.Button("New", GUILayout.Width(50)))

        {

            seedProp.intValue = UnityEngine.Random.Range(1, 999999);

            serializedObject.ApplyModifiedProperties();

            ShowFeedback($"New seed: {seedProp.intValue}", MessageType.Info);

        }



        EditorGUILayout.EndHorizontal();

        EditorGUI.EndDisabledGroup();



        EditorGUILayout.Space(15);



        // Mode-specific settings

        if (currentMode != GenerationMode.UserDefinedLayout)

        {

            // BSP Settings

            DrawSectionHeader("BSP Algorithm Settings", null);



            EditorGUILayout.PropertyField(minRoomSizeProp,

                                   new GUIContent("Min Room Size", "Minimum room dimension"));

            EditorGUILayout.PropertyField(maxIterationsProp,

                                   new GUIContent("BSP Iterations", "Number of BSP split iterations"));

            EditorGUILayout.PropertyField(roomPaddingProp,

                                   new GUIContent("Room Padding", "Space between rooms"));



            EditorGUILayout.Space(15);

        }



        // Hybrid-specific settings

        if (currentMode == GenerationMode.HybridProcedural)

        {

            // Shape Probabilities

            DrawSectionHeader("Room Type Probabilities", null);



            EditorGUILayout.Slider(lShapeProbabilityProp, 0f, 1f,

                                new GUIContent("L-Shape Chance", "Probability for L-shaped rooms"));

            EditorGUILayout.Slider(roomTemplateProbabilityProp, 0f, 1f,

                                new GUIContent("Template Chance", "Probability for template rooms"));



            float totalProb = lShapeProbabilityProp.floatValue + roomTemplateProbabilityProp.floatValue;

            if (totalProb > 1.0f)

            {

                DrawWarning("Combined probabilities exceed 100%. Remaining will be rectangles.");

            }



            // L-Shape Settings

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("L-Shape Settings", EditorStyles.boldLabel);



            EditorGUILayout.Slider(minLLegRatioProp, 0.2f, 0.8f,

                                new GUIContent("Min Leg Ratio", "Minimum ratio of short leg"));

            EditorGUILayout.Slider(maxLLegRatioProp, minLLegRatioProp.floatValue, 0.8f,

                                new GUIContent("Max Leg Ratio", "Maximum ratio of short leg"));



            // Room Templates

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Room Templates", EditorStyles.boldLabel);



            // Show number of valid templates

            int totalTemplates = roomTemplatePrefabsProp.arraySize;

            int validTemplates = CountValidTemplates();



            EditorGUILayout.LabelField($"Template Status: {validTemplates}/{totalTemplates} valid",

                                    EditorStyles.boldLabel);



            // Template list

            if (roomTemplatePrefabsProp.arraySize > 0)

            {

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);



                for (int i = 0; i < roomTemplatePrefabsProp.arraySize; i++)

                {

                    DrawTemplateElement(roomTemplatePrefabsProp.GetArrayElementAtIndex(i), i);

                }



                EditorGUILayout.Space(5);



                if (GUILayout.Button("+ Add Template"))

                {

                    roomTemplatePrefabsProp.arraySize++;

                }



                EditorGUILayout.EndVertical();

            }

            else

            {

                EditorGUILayout.HelpBox("No template prefabs assigned. These should contain a Tilemap component.", MessageType.Info);



                if (GUILayout.Button("+ Add First Template"))

                {

                    roomTemplatePrefabsProp.arraySize = 1;

                }

            }



            EditorGUILayout.Space(15);

        }



        // Corridor Settings for all modes

        DrawSectionHeader("Corridor Settings", null);



        EditorGUILayout.PropertyField(corridorWidthProp,

                               new GUIContent("Corridor Width", "Width of corridors in tiles"));



        // User Defined specific settings

        if (currentMode == GenerationMode.UserDefinedLayout)

        {

            EditorGUILayout.Space(10);

            EditorGUILayout.PropertyField(defaultSceneNodeSizeProp,

                                   new GUIContent("Default Node Size",

                                               "Default size for nodes in Visual Designer"));



            EditorGUILayout.Space(10);

            Color bottomButtonBgColor = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.4f);



            if (GUILayout.Button(new GUIContent(" Open Visual Level Designer",

                                             EditorGUIUtility.IconContent("d_EditCollider").image),

                              GUILayout.Height(30)))

            {

                OpenVisualDesigner();

            }



            GUI.backgroundColor = bottomButtonBgColor;

        }



        // Navigation buttons

        EditorGUILayout.Space(15);

        DrawNavigationButtons("← Back: Tiles", "Next: Entities →",

                            () => { currentTab = 1; }, () => { currentTab = 3; });



        // Mark completion status - valid settings

        setupComplete[2] = (levelWidthProp.intValue > 0 && levelHeightProp.intValue > 0);

    }



    private void DrawEntitiesTab()

    {

        // Add mode indicator at the top

        DrawModeIndicator();



        // Add top navigation buttons

        EditorGUILayout.BeginHorizontal();

        Color topNavBgColor = GUI.backgroundColor;

        if (GUILayout.Button("← Back: Rooms", GUILayout.Width(150)))

        {

            currentTab = 2;

        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = accentColor;

        if (GUILayout.Button("Next: Generate →", GUILayout.Width(150)))

        {

            currentTab = 4;

        }

        GUI.backgroundColor = topNavBgColor;

        EditorGUILayout.EndHorizontal();



        EditorGUILayout.Space(10);



        // Original content

        EditorGUILayout.LabelField("Step 4: Configure Entities", headerStyle);

        EditorGUILayout.Space(5);



        EditorGUILayout.HelpBox("Set up the entities that will populate your level. The player prefab is required, while enemies and decorations are optional.", MessageType.Info);

        EditorGUILayout.Space(10);



        // Auto-assign button for entities

        Color buttonBgColor = GUI.backgroundColor;

        GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);



        if (GUILayout.Button(new GUIContent(" Auto-Assign Entity Prefabs",

                                         EditorGUIUtility.IconContent("d_Refresh").image),

                          GUILayout.Height(30)))

        {

            AutoAssignEntityPrefabs();

        }



        GUI.backgroundColor = buttonBgColor;

        EditorGUILayout.Space(15);



        // Player Section

        bool playerAssigned = playerPrefabProp.objectReferenceValue != null;

        DrawSectionHeader("Player (Required)",

                        playerAssigned ? successIconTexture : warningIconTexture);



        EditorGUILayout.PropertyField(playerPrefabProp,

                               new GUIContent("Player Prefab", "Player character prefab"));



        if (!playerAssigned)

        {

            DrawWarning("Player prefab must be assigned");

        }



        EditorGUILayout.Space(15);



        // Enemies Section

        DrawSectionHeader("Enemies (Optional)", null);



        EditorGUILayout.PropertyField(enemyPrefabProp,

                               new GUIContent("Enemy Prefab", "Enemy character prefab"));

        EditorGUILayout.PropertyField(enemiesPerRoomProp,

                               new GUIContent("Enemies Per Room", "Maximum enemies per room"));



        EditorGUILayout.Space(15);



        // Decorations Section

        DrawSectionHeader("Decorations (Optional)", null);



        EditorGUILayout.PropertyField(decorationPrefabProp,

                               new GUIContent("Decoration Prefab", "Decoration object prefab"));

        EditorGUILayout.PropertyField(decorationsPerRoomProp,

                               new GUIContent("Decorations Per Room", "Maximum decorations per room"));



        // Navigation buttons

        EditorGUILayout.Space(15);

        DrawNavigationButtons("← Back: Rooms", "Next: Generate →",

                            () => { currentTab = 2; }, () => { currentTab = 4; });



        // Mark completion status

        setupComplete[3] = playerPrefabProp.objectReferenceValue != null;

    }



    private void DrawGenerateTab()

    {

        // Add mode indicator at the top

        DrawModeIndicator();



        // Add top navigation buttons

        EditorGUILayout.BeginHorizontal();

        Color topNavBgColor = GUI.backgroundColor;

        if (GUILayout.Button("← Back: Entities", GUILayout.Width(150)))

        {

            currentTab = 3;

        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = topNavBgColor;

        EditorGUILayout.EndHorizontal();



        EditorGUILayout.Space(10);



        // Original content

        EditorGUILayout.LabelField("Step 5: Generate Your Level", headerStyle);

        EditorGUILayout.Space(5);



        bool allRequiredComplete = completedAllMandatory();



        if (allRequiredComplete)

        {

            GUIStyle successBox = new GUIStyle(EditorStyles.helpBox);

            successBox.normal.textColor = successColor;



            EditorGUILayout.BeginHorizontal(successBox);

            Rect iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24));

            GUI.DrawTexture(iconRect, successIconTexture);

            EditorGUILayout.LabelField("All setup steps complete! You're ready to generate your level.",

                                    GUILayout.ExpandWidth(true));

            EditorGUILayout.EndHorizontal();

        }

        else

        {

            EditorGUILayout.HelpBox("Please complete all required setup steps before generating.",

                                 MessageType.Warning);



            // Show missing requirements

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Missing Requirements:", EditorStyles.boldLabel);



            if (!setupComplete[0]) EditorGUILayout.LabelField("• Select a generation mode");

            if (!setupComplete[1])

            {

                EditorGUILayout.LabelField("• Assign required tilemaps and tiles");

                if (GUILayout.Button("Go to Tiles Tab")) currentTab = 1;

            }

            if (!setupComplete[2])

            {

                EditorGUILayout.LabelField("• Configure level dimensions");

                if (GUILayout.Button("Go to Rooms Tab")) currentTab = 2;

            }

            if (!setupComplete[3])

            {

                EditorGUILayout.LabelField("• Assign player prefab");

                if (GUILayout.Button("Go to Entities Tab")) currentTab = 3;

            }



            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

        }



        // Summary of current settings

        EditorGUILayout.LabelField("Generation Summary", subHeaderStyle);



        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;

        EditorGUILayout.LabelField($"• Mode: {GetModeDisplayName(currentMode)}");

        EditorGUILayout.LabelField($"• Size: {levelWidthProp.intValue}×{levelHeightProp.intValue} cells");

        EditorGUILayout.LabelField($"• Seed: {(useRandomSeedProp.boolValue ? "Random" : seedProp.intValue.ToString())}");



        // Display mode-specific settings

        if (currentMode == GenerationMode.FullyProcedural)

        {

            EditorGUILayout.LabelField($"• BSP Iterations: {maxIterationsProp.intValue}");

            EditorGUILayout.LabelField($"• Min Room Size: {minRoomSizeProp.intValue}");

        }

        else if (currentMode == GenerationMode.HybridProcedural)

        {

            EditorGUILayout.LabelField($"• L-Shape Chance: {lShapeProbabilityProp.floatValue:P0}");

            EditorGUILayout.LabelField($"• Template Chance: {roomTemplateProbabilityProp.floatValue:P0}");

            EditorGUILayout.LabelField($"• Templates: {CountValidTemplates()} valid");

        }



        // Generate button

        EditorGUILayout.Space(20);

        Color btnColor = GUI.backgroundColor;

        GUI.backgroundColor = allRequiredComplete ? new Color(0.2f, 0.7f, 0.3f) : Color.gray;



        GUIStyle generateStyle = new GUIStyle(GUI.skin.button);

        generateStyle.fontSize = 14;

        generateStyle.fontStyle = FontStyle.Bold;

        generateStyle.padding = new RectOffset(20, 20, 10, 10);



        EditorGUI.BeginDisabledGroup(!allRequiredComplete);

        if (GUILayout.Button(new GUIContent(" Generate Level",

                                         EditorGUIUtility.IconContent("d_PlayButton On").image),

                          generateStyle, GUILayout.Height(50)))

        {

            GenerateLevel();

        }

        EditorGUI.EndDisabledGroup();



        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);

        if (GUILayout.Button(new GUIContent(" Clear Level",

                                         EditorGUIUtility.IconContent("d_TreeEditor.Trash").image),

                          GUILayout.Height(30)))

        {

            if (EditorUtility.DisplayDialog("Confirm Clear",

                                         "Clear all generated level content?",

                                         "Clear", "Cancel"))

            {

                ClearLevel();

            }

        }



        GUI.backgroundColor = btnColor;



        // Navigation button

        EditorGUILayout.Space(15);

        DrawNavigationButtons("← Back: Entities", null, () => { currentTab = 3; }, null);

    }



    #endregion


    #region HELPER METHODS
    // Add this method around line 925 in your HELPER METHODS region

    private void DrawModeIndicator()

    {

        GenerationMode currentMode = (GenerationMode)generationModeProp.enumValueIndex;

        string modeName = GetModeDisplayName(currentMode);



        // Get the appropriate color for the selected mode

        Color modeColor;

        switch (currentMode)

        {

            case GenerationMode.FullyProcedural:

                modeColor = new Color(0.2f, 0.6f, 0.9f, 0.8f);

                break;

            case GenerationMode.HybridProcedural:

                modeColor = new Color(0.9f, 0.6f, 0.2f, 0.8f);

                break;

            case GenerationMode.UserDefinedLayout:

                modeColor = new Color(0.2f, 0.7f, 0.4f, 0.8f);

                break;

            default:

                modeColor = Color.gray;

                break;

        }



        // Create a style for the mode indicator

        GUIStyle modeStyle = new GUIStyle(EditorStyles.helpBox);

        modeStyle.fontStyle = FontStyle.Bold;

        modeStyle.alignment = TextAnchor.MiddleLeft;

        modeStyle.padding = new RectOffset(10, 10, 8, 8);



        Color originalBg = GUI.backgroundColor;

        GUI.backgroundColor = modeColor;



        EditorGUILayout.BeginHorizontal(modeStyle);

        EditorGUILayout.LabelField($"Current Mode: {modeName}", EditorStyles.boldLabel);

        GUI.backgroundColor = originalBg;

        EditorGUILayout.EndHorizontal();



        EditorGUILayout.Space(5);

    }

    private void DrawSectionHeader(string title, Texture2D statusIcon)
    {
        EditorGUILayout.BeginHorizontal();



        // Title with bold style

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 12;
        EditorGUILayout.LabelField(title, headerStyle);



        // Status icon if provided

        if (statusIcon != null)
        {
            GUILayout.FlexibleSpace();
            Rect iconRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
            GUI.DrawTexture(iconRect, statusIcon);
        }



        EditorGUILayout.EndHorizontal();



        // Underline

        Rect lineRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,

                                              GUILayout.Height(1), GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(lineRect, new Color(0.5f, 0.5f, 0.5f, 0.5f));



        EditorGUILayout.Space(5);
    }



    private void DrawWarning(string message)
    {
        EditorGUILayout.BeginHorizontal(warningStyle);



        Rect iconRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
        GUI.DrawTexture(iconRect, warningIconTexture);



        EditorGUILayout.LabelField(message, GUILayout.ExpandWidth(true));



        EditorGUILayout.EndHorizontal();
    }



    private void DrawNavigationButtons(string backText, string nextText,

                                    System.Action onBack, System.Action onNext)
    {
        EditorGUILayout.BeginHorizontal();



        if (backText != null && onBack != null)
        {
            if (GUILayout.Button(backText, GUILayout.Width(150)))
            {
                onBack.Invoke();
            }
        }



        GUILayout.FlexibleSpace();



        if (nextText != null && onNext != null)
        {
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = accentColor;



            if (GUILayout.Button(nextText, GUILayout.Width(150)))
            {
                onNext.Invoke();
            }



            GUI.backgroundColor = originalBg;
        }



        EditorGUILayout.EndHorizontal();
    }



    private void DrawDirectionalTileField(string label, SerializedProperty directionalTileProp)
    {
        EditorGUILayout.BeginHorizontal();



        SerializedProperty tileProp = directionalTileProp.FindPropertyRelative("tile");
        SerializedProperty rotationProp = directionalTileProp.FindPropertyRelative("rotation");



        // Tile field

        EditorGUILayout.PropertyField(tileProp, new GUIContent(label),

                                   GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.6f));



        // Rotation field

        EditorGUILayout.PropertyField(rotationProp, GUIContent.none,

                                   GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.2f));



        EditorGUILayout.EndHorizontal();
    }



    private void DrawTemplateElement(SerializedProperty templateProp, int index)
    {
        EditorGUILayout.BeginHorizontal();



        // Get template reference

        GameObject prefabRef = templateProp.objectReferenceValue as GameObject;
        bool isValid = IsValidTemplate(prefabRef);



        // Status indicator

        Rect indicatorRect = GUILayoutUtility.GetRect(16, 16,

                                                   GUILayout.Width(16), GUILayout.ExpandWidth(false));
        GUI.DrawTexture(indicatorRect, isValid ? validTexture : invalidTexture);



        // Template field

        EditorGUILayout.PropertyField(templateProp, GUIContent.none);



        // Delete button

        if (GUILayout.Button("×", GUILayout.Width(20)))
        {
            roomTemplatePrefabsProp.DeleteArrayElementAtIndex(index);
        }



        EditorGUILayout.EndHorizontal();



        // Show validation warning

        if (!isValid && prefabRef != null)
        {
            EditorGUILayout.HelpBox("Invalid template. Must contain a Tilemap component.",

                                 MessageType.Warning);
        }
    }



    private void DrawFeedbackMessage()
    {
        GUIStyle style;
        Texture2D icon;



        // Determine style and icon based on message type

        switch (feedbackType)
        {
            case MessageType.Info:
                style = new GUIStyle(EditorStyles.helpBox);
                icon = infoIconTexture;
                break;
            case MessageType.Warning:
                style = new GUIStyle(EditorStyles.helpBox);
                style.normal.textColor = warningColor;
                icon = warningIconTexture;
                break;
            case MessageType.Error:
                style = new GUIStyle(EditorStyles.helpBox);
                style.normal.textColor = errorColor;
                icon = EditorGUIUtility.IconContent("console.erroricon").image as Texture2D;
                break;
            default:
                style = new GUIStyle(EditorStyles.helpBox);
                icon = infoIconTexture;
                break;
        }



        // Draw feedback box

        EditorGUILayout.BeginHorizontal(style);



        Rect iconRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
        GUI.DrawTexture(iconRect, icon);



        EditorGUILayout.LabelField(feedbackMessage, GUILayout.ExpandWidth(true));



        // Close button

        if (GUILayout.Button("×", GUILayout.Width(20)))
        {
            showFeedback = false;
        }



        EditorGUILayout.EndHorizontal();
    }



    private void ShowFeedback(string message, MessageType type, float duration = 3.5f)
    {
        feedbackMessage = message;
        feedbackType = type;
        showFeedback = true;
        feedbackExpireTime = EditorApplication.timeSinceStartup + duration;
        Repaint();
    }



    private void UpdateSetupProgress()
    {
        // Update setup progress for each step
        HybridLevelGenerator generator = (HybridLevelGenerator)target;



        // Step 1: Mode selection - always considered complete

        setupComplete[0] = true;



        // Step 2: Tiles & Tilemaps

        setupComplete[1] = generator.groundTilemap != null &&

                         generator.wallTilemap != null &&

                         generator.floorTile != null &&

                         generator.wallTile != null;



        // Step 3: Room settings

        setupComplete[2] = generator.levelWidth > 0 && generator.levelHeight > 0;



        // Step 4: Entities

        setupComplete[3] = generator.playerPrefab != null;



        // Set current tab to first incomplete step if not already done

        if (currentTab < 4)
        {
            for (int i = 0; i < setupComplete.Length; i++)
            {
                if (!setupComplete[i])
                {
                    currentTab = i;
                    break;
                }
            }
        }
    }



    private bool completedAllMandatory()
    {
        return setupComplete[0] && setupComplete[1] && setupComplete[2] && setupComplete[3];
    }



    private void GenerateLevel()
    {
        HybridLevelGenerator generator = (HybridLevelGenerator)target;



        // Skip clear for user defined mode

        bool skipClear = generator.generationMode == GenerationMode.UserDefinedLayout;



        try
        {
            Undo.RecordObject(generator, "Generate Level");
            generator.GenerateLevel(skipClear);
            ShowFeedback("Level generation complete!", MessageType.Info);
            MarkSceneDirty(generator);
        }
        catch (Exception e)
        {
            ShowFeedback($"Error generating level: {e.Message}", MessageType.Error);
            Debug.LogException(e);
        }
    }



    private void ClearLevel()
    {
        HybridLevelGenerator generator = (HybridLevelGenerator)target;



        try
        {
            Undo.RecordObject(generator, "Clear Level");
            generator.ClearLevel();
            ShowFeedback("Level cleared successfully", MessageType.Info);
            MarkSceneDirty(generator);
        }
        catch (Exception e)
        {
            ShowFeedback($"Error clearing level: {e.Message}", MessageType.Error);
            Debug.LogException(e);
        }
    }



    private void OpenVisualDesigner()
    {
        var window = EditorWindow.GetWindow<VisualLevelDesignEditor>("Visual Level Designer");
        window.Show();
        window.Focus();
        ShowFeedback("Visual Level Designer opened", MessageType.Info);
    }



    private int CountValidTemplates()
    {
        int validCount = 0;



        for (int i = 0; i < roomTemplatePrefabsProp.arraySize; i++)
        {
            SerializedProperty templateProp = roomTemplatePrefabsProp.GetArrayElementAtIndex(i);
            GameObject prefab = templateProp.objectReferenceValue as GameObject;



            if (IsValidTemplate(prefab))
            {
                validCount++;
            }
        }



        return validCount;
    }



    private bool IsValidTemplate(GameObject prefab)
    {
        if (prefab == null) return false;



        // Template must have a Tilemap component

        var tilemaps = prefab.GetComponentsInChildren<Tilemap>(true);
        return tilemaps != null && tilemaps.Length > 0;
    }



    private string GetModeDisplayName(GenerationMode mode)
    {
        switch (mode)
        {
            case GenerationMode.FullyProcedural: return "Fully Procedural";
            case GenerationMode.HybridProcedural: return "Hybrid Procedural";
            case GenerationMode.UserDefinedLayout: return "User Defined Layout";
            default: return mode.ToString();
        }
    }



    private void MarkSceneDirty(HybridLevelGenerator generator)
    {
        if (!Application.isPlaying && generator != null && generator.gameObject != null)
        {
            if (generator.gameObject.scene != null &&

                generator.gameObject.scene.IsValid() &&

                generator.gameObject.scene.isLoaded)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
            }
        }
    }



    #endregion


    #region ASSET INITIALIZATION METHODS

    private bool TryAssignDirectionalWallTiles()
    {
        bool anyAssigned = false;

        // Array of tile searches to perform
        var directionalTileSearches = new[]
        {
        // Basic wall directions
        new { PropertyPath = "wallTileBottom.tile", SearchTerms = new[] { "wallbottom", "wall_bottom", "wall-bottom", "bottomwall" } },
        new { PropertyPath = "wallTileTop.tile", SearchTerms = new[] { "walltop", "wall_top", "wall-top", "topwall" } },
        new { PropertyPath = "wallTileLeft.tile", SearchTerms = new[] { "wallleft", "wall_left", "wall-left", "leftwall" } },
        new { PropertyPath = "wallTileRight.tile", SearchTerms = new[] { "wallright", "wall_right", "wall-right", "rightwall" } },
        
        // Inner corners
        new { PropertyPath = "wallTileInnerTopLeft.tile", SearchTerms = new[] { "wallinnertopleft", "wall_inner_top_left", "inner_corner_top_left" } },
        new { PropertyPath = "wallTileInnerTopRight.tile", SearchTerms = new[] { "wallinnertopright", "wall_inner_top_right", "inner_corner_top_right" } },
        new { PropertyPath = "wallTileInnerBottomLeft.tile", SearchTerms = new[] { "wallinnerbottomleft", "wall_inner_bottom_left", "inner_corner_bottom_left" } },
        new { PropertyPath = "wallTileInnerBottomRight.tile", SearchTerms = new[] { "wallinnerbottomright", "wall_inner_bottom_right", "inner_corner_bottom_right" } },
        
        // Outer corners
        new { PropertyPath = "wallTileOuterTopLeft.tile", SearchTerms = new[] { "walloutertopleft", "wall_outer_top_left", "outer_corner_top_left" } },
        new { PropertyPath = "wallTileOuterTopRight.tile", SearchTerms = new[] { "walloutertopright", "wall_outer_top_right", "outer_corner_top_right" } },
        new { PropertyPath = "wallTileOuterBottomLeft.tile", SearchTerms = new[] { "wallouterbottomleft", "wall_outer_bottom_left", "outer_corner_bottom_left" } },
        new { PropertyPath = "wallTileOuterBottomRight.tile", SearchTerms = new[] { "wallouterbottomright", "wall_outer_bottom_right", "outer_corner_bottom_right" } },
    };

        foreach (var search in directionalTileSearches)
        {
            SerializedProperty prop = serializedObject.FindProperty(search.PropertyPath);
            if (prop == null) continue;

            foreach (string term in search.SearchTerms)
            {
                TileBase tile = FindTileAsset(term);
                if (tile != null)
                {
                    prop.objectReferenceValue = tile;
                    anyAssigned = true;
                    Debug.Log($"Assigned {search.PropertyPath}: {tile.name}");
                    break;
                }
            }
        }

        return anyAssigned;
    }

    private bool ProcessDirectionalTilesFolder(string baseFolderPath)
    {
        bool anyAssigned = false;

        // Look for specific subfolders
        string basicWallDirPath = Path.Combine(baseFolderPath, "Basic Wall Directions").Replace('\\', '/');
        string innerCornerPath = Path.Combine(baseFolderPath, "Inner Corner Tiles").Replace('\\', '/');
        string outerCornerPath = Path.Combine(baseFolderPath, "Outer Corner Tiles").Replace('\\', '/');

        // Check if these folders exist
        bool basicExists = AssetDatabase.IsValidFolder(basicWallDirPath);
        bool innerExists = AssetDatabase.IsValidFolder(innerCornerPath);
        bool outerExists = AssetDatabase.IsValidFolder(outerCornerPath);

        Debug.Log($"Basic Wall Directions folder exists: {basicExists} - {basicWallDirPath}");
        Debug.Log($"Inner Corner Tiles folder exists: {innerExists} - {innerCornerPath}");
        Debug.Log($"Outer Corner Tiles folder exists: {outerExists} - {outerCornerPath}");

        // Process Basic Wall Directions
        if (basicExists)
        {
            anyAssigned |= TryAssignTilesFromFolder(basicWallDirPath, new Dictionary<string, SerializedProperty>
        {
            {"bottom", wallTileBottomProp.FindPropertyRelative("tile")},
            {"top", wallTileTopProp.FindPropertyRelative("tile")},
            {"left", wallTileLeftProp.FindPropertyRelative("tile")},
            {"right", wallTileRightProp.FindPropertyRelative("tile")}
        });
        }

        // Process Inner Corner Tiles
        if (innerExists)
        {
            anyAssigned |= TryAssignTilesFromFolder(innerCornerPath, new Dictionary<string, SerializedProperty>
        {
            {"topleft", wallTileInnerTopLeftProp.FindPropertyRelative("tile")},
            {"topright", wallTileInnerTopRightProp.FindPropertyRelative("tile")},
            {"bottomleft", wallTileInnerBottomLeftProp.FindPropertyRelative("tile")},
            {"bottomright", wallTileInnerBottomRightProp.FindPropertyRelative("tile")}
        });
        }

        // Process Outer Corner Tiles  
        if (outerExists)
        {
            anyAssigned |= TryAssignTilesFromFolder(outerCornerPath, new Dictionary<string, SerializedProperty>
        {
            {"topleft", wallTileOuterTopLeftProp.FindPropertyRelative("tile")},
            {"topright", wallTileOuterTopRightProp.FindPropertyRelative("tile")},
            {"bottomleft", wallTileOuterBottomLeftProp.FindPropertyRelative("tile")},
            {"bottomright", wallTileOuterBottomRightProp.FindPropertyRelative("tile")}
        });
        }

        return anyAssigned;
    }
    private void InitializeAssets()
    {
        // Get the target reference
        HybridLevelGenerator generator = (HybridLevelGenerator)target;
        Undo.RecordObject(generator, "Initialize Assets");
        bool anyAssetsAssigned = false;

        Debug.Log("=== PCG LEVEL GENERATOR: INITIALIZING ASSETS ===");

        // 1. Find and assign Tilemaps in the scene
        if (generator.groundTilemap == null || generator.wallTilemap == null)
        {
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            foreach (Tilemap tilemap in tilemaps)
            {
                if (generator.groundTilemap == null &&
                    (tilemap.name.ToLower().Contains("ground") || tilemap.name.ToLower().Contains("floor")))
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

        // 2. Find and assign basic floor and wall tiles
        if (generator.floorTile == null || generator.wallTile == null)
        {
            // Look for tiles in the project
            string[] tileGuids = AssetDatabase.FindAssets("t:TileBase");

            foreach (string guid in tileGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path).ToLower();
                string directory = Path.GetDirectoryName(path).Replace('\\', '/');

                // Skip directional tiles for main wall tile assignment
                bool isDirectionalTile =
                    directory.Contains("Directional Tiles") ||
                    directory.Contains("Basic Wall Directions") ||
                    directory.Contains("Inner Corner Tiles") ||
                    directory.Contains("Outer Corner Tiles") ||
                    fileName.Contains("left") || fileName.Contains("right") ||
                    fileName.Contains("top") || fileName.Contains("bottom") ||
                    fileName.Contains("inner") || fileName.Contains("outer") ||
                    fileName.Contains("corner");

                // Assign floor tile
                if (generator.floorTile == null && fileName.Contains("floor"))
                {
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile != null)
                    {
                        generator.floorTile = tile;
                        anyAssetsAssigned = true;
                        Debug.Log($"Assigned Floor Tile: {tile.name} from {path}");
                    }
                }

                // Assign non-directional wall tile
                if (generator.wallTile == null && fileName.Contains("wall") && !isDirectionalTile)
                {
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile != null)
                    {
                        generator.wallTile = tile;
                        anyAssetsAssigned = true;
                        Debug.Log($"Assigned Wall Tile: {tile.name} from {path}");
                    }
                }
            }
        }

        // 3. Find and assign directional wall tiles
        bool directionalTilesAssigned = false;

        // Enable directional walls if we're assigning any
        generator.useDirectionalWalls = true;

        // Try the direct path approach with detailed logging
        string baseFolderPath = "Assets/PCGLevelGenerator/Tiles/Directional Tiles";
        Debug.Log($"Checking directional tiles base path: {baseFolderPath}, exists: {AssetDatabase.IsValidFolder(baseFolderPath)}");

        string basicWallDirPath = baseFolderPath + "/Basic Wall Directions";
        string innerCornerPath = baseFolderPath + "/Inner Corner Tiles";
        string outerCornerPath = baseFolderPath + "/Outer Corner Tiles";

        Debug.Log($"Checking Basic Wall Directions: {basicWallDirPath}, exists: {AssetDatabase.IsValidFolder(basicWallDirPath)}");
        Debug.Log($"Checking Inner Corner Tiles: {innerCornerPath}, exists: {AssetDatabase.IsValidFolder(innerCornerPath)}");
        Debug.Log($"Checking Outer Corner Tiles: {outerCornerPath}, exists: {AssetDatabase.IsValidFolder(outerCornerPath)}");

        // First try exact path matching
        if (AssetDatabase.IsValidFolder(baseFolderPath))
        {
            // Process Basic Wall Directions
            if (AssetDatabase.IsValidFolder(basicWallDirPath))
            {
                Debug.Log($"Found Basic Wall Directions folder: {basicWallDirPath}");

                // List all tile assets in this folder
                string[] basicWallTiles = AssetDatabase.FindAssets("t:TileBase", new[] { basicWallDirPath });
                Debug.Log($"Found {basicWallTiles.Length} tile assets in Basic Wall Directions folder");

                foreach (string tileGuid in basicWallTiles)
                {
                    string tilePath = AssetDatabase.GUIDToAssetPath(tileGuid);
                    string tileName = Path.GetFileNameWithoutExtension(tilePath).ToLower();
                    Debug.Log($"Basic Wall Directions tile: {tileName} at {tilePath}");

                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);

                    if (tile != null)
                    {
                        // Assign based on name
                        if (tileName.Contains("bottom"))
                        {
                            wallTileBottomProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileBottomProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Bottom Wall: {tile.name}");
                        }
                        else if (tileName.Contains("top"))
                        {
                            wallTileTopProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileTopProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Top Wall: {tile.name}");
                        }
                        else if (tileName.Contains("left"))
                        {
                            wallTileLeftProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileLeftProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Left Wall: {tile.name}");
                        }
                        else if (tileName.Contains("right"))
                        {
                            wallTileRightProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileRightProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Right Wall: {tile.name}");
                        }
                    }
                }
            }

            // Process Inner Corner Tiles
            if (AssetDatabase.IsValidFolder(innerCornerPath))
            {
                Debug.Log($"Found Inner Corner Tiles folder: {innerCornerPath}");

                string[] innerCornerTiles = AssetDatabase.FindAssets("t:TileBase", new[] { innerCornerPath });
                Debug.Log($"Found {innerCornerTiles.Length} tile assets in Inner Corner Tiles folder");

                foreach (string tileGuid in innerCornerTiles)
                {
                    string tilePath = AssetDatabase.GUIDToAssetPath(tileGuid);
                    string tileName = Path.GetFileNameWithoutExtension(tilePath).ToLower();
                    Debug.Log($"Inner Corner tile: {tileName} at {tilePath}");

                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);

                    if (tile != null)
                    {
                        // Assign based on name
                        if (tileName.Contains("topleft") || (tileName.Contains("top") && tileName.Contains("left")))
                        {
                            wallTileInnerTopLeftProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileInnerTopLeftProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Inner Top Left: {tile.name}");
                        }
                        else if (tileName.Contains("topright") || (tileName.Contains("top") && tileName.Contains("right")))
                        {
                            wallTileInnerTopRightProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileInnerTopRightProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Inner Top Right: {tile.name}");
                        }
                        else if (tileName.Contains("bottomleft") || (tileName.Contains("bottom") && tileName.Contains("left")))
                        {
                            wallTileInnerBottomLeftProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileInnerBottomLeftProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Inner Bottom Left: {tile.name}");
                        }
                        else if (tileName.Contains("bottomright") || (tileName.Contains("bottom") && tileName.Contains("right")))
                        {
                            wallTileInnerBottomRightProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileInnerBottomRightProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Inner Bottom Right: {tile.name}");
                        }
                    }
                }
            }

            // Process Outer Corner Tiles
            if (AssetDatabase.IsValidFolder(outerCornerPath))
            {
                Debug.Log($"Found Outer Corner Tiles folder: {outerCornerPath}");

                string[] outerCornerTiles = AssetDatabase.FindAssets("t:TileBase", new[] { outerCornerPath });
                Debug.Log($"Found {outerCornerTiles.Length} tile assets in Outer Corner Tiles folder");

                foreach (string tileGuid in outerCornerTiles)
                {
                    string tilePath = AssetDatabase.GUIDToAssetPath(tileGuid);
                    string tileName = Path.GetFileNameWithoutExtension(tilePath).ToLower();
                    Debug.Log($"Outer Corner tile: {tileName} at {tilePath}");

                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);

                    if (tile != null)
                    {
                        // Assign based on name
                        if (tileName.Contains("topleft") || (tileName.Contains("top") && tileName.Contains("left")))
                        {
                            wallTileOuterTopLeftProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileOuterTopLeftProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Outer Top Left: {tile.name}");
                        }
                        else if (tileName.Contains("topright") || (tileName.Contains("top") && tileName.Contains("right")))
                        {
                            wallTileOuterTopRightProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileOuterTopRightProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Outer Top Right: {tile.name}");
                        }
                        else if (tileName.Contains("bottomleft") || (tileName.Contains("bottom") && tileName.Contains("left")))
                        {
                            wallTileOuterBottomLeftProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileOuterBottomLeftProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Outer Bottom Left: {tile.name}");
                        }
                        else if (tileName.Contains("bottomright") || (tileName.Contains("bottom") && tileName.Contains("right")))
                        {
                            wallTileOuterBottomRightProp.FindPropertyRelative("tile").objectReferenceValue = tile;
                            wallTileOuterBottomRightProp.FindPropertyRelative("rotation").intValue = 0;
                            directionalTilesAssigned = true;
                            Debug.Log($"Assigned Outer Bottom Right: {tile.name}");
                        }
                    }
                }
            }
        }

        // If specific folder structure wasn't found, fall back to other methods
        if (!directionalTilesAssigned)
        {
            Debug.Log("Falling back to general tile name search...");
            directionalTilesAssigned = TryAssignDirectionalWallTiles();
        }

        // Set useDirectionalWalls based on if we found any
        generator.useDirectionalWalls = directionalTilesAssigned;

        if (directionalTilesAssigned)
        {
            anyAssetsAssigned = true;
            Debug.Log("Successfully assigned directional wall tiles!");

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Force repaint of inspector window
            Repaint();


        }

        // 4. Find and assign entity prefabs
        bool entityAssetsAssigned = AutoAssignEntityPrefabs();
        anyAssetsAssigned |= entityAssetsAssigned;

        // Update the serialized object to reflect changes
        serializedObject.Update();
        if (anyAssetsAssigned)
        {
            // Apply modified properties
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(generator);
            MarkSceneDirty(generator);

            // Add these additional lines
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Repaint();

            ShowFeedback("Assets initialized successfully!", MessageType.Info);
        }
        // Save changes
        if (anyAssetsAssigned)
        {
            // Apply modified properties
            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(generator);
            MarkSceneDirty(generator);
            ShowFeedback("Assets initialized successfully!", MessageType.Info);
        }
        else
        {
            ShowFeedback("No assets were found to assign. Check project structure.", MessageType.Warning);
        }
    }

    // Add this new method to help with the specific folder structure
    private bool FindAndAssignDirectionalTiles()
    {
        bool anyAssigned = false;

        // Look for the specific folder structure
        string[] possibleBasePaths = new string[]
        {
        "Assets/Tiles/Directional Tiles",
        "Assets/PCGLevelGenerator/Tiles/Directional Tiles",
        "Assets/Tiles",
        "Assets/PCGLevelGenerator/Tiles"
        };

        string directionalTilesBasePath = null;
        foreach (string path in possibleBasePaths)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                directionalTilesBasePath = path;
                Debug.Log($"Found directional tiles base path: {directionalTilesBasePath}");
                break;
            }
        }

        if (directionalTilesBasePath == null)
        {
            Debug.LogWarning("Could not find directional tiles folder. Please check your project structure.");
            return false;
        }

        // Now look for specific subfolders
        string basicDirPath = Path.Combine(directionalTilesBasePath, "Basic Wall Directions").Replace('\\', '/');
        string innerCornerPath = Path.Combine(directionalTilesBasePath, "Inner Corner Tiles").Replace('\\', '/');
        string outerCornerPath = Path.Combine(directionalTilesBasePath, "Outer Corner Tiles").Replace('\\', '/');

        Debug.Log($"Looking for Basic Wall Directions at: {basicDirPath}");
        Debug.Log($"Looking for Inner Corner Tiles at: {innerCornerPath}");
        Debug.Log($"Looking for Outer Corner Tiles at: {outerCornerPath}");

        // Process Basic Wall Directions
        if (AssetDatabase.IsValidFolder(basicDirPath))
        {
            anyAssigned |= AssignTilesFromDirectionalFolder(basicDirPath, new Dictionary<string, SerializedProperty>
        {
            {"bottom", wallTileBottomProp.FindPropertyRelative("tile")},
            {"top", wallTileTopProp.FindPropertyRelative("tile")},
            {"left", wallTileLeftProp.FindPropertyRelative("tile")},
            {"right", wallTileRightProp.FindPropertyRelative("tile")}
        });
        }

        // Process Inner Corner Tiles
        if (AssetDatabase.IsValidFolder(innerCornerPath))
        {
            anyAssigned |= AssignTilesFromDirectionalFolder(innerCornerPath, new Dictionary<string, SerializedProperty>
        {
            {"topleft", wallTileInnerTopLeftProp.FindPropertyRelative("tile")},
            {"topright", wallTileInnerTopRightProp.FindPropertyRelative("tile")},
            {"bottomleft", wallTileInnerBottomLeftProp.FindPropertyRelative("tile")},
            {"bottomright", wallTileInnerBottomRightProp.FindPropertyRelative("tile")}
        });
        }

        // Process Outer Corner Tiles
        if (AssetDatabase.IsValidFolder(outerCornerPath))
        {
            anyAssigned |= AssignTilesFromDirectionalFolder(outerCornerPath, new Dictionary<string, SerializedProperty>
        {
            {"topleft", wallTileOuterTopLeftProp.FindPropertyRelative("tile")},
            {"topright", wallTileOuterTopRightProp.FindPropertyRelative("tile")},
            {"bottomleft", wallTileOuterBottomLeftProp.FindPropertyRelative("tile")},
            {"bottomright", wallTileOuterBottomRightProp.FindPropertyRelative("tile")}
        });
        }

        return anyAssigned;
    }

    // Helper method to assign tiles from a specific directional folder
    private bool AssignTilesFromDirectionalFolder(string folderPath, Dictionary<string, SerializedProperty> propertyMap)
    {
        bool anyAssigned = false;
        string[] tileGuids = AssetDatabase.FindAssets("t:TileBase", new[] { folderPath });

        Debug.Log($"Found {tileGuids.Length} tile assets in {folderPath}");

        foreach (string guid in tileGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

            foreach (var mapping in propertyMap)
            {
                string key = mapping.Key.ToLower();

                // Check if file name contains the direction keyword
                if (fileName.Contains(key))
                {
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile != null)
                    {
                        mapping.Value.objectReferenceValue = tile;
                        anyAssigned = true;
                        Debug.Log($"Assigned {key} tile: {tile.name} from {path}");
                    }
                }
            }
        }

        return anyAssigned;
    }



    private bool TryAssignTilesFromFolder(string folderPath, Dictionary<string, SerializedProperty> propertyMap)
{
    bool anyAssigned = false;
    string[] tileGuids = AssetDatabase.FindAssets("t:TileBase", new[] { folderPath });

    Debug.Log($"Searching for tiles in folder: {folderPath}. Found {tileGuids.Length} tile assets.");

    foreach (string guid in tileGuids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

        foreach (var mapping in propertyMap)
        {
            string key = mapping.Key.ToLower();
            if (fileName.Contains(key))
            {
                TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile != null)
                {
                    mapping.Value.objectReferenceValue = tile;
                    anyAssigned = true;
                    Debug.Log($"Assigned tile from folder: {tile.name} to {mapping.Key} from {path}");
                }
            }
        }
    }

    // If we didn't find matches by name in this folder, search recursively in subfolders
    if (!anyAssigned)
    {
        string[] subfolders = AssetDatabase.GetSubFolders(folderPath);
        foreach (string subfolder in subfolders)
        {
            anyAssigned |= TryAssignTilesFromFolder(subfolder, propertyMap);
            if (anyAssigned) break;
        }
    }

    return anyAssigned;
}



    private TileBase FindTileAsset(string nameContains, string[] excludeContains = null)
    {
        // Project-wide search for matching tiles
        string[] guids = AssetDatabase.FindAssets("t:TileBase");

        Debug.Log($"Searching for tile with name containing: {nameContains}. Found {guids.Length} total tiles in project.");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

            // Check if file name contains the search term
            if (fileName.Contains(nameContains.ToLower()))
            {
                // Check exclusions if provided
                bool excluded = false;
                if (excludeContains != null)
                {
                    foreach (string exclude in excludeContains)
                    {
                        if (fileName.Contains(exclude.ToLower()))
                        {
                            excluded = true;
                            break;
                        }
                    }
                }

                if (!excluded)
                {
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile != null)
                    {
                        Debug.Log($"Found matching tile: {tile.name} at {path}");
                        return tile;
                    }
                }
            }
        }

        Debug.LogWarning($"No matching tile found for: {nameContains}");
        return null;
    }

    private bool TryAssignDirectionalTile(HybridLevelGenerator generator, string propertyName, string searchTerm)
    {
        // Find tile
        string directionalTilePath = "Assets/PCGLevelGenerator/Tiles/Directional Tiles";
        TileBase tile = FindTileAsset(searchTerm);

        if (tile != null)
        {
            // Determine the correct property and set it
            var prop = serializedObject.FindProperty(propertyName);
            if (prop != null)
            {
                var tileProp = prop.FindPropertyRelative("tile");
                if (tileProp != null)
                {
                    tileProp.objectReferenceValue = tile;
                    return true;
                }
            }
        }

        return false;
    }



    private bool AutoAssignEntityPrefabs()

    {

        HybridLevelGenerator generator = (HybridLevelGenerator)target;

        bool anyAssigned = false;



        // Possible entity prefab folders to check

        string[] possiblePrefabFolders = new string[]

        {
        "Assets/PCGLevelGenerator/Prefabs",
        "Assets/Prefabs",

        "Assets/PCGLevelGenerator/Resources",
        "Assets/Resources"

        };



        // Find the first valid prefab folder

        string prefabFolder = null;

        foreach (string folder in possiblePrefabFolders)

        {

            if (AssetDatabase.IsValidFolder(folder))

            {

                prefabFolder = folder;

                break;

            }

        }



        if (prefabFolder == null)

        {

            Debug.LogWarning("No prefab folder found in standard locations.");

            return false;

        }



        // 1. Try to assign Player prefab

        if (generator.playerPrefab == null)

        {

            GameObject playerPrefab = FindPrefabByKeywords(prefabFolder, new[] { "player", "character", "hero" });

            if (playerPrefab != null)

            {

                generator.playerPrefab = playerPrefab;

                anyAssigned = true;

                Debug.Log($"Assigned Player Prefab: {playerPrefab.name}");

            }

        }



        // 2. Try to assign Enemy prefab

        if (generator.enemyPrefab == null)

        {

            GameObject enemyPrefab = FindPrefabByKeywords(prefabFolder, new[] { "enemy", "monster", "foe", "opponent" });

            if (enemyPrefab != null)

            {

                generator.enemyPrefab = enemyPrefab;

                anyAssigned = true;

                Debug.Log($"Assigned Enemy Prefab: {enemyPrefab.name}");

            }

        }



        // 3. Try to assign Decoration prefab

        if (generator.decorationPrefab == null)

        {

            GameObject decorPrefab = FindPrefabByKeywords(prefabFolder, new[] { "decoration", "prop", "furniture", "decor", "item" });

            if (decorPrefab != null)

            {

                generator.decorationPrefab = decorPrefab;

                anyAssigned = true;

                Debug.Log($"Assigned Decoration Prefab: {decorPrefab.name}");

            }

        }



        return anyAssigned;

    }





    private GameObject FindPrefabByKeywords(string folder, string[] keywords)

    {

        string[] prefabGuids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });



        foreach (string guid in prefabGuids)

        {

            string path = AssetDatabase.GUIDToAssetPath(guid);

            string fileName = Path.GetFileNameWithoutExtension(path).ToLower();



            foreach (string keyword in keywords)

            {

                if (fileName.Contains(keyword.ToLower()))

                {

                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                    if (prefab != null)

                    {

                        return prefab;

                    }

                }

            }

        }



        // Try in subfolders if not found

        string[] subFolders = AssetDatabase.GetSubFolders(folder);

        foreach (string subFolder in subFolders)

        {

            GameObject result = FindPrefabByKeywords(subFolder, keywords);

            if (result != null)

            {

                return result;

            }

        }



        return null;

    }



    private GameObject FindPrefabAsset(string basePath, string nameContains)
    {
        return FindPrefabAsset(basePath, new[] { nameContains });
    }

    private GameObject FindPrefabAsset(string basePath, string[] nameContains)
    {
        // Search the project for matching prefabs
        string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { basePath });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path).ToLower();

            // Check if file name contains any of the search terms
            foreach (string term in nameContains)
            {
                if (fileName.Contains(term.ToLower()))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        return prefab;
                    }
                }
            }
        }

        return null;
    }

    #endregion
}