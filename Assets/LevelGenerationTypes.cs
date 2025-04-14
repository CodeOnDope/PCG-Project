using UnityEngine;
using System.Collections.Generic;

// --- Enums (Accessible by Runtime & Editor) ---

// Mode Selection for the generator
public enum GenerationMode
{
    FullyProcedural,  // BSP splits, random Rect rooms, procedural corridors
    HybridProcedural, // BSP splits, random Templates/L-Shapes/Rects, procedural corridors
    UserDefinedLayout // Reads layout from RoomNode components placed in the scene
}

// Room types used by generator and editor
public enum NodeType
{
    Rect,     // Simple rectangular room
    LShape,   // L-shaped room
    Template  // Prefab-based template room
}

// Tile Type for the generation grid
public enum TileType { Empty, Floor, Wall }


// --- JSON Data Structures (for Saving/Loading Editor Design) ---

[System.Serializable]
public class NodeData // Used by HybridLevelGenerator when parsing older JSON if needed, but primarily for editor below
{
    // This structure might be deprecated if only using Scene layout, but keep for potential future JSON use
    public string id;
    public string type; // Keep as string for flexibility from editor JSON
    public int x;
    public int y;
    public int width = 10;
    public int height = 10;
    public string templateName;
}

[System.Serializable]
public class ConnectionData // Used by HybridLevelGenerator if parsing older JSON
{
    public string from;
    public string to;
}

[System.Serializable]
public class LevelDesignData // Used by HybridLevelGenerator if parsing older JSON
{
    public List<NodeData> nodes = new List<NodeData>();
    public List<ConnectionData> connections = new List<ConnectionData>();
}

// --- Structures for Visual Editor Save/Load ---
[System.Serializable]
public class VisualDesignerSaveData // Renamed to avoid conflict, used by Editor
{
    public List<EditorNodeInfo> nodes = new List<EditorNodeInfo>();
    public List<EditorConnectionInfo> connections = new List<EditorConnectionInfo>();
    // Store view settings? (Optional)
    // public Vector2 panOffset;
    // public float zoom;

    [System.Serializable]
    public class EditorNodeInfo
    {
        public string id;
        public string displayName;
        public int nodeType; // Store enum as int
        public float x, y; // Store world position (float)
        public int logicalWidth, logicalHeight; // Store logical size
        public string templateName;
        // Store visual size
        public float visualWidth;
        public float visualHeight;
        // Store custom color? Optional, requires Color serialization handling
        // public float colorR, colorG, colorB, colorA;
    }
    [System.Serializable]
    public class EditorConnectionInfo
    {
        public string fromId;
        public string toId;
    }
}
