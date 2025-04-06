using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LevelGenerator))]
public class CustomInspector : Editor
{
    public override void OnInspectorGUI()
    {
        // Get a reference to the LevelGenerator script
        LevelGenerator levelGenerator = (LevelGenerator)target;

        // Draw the default inspector fields
        DrawDefaultInspector();

        // Add some spacing
        EditorGUILayout.Space();

        // Add a button to generate levels
        if (GUILayout.Button("Generate Levels"))
        {
            levelGenerator.GenerateLevels();
        }

        // Add a button to clear levels
        if (GUILayout.Button("Clear Levels"))
        {
            // Update to match the existing ClearLevel method
            levelGenerator.ClearLevel(); // Assuming ClearLevel is the correct method
        }

        // Add customization options
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Customization Options", EditorStyles.boldLabel);

        // Add fields for customization
        levelGenerator.numberOfLevels = EditorGUILayout.IntField("Number of Levels", levelGenerator.numberOfLevels);
        levelGenerator.minRoomsPerLevel = EditorGUILayout.IntField("Min Rooms Per Level", levelGenerator.minRoomsPerLevel);
        levelGenerator.maxRoomsPerLevel = EditorGUILayout.IntField("Max Rooms Per Level", levelGenerator.maxRoomsPerLevel);
        levelGenerator.minRoomSize = EditorGUILayout.IntField("Min Room Size", levelGenerator.minRoomSize);
        levelGenerator.maxRoomSize = EditorGUILayout.IntField("Max Room Size", levelGenerator.maxRoomSize);

        // Add fields for enemy and decorator counts
        levelGenerator.numberOfEnemies = EditorGUILayout.IntField("Number of Enemies", levelGenerator.numberOfEnemies);
        levelGenerator.numberOfDecorators = EditorGUILayout.IntField("Number of Decorators", levelGenerator.numberOfDecorators);

        // Add fields for start and end triggers
        levelGenerator.startTriggerPrefab = (GameObject)EditorGUILayout.ObjectField("Start Trigger Prefab", levelGenerator.startTriggerPrefab, typeof(GameObject), true);
        levelGenerator.endTriggerPrefab = (GameObject)EditorGUILayout.ObjectField("End Trigger Prefab", levelGenerator.endTriggerPrefab, typeof(GameObject), true);

        // Remove the "Save Settings" button since the `SaveSettings` method does not exist
    }
}