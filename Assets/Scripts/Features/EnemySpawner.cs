using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Settings")]
    public GameObject enemyPrefab; // Prefab of the enemy to spawn
    public int enemyCount = 5; // Number of enemies to spawn
    public float spawnRadius = 10f; // Radius within which to spawn enemies

    private List<GameObject> spawnedEnemies = new List<GameObject>();

    // Method to spawn enemies at random positions within a defined radius
    public void SpawnEnemies(Vector3 spawnCenter)
    {
        ClearEnemies(); // Clear previously spawned enemies

        for (int i = 0; i < enemyCount; i++)
        {
            Vector3 spawnPosition = spawnCenter + Random.insideUnitSphere * spawnRadius;
            spawnPosition.y = 0; // Ensure enemies spawn on the ground level

            GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            spawnedEnemies.Add(enemy);
        }
    }

    // Method to clear all spawned enemies
    public void ClearEnemies()
    {
        foreach (GameObject enemy in spawnedEnemies)
        {
            Destroy(enemy);
        }
        spawnedEnemies.Clear();
    }
}