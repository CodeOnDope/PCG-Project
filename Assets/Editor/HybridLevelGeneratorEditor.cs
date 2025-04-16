using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.Linq;

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

    // --- UI States ---
    private bool isInitialSetup = true;
    private float lastPreviewTime;
    private bool showFeedback = false;
    private string feedbackMessage = "";
    private MessageType feedbackType = MessageType.Info;
    private float feedbackDuration = 3f;

    // Section foldouts
    private bool showLevelDimensions = true;
    private bool showBspSettings = true;
    private bool showHybridSettings = true;
    private bool showCorridorSettings = true;
    private bool showTilemapSettings = true;
    private bool showEntitySettings = true;

    // --- Colors ---
    private readonly Color headerColor = new Color(0.2f, 0.4f, 0.7f);
    private readonly Color accentColor = new Color(0.3f, 0.6f, 1f);
    private readonly Color generateColor = new Color(0.15f, 0.75f, 0.5f);
    private readonly Color clearColor = new Color(0.85f, 0.25f, 0.25f);
    private readonly Color[] modeColors = new Color[] {
        new Color(0.4f, 0.6f, 0.9f, 0.7f),  // Fully Procedural
        new Color(0.9f, 0.6f, 0.3f, 0.7f),  // Hybrid Procedural
        new Color(0.4f, 0.8f, 0.5f, 0.7f)   // User Defined
    };

    // --- Animation Variables ---
    private float pulseTime = 0f;
    private int previousGenerationMode = -1;
    private Dictionary<string, float> foldoutAnimations = new Dictionary<string, float>();

    // --- Quick-help feature ---
    private int quickHelpStep = -1;
    private string[] quickHelpMessages = new string[] {
        "Start by selecting a Generation Mode. Each has different strengths for level design.",
        "Configure your Level Dimensions to set the overall size of your generated level.",
        "Adjust BSP Settings to control how your level space is partitioned.",
        "Make your levels unique with L-shaped rooms and Room Templates.",
        "Add Players, Enemies, and Decorations to bring your level to life!"
    };

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

        // Store previous mode
        previousGenerationMode = generationModeProp.intValue;

        // Initialize foldout animations
        foldoutAnimations["dimensions"] = showLevelDimensions ? 1f : 0f;
        foldoutAnimations["bsp"] = showBspSettings ? 1f : 0f;
        foldoutAnimations["hybrid"] = showHybridSettings ? 1f : 0f;
        foldoutAnimations["corridor"] = showCorridorSettings ? 1f : 0f;
        foldoutAnimations["tilemap"] = showTilemapSettings ? 1f : 0f;
        foldoutAnimations["entity"] = showEntitySettings ? 1f : 0f;

        // Register for editor updates
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        // Unregister from editor updates
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        // Update animations
        bool needsRepaint = false;

        // Pulse animation
        pulseTime = (pulseTime + Time.deltaTime) % 6.28f; // 2π

        // Section slide animations - allow 0.5 seconds for transitions
        const float animationSpeed = 2.0f; // Animation speed multiplier
        foreach (var key in foldoutAnimations.Keys.ToArray())
        {
            float target = 0f;
            switch (key)
            {
                case "dimensions": target = showLevelDimensions ? 1f : 0f; break;
                case "bsp": target = showBspSettings ? 1f : 0f; break;
                case "hybrid": target = showHybridSettings ? 1f : 0f; break;
                case "corridor": target = showCorridorSettings ? 1f : 0f; break;
                case "tilemap": target = showTilemapSettings ? 1f : 0f; break;
                case "entity": target = showEntitySettings ? 1f : 0f; break;
            }

            float current = foldoutAnimations[key];
            if (current != target)
            {
                // Smoothly move toward target value
                if (current < target)
                    foldoutAnimations[key] = Mathf.Min(current + Time.deltaTime * animationSpeed, target);
                else
                    foldoutAnimations[key] = Mathf.Max(current - Time.deltaTime * animationSpeed, target);

                needsRepaint = true;
            }
        }

        // Handle timed feedback messages
        if (showFeedback && EditorApplication.timeSinceStartup - lastPreviewTime > feedbackDuration)
        {
            showFeedback = false;
            needsRepaint = true;
        }

        if (needsRepaint)
            Repaint();
    }

    // Show feedback message for a set duration
    private void ShowFeedback(string message, MessageType type = MessageType.Info, float duration = 3f)
    {
        feedbackMessage = message;
        feedbackType = type;
        showFeedback = true;
        lastPreviewTime = (float)EditorApplication.timeSinceStartup;
        feedbackDuration = duration;
        Repaint();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Cache the background color
        Color cachedBgColor = GUI.backgroundColor;

        // Check for changes to update preview
        EditorGUI.BeginChangeCheck();

        // Draw stylish header
        DrawHeader();

        // Draw Generation Mode selector (the main control)
        DrawGenerationModeSelector();

        // Draw all settings in one scrollable panel
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Level dimensions section (always visible)
        DrawFoldoutSection("dimensions", "Level Dimensions", ref showLevelDimensions, DrawLevelDimensionsSection);

        // BSP Settings (visible for Fully Procedural and Hybrid modes)
        GenerationMode currentMode = (GenerationMode)generationModeProp.intValue;
        if (currentMode == GenerationMode.FullyProcedural || currentMode == GenerationMode.HybridProcedural)
        {
            DrawFoldoutSection("bsp", "BSP Algorithm Settings", ref showBspSettings, DrawBspSection);
        }

        // Hybrid Settings (only visible for Hybrid mode)
        if (currentMode == GenerationMode.HybridProcedural)
        {
            DrawFoldoutSection("hybrid", "Room Type Settings", ref showHybridSettings, DrawHybridSection);
        }

        // Corridor Settings (always visible)
        DrawFoldoutSection("corridor", "Corridor Settings", ref showCorridorSettings, DrawCorridorSection);

        // Tilemap Settings (always visible)
        DrawFoldoutSection("tilemap", "Tiles & Tilemaps", ref showTilemapSettings, DrawTilemapSection);

        // Entity Settings (always visible)
        DrawFoldoutSection("entity", "Entity Settings", ref showEntitySettings, DrawEntitySection);

        EditorGUILayout.EndVertical();

        // Show feedback message if active
        if (showFeedback)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(feedbackMessage, feedbackType);
        }

        // Bottom action controls - always visible
        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();

        // Generate Button
        GUI.backgroundColor = generateColor;
        if (GUILayout.Button("Generate Level", GUILayout.Height(40)))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            Undo.RecordObject(generator, "Generate Level");
            generator.GenerateLevel();
            MarkSceneDirty(generator);

            // Show feedback
            ShowFeedback("Level Generated Successfully!", MessageType.Info);
        }

        // Clear Button
        GUI.backgroundColor = clearColor;
        if (GUILayout.Button("Clear Level", GUILayout.Height(40)))
        {
            HybridLevelGenerator generator = (HybridLevelGenerator)target;
            if (EditorUtility.DisplayDialog("Confirm Clear",
                "Clear generated level AND scene design nodes?",
                "Clear All", "Cancel"))
            {
                Undo.RecordObject(generator, "Clear Level");
                generator.ClearLevel();
                MarkSceneDirty(generator);

                // Show feedback
                ShowFeedback("Level Cleared", MessageType.Info);
            }
        }

        GUI.backgroundColor = cachedBgColor;
        EditorGUILayout.EndHorizontal();

        // Show Quick-Help if enabled
        if (quickHelpStep >= 0 && quickHelpStep < quickHelpMessages.Length)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox($"TIP {quickHelpStep + 1}/{quickHelpMessages.Length}: {quickHelpMessages[quickHelpStep]}",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(quickHelpStep < quickHelpMessages.Length - 1 ? "Next Tip" : "Close Help",
                GUILayout.Width(100)))
            {
                quickHelpStep++;
                if (quickHelpStep >= quickHelpMessages.Length)
                {
                    quickHelpStep = -1;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // Check for generation mode change
        if (previousGenerationMode != generationModeProp.intValue)
        {
            // Mode has changed
            previousGenerationMode = generationModeProp.intValue;

            // Auto-show settings relevant to the new mode
            if (generationModeProp.intValue == (int)GenerationMode.HybridProcedural)
            {
                showHybridSettings = true;
                foldoutAnimations["hybrid"] = 0.01f; // Start animation
                ShowFeedback("Hybrid mode enables L-shaped rooms and room templates", MessageType.Info);
            }
            else if (generationModeProp.intValue == (int)GenerationMode.UserDefinedLayout)
            {
                ShowFeedback("User Defined mode requires using the Visual Level Designer", MessageType.Info);
            }
        }

        // Check if any property was modified
        if (EditorGUI.EndChangeCheck())
        {
            // Give feedback about changes
            if (!showFeedback) // Don't overwrite more specific feedback
                ShowFeedback("Settings updated", MessageType.Info, 1.5f);
        }

        serializedObject.ApplyModifiedProperties();

        // Initial setup prompt for new users
        if (isInitialSetup)
        {
            isInitialSetup = false;
            if (EditorUtility.DisplayDialog("Procedural Level Generator Setup",
                "Welcome to the Procedural Level Generator!\n\nWould you like to enable the Quick Help Guide to get started?",
                "Yes, Show Help", "No Thanks"))
            {
                quickHelpStep = 0;
            }
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.Space(5);

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 16;
        headerStyle.alignment = TextAnchor.MiddleCenter;

        Rect headerRect = GUILayoutUtility.GetRect(GUIContent.none, headerStyle, GUILayout.Height(30));
        EditorGUI.DrawRect(headerRect, headerColor);

        // Adjust text color for better contrast
        headerStyle.normal.textColor = Color.white;
        GUI.Label(headerRect, "PROCEDURAL LEVEL GENERATOR", headerStyle);

        EditorGUILayout.Space(5);
    }

    private void DrawGenerationModeSelector()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Select Generation Method", EditorStyles.boldLabel);

        // Get current mode
        int currentMode = generationModeProp.intValue;

        // Animation pulse effect for the selected button
        float pulse = 0.8f + 0.2f * Mathf.Sin(pulseTime * 2);

        // Mode selection buttons
        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < 3; i++) // Assuming 3 generation modes
        {
            // Determine if this mode is selected
            bool isSelected = currentMode == i;

            // Determine button style and color
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
            buttonStyle.normal.textColor = isSelected ? Color.white : Color.black;
            buttonStyle.fixedHeight = 30;

            // Apply pulse animation if selected
            Color btnColor = modeColors[i];
            if (isSelected)
            {
                btnColor.a *= pulse; // Pulsing effect
            }

            // Draw the button
            GUI.backgroundColor = btnColor;
            string modeName = ((GenerationMode)i).ToString();

            // Split camel case for display
            string displayName = System.Text.RegularExpressions.Regex.Replace(
                modeName, "([a-z](?=[A-Z]))", "$1 ");

            if (GUILayout.Button(displayName, buttonStyle))
            {
                generationModeProp.intValue = i;
                GUI.FocusControl(null); // Clear focus
            }
        }

        GUI.backgroundColor = Color.white; // Reset color
        EditorGUILayout.EndHorizontal();

        // Help text for the selected mode
        string helpText = "";
        switch ((GenerationMode)currentMode)
        {
            case GenerationMode.FullyProcedural:
                helpText = "BSP + Random Rect Rooms + MST Corridors.\nBest for: Roguelike dungeons with rectangular rooms";
                break;
            case GenerationMode.HybridProcedural:
                helpText = "BSP + Templates/L-Shapes/Rects + MST Corridors.\nBest for: Varied levels with predefined room templates";
                break;
            case GenerationMode.UserDefinedLayout:
                helpText = "Uses RoomNode components in the scene.\nBest for: Hand-crafted levels with procedural elements";
                break;
        }

        EditorGUILayout.HelpBox(helpText, MessageType.Info);

        // Open Designer Button (if User-Defined mode)
        if ((GenerationMode)currentMode == GenerationMode.UserDefinedLayout)
        {
            EditorGUILayout.Space(5);
            Color cachedBgColor = GUI.backgroundColor;
            GUI.backgroundColor = accentColor;
            if (GUILayout.Button("Open Visual Level Designer", GUILayout.Height(30)))
            {
                VisualLevelDesignEditor.ShowWindow();
                ShowFeedback("Visual Level Designer opened", MessageType.Info);
            }
            GUI.backgroundColor = cachedBgColor;
        }

        EditorGUILayout.Space(5);
    }

    private void DrawFoldoutSection(string key, string title, ref bool foldout, Action drawContent)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        Rect foldoutRect = EditorGUILayout.GetControlRect(true);
        bool newFoldout = EditorGUI.Foldout(foldoutRect, foldout, title, true, EditorStyles.foldoutHeader);

        // If foldout state changed
        if (newFoldout != foldout)
        {
            foldout = newFoldout;
            // Don't change animation value immediately - let animation handle it
        }

        // Get animated progress value (0-1)
        float progress = foldoutAnimations[key];

        if (progress > 0.01f) // Draw if at least slightly visible
        {
            // Calculate height for animated opening/closing
            float fadeHeight = progress; // Multiply by max height if needed for larger sections

            // Semi-transparent during transition
            GUI.color = new Color(1, 1, 1, progress);

            // Begin animation clip area
            EditorGUILayout.BeginVertical();

            if (progress < 1.0f)
            {
                // Animated sliding effect
                GUILayout.Space(10 * (1.0f - progress)); // Slide down effect
            }

            if (drawContent != null)
            {
                drawContent();
            }

            EditorGUILayout.EndVertical();

            // Reset color
            GUI.color = Color.white;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawLevelDimensionsSection()
    {
        EditorGUILayout.Space(5);

        // Level size field with visual slider
        EditorGUI.BeginChangeCheck();

        // Width slider with better visual feedback
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Width");
        EditorGUILayout.BeginVertical();
        levelWidthProp.intValue = EditorGUILayout.IntSlider(levelWidthProp.intValue, 10, 100);

        // Draw a visual representation of width
        Rect widthRect = EditorGUILayout.GetControlRect(false, 8);
        float widthPercentage = (levelWidthProp.intValue - 10f) / 90f; // 10 to 100 range
        Rect filledRect = new Rect(widthRect.x, widthRect.y, widthRect.width * widthPercentage, widthRect.height);
        EditorGUI.DrawRect(widthRect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUI.DrawRect(filledRect, accentColor);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        // Height slider with visual feedback
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Height");
        EditorGUILayout.BeginVertical();
        levelHeightProp.intValue = EditorGUILayout.IntSlider(levelHeightProp.intValue, 10, 100);

        // Draw a visual representation of height
        Rect heightRect = EditorGUILayout.GetControlRect(false, 8);
        float heightPercentage = (levelHeightProp.intValue - 10f) / 90f; // 10 to 100 range
        Rect filledHeightRect = new Rect(heightRect.x, heightRect.y, heightRect.width * heightPercentage, heightRect.height);
        EditorGUI.DrawRect(heightRect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUI.DrawRect(filledHeightRect, accentColor);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        // Preview area showing relative dimensions
        float aspectRatio = (float)levelWidthProp.intValue / levelHeightProp.intValue;
        float previewHeight = 60; // Fixed height
        float previewWidth = previewHeight * aspectRatio;

        // Limit preview width
        if (previewWidth > 300)
        {
            previewWidth = 300;
            previewHeight = previewWidth / aspectRatio;
        }

        // Center the preview
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight);

        // Pulse effect for the preview
        float pulse = 0.8f + 0.2f * Mathf.Sin(pulseTime);
        Color previewColor = new Color(accentColor.r * pulse, accentColor.g * pulse, accentColor.b);

        EditorGUI.DrawRect(previewRect, previewColor);
        GUI.Label(previewRect, $"{levelWidthProp.intValue} x {levelHeightProp.intValue}",
            new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            });
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // Randomness Section
        EditorGUILayout.LabelField("Randomness", EditorStyles.boldLabel);

        // Use a more intuitive toggle with explanation
        bool useRandom = EditorGUILayout.Toggle("Use Random Seed", useRandomSeedProp.boolValue);
        if (useRandom != useRandomSeedProp.boolValue)
        {
            useRandomSeedProp.boolValue = useRandom;
            if (useRandom)
            {
                ShowFeedback("Using a random seed each time", MessageType.Info);
            }
            else
            {
                ShowFeedback("Using fixed seed value", MessageType.Info);
            }
        }

        if (!useRandomSeedProp.boolValue)
        {
            EditorGUILayout.BeginHorizontal();

            int oldSeed = seedProp.intValue;
            EditorGUILayout.PropertyField(seedProp, new GUIContent("Seed Value"));

            // Button to randomize seed
            if (GUILayout.Button("Randomize", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                seedProp.intValue = UnityEngine.Random.Range(1, 9999);
                ShowFeedback($"New seed: {seedProp.intValue}", MessageType.Info);
            }

            EditorGUILayout.EndHorizontal();

            // Show visual feedback of seed change
            if (oldSeed != seedProp.intValue && Event.current.type == EventType.Used)
            {
                ShowFeedback($"Seed changed to: {seedProp.intValue}", MessageType.Info);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("A new random seed will be generated each time.", MessageType.Info);
        }
    }

    private void DrawBspSection()
    {
        EditorGUILayout.Space(5);

        // Min Room Size with visual slider
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Min Room Size");
        EditorGUILayout.BeginVertical();
        minRoomSizeProp.intValue = EditorGUILayout.IntSlider(minRoomSizeProp.intValue, 3, 20);

        // Draw a visual representation
        Rect sizeRect = EditorGUILayout.GetControlRect(false, 8);
        float sizePercentage = (minRoomSizeProp.intValue - 3f) / 17f; // 3 to 20 range
        Rect filledRect = new Rect(sizeRect.x, sizeRect.y, sizeRect.width * sizePercentage, sizeRect.height);
        EditorGUI.DrawRect(sizeRect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUI.DrawRect(filledRect, accentColor);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        // BSP Iterations with visual effect
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("BSP Iterations");
        EditorGUILayout.BeginVertical();
        int oldValue = maxIterationsProp.intValue;
        maxIterationsProp.intValue = EditorGUILayout.IntSlider(maxIterationsProp.intValue, 1, 10);

        // Draw visualization of BSP iterations
        Rect iterationBar = EditorGUILayout.GetControlRect(false, 20);
        int iterations = maxIterationsProp.intValue;
        float segmentWidth = iterationBar.width / 10f;

        // Draw segments
        for (int i = 0; i < 10; i++)
        {
            Rect segment = new Rect(iterationBar.x + (i * segmentWidth), iterationBar.y, segmentWidth - 2, iterationBar.height);

            // Active segments with pulse
            if (i < iterations)
            {
                float segmentPulse = 0.8f + 0.2f * Mathf.Sin(pulseTime + i * 0.3f); // Offset each segment
                Color segmentColor = new Color(accentColor.r * segmentPulse, accentColor.g * segmentPulse, accentColor.b);
                EditorGUI.DrawRect(segment, segmentColor);
            }
            // Inactive segments
            else
            {
                EditorGUI.DrawRect(segment, new Color(0.3f, 0.3f, 0.3f));
            }
        }

        // Give visual feedback when iterations change
        if (oldValue != maxIterationsProp.intValue && Event.current.type == EventType.Used)
        {
            if (maxIterationsProp.intValue > oldValue)
            {
                ShowFeedback("More iterations = smaller, more numerous rooms", MessageType.Info);
            }
            else
            {
                ShowFeedback("Fewer iterations = larger, fewer rooms", MessageType.Info);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        // Room Padding with visual slider
        EditorGUILayout.Slider(roomPaddingProp, 0, 5, new GUIContent("Room Padding"));

        EditorGUILayout.Space(5);
    }

    private void DrawHybridSection()
    {
        EditorGUILayout.Space(5);

        // Room type probabilities with visual distribution
        float lProb = lShapeProbabilityProp.floatValue;
        float templateProb = roomTemplateProbabilityProp.floatValue;
        float rectProb = 1f - lProb - templateProb;

        EditorGUILayout.Slider(lShapeProbabilityProp, 0f, 1f, new GUIContent("L-Shape Probability"));
        EditorGUILayout.Slider(roomTemplateProbabilityProp, 0f, 1f, new GUIContent("Template Probability"));

        // Recalculate after potential changes
        lProb = lShapeProbabilityProp.floatValue;
        templateProb = roomTemplateProbabilityProp.floatValue;
        rectProb = 1f - lProb - templateProb;

        // Warning if probabilities exceed 100%
        if (rectProb < 0)
        {
            EditorGUILayout.HelpBox("Warning: Probabilities exceed 100%! Rectangle probability will be 0%.", MessageType.Warning);
            rectProb = 0f;
        }

        // Visual distribution bar
        Rect distributionRect = EditorGUILayout.GetControlRect(false, 25);
        float totalWidth = distributionRect.width;

        // Calculate widths
        // Calculate widths
        float lWidth = totalWidth * lProb;
        float templateWidth = totalWidth * templateProb;
        float rectWidth = totalWidth * rectProb;

        // Draw distribution segments with pulsing effect
        float lPulse = 0.8f + 0.2f * Mathf.Sin(pulseTime);
        float tPulse = 0.8f + 0.2f * Mathf.Sin(pulseTime + 2.0f);
        float rPulse = 0.8f + 0.2f * Mathf.Sin(pulseTime + 4.0f);

        // L-shape segment
        Rect lRect = new Rect(distributionRect.x, distributionRect.y, lWidth, distributionRect.height);
        EditorGUI.DrawRect(lRect, new Color(0.9f * lPulse, 0.6f * lPulse, 0.3f * lPulse));

        // Template segment
        Rect templateRect = new Rect(distributionRect.x + lWidth, distributionRect.y, templateWidth, distributionRect.height);
        EditorGUI.DrawRect(templateRect, new Color(0.3f * tPulse, 0.6f * tPulse, 0.9f * tPulse));

        // Rectangle segment
        Rect rectRect = new Rect(distributionRect.x + lWidth + templateWidth, distributionRect.y, rectWidth, distributionRect.height);
        EditorGUI.DrawRect(rectRect, new Color(0.6f * rPulse, 0.4f * rPulse, 0.8f * rPulse));

        // Labels
        EditorGUILayout.BeginHorizontal();
        GUIStyle smallLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };

        if (lProb > 0.1f)
            GUILayout.Label("L-Shapes", smallLabel, GUILayout.Width(lWidth));
        if (templateProb > 0.1f)
            GUILayout.Label("Templates", smallLabel, GUILayout.Width(templateWidth));
        if (rectProb > 0.1f)
            GUILayout.Label("Rectangles", smallLabel, GUILayout.Width(rectWidth));

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // L-Shape Settings
        EditorGUILayout.LabelField("L-Shape Settings", EditorStyles.boldLabel);

        // Min Leg Ratio with visual representation
        EditorGUILayout.Slider(minLLegRatioProp, 0.2f, 0.8f, new GUIContent("Min Leg Ratio"));

        // Ensure max leg ratio is at least min leg ratio
        maxLLegRatioProp.floatValue = Mathf.Max(maxLLegRatioProp.floatValue, minLLegRatioProp.floatValue);
        EditorGUILayout.Slider(maxLLegRatioProp, minLLegRatioProp.floatValue, 0.8f, new GUIContent("Max Leg Ratio"));

        // L-shape visualization with animation
        float legRatio = (minLLegRatioProp.floatValue + maxLLegRatioProp.floatValue) / 2f;
        EditorGUILayout.Space(5);

        Rect lShapeRect = EditorGUILayout.GetControlRect(false, 80);
        float size = Mathf.Min(lShapeRect.width, lShapeRect.height) * 0.8f;
        float padding = 10;
        float centerX = lShapeRect.x + (lShapeRect.width - size) / 2;
        float centerY = lShapeRect.y + (lShapeRect.height - size) / 2;

        // Vertical leg width & height with animation
        float pulseRatio = legRatio * (0.9f + 0.1f * Mathf.Sin(pulseTime));
        float vertLegWidth = size * pulseRatio;
        float vertLegHeight = size;

        // Horizontal leg width & height with animation
        float horizLegWidth = size;
        float horizLegHeight = size * pulseRatio;

        // Draw L-shape
        Rect vertLeg = new Rect(
            centerX,
            centerY,
            vertLegWidth,
            vertLegHeight
        );

        Rect horizLeg = new Rect(
            centerX,
            centerY + (vertLegHeight - horizLegHeight),
            horizLegWidth,
            horizLegHeight
        );

        EditorGUI.DrawRect(vertLeg, accentColor);
        EditorGUI.DrawRect(horizLeg, accentColor);

        EditorGUILayout.Space(8);

        // Room Templates
        EditorGUILayout.LabelField("Room Templates", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox("Add prefabs that will be used as room templates. Templates should have a RoomTemplate component attached.", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(roomTemplatePrefabsProp, true);

        if (EditorGUI.EndChangeCheck())
        {
            ShowFeedback("Room templates updated", MessageType.Info);
        }
    }

    private void DrawCorridorSection()
    {
        EditorGUILayout.Space(5);

        EditorGUI.BeginChangeCheck();
        int oldCorridorWidth = corridorWidthProp.intValue;
        EditorGUILayout.IntSlider(corridorWidthProp, 1, 5, new GUIContent("Corridor Width"));

        // Visual corridor width preview
        Rect corridorPreviewArea = EditorGUILayout.GetControlRect(false, 40);
        float corridorPreviewWidth = corridorPreviewArea.width - 20;

        // Animated corridor
        float pulseWidth = 1.0f + 0.05f * Mathf.Sin(pulseTime * 2);

        Rect corridorVisual = new Rect(
            corridorPreviewArea.x + 10,
            corridorPreviewArea.y + (corridorPreviewArea.height - corridorWidthProp.intValue * 5 * pulseWidth) / 2,
            corridorPreviewWidth,
            corridorWidthProp.intValue * 5 * pulseWidth  // Scale for visibility with pulse
        );

        EditorGUI.DrawRect(corridorVisual, accentColor);

        if (EditorGUI.EndChangeCheck() && oldCorridorWidth != corridorWidthProp.intValue)
        {
            ShowFeedback($"Corridor width set to {corridorWidthProp.intValue}", MessageType.Info);
        }
    }

    private void DrawTilemapSection()
    {
        EditorGUILayout.Space(5);

        EditorGUI.BeginChangeCheck();

        // Ground tilemap with icon
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(EditorGUIUtility.IconContent("Tilemap Icon"), GUILayout.Width(20), GUILayout.Height(20));
        EditorGUILayout.PropertyField(groundTilemapProp, new GUIContent("Ground Tilemap"));
        EditorGUILayout.EndHorizontal();

        // Wall tilemap with icon
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(EditorGUIUtility.IconContent("Tilemap Icon"), GUILayout.Width(20), GUILayout.Height(20));
        EditorGUILayout.PropertyField(wallTilemapProp, new GUIContent("Wall Tilemap"));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Floor tile with color preview
        EditorGUILayout.BeginHorizontal();
        Rect floorColorRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(20));
        EditorGUI.DrawRect(floorColorRect, new Color(0.2f, 0.8f, 0.3f));
        EditorGUILayout.PropertyField(floorTileProp, new GUIContent("Floor Tile"));
        EditorGUILayout.EndHorizontal();

        // Wall tile with color preview
        EditorGUILayout.BeginHorizontal();
        Rect wallColorRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(20));
        EditorGUI.DrawRect(wallColorRect, new Color(0.8f, 0.4f, 0.3f));
        EditorGUILayout.PropertyField(wallTileProp, new GUIContent("Wall Tile"));
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            ShowFeedback("Tile references updated", MessageType.Info);
        }
    }

    private void DrawEntitySection()
    {
        EditorGUILayout.Space(5);

        // Player settings
        EditorGUILayout.LabelField("Player Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        // Player prefab with icon
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(EditorGUIUtility.IconContent("d_AnimatorController Icon"), GUILayout.Width(20), GUILayout.Height(20));
        EditorGUILayout.PropertyField(playerPrefabProp, new GUIContent("Player Prefab"));
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            if (playerPrefabProp.objectReferenceValue != null)
            {
                ShowFeedback("Player prefab set", MessageType.Info);
            }
            else
            {
                ShowFeedback("Player prefab removed", MessageType.Warning);
            }
        }

        EditorGUILayout.Space(10);

        // Enemy settings
        EditorGUILayout.LabelField("Enemy Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        // Enemy prefab with icon
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(EditorGUIUtility.IconContent("d_PreMatCube"), GUILayout.Width(20), GUILayout.Height(20));
        EditorGUILayout.PropertyField(enemyPrefabProp, new GUIContent("Enemy Prefab"));
        EditorGUILayout.EndHorizontal();

        // Enemy count per room with visual feedback
        int oldEnemyCount = enemiesPerRoomProp.intValue;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Enemies Per Room");
        enemiesPerRoomProp.intValue = EditorGUILayout.IntSlider(enemiesPerRoomProp.intValue, 0, 10);
        EditorGUILayout.EndHorizontal();

        // Visual enemy count indicator with animation
        if (enemyPrefabProp.objectReferenceValue != null && enemiesPerRoomProp.intValue > 0)
        {
            Rect enemyVisRect = EditorGUILayout.GetControlRect(false, 30);
            float iconWidth = 15f;
            float spacing = 5f;
            float totalWidth = enemiesPerRoomProp.intValue * (iconWidth + spacing);
            float startX = enemyVisRect.x + (enemyVisRect.width - totalWidth) / 2;

            for (int i = 0; i < enemiesPerRoomProp.intValue; i++)
            {
                // Animated pulse offset by icon index
                float pulse = 0.8f + 0.2f * Mathf.Sin(pulseTime + i * 0.3f);

                Rect enemyRect = new Rect(
                    startX + (i * (iconWidth + spacing)),
                    enemyVisRect.y + (enemyVisRect.height - iconWidth) / 2,
                    iconWidth,
                    iconWidth
                );

                EditorGUI.DrawRect(enemyRect, new Color(0.85f * pulse, 0.3f, 0.3f));
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            if (enemiesPerRoomProp.intValue != oldEnemyCount)
            {
                ShowFeedback($"Enemy count set to {enemiesPerRoomProp.intValue} per room", MessageType.Info);
            }
        }

        EditorGUILayout.Space(10);

        // Decoration settings
        EditorGUILayout.LabelField("Decoration Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        // Decoration prefab with icon
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(EditorGUIUtility.IconContent("d_TerrainInspector.TerrainToolSplat"), GUILayout.Width(20), GUILayout.Height(20));
        EditorGUILayout.PropertyField(decorationPrefabProp, new GUIContent("Decoration Prefab"));
        EditorGUILayout.EndHorizontal();

        // Decoration count per room with visual feedback
        int oldDecorCount = decorationsPerRoomProp.intValue;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Decorations Per Room");
        decorationsPerRoomProp.intValue = EditorGUILayout.IntSlider(decorationsPerRoomProp.intValue, 0, 15);
        EditorGUILayout.EndHorizontal();

        // Visual decoration count indicator with animation
        if (decorationPrefabProp.objectReferenceValue != null && decorationsPerRoomProp.intValue > 0)
        {
            Rect decorVisRect = EditorGUILayout.GetControlRect(false, 30);
            float iconWidth = 12f;
            float spacing = 4f;
            float totalWidth = decorationsPerRoomProp.intValue * (iconWidth + spacing);
            float startX = decorVisRect.x + (decorVisRect.width - totalWidth) / 2;

            for (int i = 0; i < decorationsPerRoomProp.intValue; i++)
            {
                // Animated pulse with different offsets for different types
                float pulse = 0.8f + 0.2f * Mathf.Sin(pulseTime + i * 0.2f);

                Rect decorRect = new Rect(
                    startX + (i * (iconWidth + spacing)),
                    decorVisRect.y + (decorVisRect.height - iconWidth) / 2,
                    iconWidth,
                    iconWidth
                );

                // Alternate decoration colors
                Color decorColor = (i % 3 == 0) ?
                    new Color(0.3f, 0.7f * pulse, 0.3f) :
                    (i % 3 == 1) ?
                        new Color(0.3f, 0.6f, 0.8f * pulse) :
                        new Color(0.8f * pulse, 0.7f, 0.3f);

                EditorGUI.DrawRect(decorRect, decorColor);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            if (decorationsPerRoomProp.intValue != oldDecorCount)
            {
                ShowFeedback($"Decoration count set to {decorationsPerRoomProp.intValue} per room", MessageType.Info);
            }
        }
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
}