using UnityEngine;
using System.Collections.Generic;

// Make sure NodeType enum is accessible (e.g., defined globally or in the same file/namespace)
// public enum NodeType { Rect, LShape, Template } // Assuming defined elsewhere or moved here

public class RoomNode : MonoBehaviour
{
    [Tooltip("Unique identifier for this room node (optional, can be auto-generated or used for specific lookups).")]
    public string roomId = ""; // Can be set manually or assigned by generator if needed

    [Tooltip("The type of room this node represents.")]
    public NodeType roomType = NodeType.Rect;

    [Tooltip("Assign the specific Room Template Prefab here if roomType is set to Template.")]
    public GameObject roomTemplatePrefab; // Assign only if type is Template

    [Tooltip("Drag other RoomNode GameObjects here to define connections (corridors).")]
    public List<RoomNode> connectedRooms;

    // Optional: Add size parameters if you want to define specific sizes for Rect/LShape
    // public Vector2Int roomSize = new Vector2Int(10, 10);

    // Automatically generate a simple ID if none is provided in editor
    void OnValidate()
    {
        if (string.IsNullOrEmpty(roomId))
        {
            // Generate a temporary ID based on GameObject name/instance ID for uniqueness
            // This might change between sessions if not saved properly, manual IDs are safer
            // For persistent IDs, consider using a dedicated ID system or asset references.
            roomId = $"{this.gameObject.name}_{this.GetInstanceID()}";
        }

        // Ensure template field is relevant
        if (roomType != NodeType.Template && roomTemplatePrefab != null)
        {
            // Optionally clear it or just leave it, user needs to manage this
            // Debug.LogWarning($"Room Template Prefab assigned to Node '{roomId}' but type is not Template.", this);
        }
        if (roomType == NodeType.Template && roomTemplatePrefab == null)
        {
            Debug.LogWarning($"Node '{roomId}' type is Template, but no Room Template Prefab is assigned.", this);
        }
    }

    // Draw simple gizmos in the scene view for visualization
    void OnDrawGizmos()
    {
        // Draw a sphere at the node's position
        switch (roomType)
        {
            case NodeType.Rect: Gizmos.color = Color.blue; break;
            case NodeType.LShape: Gizmos.color = Color.green; break;
            case NodeType.Template: Gizmos.color = Color.magenta; break;
            default: Gizmos.color = Color.gray; break;
        }
        Gizmos.DrawSphere(transform.position, 0.5f); // Adjust size as needed

        // Draw lines to connected rooms
        if (connectedRooms != null)
        {
            Gizmos.color = Color.yellow;
            foreach (RoomNode connectedNode in connectedRooms)
            {
                if (connectedNode != null)
                {
                    Gizmos.DrawLine(transform.position, connectedNode.transform.position);
                }
            }
        }
    }
}
