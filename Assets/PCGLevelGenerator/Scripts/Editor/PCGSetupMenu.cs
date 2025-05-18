using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;
using System.Collections.Generic;

// ██████╗  ██████╗  ██████╗    ██╗     ███████╗██╗   ██╗███████╗██╗         ███╗   ███╗ █████╗ ███████╗████████╗███████╗██████╗  
// ██╔══██╗██╔════╝ ██╔════╝    ██║     ██╔════╝██║   ██║██╔════╝██║         ████╗ ████║██╔══██╗██╔════╝╚══██╔══╝██╔════╝██╔══██╗ 
// ██████╔╝██║      ██║  ███╗   ██║     █████╗  ██║   ██║█████╗  ██║         ██╔████╔██║███████║███████╗   ██║   █████╗  ██████╔╝ 
// ██╔═══╝ ██║      ██║   ██║   ██║     ██╔══╝  ╚██╗ ██╔╝██╔══╝  ██║         ██║╚██╔╝██║██╔══██║╚════██║   ██║   ██╔══╝  ██╔══██╗ 
// ██║     ╚██████╗ ╚██████╔╝   ███████╗███████╗ ╚████╔╝ ███████╗███████╗    ██║ ╚═╝ ██║██║  ██║███████║   ██║   ███████╗██║  ██║ 
// ╚═╝      ╚═════╝  ╚═════╝    ╚══════╝╚══════╝  ╚═══╝  ╚══════╝╚══════╝    ╚═╝     ╚═╝╚═╝  ╚═╝╚══════╝   ╚═╝   ╚══════╝╚═╝  ╚═╝ 
//
// PCG Level Generator for Unity
// Copyright © 2025 Dineshkumar, Mahmud Hasan, Kevin A. Moberly, & Kamalanathan
// Version: 1.0.0

namespace PCGLevelGenerator
{
    public static class PCGSetupMenu
    {
        // Implementation method - NOT directly tied to the menu anymore
        public static void SetupCompletePCGSystem()
        {
            // Check if components already exist
            GameObject existingGenerator = GameObject.Find("HybridLevelGenerator");
            GameObject existingGrid = GameObject.Find("Grid");

            if (existingGenerator != null || existingGrid != null)
            {
                bool proceed = EditorUtility.DisplayDialog("Components Already Exist",
                    "Some PCG Level Generator components already exist in the scene. " +
                    "Do you want to replace them?", "Replace", "Cancel");

                if (!proceed) return;

                // Destroy existing components
                if (existingGrid != null) Object.DestroyImmediate(existingGrid);
                if (existingGenerator != null) Object.DestroyImmediate(existingGenerator);
            }

            // Create required sorting layers
            CreateSortingLayers();

            // --- Create Grid with exactly three Tilemaps ---
            GameObject gridObject = new GameObject("Grid");
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            // Create only the three tilemaps required with proper sorting layers
            Tilemap groundTilemap = CreateTilemap(gridObject, "GroundMap", 0, "Ground");
            Tilemap wallTilemap = CreateTilemap(gridObject, "WallMap", 1, "Walls");
            Tilemap decorTilemap = CreateTilemap(gridObject, "DecorMap", 2, "Decors");

            // --- Create HybridLevelGenerator with child holders ---
            GameObject generatorObject = new GameObject("HybridLevelGenerator");
            HybridLevelGenerator generator = generatorObject.AddComponent<HybridLevelGenerator>();

            // Create holder objects
            CreateHolderObject(generatorObject, "PlayerHolder");
            CreateHolderObject(generatorObject, "EnemiesHolder");
            CreateHolderObject(generatorObject, "DecorationsHolder");

            // --- Assign references to the generator ---
            generator.groundTilemap = groundTilemap;
            generator.wallTilemap = wallTilemap;

            // Find and assign floor/wall tiles with proper filtering
            FindAndAssignTiles(generator);

            // Auto-assign prefabs from the PCGLevelGenerator/Prefabs folder
            AssignPrefabs(generator);

            // Set default parameters
            generator.levelWidth = 100;
            generator.levelHeight = 100;
            generator.corridorWidth = 2;
            generator.roomPadding = 2f;
            SetupCameraFollow();
            // Select the generator in the hierarchy
            Selection.activeGameObject = generatorObject;
            generator.generationMode = GenerationMode.FullyProcedural;

            // Show the setup complete dialog using EditorUtility.DisplayDialog instead
            EditorUtility.DisplayDialog("PCG Level Generator Setup Complete",
                "The PCG Level Generator has been successfully set up in your scene!\n\n" +
                "Next Steps:\n" +
                "1. Choose a generation mode\n" +
                "2. Click 'Generate Level' in the Inspector\n\n" +
                "For user-defined layouts, use the 'Open Visual Level Designer' button.",
                "Got it!");
        }

        // Create required sorting layers if they don't exist
        private static void CreateSortingLayers()
        {
            string[] requiredLayers = new string[] { "Ground", "Walls", "Decors", "Player", "UI" };

            // Get the TagManager asset
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
            );

            // Get the sorting layers array
            SerializedProperty sortingLayersProp = tagManager.FindProperty("m_SortingLayers");

            if (sortingLayersProp != null)
            {
                // Check existing layers to avoid duplicates
                HashSet<string> existingLayers = new HashSet<string>();
                for (int i = 0; i < sortingLayersProp.arraySize; i++)
                {
                    SerializedProperty layerProp = sortingLayersProp.GetArrayElementAtIndex(i);
                    string layerName = layerProp.FindPropertyRelative("name").stringValue;
                    existingLayers.Add(layerName);
                }

                // Add any missing layers
                foreach (string layerName in requiredLayers)
                {
                    if (!existingLayers.Contains(layerName))
                    {
                        // Add a new layer
                        sortingLayersProp.arraySize++;
                        SerializedProperty newLayer = sortingLayersProp.GetArrayElementAtIndex(sortingLayersProp.arraySize - 1);

                        // Set a unique ID
                        newLayer.FindPropertyRelative("uniqueID").intValue = layerName.GetHashCode();
                        newLayer.FindPropertyRelative("name").stringValue = layerName;
                        newLayer.FindPropertyRelative("locked").boolValue = false;

                        Debug.Log($"Created sorting layer: {layerName}");
                    }
                }

                // Apply the changes
                tagManager.ApplyModifiedProperties();
            }
        }

        private static void SetupCameraFollow()
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
                // Try to find the CameraFollow script type by path
                string scriptPath = "Assets/PCGLevelGenerator/Scripts/Core/CameraFollow.cs";
                MonoScript scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);

                if (scriptAsset != null)
                {
                    // Add the script component
                    mainCamera.gameObject.AddComponent(scriptAsset.GetClass());
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


        private static void FindAndAssignTiles(HybridLevelGenerator generator)
        {
            Debug.Log("Finding and assigning floor and wall tiles...");

            // First try to find tiles in the Floor and Wall Tiles folder ONLY
            string mainFolderPath = "Assets/PCGLevelGenerator/Tiles/Floor and Wall Tiles";
            if (Directory.Exists(mainFolderPath))
            {
                Debug.Log("Searching for tiles in: " + mainFolderPath);

                // Try to find the wall tile first with specific names
                string[] wallTileNames = {
            "Wall Tile.asset",
            "WallTile.asset",
            "Wall Tile 1.asset",
            "WallTile1.asset"
        };

                foreach (string tileName in wallTileNames)
                {
                    string fullPath = Path.Combine(mainFolderPath, tileName);
                    if (File.Exists(fullPath))
                    {
                        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(fullPath);
                        if (tile != null)
                        {
                            generator.wallTile = tile;
                            Debug.Log("Assigned Wall Tile: " + tile.name);
                            break;
                        }
                    }
                }

                // If no specific wall tile found, search for any with "Wall" in name
                if (generator.wallTile == null)
                {
                    string[] assetFiles = Directory.GetFiles(mainFolderPath, "*.asset", SearchOption.TopDirectoryOnly);
                    foreach (string assetPath in assetFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(assetPath);

                        // Only look for wall tiles that are not directional
                        if (fileName.Contains("Wall") &&
                            !fileName.Contains("Bottom") &&
                            !fileName.Contains("Top") &&
                            !fileName.Contains("Left") &&
                            !fileName.Contains("Right") &&
                            !fileName.Contains("Corner") &&
                            !fileName.Contains("Inner") &&
                            !fileName.Contains("Outer"))
                        {
                            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(assetPath);
                            if (tile != null)
                            {
                                generator.wallTile = tile;
                                Debug.Log("Assigned Wall Tile: " + tile.name);
                                break;
                            }
                        }
                    }
                }

                // Now look for floor tile
                string[] floorTileNames = {
            "Floor Tile.asset",
            "FloorTile.asset",
            "Floor Tile 1.asset",
            "FloorTile1.asset"
        };

                foreach (string tileName in floorTileNames)
                {
                    string fullPath = Path.Combine(mainFolderPath, tileName);
                    if (File.Exists(fullPath))
                    {
                        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(fullPath);
                        if (tile != null)
                        {
                            generator.floorTile = tile;
                            Debug.Log("Assigned Floor Tile: " + tile.name);
                            break;
                        }
                    }
                }

                // If no specific floor tile found, search for any with "Floor" in name
                if (generator.floorTile == null)
                {
                    string[] assetFiles = Directory.GetFiles(mainFolderPath, "*.asset", SearchOption.TopDirectoryOnly);
                    foreach (string assetPath in assetFiles)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(assetPath);

                        if (fileName.Contains("Floor"))
                        {
                            TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(assetPath);
                            if (tile != null)
                            {
                                generator.floorTile = tile;
                                Debug.Log("Assigned Floor Tile: " + tile.name);
                                break;
                            }
                        }
                    }
                }
            }

            // Fallback to project-wide search if needed, but still filter directional tiles
            if (generator.wallTile == null || generator.floorTile == null)
            {
                Debug.Log("Falling back to project-wide search for missing tiles");

                string[] allTileGuids = AssetDatabase.FindAssets("t:TileBase");

                foreach (string guid in allTileGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string fileName = Path.GetFileNameWithoutExtension(path);

                    // Skip any directional tiles
                    if (path.Contains("Directional") ||
                        fileName.Contains("Bottom") ||
                        fileName.Contains("Top") ||
                        fileName.Contains("Left") ||
                        fileName.Contains("Right") ||
                        fileName.Contains("Inner") ||
                        fileName.Contains("Outer") ||
                        fileName.Contains("Corner"))
                    {
                        continue;
                    }

                    // Wall tile
                    if (generator.wallTile == null && fileName.Contains("Wall"))
                    {
                        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                        if (tile != null)
                        {
                            generator.wallTile = tile;
                            Debug.Log("Assigned Wall Tile (from project): " + tile.name);
                        }
                    }

                    // Floor tile
                    if (generator.floorTile == null && fileName.Contains("Floor"))
                    {
                        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                        if (tile != null)
                        {
                            generator.floorTile = tile;
                            Debug.Log("Assigned Floor Tile (from project): " + tile.name);
                        }
                    }
                }
            }

            // Report results
            if (generator.wallTile != null)
                Debug.Log("Wall Tile assigned: " + generator.wallTile.name);
            else
                Debug.LogWarning("Failed to assign Wall Tile!");

            if (generator.floorTile != null)
                Debug.Log("Floor Tile assigned: " + generator.floorTile.name);
            else
                Debug.LogWarning("Failed to assign Floor Tile!");
        }

        private static Tilemap CreateTilemap(GameObject parent, string name, int sortingOrder, string sortingLayer = "Default")
        {
            GameObject tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent.transform);

            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();

            // Set properties
            renderer.sortingOrder = sortingOrder;

            // Set sorting layer if it exists
            renderer.sortingLayerName = sortingLayer;

            // Add TilemapCollider2D if this is the WallMap
            if (name == "WallMap")
            {
                tilemapObject.AddComponent<TilemapCollider2D>();
            }

            return tilemap;
        }

        private static GameObject CreateHolderObject(GameObject parent, string name)
        {
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(parent.transform);
            holder.transform.localPosition = Vector3.zero;
            return holder;
        }

        private static void AssignPrefabs(HybridLevelGenerator generator)
        {
            // Auto-assign prefabs from the PCGLevelGenerator/Prefabs folder
            string prefabsPath = "Assets/PCGLevelGenerator/Prefabs";

            // Try to find Player prefab
            GameObject playerPrefab = FindPrefab(prefabsPath, "Player");
            if (playerPrefab != null)
            {
                generator.playerPrefab = playerPrefab;
                SetPrefabSortingLayer(playerPrefab, "Player");
            }

            // Try to find Enemy prefab
            GameObject enemyPrefab = FindPrefab(prefabsPath, "Enemy");
            if (enemyPrefab != null)
            {
                generator.enemyPrefab = enemyPrefab;
                SetPrefabSortingLayer(enemyPrefab, "Player");
            }

            // Try to find Tree/Decoration prefab
            GameObject decorPrefab = FindPrefab(prefabsPath, "Tree");
            if (decorPrefab == null) // Try alternative name if Tree not found
                decorPrefab = FindPrefab(prefabsPath, "Decoration");

            if (decorPrefab != null)
            {
                generator.decorationPrefab = decorPrefab;
                SetPrefabSortingLayer(decorPrefab, "Decors");
            }

            // Find and set sorting layers for bullet prefabs
            GameObject bulletEnPrefab = FindPrefab(prefabsPath, "BulletEn");
            if (bulletEnPrefab != null)
            {
                SetPrefabSortingLayer(bulletEnPrefab, "Player");
            }

            GameObject bulletPlPrefab = FindPrefab(prefabsPath, "BulletPl");
            if (bulletPlPrefab != null)
            {
                SetPrefabSortingLayer(bulletPlPrefab, "Player");
            }

            // Try to find Room Templates for hybrid generation
            string templatePath = prefabsPath + "/Room Templates";
            if (Directory.Exists(templatePath))
            {
                string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { templatePath });
                List<GameObject> templates = new List<GameObject>();

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (template != null && template.GetComponentInChildren<Tilemap>() != null)
                    {
                        templates.Add(template);
                    }
                }

                if (templates.Count > 0)
                {
                    generator.roomTemplatePrefabs = templates;
                }
            }
        }

        // Set the sorting layer for a prefab's renderers
        private static void SetPrefabSortingLayer(GameObject prefab, string sortingLayerName)
        {
            // We need to use prefab modification for this
            string prefabPath = AssetDatabase.GetAssetPath(prefab);

            if (string.IsNullOrEmpty(prefabPath))
                return;

            // Open the prefab for editing
            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
            bool madeChanges = false;

            // Set sorting layer on all renderers
            foreach (Renderer renderer in prefabInstance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sortingLayerName != sortingLayerName)
                {
                    renderer.sortingLayerName = sortingLayerName;
                    madeChanges = true;
                }
            }

            // Set sorting layer on all sprite renderers (redundant with above, but just to be safe)
            foreach (SpriteRenderer renderer in prefabInstance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sortingLayerName != sortingLayerName)
                {
                    renderer.sortingLayerName = sortingLayerName;
                    madeChanges = true;
                }
            }

            // Save changes if needed
            if (madeChanges)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                Debug.Log($"Set sorting layer '{sortingLayerName}' on prefab: {prefab.name}");
            }

            // Unload the prefab
            PrefabUtility.UnloadPrefabContents(prefabInstance);
        }

        private static GameObject FindPrefab(string basePath, string prefabName)
        {
            string[] guids = AssetDatabase.FindAssets("t:GameObject " + prefabName);

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Check if this is in our prefab folder first
                if (path.StartsWith(basePath))
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                        return prefab;
                }
            }

            // If not found in our specific folder, check the entire project
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    return prefab;
            }

            return null;
        }

        private static TileBase FindDefaultTile(string tileType)
        {
            // First look in our own package
            string packagePath = "Assets/PCGLevelGenerator/Tiles/";
            if (Directory.Exists(packagePath))
            {
                string[] guids = AssetDatabase.FindAssets("t:TileBase", new[] { packagePath });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.ToLower().Contains(tileType.ToLower()))
                    {
                        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                        if (tile != null) return tile;
                    }
                }
            }

            // Search project-wide if not found in our package
            string[] allGuids = AssetDatabase.FindAssets("t:TileBase");
            foreach (string guid in allGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.ToLower().Contains(tileType.ToLower()))
                {
                    TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                    if (tile != null) return tile;
                }
            }

            return null;
        }
    }

    // Auto-show welcome window on import (keep this part)
    [InitializeOnLoad]
    public class PCGLevelGeneratorImportHandler
    {
        static PCGLevelGeneratorImportHandler()
        {
            EditorApplication.delayCall += ShowWelcomeWindow;
        }

        private static void ShowWelcomeWindow()
        {
            // Check if this is the first time the asset is imported
            if (!EditorPrefs.GetBool("PCGLevelGenerator_Welcomed", false))
            {
                EditorPrefs.SetBool("PCGLevelGenerator_Welcomed", true);
                PCGLevelGeneratorWelcome.ShowWindow();
            }
        }
    }

    // Simplified welcome window that should work without errors
    public class PCGLevelGeneratorWelcome : EditorWindow
    {
        // Hold reference to the texture but don't use it in OnEnable
        private Texture2D headerTexture;

        public static void ShowWindow()
        {
            PCGLevelGeneratorWelcome window = GetWindow<PCGLevelGeneratorWelcome>(true, "PCG Level Master", true);
            window.minSize = new Vector2(450, 400);
            window.Show();
        }

        void OnGUI()
        {
            // Load the texture during OnGUI if needed (not in OnEnable)
            if (headerTexture == null)
            {
                headerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/PCGLevelGenerator/Editor/Resources/PCGHeader.png");
            }

            // Simple layout using EditorGUILayout
            EditorGUILayout.BeginVertical();

            // Simplified dark header area
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Space(10);

            // Title
            GUILayout.Label("PCG Level Master", EditorStyles.boldLabel);

            GUILayout.Label("Developed by Dineshkumar, Mahmud Hasan, Kevin A. Moberly, & Kamalanathan", EditorStyles.centeredGreyMiniLabel);
            GUILayout.Space(10);
            EditorGUILayout.EndVertical();

            GUILayout.Space(20);

            // Info box
            EditorGUILayout.HelpBox(
                "Welcome to PCG Level Master!\n\n" +
                "This asset provides three powerful ways to create procedural 2D levels:\n" +
                "• Fully Procedural: Classic BSP dungeons\n" +
                "• Hybrid Procedural: Mixing auto-generation with custom templates\n" +
                "• User Defined Layout: Design your levels visually",
                MessageType.Info);

            GUILayout.Space(20);

            // Setup button
            if (GUILayout.Button("ONE-CLICK SETUP (READY IN 10 SECONDS!)", GUILayout.Height(40)))
            {
                PCGSetupMenu.SetupCompletePCGSystem();
                Close();
            }

            GUILayout.Space(10);

            // Info text
            EditorGUILayout.HelpBox(
                "This will set up a complete PCG Level Master system with all required components in your scene.",
                MessageType.None);


            GUILayout.FlexibleSpace();

            // Footer
            GUILayout.Label("You can access these tools anytime via the Tools > PCG Level Master menu",
                EditorStyles.centeredGreyMiniLabel);
            GUILayout.Label("Copyright © 2025 Dineshkumar, Mahmud Hasan, Kevin A. Moberly, & Kamalanathan. All rights reserved.",
                EditorStyles.centeredGreyMiniLabel);

            GUILayout.Space(10);
            EditorGUILayout.EndVertical();
        }

        // Single combined menu item
        [MenuItem("Tools/PCG Level Master/Setup & Welcome")]
        public static void ShowSetupAndWelcome()
        {
            ShowWindow();
        }

        [MenuItem("Tools/PCG Level Master/Documentation")]
        public static void OpenDocumentation()
        {
            string docPath = "Assets/PCGLevelGenerator/Documentation/Documentation.html";
            Application.OpenURL("file://" + Path.GetFullPath(docPath));
        }
    }
}