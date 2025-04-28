using UnityEngine;
using UnityEngine.Tilemaps; // Keep for potential future use in OnValidate
using System.Collections.Generic;
using System;

// Assumes NodeType is defined in LevelGenerationTypes.cs or similar
// public enum NodeType { Rect, LShape, Template } // Example if not defined elsewhere

[ExecuteInEditMode] // Run OnValidate in the editor
public class RoomNode : MonoBehaviour
{
    [Tooltip("Unique identifier for this room. Auto-generated if empty.")]
    public string roomId;

    [Tooltip("Type of room to generate")]
    public NodeType roomType = NodeType.Rect;

    [Tooltip("Logical size used for Rect/L-Shape generation. Not the visual size.")]
    public Vector2Int roomSize = new Vector2Int(10, 10);

    [Tooltip("For Template type, assign the prefab containing a Tilemap")]
    public GameObject roomTemplatePrefab;

    [Tooltip("Drag other RoomNode GameObjects here to define connections (corridors)")]
    public List<RoomNode> connectedRooms = new List<RoomNode>();

    // Called when script values are changed in the Inspector or script is loaded
    private void OnValidate()
    {
        // Auto-generate a unique ID if empty
        if (string.IsNullOrEmpty(roomId))
        {
            roomId = Guid.NewGuid().ToString().Substring(0, 8);
#if UNITY_EDITOR
            // Ensure changes are saved in prefab mode or when scene is modified
            if (!Application.isPlaying)
            {
                if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                {
                    UnityEditor.EditorUtility.SetDirty(this);
                }
                else if (this.gameObject.scene != null && this.gameObject.scene.IsValid())
                {
                    // Only mark scene dirty if it's a valid, loaded scene object
                    if (this.gameObject.scene.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(this.gameObject.scene);
                }
                else if (UnityEditor.EditorUtility.IsPersistent(this))
                {
                    // Handle cases where it might be part of an asset not in a scene
                    UnityEditor.EditorUtility.SetDirty(this);
                }
            }
#endif
        }

        // Basic validation for template assignment
        if (roomType == NodeType.Template && roomTemplatePrefab == null)
        {
            Debug.LogWarningFormat(this, "RoomNode '{0}' (ID: {1}) is type Template but has no Template Prefab assigned.", gameObject.name, roomId);
        }

        // Ensure logical size is positive
        roomSize.x = Mathf.Max(1, roomSize.x);
        roomSize.y = Mathf.Max(1, roomSize.y);
    }

    // Gizmo drawing is disabled as requested
    private void OnDrawGizmos()
    {
        // --- GIZMO DRAWING DISABLED ---
    }

} // --- End of RoomNode Class ---