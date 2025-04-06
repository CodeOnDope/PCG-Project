using UnityEngine;

public class StartTrigger : MonoBehaviour
{
    public delegate void GameStartAction();
    public static event GameStartAction OnGameStart;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            StartGame();
        }
    }

    private void StartGame()
    {
        OnGameStart?.Invoke();
        // Additional logic for starting the game can be added here
        Debug.Log("Game Started!");
    }
}