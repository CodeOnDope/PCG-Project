using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

public class AssetDependencyAnalyzer : EditorWindow
{
    private enum AnalysisState
    {
        Idle,
        Analyzing,
        Complete
    }

    // Analysis settings
    private bool includeScenes = true;
    private bool includePrefabs = true;
    private bool includeScripts = true;
    private bool ignoreEditorAssets = true;
    private string outputFolderPath = "Assets/UnusedAssets";

    // Preview settings
    private bool showPreview = true;
    private float previewSize = 64f;
    private bool showDependencyFlow = false;
    private List<string> corruptedAssets = new List<string>();

    // Exclusion settings
    private List<string> excludeFolders = new List<string>();
    private List<string> excludeExtensions = new List<string>();
    private string newExcludeFolder = "Assets/Example";
    private string newExcludeExtension = ".psd";
    private bool folderExclusionsFoldout = false;
    private bool extensionExclusionsFoldout = false;

    // Analysis state
    private AnalysisState currentState = AnalysisState.Idle;
    private HashSet<string> usedAssets = new HashSet<string>();
    private List<string> unusedAssets = new List<string>();
    private Dictionary<string, bool> assetUsageStatus = new Dictionary<string, bool>();
    private Dictionary<string, HashSet<string>> dependencyCache = new Dictionary<string, HashSet<string>>();
    private Dictionary<string, List<string>> dependsByCache = new Dictionary<string, List<string>>();
    private float analysisProgress = 0f;
    private string currentOperation = "";
    private bool showUsedAssets = false;
    private bool showCorruptedAssets = false;
    private int totalAssetsCount = 0;
    private long totalAssetSize = 0;
    private long unusedAssetSize = 0;
    private double lastAnalysisTime = 0;

    // For dependency visualization
    private string selectedAsset = null;
    private bool showFullDependencyChain = false;
    private int maxDepth = 2;
    private Vector2 dependencyScrollPosition;

    // UI
    private Vector2 scrollPosition;
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private string searchFilter = "";
    private bool showCategorizedResults = true;
    private bool compactMode = false;
    private Dictionary<string, Texture2D> previewCache = new Dictionary<string, Texture2D>();

    [MenuItem("Tools/Asset Dependency Analyzer")]
    public static void ShowWindow()
    {
        GetWindow<AssetDependencyAnalyzer>("Asset Analyzer");
    }

    private void OnEnable()
    {
        // Add default excludes
        if (excludeFolders.Count == 0)
        {
            excludeFolders.Add("Assets/Editor");
            excludeFolders.Add("Assets/Plugins/Editor");
        }

        if (excludeExtensions.Count == 0)
        {
            excludeExtensions.Add(".meta");
            excludeExtensions.Add(".cs.meta");
        }
    }

    private void CreateStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 14;
            headerStyle.margin = new RectOffset(0, 0, 10, 5);
        }

        if (subHeaderStyle == null)
        {
            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel);
            subHeaderStyle.margin = new RectOffset(0, 0, 5, 5);
        }
    }

    void OnGUI()
    {
        CreateStyles();

        EditorGUILayout.BeginVertical();

        GUILayout.Label("Asset Dependency Analyzer", headerStyle);
        EditorGUILayout.Space();

        // Settings section
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Analysis Settings", subHeaderStyle);

        EditorGUI.BeginDisabledGroup(currentState == AnalysisState.Analyzing);

        includeScenes = EditorGUILayout.Toggle("Include Scenes", includeScenes);
        includePrefabs = EditorGUILayout.Toggle("Include Prefabs", includePrefabs);
        includeScripts = EditorGUILayout.Toggle("Include Scripts", includeScripts);
        ignoreEditorAssets = EditorGUILayout.Toggle("Ignore Editor Assets", ignoreEditorAssets);

        EditorGUILayout.Space();

        // Preview settings
        showPreview = EditorGUILayout.Toggle("Show Asset Preview", showPreview);
        if (showPreview)
        {
            EditorGUI.indentLevel++;
            previewSize = EditorGUILayout.Slider("Preview Size", previewSize, 32f, 128f);
            EditorGUI.indentLevel--;
        }

        // Dependency Flow settings
        showDependencyFlow = EditorGUILayout.Toggle("Show Dependency Flow", showDependencyFlow);
        if (showDependencyFlow)
        {
            EditorGUI.indentLevel++;
            showFullDependencyChain = EditorGUILayout.Toggle("Show Full Chain", showFullDependencyChain);
            maxDepth = EditorGUILayout.IntSlider("Max Depth", maxDepth, 1, 5);
            EditorGUI.indentLevel--;
        }

        // Folder exclusions
        folderExclusionsFoldout = EditorGUILayout.Foldout(folderExclusionsFoldout, "Folder Exclusions");
        if (folderExclusionsFoldout)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < excludeFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                excludeFolders[i] = EditorGUILayout.TextField(excludeFolders[i]);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    excludeFolders.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            newExcludeFolder = EditorGUILayout.TextField(newExcludeFolder);
            if (GUILayout.Button("Add Folder", GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(newExcludeFolder) && !excludeFolders.Contains(newExcludeFolder))
                {
                    excludeFolders.Add(newExcludeFolder);
                    newExcludeFolder = "";
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        // Extension exclusions
        extensionExclusionsFoldout = EditorGUILayout.Foldout(extensionExclusionsFoldout, "Extension Exclusions");
        if (extensionExclusionsFoldout)
        {
            EditorGUI.indentLevel++;

            for (int i = 0; i < excludeExtensions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                excludeExtensions[i] = EditorGUILayout.TextField(excludeExtensions[i]);
                if (GUILayout.Button("Remove", GUILayout.Width(70)))
                {
                    excludeExtensions.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            newExcludeExtension = EditorGUILayout.TextField(newExcludeExtension);
            if (GUILayout.Button("Add Extension", GUILayout.Width(100)))
            {
                if (!string.IsNullOrEmpty(newExcludeExtension) && !excludeExtensions.Contains(newExcludeExtension))
                {
                    excludeExtensions.Add(newExcludeExtension);
                    newExcludeExtension = "";
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        outputFolderPath = EditorGUILayout.TextField("Output Folder Path", outputFolderPath);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndVertical();

        // Actions
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Actions", subHeaderStyle);
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginDisabledGroup(currentState == AnalysisState.Analyzing);
        if (GUILayout.Button("Analyze Project", GUILayout.Height(30)))
        {
            StartAnalysis();
        }

        if (currentState == AnalysisState.Complete)
        {
            if (GUILayout.Button("Export Results", GUILayout.Height(30)))
            {
                ExportResults();
            }

            if (GUILayout.Button("Export Dependency Graph", GUILayout.Height(30)))
            {
                ExportDependencyGraph();
            }

            if (GUILayout.Button("Move Unused Assets", GUILayout.Height(30)))
            {
                MoveUnusedAssets();
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // Progress bar
        if (currentState == AnalysisState.Analyzing)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Analysis Progress", subHeaderStyle);
            EditorGUILayout.LabelField(currentOperation);
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), analysisProgress, $"{(int)(analysisProgress * 100)}%");
            EditorGUILayout.EndVertical();
        }

        // Results
        if (currentState == AnalysisState.Complete)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Results", subHeaderStyle);

            EditorGUILayout.LabelField($"Total assets: {totalAssetsCount} | Unused assets: {unusedAssets.Count} | Analysis time: {lastAnalysisTime:F1} seconds");
            EditorGUILayout.LabelField($"Total size: {GetSizeString(totalAssetSize)} | Unused size: {GetSizeString(unusedAssetSize)} ({(totalAssetSize > 0 ? unusedAssetSize * 100f / totalAssetSize : 0):F1}%)");

            if (corruptedAssets.Count > 0)
            {
                EditorGUILayout.LabelField($"Corrupted assets: {corruptedAssets.Count} (these were excluded from analysis)", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space();

            // Display options
            EditorGUILayout.BeginHorizontal();
            showUsedAssets = EditorGUILayout.Toggle("Show Used Assets", showUsedAssets);
            showCategorizedResults = EditorGUILayout.Toggle("Categorize Results", showCategorizedResults);
            compactMode = EditorGUILayout.Toggle("Compact Mode", compactMode);
            EditorGUILayout.EndHorizontal();

            if (corruptedAssets.Count > 0)
            {
                showCorruptedAssets = EditorGUILayout.Toggle("Show Corrupted Assets", showCorruptedAssets);
            }

            // Search filter
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter:", GUILayout.Width(40));
            searchFilter = EditorGUILayout.TextField(searchFilter);
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                searchFilter = "";
                selectedAsset = null;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Display selected asset with dependency flow if enabled
            if (showDependencyFlow && selectedAsset != null)
            {
                DisplayDependencyFlow(selectedAsset);
            }

            // Results list
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (showCorruptedAssets)
            {
                DisplayCorruptedAssetsList();
            }
            else
            {
                List<string> assetsToDisplay = showUsedAssets ? usedAssets.ToList() : unusedAssets;
                DisplayAssetList(assetsToDisplay, showCategorizedResults, compactMode);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private void Update()
    {
        // Process analysis in the Update loop if we're analyzing
        if (currentState == AnalysisState.Analyzing)
        {
            EditorApplication.update -= PerformAnalysis;
            EditorApplication.update += PerformAnalysis;
            currentState = AnalysisState.Idle; // Prevent adding multiple update callbacks
        }
    }

    private void StartAnalysis()
    {
        DateTime startTime = DateTime.Now;

        currentState = AnalysisState.Analyzing;
        analysisProgress = 0f;
        currentOperation = "Initializing analysis...";

        // Reset collections
        usedAssets = new HashSet<string>();
        unusedAssets = new List<string>();
        assetUsageStatus = new Dictionary<string, bool>();
        dependencyCache = new Dictionary<string, HashSet<string>>();
        dependsByCache = new Dictionary<string, List<string>>();
        corruptedAssets = new List<string>();
        previewCache.Clear();
        selectedAsset = null;

        // Add to update queue
        EditorApplication.update += PerformAnalysis;

        lastAnalysisTime = (DateTime.Now - startTime).TotalSeconds;
    }

    private void PerformAnalysis()
    {
        // This method will be called from the Update loop
        try
        {
            // Step 1: Get all assets
            currentOperation = "Finding all assets...";
            analysisProgress = 0.1f;
            EditorUtility.DisplayProgressBar("Asset Analysis", currentOperation, analysisProgress);

            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/") &&
                               !IsExcludedPath(path) &&
                               !IsExcludedExtension(path))
                .ToArray();

            totalAssetsCount = allAssetPaths.Length;
            totalAssetSize = 0;

            // Step 2: Initialize asset status
            currentOperation = "Initializing asset status...";
            analysisProgress = 0.2f;
            EditorUtility.DisplayProgressBar("Asset Analysis", currentOperation, analysisProgress);

            foreach (string assetPath in allAssetPaths)
            {
                assetUsageStatus[assetPath] = false;

                try
                {
                    if (File.Exists(assetPath))
                    {
                        totalAssetSize += new FileInfo(assetPath).Length;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Could not get file size for {assetPath}: {ex.Message}");
                }
            }

            // Step 3: Build dependency cache
            currentOperation = "Building dependency cache...";
            analysisProgress = 0.3f;
            EditorUtility.DisplayProgressBar("Asset Analysis", currentOperation, analysisProgress);

            for (int i = 0; i < allAssetPaths.Length; i++)
            {
                string assetPath = allAssetPaths[i];

                if (i % 100 == 0)
                {
                    float progress = 0.3f + 0.3f * ((float)i / allAssetPaths.Length);
                    EditorUtility.DisplayProgressBar("Asset Analysis",
                        $"Building dependency cache ({i}/{allAssetPaths.Length})...", progress);
                }

                try
                {
                    string[] dependencies = AssetDatabase.GetDependencies(assetPath, false);
                    dependencyCache[assetPath] = new HashSet<string>(dependencies);

                    // Build reverse dependency cache for visualization
                    foreach (string dependency in dependencies)
                    {
                        if (!dependsByCache.ContainsKey(dependency))
                        {
                            dependsByCache[dependency] = new List<string>();
                        }
                        dependsByCache[dependency].Add(assetPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error processing asset {assetPath}: {ex.Message}");
                    corruptedAssets.Add(assetPath);
                }
            }

            // Step 4: Process roots (scenes, resources, etc.)
            currentOperation = "Processing root assets...";
            analysisProgress = 0.6f;
            EditorUtility.DisplayProgressBar("Asset Analysis", currentOperation, analysisProgress);

            // Mark Resources folders as used
            foreach (string assetPath in allAssetPaths)
            {
                if (assetPath.Contains("/Resources/"))
                {
                    MarkAssetAsUsed(assetPath);
                }
            }

            // Mark scenes as used if enabled
            if (includeScenes)
            {
                foreach (string assetPath in allAssetPaths)
                {
                    if (assetPath.EndsWith(".unity") && !corruptedAssets.Contains(assetPath))
                    {
                        MarkAssetAsUsed(assetPath);
                        MarkDependenciesAsUsed(assetPath);
                    }
                }
            }

            // Mark prefabs as used if enabled
            if (includePrefabs)
            {
                foreach (string assetPath in allAssetPaths.Where(p => p.EndsWith(".prefab") && !corruptedAssets.Contains(p)))
                {
                    if (assetUsageStatus.ContainsKey(assetPath) && assetUsageStatus[assetPath])
                    {
                        MarkDependenciesAsUsed(assetPath);
                    }
                }
            }

            // Mark scripts as used if enabled
            if (includeScripts)
            {
                foreach (string assetPath in allAssetPaths.Where(p => p.EndsWith(".cs") && !corruptedAssets.Contains(p)))
                {
                    if (assetUsageStatus.ContainsKey(assetPath) && assetUsageStatus[assetPath])
                    {
                        MarkDependenciesAsUsed(assetPath);
                    }
                }
            }

            // Step 5: Propagate dependencies to find all used assets
            currentOperation = "Propagating dependencies...";
            analysisProgress = 0.8f;
            EditorUtility.DisplayProgressBar("Asset Analysis", currentOperation, analysisProgress);

            bool changed = true;
            while (changed)
            {
                int beforeCount = usedAssets.Count;

                foreach (string assetPath in usedAssets.ToArray())
                {
                    MarkDependenciesAsUsed(assetPath);
                }

                changed = usedAssets.Count > beforeCount;
            }

            // Step 6: Determine unused assets
            currentOperation = "Finalizing results...";
            analysisProgress = 0.9f;
            EditorUtility.DisplayProgressBar("Asset Analysis", currentOperation, analysisProgress);

            unusedAssets = allAssetPaths
                .Where(path => !usedAssets.Contains(path) && !corruptedAssets.Contains(path))
                .OrderBy(path => Path.GetExtension(path))
                .ThenBy(path => path)
                .ToList();

            // Calculate unused asset size
            unusedAssetSize = 0;
            foreach (string assetPath in unusedAssets)
            {
                try
                {
                    if (File.Exists(assetPath))
                    {
                        unusedAssetSize += new FileInfo(assetPath).Length;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Could not get file size for {assetPath}: {ex.Message}");
                }
            }

            // Pre-cache some previews for performance
            PreCachePreviews(unusedAssets, 50);

            // Complete analysis
            currentState = AnalysisState.Complete;
            analysisProgress = 1.0f;
            EditorUtility.ClearProgressBar();

            Debug.Log($"Analysis complete. Found {unusedAssets.Count} unused assets out of {totalAssetsCount} total assets.");
            if (corruptedAssets.Count > 0)
            {
                Debug.LogWarning($"Found {corruptedAssets.Count} corrupted assets that were excluded from analysis.");
            }

            // Clean up
            EditorApplication.update -= PerformAnalysis;
            Repaint();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during asset analysis: {ex.Message}\n{ex.StackTrace}");
            currentState = AnalysisState.Idle;
            EditorUtility.ClearProgressBar();
            EditorApplication.update -= PerformAnalysis;
        }
    }

    private void PreCachePreviews(List<string> assets, int maxCount)
    {
        int count = 0;
        foreach (string assetPath in assets)
        {
            if (count >= maxCount) break;

            if (CanShowPreview(assetPath))
            {
                GetAssetPreview(assetPath);
                count++;
            }
        }
    }

    private void DisplayAssetList(List<string> assets, bool categorized, bool compact)
    {
        List<string> filteredAssets = string.IsNullOrEmpty(searchFilter)
            ? assets
            : assets.Where(path => path.ToLower().Contains(searchFilter.ToLower())).ToList();

        if (filteredAssets.Count == 0)
        {
            EditorGUILayout.HelpBox("No assets found matching the current filters.", MessageType.Info);
            return;
        }

        if (string.IsNullOrEmpty(searchFilter) == false)
        {
            EditorGUILayout.LabelField($"Filtered results: {filteredAssets.Count} items");
        }

        if (categorized)
        {
            Dictionary<string, List<string>> categorizedAssets = CategorizeAssets(filteredAssets);

            foreach (var category in categorizedAssets.OrderBy(kv => kv.Key))
            {
                if (category.Value.Count > 0)
                {
                    EditorGUILayout.LabelField($"{category.Key} ({category.Value.Count})", EditorStyles.boldLabel);

                    foreach (var asset in category.Value)
                    {
                        DisplayAssetEntry(asset, compact);
                    }

                    EditorGUILayout.Space();
                }
            }
        }
        else
        {
            foreach (var asset in filteredAssets)
            {
                DisplayAssetEntry(asset, compact);
            }
        }
    }

    private void DisplayCorruptedAssetsList()
    {
        EditorGUILayout.LabelField("Corrupted Assets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These assets couldn't be analyzed properly and were excluded from the dependency analysis.", MessageType.Warning);

        if (corruptedAssets.Count == 0)
        {
            EditorGUILayout.LabelField("No corrupted assets were found.");
            return;
        }

        List<string> filteredAssets = string.IsNullOrEmpty(searchFilter)
            ? corruptedAssets
            : corruptedAssets.Where(path => path.ToLower().Contains(searchFilter.ToLower())).ToList();

        foreach (var asset in filteredAssets)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(asset);
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }

            EditorGUILayout.LabelField(asset, EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DisplayAssetEntry(string assetPath, bool compact)
    {
        try
        {
            bool isSelected = assetPath == selectedAsset;

            // Use a different background color for selected asset
            if (isSelected)
                GUI.backgroundColor = new Color(0.8f, 0.85f, 1f);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.backgroundColor = Color.white; // Reset color

            if (GUILayout.Button("Select", GUILayout.Width(60)))
            {
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                selectedAsset = assetPath;
            }

            // Asset preview
            if (showPreview && CanShowPreview(assetPath))
            {
                Texture2D preview = GetAssetPreview(assetPath);
                if (preview != null)
                {
                    GUILayout.Box(preview, GUILayout.Width(previewSize), GUILayout.Height(previewSize));
                }
            }

            if (compact)
            {
                if (GUILayout.Button(Path.GetFileName(assetPath), GUILayout.ExpandWidth(true)))
                {
                    selectedAsset = assetPath;
                }

                long fileSize = 0;
                try
                {
                    if (File.Exists(assetPath))
                    {
                        fileSize = new FileInfo(assetPath).Length;
                    }
                }
                catch { }

                EditorGUILayout.LabelField(GetSizeString(fileSize), GUILayout.Width(60));
            }
            else
            {
                if (GUILayout.Button(assetPath, GUILayout.ExpandWidth(true)))
                {
                    selectedAsset = assetPath;
                }
            }

            EditorGUILayout.EndHorizontal();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error displaying asset {assetPath}: {ex.Message}");
        }
    }

    private bool CanShowPreview(string assetPath)
    {
        string ext = Path.GetExtension(assetPath).ToLower();
        return ext == ".png" || ext == ".jpg" || ext == ".jpeg" ||
               ext == ".tga" || ext == ".psd" || ext == ".prefab" ||
               ext == ".fbx" || ext == ".obj" || ext == ".mat" ||
               ext == ".asset" || ext == ".controller";
    }

    private Texture2D GetAssetPreview(string assetPath)
    {
        if (previewCache.ContainsKey(assetPath))
            return previewCache[assetPath];

        try
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj == null) return null;

            Texture2D preview = AssetPreview.GetAssetPreview(obj);
            if (preview == null)
                preview = AssetPreview.GetMiniThumbnail(obj);

            if (preview != null)
                previewCache[assetPath] = preview;

            return preview;
        }
        catch
        {
            return null;
        }
    }

    private void DisplayDependencyFlow(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Dependency Flow Chart", EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"Selected Asset: {assetPath}", EditorStyles.wordWrappedLabel);

        // Flow chart is displayed as an indented hierarchy
        dependencyScrollPosition = EditorGUILayout.BeginScrollView(dependencyScrollPosition, GUILayout.Height(200));

        // Display dependencies (what this asset depends on)
        if (dependencyCache.ContainsKey(assetPath) && dependencyCache[assetPath].Count > 0)
        {
            EditorGUILayout.LabelField("Direct Dependencies (this asset depends on):", EditorStyles.boldLabel);
            foreach (string dependency in dependencyCache[assetPath].OrderBy(d => d))
            {
                DisplayDependencyNode(dependency, 1, maxDepth, true);
            }
        }
        else
        {
            EditorGUILayout.LabelField("This asset has no dependencies.");
        }

        EditorGUILayout.Space();

        // Display reverse dependencies (what depends on this asset)
        if (dependsByCache.ContainsKey(assetPath) && dependsByCache[assetPath].Count > 0)
        {
            EditorGUILayout.LabelField("Reverse Dependencies (assets that depend on this):", EditorStyles.boldLabel);
            foreach (string dependedBy in dependsByCache[assetPath].OrderBy(d => d))
            {
                DisplayDependencyNode(dependedBy, 1, maxDepth, false);
            }
        }
        else
        {
            EditorGUILayout.LabelField("No other assets depend on this asset.");
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DisplayDependencyNode(string assetPath, int depth, int maxDepth, bool isDependency)
    {
        string indent = new string(' ', depth * 4);
        bool isUsed = usedAssets.Contains(assetPath);

        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(depth * 20); // Indentation

        // Small preview icon
        if (showPreview && CanShowPreview(assetPath))
        {
            Texture2D preview = GetAssetPreview(assetPath);
            if (preview != null)
            {
                GUILayout.Box(preview, GUILayout.Width(16), GUILayout.Height(16));
            }
        }

        // Use different colors for used vs unused assets
        GUI.color = isUsed ? Color.black : Color.red;

        if (GUILayout.Button(Path.GetFileName(assetPath), EditorStyles.label))
        {
            selectedAsset = assetPath;
        }

        GUI.color = Color.white; // Reset color

        EditorGUILayout.EndHorizontal();

        // Show next level of dependencies if needed
        if (showFullDependencyChain && depth < maxDepth)
        {
            if (isDependency && dependencyCache.ContainsKey(assetPath))
            {
                foreach (string dependency in dependencyCache[assetPath].OrderBy(d => d).Take(5)) // Limit to 5 per node
                {
                    DisplayDependencyNode(dependency, depth + 1, maxDepth, true);
                }

                if (dependencyCache[assetPath].Count > 5)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space((depth + 1) * 20);
                    EditorGUILayout.LabelField($"... and {dependencyCache[assetPath].Count - 5} more");
                    EditorGUILayout.EndHorizontal();
                }
            }
            else if (!isDependency && dependsByCache.ContainsKey(assetPath))
            {
                foreach (string dependedBy in dependsByCache[assetPath].OrderBy(d => d).Take(5)) // Limit to 5 per node
                {
                    DisplayDependencyNode(dependedBy, depth + 1, maxDepth, false);
                }

                if (dependsByCache[assetPath].Count > 5)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space((depth + 1) * 20);
                    EditorGUILayout.LabelField($"... and {dependsByCache[assetPath].Count - 5} more");
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
    }

    private Dictionary<string, List<string>> CategorizeAssets(List<string> assets)
    {
        Dictionary<string, List<string>> categorized = new Dictionary<string, List<string>>();

        foreach (var asset in assets)
        {
            string extension = Path.GetExtension(asset).ToLower();
            string category;

            switch (extension)
            {
                case ".cs":
                    category = "Scripts";
                    break;
                case ".unity":
                    category = "Scenes";
                    break;
                case ".prefab":
                    category = "Prefabs";
                    break;
                case ".mat":
                    category = "Materials";
                    break;
                case ".fbx":
                case ".obj":
                case ".blend":
                    category = "Models";
                    break;
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".psd":
                    category = "Textures";
                    break;
                case ".wav":
                case ".mp3":
                case ".ogg":
                    category = "Audio";
                    break;
                case ".shader":
                case ".cginc":
                    category = "Shaders";
                    break;
                case ".anim":
                case ".controller":
                    category = "Animation";
                    break;
                case ".asset":
                    category = "Assets";
                    break;
                case ".ttf":
                case ".otf":
                    category = "Fonts";
                    break;
                default:
                    category = "Other";
                    break;
            }

            if (!categorized.ContainsKey(category))
            {
                categorized[category] = new List<string>();
            }

            categorized[category].Add(asset);
        }

        return categorized;
    }

    private bool IsExcludedPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return true;

        // Check if path is in excluded folders
        foreach (var excludedFolder in excludeFolders)
        {
            if (path.StartsWith(excludedFolder) ||
                (ignoreEditorAssets && (path.Contains("/Editor/") || path.EndsWith("/Editor"))))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsExcludedExtension(string path)
    {
        string ext = Path.GetExtension(path).ToLower();
        return excludeExtensions.Contains(ext);
    }

    private void MarkAssetAsUsed(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return;

        if (assetUsageStatus.ContainsKey(assetPath))
        {
            assetUsageStatus[assetPath] = true;
            usedAssets.Add(assetPath);
        }
    }

    private void MarkDependenciesAsUsed(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return;

        if (dependencyCache.TryGetValue(assetPath, out HashSet<string> dependencies))
        {
            foreach (string dependency in dependencies)
            {
                if (!IsExcludedPath(dependency))
                {
                    MarkAssetAsUsed(dependency);
                }
            }
        }
    }

    private string GetSizeString(long size)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double len = size;
        int order = 0;

        while (len >= 1024 && order < units.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {units[order]}";
    }

    private void MoveUnusedAssets()
    {
        if (unusedAssets == null || unusedAssets.Count == 0)
        {
            EditorUtility.DisplayDialog("No Unused Assets", "No unused assets were found.", "OK");
            return;
        }

        // Confirm with user
        bool proceed = EditorUtility.DisplayDialog(
            "Move Unused Assets",
            $"Are you sure you want to move {unusedAssets.Count} unused assets to {outputFolderPath}?",
            "Yes", "Cancel");

        if (!proceed) return;

        try
        {
            // Create output directory if it doesn't exist
            if (!AssetDatabase.IsValidFolder(outputFolderPath))
            {
                string parentFolder = Path.GetDirectoryName(outputFolderPath);
                string newFolderName = Path.GetFileName(outputFolderPath);

                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    // Create parent directories recursively
                    CreateDirectoryRecursively(parentFolder);
                }

                AssetDatabase.CreateFolder(parentFolder, newFolderName);
            }

            int successCount = 0;
            List<string> failedAssets = new List<string>();

            // Move all unused assets
            foreach (string assetPath in unusedAssets)
            {
                try
                {
                    string fileName = Path.GetFileName(assetPath);

                    // Create subdirectory structure in target folder
                    string relativeDir = Path.GetDirectoryName(assetPath).Replace("Assets/", "");
                    string targetDir = Path.Combine(outputFolderPath, relativeDir);

                    if (!AssetDatabase.IsValidFolder(targetDir))
                    {
                        CreateDirectoryRecursively(targetDir);
                    }

                    string destPath = Path.Combine(targetDir, fileName);

                    // Make sure the destination path is unique
                    destPath = AssetDatabase.GenerateUniqueAssetPath(destPath);

                    // Move the asset
                    string result = AssetDatabase.MoveAsset(assetPath, destPath);
                    if (string.IsNullOrEmpty(result))
                    {
                        successCount++;
                    }
                    else
                    {
                        failedAssets.Add(assetPath + " - " + result);
                    }
                }
                catch (Exception ex)
                {
                    failedAssets.Add(assetPath + " - " + ex.Message);
                }
            }

            AssetDatabase.Refresh();

            if (failedAssets.Count > 0)
            {
                Debug.LogWarning($"Failed to move {failedAssets.Count} assets:");
                foreach (var failed in failedAssets)
                {
                    Debug.LogWarning(failed);
                }

                EditorUtility.DisplayDialog("Operation Partially Complete",
                    $"Moved {successCount} unused assets to {outputFolderPath}\nFailed to move {failedAssets.Count} assets. See console for details.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Operation Complete",
                    $"Successfully moved {successCount} unused assets to {outputFolderPath}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error moving unused assets: {ex.Message}\n{ex.StackTrace}");
            EditorUtility.DisplayDialog("Error", $"An error occurred while moving unused assets: {ex.Message}", "OK");
        }
    }

    private void CreateDirectoryRecursively(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parentDir = Path.GetDirectoryName(path);
        string folderName = Path.GetFileName(path);

        // Create parent directories first
        if (!AssetDatabase.IsValidFolder(parentDir))
        {
            CreateDirectoryRecursively(parentDir);
        }

        AssetDatabase.CreateFolder(parentDir, folderName);
    }

    private void ExportResults()
    {
        if ((unusedAssets == null || unusedAssets.Count == 0) && !showUsedAssets)
        {
            EditorUtility.DisplayDialog("No Results", "There are no results to export.", "OK");
            return;
        }

        string fileName = showUsedAssets ? "UsedAssets.csv" : "UnusedAssets.csv";
        string savePath = EditorUtility.SaveFilePanel("Export Results", "", fileName, "csv");
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            using (StreamWriter writer = new StreamWriter(savePath))
            {
                // Write header
                writer.WriteLine("Asset Path,Type,Size (bytes),Last Modified,Dependencies Count,Referenced By Count");

                // Get list to export
                List<string> assetsToExport = showUsedAssets ? usedAssets.ToList() : unusedAssets;

                // Apply filter if needed
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    assetsToExport = assetsToExport
                        .Where(path => path.ToLower().Contains(searchFilter.ToLower()))
                        .ToList();
                }

                // Write asset data
                foreach (string assetPath in assetsToExport)
                {
                    string extension = Path.GetExtension(assetPath);
                    long size = 0;
                    DateTime lastModified = DateTime.MinValue;

                    try
                    {
                        if (File.Exists(assetPath))
                        {
                            FileInfo fileInfo = new FileInfo(assetPath);
                            size = fileInfo.Length;
                            lastModified = fileInfo.LastWriteTime;
                        }
                    }
                    catch { }

                    int dependsOnCount = dependencyCache.ContainsKey(assetPath) ? dependencyCache[assetPath].Count : 0;
                    int referencedByCount = dependsByCache.ContainsKey(assetPath) ? dependsByCache[assetPath].Count : 0;

                    writer.WriteLine($"\"{assetPath}\",{extension},{size},{lastModified.ToString("yyyy-MM-dd HH:mm:ss")},{dependsOnCount},{referencedByCount}");
                }
            }

            EditorUtility.DisplayDialog("Export Complete", $"Results exported to:\n{savePath}", "OK");

            // Open the file in the default CSV application
            if (EditorUtility.DisplayDialog("Open File", "Do you want to open the exported file?", "Yes", "No"))
            {
                Application.OpenURL("file://" + savePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error exporting results: {ex.Message}");
            EditorUtility.DisplayDialog("Export Error", $"An error occurred while exporting results: {ex.Message}", "OK");
        }
    }

    private void ExportDependencyGraph()
    {
        string savePath = EditorUtility.SaveFilePanel("Export Dependency Graph", "", "DependencyGraph", "html");
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            using (StreamWriter writer = new StreamWriter(savePath))
            {
                writer.WriteLine("<!DOCTYPE html>");
                writer.WriteLine("<html>");
                writer.WriteLine("<head>");
                writer.WriteLine("    <title>Unity Asset Dependency Graph</title>");
                writer.WriteLine("    <style>");
                writer.WriteLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
                writer.WriteLine("        h1, h2, h3 { color: #333; }");
                writer.WriteLine("        .stats { background-color: #f5f5f5; padding: 10px; border-radius: 5px; margin-bottom: 20px; }");
                writer.WriteLine("        .search { margin-bottom: 20px; }");
                writer.WriteLine("        #searchInput { width: 100%; padding: 8px; box-sizing: border-box; }");
                writer.WriteLine("        .asset-item { border: 1px solid #ddd; margin-bottom: 8px; padding: 8px; border-radius: 4px; }");
                writer.WriteLine("        .asset-item:hover { background-color: #f0f0f0; }");
                writer.WriteLine("        .asset-name { font-weight: bold; }");
                writer.WriteLine("        .asset-path { color: #666; font-size: 0.9em; }");
                writer.WriteLine("        .asset-stats { color: #333; margin-top: 5px; }");
                writer.WriteLine("        .used { background-color: #e8f5e9; }");
                writer.WriteLine("        .unused { background-color: #ffebee; }");
                writer.WriteLine("        .corrupted { background-color: #fff3e0; }");
                writer.WriteLine("        .dependency-list { margin-left: 20px; margin-top: 5px; }");
                writer.WriteLine("        .dependency-item { padding: 2px 5px; margin: 2px 0; border-radius: 3px; display: inline-block; margin-right: 5px; }");
                writer.WriteLine("        .toggle-btn { cursor: pointer; color: blue; text-decoration: underline; }");
                writer.WriteLine("        .hidden { display: none; }");
                writer.WriteLine("    </style>");
                writer.WriteLine("    <script>");
                writer.WriteLine("        function filterAssets() {");
                writer.WriteLine("            var input = document.getElementById('searchInput').value.toLowerCase();");
                writer.WriteLine("            var assets = document.getElementsByClassName('asset-item');");
                writer.WriteLine("            for (var i = 0; i < assets.length; i++) {");
                writer.WriteLine("                var assetText = assets[i].textContent.toLowerCase();");
                writer.WriteLine("                if (assetText.includes(input)) {");
                writer.WriteLine("                    assets[i].style.display = '';");
                writer.WriteLine("                } else {");
                writer.WriteLine("                    assets[i].style.display = 'none';");
                writer.WriteLine("                }");
                writer.WriteLine("            }");
                writer.WriteLine("        }");
                writer.WriteLine("        function toggleDependencies(id) {");
                writer.WriteLine("            var element = document.getElementById(id);");
                writer.WriteLine("            if (element.classList.contains('hidden')) {");
                writer.WriteLine("                element.classList.remove('hidden');");
                writer.WriteLine("            } else {");
                writer.WriteLine("                element.classList.add('hidden');");
                writer.WriteLine("            }");
                writer.WriteLine("        }");
                writer.WriteLine("    </script>");
                writer.WriteLine("</head>");
                writer.WriteLine("<body>");
                writer.WriteLine("    <h1>Unity Asset Dependency Graph</h1>");
                writer.WriteLine("    <div class='stats'>");
                writer.WriteLine($"        <p><strong>Total Assets:</strong> {totalAssetsCount} | <strong>Used Assets:</strong> {usedAssets.Count} | <strong>Unused Assets:</strong> {unusedAssets.Count}</p>");
                writer.WriteLine($"        <p><strong>Total Size:</strong> {GetSizeString(totalAssetSize)} | <strong>Unused Size:</strong> {GetSizeString(unusedAssetSize)} ({(totalAssetSize > 0 ? unusedAssetSize * 100f / totalAssetSize : 0):F1}%)</p>");
                writer.WriteLine($"        <p><strong>Corrupted Assets:</strong> {corruptedAssets.Count}</p>");
                writer.WriteLine($"        <p><strong>Report Generated:</strong> {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}</p>");
                writer.WriteLine("    </div>");

                writer.WriteLine("    <div class='search'>");
                writer.WriteLine("        <input type='text' id='searchInput' placeholder='Search assets...' onkeyup='filterAssets()' />");
                writer.WriteLine("    </div>");

                // Write dependency flow data for interactive visualization
                writer.WriteLine("    <h2>Unused Assets</h2>");
                int unusedAssetsCount = 0;
                foreach (string assetPath in unusedAssets)
                {
                    if (unusedAssetsCount >= 1000) // Limit to first 1000 to avoid massive files
                    {
                        writer.WriteLine("    <p>Only showing first 1000 unused assets. Total: " + unusedAssets.Count + "</p>");
                        break;
                    }

                    string assetClass = "asset-item unused";
                    string assetName = Path.GetFileName(assetPath);
                    string extension = Path.GetExtension(assetPath).ToLower();

                    writer.WriteLine($"    <div class='{assetClass}' id='asset-{unusedAssetsCount}'>");
                    writer.WriteLine($"        <div class='asset-name'>{assetName}</div>");
                    writer.WriteLine($"        <div class='asset-path'>{assetPath}</div>");

                    // Asset stats
                    long size = 0;
                    try { if (File.Exists(assetPath)) size = new FileInfo(assetPath).Length; } catch { }

                    int dependsOnCount = dependencyCache.ContainsKey(assetPath) ? dependencyCache[assetPath].Count : 0;
                    int referencedByCount = dependsByCache.ContainsKey(assetPath) ? dependsByCache[assetPath].Count : 0;

                    writer.WriteLine($"        <div class='asset-stats'>Type: {extension}, Size: {GetSizeString(size)}, Dependencies: {dependsOnCount}, Referenced by: {referencedByCount}</div>");

                    // Dependencies
                    if (dependsOnCount > 0)
                    {
                        writer.WriteLine($"        <div><span class='toggle-btn' onclick=\"toggleDependencies('depends-{unusedAssetsCount}')\">Dependencies ▼</span></div>");
                        writer.WriteLine($"        <div class='dependency-list hidden' id='depends-{unusedAssetsCount}'>");

                        foreach (string dependency in dependencyCache[assetPath].OrderBy(d => d).Take(20))
                        {
                            string depClass = usedAssets.Contains(dependency) ? "dependency-item used" : "dependency-item unused";
                            writer.WriteLine($"            <div class='{depClass}'>{Path.GetFileName(dependency)}</div>");
                        }

                        if (dependencyCache[assetPath].Count > 20)
                        {
                            writer.WriteLine($"            <div>... and {dependencyCache[assetPath].Count - 20} more</div>");
                        }

                        writer.WriteLine("        </div>");
                    }

                    // Assets that reference this
                    if (referencedByCount > 0)
                    {
                        writer.WriteLine($"        <div><span class='toggle-btn' onclick=\"toggleDependencies('referenced-{unusedAssetsCount}')\">Referenced By ▼</span></div>");
                        writer.WriteLine($"        <div class='dependency-list hidden' id='referenced-{unusedAssetsCount}'>");

                        foreach (string refBy in dependsByCache[assetPath].OrderBy(d => d).Take(20))
                        {
                            string refClass = usedAssets.Contains(refBy) ? "dependency-item used" : "dependency-item unused";
                            writer.WriteLine($"            <div class='{refClass}'>{Path.GetFileName(refBy)}</div>");
                        }

                        if (dependsByCache[assetPath].Count > 20)
                        {
                            writer.WriteLine($"            <div>... and {dependsByCache[assetPath].Count - 20} more</div>");
                        }

                        writer.WriteLine("        </div>");
                    }

                    writer.WriteLine("    </div>");
                    unusedAssetsCount++;
                }

                if (corruptedAssets.Count > 0)
                {
                    writer.WriteLine("    <h2>Corrupted Assets</h2>");
                    writer.WriteLine("    <p>These assets couldn't be analyzed properly and were excluded from dependency analysis.</p>");

                    int corruptedCount = 0;
                    foreach (string assetPath in corruptedAssets)
                    {
                        writer.WriteLine($"    <div class='asset-item corrupted'>");
                        writer.WriteLine($"        <div class='asset-name'>{Path.GetFileName(assetPath)}</div>");
                        writer.WriteLine($"        <div class='asset-path'>{assetPath}</div>");
                        writer.WriteLine("    </div>");

                        corruptedCount++;
                        if (corruptedCount >= 100) // Limit to first 100
                        {
                            writer.WriteLine("    <p>Only showing first 100 corrupted assets. Total: " + corruptedAssets.Count + "</p>");
                            break;
                        }
                    }
                }

                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }

            EditorUtility.DisplayDialog("Export Complete", $"Dependency graph exported to:\n{savePath}", "OK");

            // Open the file in the default browser
            if (EditorUtility.DisplayDialog("Open File", "Do you want to open the exported file?", "Yes", "No"))
            {
                Application.OpenURL("file://" + savePath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error exporting dependency graph: {ex.Message}");
            EditorUtility.DisplayDialog("Export Error", $"An error occurred while exporting the dependency graph: {ex.Message}", "OK");
        }
    }
}