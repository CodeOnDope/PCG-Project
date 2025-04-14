using UnityEngine;
using UnityEngine.Tilemaps; // Added for template bounds check in Gizmos
using System.Collections.Generic;
using System; // Added for Guid

// This component is attached to GameObjects in the scene to define rooms for SceneDefinedLayout mode
[ExecuteInEditMode] // Run OnValidate and OnDrawGizmos in the editor
public class RoomNode : MonoBehaviour
{
    [Tooltip("Unique identifier for this room. Auto-generated if empty.")]
    public string roomId;

    [Tooltip("Type of room to generate")]
    public NodeType roomType = NodeType.Rect; // Uses shared enum

    [Tooltip("Size for Rect and L-Shape rooms (used for placement/generation)")]
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
            // Generate a GUID-based ID for better uniqueness across sessions
            roomId = Guid.NewGuid().ToString().Substring(0, 8); // Get first 8 chars
#if UNITY_EDITOR
            // If in editor, mark object as dirty so change persists
            if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        // Basic validation for template assignment
        if (roomType == NodeType.Template && roomTemplatePrefab == null)
        {
            Debug.LogWarning($"RoomNode '{gameObject.name}' (ID: {roomId}) is type Template but has no Template Prefab assigned.", this);
        }
        if (roomType != NodeType.Template && roomTemplatePrefab != null)
        {
            // Optionally clear it or just warn
            // Debug.LogWarning($"RoomNode '{gameObject.name}' (ID: {roomId}) has a Template Prefab assigned but is not type Template.", this);
        }
        // Ensure size is positive
        roomSize.x = Mathf.Max(1, roomSize.x);
        roomSize.y = Mathf.Max(1, roomSize.y);
    }

    // Simple visualization in the scene view
    private void OnDrawGizmos()
    {
        Color nodeColor = Color.gray; // Default
        switch (roomType)
        {
            case NodeType.Rect: nodeColor = Color.blue; break;
            case NodeType.LShape: nodeColor = Color.green; break;
            case NodeType.Template: nodeColor = Color.magenta; break; // Changed from yellow for better contrast
        }

        Vector3 position = transform.position;

        // Draw connections first (behind node gizmo)
        Gizmos.color = Color.cyan;
        if (connectedRooms != null)
        {
            foreach (var connectedRoom in connectedRooms)
            {
                if (connectedRoom != null)
                {
                    // Draw line only if the other node also connects back (or draw all defined)
                    // For simplicity, draw all defined connections from this node
                    Gizmos.DrawLine(position, connectedRoom.transform.position);
                }
            }
        }

        // Draw node representation
        Gizmos.color = nodeColor;
        Gizmos.DrawSphere(position, 0.6f); // Slightly larger sphere

        // Draw room bounds outline based on type and size
        Gizmos.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.3f); // Semi-transparent
        Vector3 gizmoSize = Vector3.zero;
        Vector3 centerOffset = Vector3.zero; // Offset if pivot isn't center

        if (roomType == NodeType.Template && roomTemplatePrefab != null)
        {
            // Try to get bounds from prefab's tilemap for visualization
            var tilemap = roomTemplatePrefab.GetComponentInChildren<Tilemap>(true); // Include inactive
            if (tilemap != null)
            {
                // Note: Getting bounds from prefab asset might not be perfectly accurate
                // without instantiation, but good for a rough Gizmo.
                tilemap.CompressBounds();
                BoundsInt bounds = tilemap.cellBounds;
                // Assume cell size is 1x1 for Gizmo size calculation
                gizmoSize = new Vector3(bounds.size.x, bounds.size.y, 0.1f);
                // Center the gizmo based on the tilemap bounds relative to its pivot
                centerOffset = bounds.center - tilemap.transform.position; // Use bounds center
            }
            else
            { // Fallback if no tilemap found
                gizmoSize = new Vector3(roomSize.x, roomSize.y, 0.1f);
            }
        }
        else if (roomType == NodeType.LShape)
        {
            // Draw simple L-shape Gizmo (centered approx)
            Vector2Int size = roomSize.x > 0 && roomSize.y > 0 ? roomSize : new Vector2Int(5, 5); // Use default if size is invalid
            float stemWidth = size.x;
            float stemHeight = size.y * 0.7f;
            float legWidth = size.x * 0.7f;
            float legHeight = size.y * 0.3f;

            // Draw stem centered horizontally, lower part vertically
            Vector3 stemCenter = position + new Vector3(0, -size.y * 0.15f, 0); // Offset slightly
            Gizmos.DrawCube(stemCenter, new Vector3(stemWidth, stemHeight, 0.1f));

            // Draw leg centered vertically, right part horizontally
            Vector3 legCenter = position + new Vector3(size.x * 0.15f, size.y * 0.35f, 0); // Offset slightly
            Gizmos.DrawCube(legCenter, new Vector3(legWidth, legHeight, 0.1f));
            return; // Skip default cube draw for L-shape
        }
        else // Rect or fallback
        {
            gizmoSize = new Vector3(roomSize.x, roomSize.y, 0.1f);
        }

        if (gizmoSize != Vector3.zero)
        {
            Gizmos.DrawCube(position + centerOffset, gizmoSize);
        }
    }
}
