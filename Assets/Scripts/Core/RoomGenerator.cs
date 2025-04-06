using UnityEngine;
using System.Collections.Generic;

public class RoomGenerator : MonoBehaviour
{
    [Header("Room Settings")]
    public int minRoomWidth = 5;
    public int maxRoomWidth = 15;
    public int minRoomHeight = 5;
    public int maxRoomHeight = 15;

    [Header("Room Connections")]
    public bool allowDiagonalConnections = true;

    private List<Rect> rooms = new List<Rect>();

    public void GenerateRooms(int roomCount)
    {
        rooms.Clear();

        for (int i = 0; i < roomCount; i++)
        {
            Rect room = CreateRoom();
            rooms.Add(room);
        }

        ConnectRooms();
    }

    private Rect CreateRoom()
    {
        int width = Random.Range(minRoomWidth, maxRoomWidth + 1);
        int height = Random.Range(minRoomHeight, maxRoomHeight + 1);
        int x = Random.Range(0, (int)(Screen.width - width));
        int y = Random.Range(0, (int)(Screen.height - height));

        return new Rect(x, y, width, height);
    }

    private void ConnectRooms()
    {
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            ConnectTwoRooms(rooms[i], rooms[i + 1]);
        }
    }

    private void ConnectTwoRooms(Rect roomA, Rect roomB)
    {
        Vector2Int centerA = new Vector2Int(Mathf.FloorToInt(roomA.x + roomA.width / 2), Mathf.FloorToInt(roomA.y + roomA.height / 2));
        Vector2Int centerB = new Vector2Int(Mathf.FloorToInt(roomB.x + roomB.width / 2), Mathf.FloorToInt(roomB.y + roomB.height / 2));

        if (allowDiagonalConnections)
        {
            // Create a diagonal connection
            DrawLine(centerA, centerB);
        }
        else
        {
            // Create an L-shaped connection
            DrawLine(centerA, new Vector2Int(centerA.x, centerB.y));
            DrawLine(new Vector2Int(centerA.x, centerB.y), centerB);
        }
    }

    private void DrawLine(Vector2Int from, Vector2Int to)
    {
        // Implement the logic to draw a line between two points
        // This could involve setting tiles or creating a corridor
    }
}