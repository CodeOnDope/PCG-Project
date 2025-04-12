using UnityEngine;
using System.Collections.Generic; // Needed for lists if you store multiple tags

/// <summary>
/// Defines the possible purposes or types for a generated room.
/// You can easily expand this enum with more types as needed.
/// </summary>
public enum RoomPurpose
{
    Undefined,      // Default or unassigned
    SpawnRoom,      // Starting point for the player
    TreasureRoom,   // Contains valuable loot
    BossRoom,       // Location of a boss enemy
    CorridorHub,    // A central point connecting multiple corridors/rooms
    Standard,       // A regular room with common enemies/obstacles
    PuzzleRoom,     // Contains a puzzle element
    ShopRoom        // A room where the player can buy/sell items
    // Add more room types here!
}

/// <summary>
/// Component to be attached to Room GameObjects.
/// Stores data about the room, including its purpose.
/// </summary>
public class RoomData : MonoBehaviour
{
    [Header("Room Properties")]
    [Tooltip("The designated purpose of this room.")]
    public RoomPurpose Purpose = RoomPurpose.Undefined; // Default purpose

    [Tooltip("Unique identifier for this room (optional, could be assigned during generation).")]
    public string RoomID;

    // You could add more data here later, such as:
    // - Bounds (RectInt or Vector3 min/max)
    // - List of connected rooms or doors
    // - Difficulty rating
    // - Decoration theme
    // - etc.

    /// <summary>
    /// Example function called after a room is generated and instantiated.
    /// This is where you might assign the purpose based on generation logic.
    /// </summary>
    /// <param name="assignedPurpose">The purpose determined by the generator.</param>
    public void InitializeRoom(RoomPurpose assignedPurpose, string id)
    {
        Purpose = assignedPurpose;
        RoomID = id;
        gameObject.name = $"Room_{id} ({Purpose})"; // Rename GameObject for clarity in hierarchy

        Debug.Log($"Initialized Room {RoomID} with Purpose: {Purpose}");

        // --- Placeholder for further logic ---
        // Based on the 'Purpose', you could now:
        // - Spawn specific enemies (e.g., stronger enemies in BossRoom)
        // - Place specific decorations (e.g., chests in TreasureRoom)
        // - Trigger specific events or logic
        // Example:
        // if (Purpose == RoomPurpose.TreasureRoom)
        // {
        //     SpawnTreasureChest();
        // }
        // else if (Purpose == RoomPurpose.SpawnRoom)
        // {
        //     // Mark this as the player's start location
        // }
    }

    // --- Example Placeholder Functions ---
    // private void SpawnTreasureChest()
    // {
    //     Debug.Log($"Spawning treasure in Room {RoomID}!");
    //     // Add logic to instantiate a treasure chest prefab
    // }
}

// --- Example Usage in a Hypothetical Dungeon Generator ---
/*
public class DungeonGenerator : MonoBehaviour
{
    public GameObject roomPrefab; // Assign a prefab with the RoomData component

    void GenerateDungeon()
    {
        // ... (Your BSP or other generation logic) ...

        // Example: When creating a room instance from a leaf node in BSP
        // Assume 'leafNode' represents a space where a room should be created

        // 1. Instantiate the room GameObject
        GameObject newRoomObject = Instantiate(roomPrefab, /* position * /, /* rotation * /);

        // 2. Get the RoomData component
        RoomData roomData = newRoomObject.GetComponent<RoomData>();
        if (roomData != null)
        {
            // 3. Determine the room's purpose based on your rules
            RoomPurpose purpose = DetermineRoomPurpose(leafNode); // Your custom logic here
            string uniqueID = System.Guid.NewGuid().ToString(); // Generate a unique ID

            // 4. Initialize the room with its data
            roomData.InitializeRoom(purpose, uniqueID);
        }
        else
        {
            Debug.LogError("Room prefab is missing the RoomData component!");
        }

        // ... (Continue generation) ...
    }

    // Example function to decide room purpose (replace with your actual logic)
    RoomPurpose DetermineRoomPurpose(object nodeData) // Replace 'object' with your node type
    {
        // Simple example: Assign purpose randomly or based on size/location
        float randomValue = Random.value;
        if (randomValue < 0.1f) return RoomPurpose.TreasureRoom;
        if (randomValue < 0.15f) return RoomPurpose.BossRoom; // Less likely
        if (randomValue < 0.3f) return RoomPurpose.CorridorHub;
        // ... add more rules ...

        // Default to standard room
        return RoomPurpose.Standard;

        // More complex logic could consider:
        // - Is this the first room? -> SpawnRoom
        // - Is the room very large? -> BossRoom or CorridorHub
        // - Is the room a dead end? -> TreasureRoom or PuzzleRoom
        // - How many connections does it have?
    }
}
*/
