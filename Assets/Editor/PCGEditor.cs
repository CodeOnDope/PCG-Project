using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LevelGenerator))]
public class PCGEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LevelGenerator levelGenerator = (LevelGenerator)target;

        // Draw default inspector properties
        DrawDefaultInspector();

        EditorGUILayout.Space();

        // Customizable options for level generation
        EditorGUILayout.LabelField("Level Generation Settings", EditorStyles.boldLabel);
        levelGenerator.numberOfLevels = EditorGUILayout.IntField("Number of Levels", levelGenerator.numberOfLevels);
        levelGenerator.numberOfRooms = EditorGUILayout.IntField("Number of Rooms", levelGenerator.numberOfRooms);
        levelGenerator.numberOfEnemies = EditorGUILayout.IntField("Number of Enemies", levelGenerator.numberOfEnemies);
        levelGenerator.numberOfDecorators = EditorGUILayout.IntField("Number of Decorators", levelGenerator.numberOfDecorators);
        levelGenerator.startLevel = EditorGUILayout.IntField("Start Level", levelGenerator.startLevel);
        levelGenerator.endLevel = EditorGUILayout.IntField("End Level", levelGenerator.endLevel);

        EditorGUILayout.Space();

        // Buttons for generating and clearing levels
        if (GUILayout.Button("Generate Levels"))
        {
            levelGenerator.GenerateLevels();
        }

        if (GUILayout.Button("Clear Levels"))
        {
            levelGenerator.ClearLevels();
        }

        // Apply changes to the serialized object
        if (GUI.changed)
        {
            EditorUtility.SetDirty(levelGenerator);
        }
    }
}