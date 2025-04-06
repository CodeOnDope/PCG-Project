using UnityEngine;
public class Bullet : MonoBehaviour {
    public float speed = 10f;
    public float lifetime = 3f;
    public int damage = 10; // Damage this bullet deals

    void Start() {
        // Destroy the bullet after its lifetime expires
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other) {
        // Check if the bullet hit the player
        PlayerController playerController = other.GetComponent<PlayerController>();
        if (playerController != null) {
       //     playerController.TakeDamage(damage); // Call the TakeDamage method in PlayerController
       //     Destroy(gameObject); // Destroy the bullet on hit
            return;
        }

        // Check if the bullet hit an enemy
        EnemyAI enemyAI = other.GetComponent<EnemyAI>();
        if (enemyAI != null) {
            enemyAI.TakeDamage(damage); // Call the TakeDamage method in EnemyAI
            Destroy(gameObject); // Destroy the bullet on hit
            return;
        }

        // Optionally, check if the bullet hit a wall or obstacle
        // if (((1 << other.gameObject.layer) & obstacleLayer) != 0) {
        //     Destroy(gameObject);
        // }
    }
}