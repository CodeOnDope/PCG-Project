using UnityEngine;
using UnityEditor; // Required for Editor scripts

// This attribute tells Unity to use this class to draw the Inspector
// for components of type HybridLevelGenerator.
[CustomEditor(typeof(HybridLevelGenerator))]
public class HybridLevelGeneratorEditor : Editor // Inherit from Editor
{
    // This method is called by Unity to draw the Inspector GUI.
    public override void OnInspectorGUI()
    {
        // Draw the default Inspector fields first (all the public variables like
        // levelWidth, minRoomSize, tilemap references, prefabs lists, etc.)
        DrawDefaultInspector();

        // Get a reference to the actual HybridLevelGenerator component instance
        // that this Inspector is showing. 'target' is a property of the base Editor class.
        HybridLevelGenerator generatorScript = (HybridLevelGenerator)target;

        // Add some vertical spacing before our custom buttons for better layout.
        EditorGUILayout.Space(10); // You can use GUILayout.Space(10) too

        // Add a button labeled "Generate Level"
        // GUILayout.Button returns true when the button is clicked in the Inspector.
        if (GUILayout.Button("Generate Level"))
        {
            // Optional but recommended: Register the action with the Undo system.
            // This allows the user to press Ctrl+Z/Cmd+Z to undo the generation.
            Undo.RecordObject(generatorScript, "Generate Level"); // Record changes potentially made TO the script component itself

            // Call the public GenerateLevel() method on the target script.
            generatorScript.GenerateLevel();

            // Note: Changes to Tilemaps and instantiated/destroyed objects usually
            // register themselves with the scene/undo system automatically.
            // If GenerateLevel directly modified properties *on the generatorScript component itself*,
            // you might add EditorUtility.SetDirty(generatorScript); here.
        }

        // Add a button labeled "Clear Level"
        if (GUILayout.Button("Clear Level"))
        {
            // Register the action with the Undo system.
            Undo.RecordObject(generatorScript, "Clear Level");

            // Call the public ClearLevel() method on the target script.
            generatorScript.ClearLevel();

            // Optional: EditorUtility.SetDirty(generatorScript);
        }

        // You could add more buttons or custom controls here if needed.
    }
}