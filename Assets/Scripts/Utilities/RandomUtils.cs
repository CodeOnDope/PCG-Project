using UnityEngine;

public static class RandomUtils
{
    public static int GetRandomInt(int min, int max)
    {
        return Random.Range(min, max);
    }

    public static float GetRandomFloat(float min, float max)
    {
        return Random.Range(min, max);
    }

    public static Vector2 GetRandomPositionInRange(Vector2 min, Vector2 max)
    {
        float x = GetRandomFloat(min.x, max.x);
        float y = GetRandomFloat(min.y, max.y);
        return new Vector2(x, y);
    }

    public static T GetRandomElement<T>(T[] array)
    {
        if (array == null || array.Length == 0)
        {
            Debug.LogWarning("Array is null or empty. Returning default value.");
            return default;
        }
        int randomIndex = GetRandomInt(0, array.Length);
        return array[randomIndex];
    }
}