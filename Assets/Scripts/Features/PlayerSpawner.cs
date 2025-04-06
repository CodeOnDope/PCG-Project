using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Player Settings")]
    public GameObject playerPrefab; // Reference to the player prefab
    public Vector2 spawnPosition; // Position where the player will spawn
    public int numberOfPlayers = 1; // Number of players to spawn

    // Method to spawn players at the specified position
    public void SpawnPlayers()
    {
        for (int i = 0; i < numberOfPlayers; i++)
        {
            Vector2 spawnPos = spawnPosition + new Vector2(i * 2, 0); // Adjust spawn position for multiple players
            Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        }
    }
}