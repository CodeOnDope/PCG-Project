using UnityEngine;
using System.Collections.Generic; // Keep if used elsewhere, not needed for below

// --- Enums (Accessible by Runtime & Editor) ---

// Mode Selection for the generator (JSON mode removed)
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

// --- JSON Data Structures Removed ---
// NodeData, ConnectionData, LevelDesignData are no longer needed for this workflow
