using UnityEngine;
using System.Collections.Generic;

public class DecoratorManager : MonoBehaviour
{/*
    [Header("Decorator Categories")]
    public GameObject[] environmentalDecorators; // Static objects like rocks, plants, etc.
    public GameObject[] interactiveDecorators;   // Objects that players can interact with
    public GameObject[] collectibleDecorators;   // Collectibles like coins, power-ups, etc.

    [Header("Decorator Settings")]
    public int minDecoratorsPerRoom = 1;
    public int maxDecoratorsPerRoom = 5;

    private System.Random random;

    void Start()
    {
        random = new System.Random();
    }

    /// <summary>
    /// Places decorators in a given room.
    /// </summary>
    /// <param name="room">The room where decorators will be placed.</param>
    public void PlaceDecorators(Room room)
    {
        int decoratorCount = Random.Range(minDecoratorsPerRoom, maxDecoratorsPerRoom + 1);

        for (int i = 0; i < decoratorCount; i++)
        {
            // Randomly select a category
            GameObject[] selectedCategory = SelectRandomCategory();

            // Randomly select a decorator from the chosen category
            GameObject decoratorPrefab = selectedCategory[random.Next(selectedCategory.Length)];

            // Randomly position the decorator within the room
            Vector2 decoratorPosition = new Vector2(
                room.Position.x + Random.Range(1, room.Width - 1),
                room.Position.y + Random.Range(1, room.Height - 1)
            );

            // Instantiate the decorator
            Instantiate(decoratorPrefab, decoratorPosition, Quaternion.identity);
        }
    }

    /// <summary>
    /// Selects a random category of decorators.
    /// </summary>
    private GameObject[] SelectRandomCategory()
    {
        int categoryIndex = random.Next(3); // 0 = Environmental, 1 = Interactive, 2 = Collectible

        switch (categoryIndex)
        {
            case 0: return environmentalDecorators;
            case 1: return interactiveDecorators;
            case 2: return collectibleDecorators;
            default: return environmentalDecorators; // Fallback to environmental
        }
    }*/
}