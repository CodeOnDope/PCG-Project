using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// Place this script inside a folder named "Editor" anywhere in your Assets folder.
// Access the window via the Unity top menu: Window > Custom Tools > Asset Tagger

public class AssetTaggerWindow : EditorWindow
{
    private string newTag = ""; // Input field for adding new tags
    private Vector2 scrollPosition; // For the list of selected assets

    // Create the menu item to open this window
    [MenuItem("Window/Custom Tools/Asset Tagger")]
    public static void ShowWindow()
    {
        // Get existing open window or if none, make a new one.
        GetWindow<AssetTaggerWindow>("Asset Tagger");
    }

    // Draw the window GUI
    void OnGUI()
    {
        GUILayout.Label("Asset Tagger", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Select assets in the Project window to view and manage their tags (Labels).", MessageType.Info);

        EditorGUILayout.Space();

        // --- Section for Adding Tags ---
        GUILayout.Label("Add New Tag", EditorStyles.label);
        EditorGUILayout.BeginHorizontal();
        newTag = EditorGUILayout.TextField("Tag Name:", newTag);

        // Disable Add button if no assets are selected or tag input is empty
        GUI.enabled = Selection.objects.Length > 0 && !string.IsNullOrWhiteSpace(newTag);
        if (GUILayout.Button("Add Tag to Selected", GUILayout.Width(150)))
        {
            AddTagToSelectedAssets(newTag.Trim());
            newTag = ""; // Clear input field after adding
            GUI.FocusControl(null); // Remove focus from text field
        }
        GUI.enabled = true; // Re-enable GUI
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10); // Add some vertical space

        // --- Section for Displaying Selected Assets and their Tags ---
        GUILayout.Label("Selected Assets:", EditorStyles.boldLabel);

        // Scroll view for the list of assets and their tags
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

        if (Selection.objects.Length == 0)
        {
            GUILayout.Label("No assets selected.", EditorStyles.miniLabel);
        }
        else
        {
            // Display each selected asset and its tags
            foreach (Object obj in Selection.objects)
            {
                // Skip non-asset objects if any are selected (e.g., scene objects)
                if (!AssetDatabase.Contains(obj)) continue;

                string assetPath = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(assetPath)) continue; // Should have a path if it's an asset

                EditorGUILayout.BeginVertical(EditorStyles.helpBox); // Use helpBox for grouping
                EditorGUILayout.ObjectField(obj, typeof(Object), false); // Display the asset field

                // Get existing labels (tags) for the asset
                string[] currentLabels = AssetDatabase.GetLabels(obj);
                List<string> labelsList = currentLabels.ToList();

                if (labelsList.Count == 0)
                {
                    GUILayout.Label("No tags.", EditorStyles.miniLabel);
                }
                else
                {
                    GUILayout.Label("Current Tags:", EditorStyles.miniBoldLabel);
                    // Display existing tags with a remove button for each
                    for (int i = labelsList.Count - 1; i >= 0; i--) // Iterate backwards for safe removal
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(" - " + labelsList[i]);
                        if (GUILayout.Button("X", GUILayout.Width(25)))
                        {
                            RemoveTagFromAsset(obj, labelsList[i]);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5); // Space between asset entries
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // Function to add a tag to all selected assets
    void AddTagToSelectedAssets(string tagToAdd)
    {
        if (string.IsNullOrWhiteSpace(tagToAdd)) return;

        foreach (Object obj in Selection.objects)
        {
            if (!AssetDatabase.Contains(obj)) continue; // Only process project assets

            string[] currentLabels = AssetDatabase.GetLabels(obj);
            List<string> labelsList = currentLabels.ToList();

            // Add the tag only if it doesn't already exist (case-insensitive check might be better)
            if (!labelsList.Contains(tagToAdd, System.StringComparer.OrdinalIgnoreCase))
            {
                labelsList.Add(tagToAdd);
                AssetDatabase.SetLabels(obj, labelsList.ToArray());
                EditorUtility.SetDirty(obj); // Mark asset as dirty to ensure change is saved
                Debug.Log($"Added tag '{tagToAdd}' to asset: {obj.name}");
            }
        }
        AssetDatabase.SaveAssets(); // Save changes to the asset database
        Repaint(); // Refresh the window UI
    }

    // Function to remove a specific tag from a specific asset
    void RemoveTagFromAsset(Object obj, string tagToRemove)
    {
        if (!AssetDatabase.Contains(obj)) return;

        string[] currentLabels = AssetDatabase.GetLabels(obj);
        List<string> labelsList = currentLabels.ToList();

        if (labelsList.Remove(tagToRemove)) // Remove the tag if found
        {
            AssetDatabase.SetLabels(obj, labelsList.ToArray());
            EditorUtility.SetDirty(obj);
            Debug.Log($"Removed tag '{tagToRemove}' from asset: {obj.name}");
            AssetDatabase.SaveAssets(); // Save changes
            Repaint(); // Refresh UI
        }
    }

    // Optional: Refresh the window if the project selection changes
    void OnSelectionChange()
    {
        Repaint();
    }
}