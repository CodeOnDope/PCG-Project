using UnityEngine;
using System.Collections.Generic; // Required for Lists

public class BasicDungeonGenerator : MonoBehaviour
{
    [Header("Room Settings")]
    [Tooltip("The prefab for rooms. Must have RoomData component.")]
    public GameObject roomPrefab;
    [Tooltip("Minimum number of rooms to attempt generating.")]
    public int minRooms = 8;
    [Tooltip("Maximum number of rooms to attempt generating.")]
    public int maxRooms = 15;
    [Tooltip("Minimum size (width/depth) of a room.")]
    public int minRoomSize = 5;
    [Tooltip("Maximum size (width/depth) of a room.")]
    public int maxRoomSize = 12;
    [Tooltip("Maximum attempts to place a room before giving up.")]
    public int maxPlacementAttempts = 50;

    [Header("Generation Area")]
    [Tooltip("Size of the area where rooms can be placed.")]
    public Vector2 generationAreaSize = new Vector2(100, 100);

    [Header("Corridor Settings (Optional)")]
    [Tooltip("Prefab for corridor segments (e.g., a simple cube/plane).")]
    public GameObject corridorPrefab; // Assign if you want visual corridors
    [Tooltip("Width of the corridor visuals.")]
    public float corridorWidth = 1.0f;

    // Private list to keep track of generated rooms and their bounds
    private List<RoomInfo> generatedRooms = new List<RoomInfo>();

    // Helper class to store room data during generation
    private class RoomInfo
    {
        public Rect bounds; // Using Rect for 2D bounds check
        public Vector3 center;
        public RoomData roomData; // Reference to the attached RoomData component
        public GameObject roomObject; // Reference to the instantiated GameObject

        public RoomInfo(Rect b, Vector3 c, RoomData data, GameObject obj)
        {
            bounds = b;
            center = c;
            roomData = data;
            roomObject = obj;
        }
    }

    // --- Main Generation Function ---
    void Start()
    {
        // Ensure prefabs are assigned
        if (roomPrefab == null)
        {
            Debug.LogError("Room Prefab is not assigned in the Inspector!");
            return;
        }
        // Optional: Check for corridor prefab only if you intend to use it
        // if (corridorPrefab == null) { ... }

        GenerateDungeon();
    }

    void GenerateDungeon()
    {
        ClearDungeon(); // Clear previous generation if any

        int roomsToGenerate = Random.Range(minRooms, maxRooms + 1);
        int roomsSuccessfullyPlaced = 0;

        for (int i = 0; i < roomsToGenerate; i++)
        {
            bool roomPlaced = TryPlaceRoom();
            if (roomPlaced)
            {
                roomsSuccessfullyPlaced++;
            }
        }

        Debug.Log($"Attempted to generate {roomsToGenerate} rooms. Successfully placed {roomsSuccessfullyPlaced}.");

        // Connect the rooms (simple example)
        if (generatedRooms.Count > 1)
        {
            ConnectRooms();
        }

        // Assign Player Spawn (Example)
        AssignSpawnRoom();
    }

    // --- Room Placement ---
    bool TryPlaceRoom()
    {
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            // 1. Determine random size
            int roomWidth = Random.Range(minRoomSize, maxRoomSize + 1);
            int roomDepth = Random.Range(minRoomSize, maxRoomSize + 1);

            // 2. Determine random position within generation area
            // Ensure position accounts for room size so it stays within bounds
            float randomX = Random.Range(-generationAreaSize.x / 2 + roomWidth / 2f, generationAreaSize.x / 2 - roomWidth / 2f);
            float randomZ = Random.Range(-generationAreaSize.y / 2 + roomDepth / 2f, generationAreaSize.y / 2 - roomDepth / 2f);
            Vector3 potentialPosition = new Vector3(randomX, 0, randomZ); // Assuming Y=0 for floor level

            // 3. Create potential bounds (using Rect for 2D overlap check)
            // Rect uses (x, y, width, height) - we use x, z for our ground plane
            Rect potentialBounds = new Rect(
                potentialPosition.x - roomWidth / 2f,
                potentialPosition.z - roomDepth / 2f,
                roomWidth,
                roomDepth
            );

            // 4. Check for overlaps with existing rooms
            bool overlaps = false;
            foreach (RoomInfo existingRoom in generatedRooms)
            {
                // Add padding to prevent rooms touching directly (optional)
                if (potentialBounds.Overlaps(existingRoom.bounds.Pad(1.0f))) // Rect.Overlaps checks intersection
                {
                    overlaps = true;
                    break;
                }
            }

            // 5. If no overlap, place the room
            if (!overlaps)
            {
                PlaceRoom(potentialPosition, roomWidth, roomDepth, potentialBounds);
                return true; // Successfully placed
            }
        }

        // Failed to place after max attempts
        // Debug.LogWarning("Failed to place a room after " + maxPlacementAttempts + " attempts.");
        return false;
    }

    void PlaceRoom(Vector3 position, int width, int depth, Rect bounds)
    {
        // Instantiate the room prefab
        GameObject newRoomObject = Instantiate(roomPrefab, position, Quaternion.identity, this.transform); // Parent to generator
        newRoomObject.transform.localScale = new Vector3(width, 1, depth); // Scale the *visual part* if needed, or handle size internally

        // Get RoomData component (essential)
        RoomData roomData = newRoomObject.GetComponent<RoomData>();
        if (roomData == null)
        {
            Debug.LogError("Instantiated Room Prefab is missing the RoomData component!", newRoomObject);
            Destroy(newRoomObject); // Clean up invalid object
            return;
        }

        // Determine purpose and initialize
        RoomPurpose purpose = DetermineRoomPurpose(bounds.size); // Pass size info
        string uniqueID = $"Room_{generatedRooms.Count}"; // Simple ID based on order
        roomData.InitializeRoom(purpose, uniqueID);

        // Store room info
        RoomInfo newRoomInfo = new RoomInfo(bounds, position, roomData, newRoomObject);
        generatedRooms.Add(newRoomInfo);

        // Optional: Adjust visual elements based on size (e.g., scale a child 'Floor' object)
        Transform floor = newRoomObject.transform.Find("Floor"); // Example: Assuming a child named Floor
        if (floor != null)
        {
           // If the room prefab itself is 1x1, scale the floor to match width/depth
           floor.localScale = new Vector3(width, 1, depth);
        } else {
            // If the root object represents the floor, scale it directly
            newRoomObject.transform.localScale = new Vector3(width, newRoomObject.transform.localScale.y, depth);
        }
    }

    // --- Room Purpose Assignment ---
    RoomPurpose DetermineRoomPurpose(Vector2 roomSize)
    {
        // Example Logic: Assign purpose based on size or randomly
        float area = roomSize.x * roomSize.y;

        // Very first room could be spawn (handled separately maybe)
        if (generatedRooms.Count == 0) return RoomPurpose.Standard; // Will be reassigned later if needed

        // Example rules (adjust probabilities as needed)
        if (area > (maxRoomSize * maxRoomSize * 0.7f) && Random.value < 0.3f) return RoomPurpose.BossRoom; // Large rooms have a chance to be boss rooms
        if (area < (minRoomSize * minRoomSize * 1.5f) && Random.value < 0.4f) return RoomPurpose.TreasureRoom; // Small rooms have a chance to be treasure
        if (Random.value < 0.1f) return RoomPurpose.PuzzleRoom;
        if (Random.value < 0.05f) return RoomPurpose.ShopRoom;

        // Default
        return RoomPurpose.Standard;
    }

     void AssignSpawnRoom()
    {
        if (generatedRooms.Count > 0)
        {
            // Example: Assign the first generated room as the spawn room
            RoomInfo spawnRoom = generatedRooms[0];
            spawnRoom.roomData.Purpose = RoomPurpose.SpawnRoom; // Override purpose
            spawnRoom.roomObject.name = $"Room_{spawnRoom.roomData.RoomID} ({RoomPurpose.SpawnRoom})"; // Update name
            Debug.Log($"Assigned Room {spawnRoom.roomData.RoomID} as Spawn Room.");

            // --- TODO: Place Player ---
            // Instantiate player prefab at spawnRoom.center + Vector3.up * 0.5f;
        }
        else
        {
            Debug.LogWarning("No rooms generated, cannot assign spawn room.");
        }
    }


    // --- Corridor Generation (Basic Example) ---
    void ConnectRooms()
    {
        // Simple connection: Connect each room to the next one in the list
        for (int i = 0; i < generatedRooms.Count - 1; i++)
        {
            Vector3 start = generatedRooms[i].center;
            Vector3 end = generatedRooms[i + 1].center;
            CreateCorridor(start, end);
        }

        // Alternative: Connect randomly, or use Minimum Spanning Tree for better layouts
    }

    void CreateCorridor(Vector3 startPos, Vector3 endPos)
    {
        if (corridorPrefab == null)
        {
            // Draw debug lines if no prefab is assigned
            Debug.DrawLine(startPos, endPos, Color.cyan, 60f); // Draw line for 60 seconds
            return;
        }

        // --- Visual Corridor Instantiation ---
        // This is a very basic straight-line corridor.
        // Real corridors often need pathfinding (like A*) and tile-based placement.

        Vector3 direction = (endPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, endPos);
        Vector3 centerPos = startPos + direction * distance / 2;

        GameObject corridor = Instantiate(corridorPrefab, centerPos, Quaternion.LookRotation(direction), this.transform);
        // Scale the corridor prefab (assuming it's a 1x1x1 cube/quad initially)
        corridor.transform.localScale = new Vector3(corridorWidth, 1f, distance);

        // Add a CorridorData component if needed for interaction/decoration
        corridor.name = $"Corridor_{generatedRooms[0].roomData.RoomID}_to_{generatedRooms[1].roomData.RoomID}"; // Example naming
    }


    // --- Utility Functions ---
    void ClearDungeon()
    {
        // Destroy previously generated rooms and corridors
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        generatedRooms.Clear();
        Debug.Log("Previous dungeon cleared.");
    }
}

// Helper extension for Rect padding (optional but useful)
public static class RectExtensions
{
    public static Rect Pad(this Rect rect, float padding)
    {
        return new Rect(
            rect.x - padding,
            rect.y - padding,
            rect.width + padding * 2,
            rect.height + padding * 2
        );
    }
}
