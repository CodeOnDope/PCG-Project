using UnityEngine;

public class DecoratorSpawner : MonoBehaviour
{
    [Header("Decorator Settings")]
    public GameObject decoratorPrefab; // Prefab for the decorative element
    public int numberOfDecorators = 10; // Number of decorators to spawn
    public Vector2 levelSize; // Size of the level for positioning decorators

    // Method to spawn decorators in the level
    public void SpawnDecorators()
    {
        for (int i = 0; i < numberOfDecorators; i++)
        {
            Vector2 spawnPosition = GetRandomPosition();
            Instantiate(decoratorPrefab, spawnPosition, Quaternion.identity);
        }
    }

    // Method to get a random position within the level bounds
    private Vector2 GetRandomPosition()
    {
        float x = Random.Range(0, levelSize.x);
        float y = Random.Range(0, levelSize.y);
        return new Vector2(x, y);
    }
}