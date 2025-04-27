using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Menu extensions for Procedural Content Generation tools
/// </summary>
public static class PCGMenuExtensions
{
    private const string PCGMenuRoot = "GameObject/2D Object/PCG/";

    [MenuItem(PCGMenuRoot + "Graph-Based Level Generator", false, 10)]
    static void CreateGraphBasedLevelGenerator(MenuCommand menuCommand)
    {
        // Create parent GameObject
        GameObject generatorObject = new GameObject("GraphBasedLevelGenerator");
        GameObjectUtility.SetParentAndAlign(generatorObject, menuCommand.context as GameObject);

        // Create tilemaps parent
        GameObject tilemapsObject = new GameObject("GeneratorTilemaps");
        tilemapsObject.transform.SetParent(generatorObject.transform);
        tilemapsObject.transform.localPosition = Vector3.zero;

        // Create default tilemaps for corridors
        Tilemap floorMap = CreateTilemapObject("CorridorFloor", tilemapsObject.transform, 0);
        Tilemap wallMap = CreateTilemapObject("CorridorWalls", tilemapsObject.transform, 1);

        // Add the level generator component
        GraphBasedLevelGenerator generator = generatorObject.AddComponent<GraphBasedLevelGenerator>();

        // Reference corridor tilemaps
        generator.floorTilemap = floorMap;
        generator.wallTilemap = wallMap;

        // Default generator properties
        generator.minRooms = 8;
        generator.maxRooms = 15;
        generator.minMainPathRooms = 5;
        generator.branchProbability = 0.3f;
        generator.maxBranchLength = 3;
        generator.minRoomDistance = 1;
        generator.corridorWidth = 2;
        generator.useCornerCorridors = true;
        generator.generateCorridors = true;

        // Try to find default tiles in the project
        generator.defaultCorridorFloorTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/DefaultFloorTile.asset");
        generator.defaultCorridorWallTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/DefaultWallTile.asset");

        // Register undo system
        Undo.RegisterCreatedObjectUndo(generatorObject, "Create Graph-Based Level Generator");

        // Select the generator
        Selection.activeGameObject = generatorObject;

        Debug.Log("Graph-Based Level Generator created. Assign Room Template Prefabs with analyzed RoomTemplateAnalyzer components.");
    }

    [MenuItem(PCGMenuRoot + "Room Template", false, 11)]
    static void CreateRoomTemplate(MenuCommand menuCommand)
    {
        // Create parent GameObject
        GameObject templateObject = new GameObject("RoomTemplate");
        GameObjectUtility.SetParentAndAlign(templateObject, menuCommand.context as GameObject);

        // Add Grid component
        if (templateObject.GetComponent<Grid>() == null)
            templateObject.AddComponent<Grid>();

        // Create tilemaps structure
        GameObject tilemapsParent = new GameObject("Tilemaps");
        tilemapsParent.transform.SetParent(templateObject.transform);
        tilemapsParent.transform.localPosition = Vector3.zero;

        CreateTilemapObject("FloorTilemap", tilemapsParent.transform, 0);
        CreateTilemapObject("WallTilemap", tilemapsParent.transform, 1);
        CreateTilemapObject("DoorTilemap", tilemapsParent.transform, 2);

        // Add analyzer component
        RoomTemplateAnalyzer analyzer = templateObject.AddComponent<RoomTemplateAnalyzer>();
        analyzer.floorTilemap = tilemapsParent.transform.Find("FloorTilemap")?.GetComponent<Tilemap>();
        analyzer.wallTilemap = tilemapsParent.transform.Find("WallTilemap")?.GetComponent<Tilemap>();
        analyzer.doorTilemap = tilemapsParent.transform.Find("DoorTilemap")?.GetComponent<Tilemap>();

        // Add example generator component
        ExampleRoomTemplate exampleGen = templateObject.AddComponent<ExampleRoomTemplate>();

        // Try to find default tiles
        exampleGen.floorTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/DefaultFloorTile.asset");
        exampleGen.wallTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/DefaultWallTile.asset");
        exampleGen.doorTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/DefaultDoorTile.asset");

        // Register undo
        Undo.RegisterCreatedObjectUndo(templateObject, "Create Room Template");

        // Select the template
        Selection.activeGameObject = templateObject;

        Debug.Log("Room Template created. Use the ExampleRoomTemplate component to generate a layout, or design it manually.");
    }

    // Helper to create a Tilemap GameObject
    private static Tilemap CreateTilemapObject(string name, Transform parent, int sortingOrder)
    {
        GameObject tilemapObject = new GameObject(name);
        tilemapObject.transform.SetParent(parent);
        tilemapObject.transform.localPosition = Vector3.zero;

        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;

        return tilemap;
    }

    [MenuItem("Assets/Create/PCG/Default Tile Set", false, 100)]
    static void CreateDefaultTileSet()
    {
        string tileDir = "Assets/Tiles";

        // Create directory if it doesn't exist
        if (!Directory.Exists(tileDir))
        {
            Directory.CreateDirectory(tileDir);
            AssetDatabase.Refresh();
        }

        // Create default tiles
        CreateDefaultTileAsset(Path.Combine(tileDir, "DefaultFloorTile.asset"), Color.grey);
        CreateDefaultTileAsset(Path.Combine(tileDir, "DefaultWallTile.asset"), Color.white * 0.8f);
        CreateDefaultTileAsset(Path.Combine(tileDir, "DefaultDoorTile.asset"), new Color(0.6f, 0.4f, 0.2f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Default Tile Set created in '{tileDir}'.");
    }

    private static TileBase CreateDefaultTileAsset(string path, Color color)
    {
        // Check if tile already exists
        Tile existingTile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (existingTile != null)
        {
            return existingTile;
        }

        // Create new tile
        Tile tile = ScriptableObject.CreateInstance<Tile>();

        // Create simple texture
        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();

        // Create sprite
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 16);
        tile.sprite = sprite;
        tile.name = Path.GetFileNameWithoutExtension(path);

        // Save asset
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }
}
#endif