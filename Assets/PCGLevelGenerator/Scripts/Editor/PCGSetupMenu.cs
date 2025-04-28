using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;
using System.Collections.Generic;

namespace PCGLevelGenerator
{
    public static class PCGSetupMenu
    {
        [MenuItem("Tools/PCG Level Generator/One-Click Setup")]
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

            // --- Create Grid with exactly three Tilemaps (as shown in your new screenshot) ---
            GameObject gridObject = new GameObject("Grid");
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = new Vector3(1, 1, 0);

            // Create only the three tilemaps required
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

            // Assign tilemaps
            generator.groundTilemap = groundTilemap;
            generator.wallTilemap = wallTilemap;

            // Find and assign floor/wall tiles
            TileBase floorTile = FindDefaultTile("floor");
            TileBase wallTile = FindDefaultTile("wall");

            if (floorTile != null)
                generator.floorTile = floorTile;

            if (wallTile != null)
                generator.wallTile = wallTile;

            // Auto-assign prefabs from the PCGLevelGenerator/Prefabs folder
            AssignPrefabs(generator);

            // Set default parameters
            generator.levelWidth = 100;
            generator.levelHeight = 100;
            generator.corridorWidth = 2;
            generator.roomPadding = 2.37f; // As shown in your screenshot

            // Select the generator in the hierarchy to show its Inspector
            Selection.activeGameObject = generatorObject;

            // Display success message with next steps
            EditorUtility.DisplayDialog("PCG Level Generator Setup Complete",
                "The PCG Level Generator has been successfully set up in your scene!\n\n" +
                "Next Steps:\n" +
                "1. Choose a generation mode\n" +
                "2. Click 'Generate Level' in the Inspector\n\n" +
                "For user-defined layouts, use the 'Open Visual Level Designer' button.",
                "Got it!");
        }

        private static Tilemap CreateTilemap(GameObject parent, string name, int sortingOrder, string sortingLayer = "Default")
        {
            GameObject tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent.transform);

            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();

            // Set properties
            renderer.sortingOrder = sortingOrder;

            // Try to set sorting layer if it exists
            if (SortingLayer.NameToID(sortingLayer) != 0)
            {
                renderer.sortingLayerName = sortingLayer;
            }

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
                generator.playerPrefab = playerPrefab;

            // Try to find Enemy prefab
            GameObject enemyPrefab = FindPrefab(prefabsPath, "Enemy");
            if (enemyPrefab != null)
                generator.enemyPrefab = enemyPrefab;

            // Try to find Tree/Decoration prefab
            GameObject decorPrefab = FindPrefab(prefabsPath, "Tree");
            if (decorPrefab == null) // Try alternative name if Tree not found
                decorPrefab = FindPrefab(prefabsPath, "Decoration");

            if (decorPrefab != null)
                generator.decorationPrefab = decorPrefab;

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

    // Auto-show welcome window on import
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

    // Welcome window that appears on first import
    public class PCGLevelGeneratorWelcome : EditorWindow
    {
        private Texture2D headerImage;

        public static void ShowWindow()
        {
            PCGLevelGeneratorWelcome window = GetWindow<PCGLevelGeneratorWelcome>(true, "PCG Level Generator", true);
            window.minSize = new Vector2(450, 400);
            window.Show();
        }

        void OnEnable()
        {
            // Try to load header image
            headerImage = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/PCGLevelGenerator/Editor/Resources/PCGHeader.png");
        }

        void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            // Header
            if (headerImage != null)
            {
                GUILayout.Label(headerImage, GUILayout.Width(450), GUILayout.Height(100));
            }
            else
            {
                GUILayout.Space(20);

                GUIStyle titleStyle = new GUIStyle(EditorStyles.largeLabel);
                titleStyle.fontSize = 22;
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;

                GUILayout.Label("PCG Level Generator", titleStyle);
                GUILayout.Space(10);
            }

            GUILayout.Space(20);

            EditorGUILayout.HelpBox(
                "Welcome to PCG Level Generator!\n\n" +
                "This asset provides three powerful ways to create procedural 2D levels:\n" +
                "� Fully Procedural: Classic BSP dungeons\n" +
                "� Hybrid Procedural: Mixing auto-generation with custom templates\n" +
                "� User Defined Layout: Design your levels visually",
                MessageType.Info);

            GUILayout.Space(20);

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 14;
            buttonStyle.padding = new RectOffset(10, 10, 10, 10);

            if (GUILayout.Button("ONE-CLICK SETUP (READY IN 10 SECONDS!)", buttonStyle, GUILayout.Height(50)))
            {
                PCGSetupMenu.SetupCompletePCGSystem();
                Close();
            }

            GUILayout.Space(15);

            EditorGUILayout.HelpBox(
                "This will set up a complete PCG Level Generator system with all required components in your scene, " +
                "exactly matching the configuration shown in the documentation.",
                MessageType.None);

            GUILayout.Space(20);

            if (GUILayout.Button("Open Visual Level Designer", GUILayout.Height(30)))
            {
                // Open the visual designer window
                EditorApplication.ExecuteMenuItem("Window/Visual Level Designer");
                Close();
            }

            GUILayout.FlexibleSpace();

            GUILayout.Label("You can access these tools anytime via the Tools > PCG Level Generator menu",
                EditorStyles.centeredGreyMiniLabel);

            GUILayout.Space(10);

             GUILayout.FlexibleSpace();
    
    // Footer with copyright
    GUILayout.Label("© 2025 Dineshkumar & Kamalanathan. All rights reserved.", 
        EditorStyles.centeredGreyMiniLabel);
    
    GUILayout.Space(10);

            EditorGUILayout.EndVertical();
        }

        // Menu item to reopen this window
        [MenuItem("Tools/PCG Level Generator/Welcome Screen")]
        public static void ShowWindowMenuItem()
        {
            ShowWindow();
        }
    }
}