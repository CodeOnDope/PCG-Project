using UnityEngine;

public class EndTrigger : MonoBehaviour
{
    public delegate void EndGameAction();
    public static event EndGameAction OnEndGame;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TriggerEndGame();
        }
    }

    private void TriggerEndGame()
    {
        OnEndGame?.Invoke();
        Debug.Log("Game Over! Triggering end game events.");
        // Additional logic for ending the game can be added here, such as loading a new scene or displaying a game over screen.
    }
}