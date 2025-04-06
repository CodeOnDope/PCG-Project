using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Required for Coroutines

// Enum to manage enemy states
public enum EnemyState { Idle, Patrolling, Chasing, Attacking, Returning }

[RequireComponent(typeof(Collider2D))] // Ensure collider exists
public class EnemyAI : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float rotationSpeed = 180f; // Degrees per second to turn
    [SerializeField] private float detectionRange = 10f;
    [SerializeField] private float attackRange = 8f; // Must be <= detectionRange
    [SerializeField] private float stoppingDistance = 5f; // How close to get to player when attacking/chasing
    [SerializeField] private float loseSightDuration = 3f; // Time after losing sight before returning
    // --- ADDED MISSING VARIABLE ---
    [Tooltip("Range beyond which player is considered lost (usually > detectionRange).")]
    [SerializeField] private float loseSightRange = 12f;
    // -----------------------------
    [SerializeField] private float fireRate = 1f; // Shots per second
    // Stats below are set by InitializeEnemy primarily
    private float currentHealth;

    [Header("Combat References")]
    [SerializeField] private GameObject bulletPrefab; // Assign enemy bullet prefab
    [SerializeField] private Transform firePoint; // Assign child object for bullet spawn
    [SerializeField] private LayerMask playerLayer; // Set to Player layer in Inspector
    [SerializeField] private LayerMask obstacleLayer; // Set to Wall/Obstacle layer(s) in Inspector

    [Header("Patrol")]
    [SerializeField] private float waypointReachedThreshold = 0.2f;
    private List<Vector3> patrolWaypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private bool waitingAtWaypoint = false;
    private float waitTimer = 0f;
    [SerializeField] private float waypointWaitTime = 1.0f; // How long to pause at a waypoint

    // State Machine
    [Header("State (Read Only)")]
    [SerializeField] // Show in inspector for debugging
    private EnemyState currentState = EnemyState.Idle; // Start Idle until initialized

    // References & State Tracking
    private Transform playerTarget;
    private Vector3 lastKnownPlayerPosition;
    private float timeLostPlayer = -1f;
    private float nextFireTime = 0f;
    private Coroutine waitCoroutine; // To handle waiting at waypoints

    // Optional Component References
    // private Rigidbody2D rb;
    // private Animator animator;

    void Awake()
    {
        // rb = GetComponent<Rigidbody2D>();
        // animator = GetComponentInChildren<Animator>();
        currentHealth = maxHealth; // Initialize health here too
        currentState = EnemyState.Idle;
    }

    // Called by Level Generator to provide waypoints and stats
    public void InitializeEnemy(List<Vector3> waypoints, float health, float speed /*, other stats like damage, range etc.*/)
    {
        // Debug.Log($"Initializing Enemy {gameObject.name} with {waypoints?.Count ?? 0} waypoints.");
        this.patrolWaypoints = new List<Vector3>(waypoints ?? new List<Vector3>());
        this.maxHealth = health;
        this.currentHealth = health;
        this.moveSpeed = speed;
        // Assign other stats here...

        if (this.patrolWaypoints.Count > 0)
        {
            currentWaypointIndex = 0;
            currentState = EnemyState.Patrolling;
            // Prevent spawning inside geometry if possible
            Vector3 startPos = patrolWaypoints[0];
            Collider2D myCollider = GetComponent<Collider2D>();
            if (myCollider != null)
            {
                Collider2D overlap = Physics2D.OverlapCircle(startPos, myCollider.bounds.extents.magnitude, obstacleLayer);
                if (overlap != null)
                {
                    Debug.LogWarning($"Enemy {gameObject.name} start waypoint {startPos} overlaps obstacle {overlap.name}. Starting at current position instead.");
                }
                else
                {
                    transform.position = startPos; // Start at the first waypoint if clear
                }
            }
            else
            {
                transform.position = startPos;
            }

        }
        else
        {
            currentState = EnemyState.Idle;
        }
        FindPlayer(); // Initial attempt to locate player
    }

    void Update()
    {
        if (currentHealth <= 0) return; // Don't run logic if dead

        if (playerTarget == null && currentState != EnemyState.Idle && currentState != EnemyState.Patrolling)
        {
            // If we were aggressive but suddenly lost target reference, try finding again or return
            FindPlayer();
            if (playerTarget == null && currentState != EnemyState.Returning)
            { // Check again after FindPlayer
                ChangeState(EnemyState.Returning);
            }
        }

        // State Machine Execution
        switch (currentState)
        {
            case EnemyState.Idle: IdleBehavior(); break;
            case EnemyState.Patrolling: PatrolBehavior(); break;
            case EnemyState.Chasing: ChaseBehavior(); break;
            case EnemyState.Attacking: AttackBehavior(); break;
            case EnemyState.Returning: ReturnBehavior(); break;
        }

        CheckStateTransitions(); // Check transitions after behavior update
    }

    // --- State Behaviors ---

    void IdleBehavior() { CheckForPlayer(); }

    void PatrolBehavior()
    {
        StopWaitingIfNeeded();
        if (patrolWaypoints == null || patrolWaypoints.Count == 0) { ChangeState(EnemyState.Idle); return; }

        Vector3 targetWaypoint = patrolWaypoints[currentWaypointIndex];
        MoveTowards(targetWaypoint);
        RotateTowards(targetWaypoint);

        if (Vector3.Distance(transform.position, targetWaypoint) < waypointReachedThreshold)
        {
            if (!waitingAtWaypoint) { waitCoroutine = StartCoroutine(WaitAtWaypoint()); }
        }
        CheckForPlayer(); // Can detect player while patrolling
    }

    IEnumerator WaitAtWaypoint()
    {
        waitingAtWaypoint = true; waitTimer = 0f;
        while (waitTimer < waypointWaitTime)
        {
            CheckForPlayer();
            if (currentState != EnemyState.Patrolling) { waitingAtWaypoint = false; yield break; } // Exit if state changed
            waitTimer += Time.deltaTime; yield return null;
        }
        waitingAtWaypoint = false;
        currentWaypointIndex = (currentWaypointIndex + 1) % patrolWaypoints.Count;
        waitCoroutine = null;
    }

    void StopWaitingIfNeeded()
    {
        if (waitCoroutine != null) { StopCoroutine(waitCoroutine); waitCoroutine = null; }
        waitingAtWaypoint = false;
    }

    void ChaseBehavior()
    {
        StopWaitingIfNeeded();
        if (playerTarget == null) { ChangeState(EnemyState.Returning); return; }

        if (Vector3.Distance(transform.position, playerTarget.position) > stoppingDistance)
        {
            MoveTowards(playerTarget.position);
        }
        RotateTowards(playerTarget.position);
        lastKnownPlayerPosition = playerTarget.position;
    }

    void AttackBehavior()
    {
        StopWaitingIfNeeded();
        if (playerTarget == null) { ChangeState(EnemyState.Returning); return; }

        RotateTowards(playerTarget.position); // Keep facing player

        // Check fire rate and Line of Sight
        if (Time.time >= nextFireTime && HasLineOfSight(playerTarget))
        {
            Shoot();
            nextFireTime = Time.time + (1f / fireRate);
        }
    }

    void ReturnBehavior()
    {
        StopWaitingIfNeeded();
        Vector3 returnDestination = GetReturnDestination();
        MoveTowards(returnDestination);
        RotateTowards(returnDestination);

        if (Vector3.Distance(transform.position, returnDestination) < waypointReachedThreshold)
        {
            currentWaypointIndex = FindNearestWaypointIndex(); // Snap to nearest waypoint index
            ChangeState(EnemyState.Patrolling);
        }
        CheckForPlayer(); // Can re-detect player while returning
    }

    // --- State Transitions & Checks ---

    void CheckStateTransitions()
    {
        if (playerTarget == null)
        {
            if ((currentState == EnemyState.Chasing || currentState == EnemyState.Attacking) && currentState != EnemyState.Returning)
            {
                ChangeState(EnemyState.Returning); // Lost target while aggressive -> Return
            }
            return; // No player, no need for aggressive transitions
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTarget.position);
        bool hasLOS = HasLineOfSight(playerTarget);

        switch (currentState)
        {
            case EnemyState.Idle:
            case EnemyState.Patrolling:
            case EnemyState.Returning:
                if (distanceToPlayer <= detectionRange && hasLOS) ChangeState(EnemyState.Chasing);
                break;
            case EnemyState.Chasing:
                if (distanceToPlayer <= attackRange && hasLOS) ChangeState(EnemyState.Attacking);
                else if (distanceToPlayer > loseSightRange || !hasLOS)
                { // Use the declared loseSightRange
                    if (timeLostPlayer < 0) timeLostPlayer = Time.time;
                    if (Time.time > timeLostPlayer + loseSightDuration) { ChangeState(EnemyState.Returning); playerTarget = null; }
                }
                else timeLostPlayer = -1f; // Reset timer if still in sight/range
                break;
            case EnemyState.Attacking:
                if (distanceToPlayer > attackRange || !hasLOS)
                {
                    if (distanceToPlayer <= loseSightRange && hasLOS) ChangeState(EnemyState.Chasing); // Still visible but out of attack range
                    else
                    { // Lost sight or too far
                        if (timeLostPlayer < 0) timeLostPlayer = Time.time;
                        if (Time.time > timeLostPlayer + loseSightDuration) { ChangeState(EnemyState.Returning); playerTarget = null; }
                    }
                }
                else timeLostPlayer = -1f; // Reset timer if still attacking validly
                break;
        }
    }

    void CheckForPlayer()
    {
        if (playerTarget != null) return; // Already have a target
        if (currentState == EnemyState.Chasing || currentState == EnemyState.Attacking) return; // Don't search if already aggressive

        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, detectionRange, playerLayer);
        if (playerCollider != null)
        {
            if (HasLineOfSight(playerCollider.transform))
            {
                playerTarget = playerCollider.transform;
                lastKnownPlayerPosition = playerTarget.position;
                timeLostPlayer = -1f;
                ChangeState(EnemyState.Chasing);
            }
        }
    }

    bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        Transform origin = firePoint ?? transform; // Use firepoint if available, else enemy center
        Vector2 directionToTarget = (target.position - origin.position).normalized;
        float distanceToTarget = Vector2.Distance(origin.position, target.position);
        RaycastHit2D hit = Physics2D.Raycast(origin.position, directionToTarget, distanceToTarget, obstacleLayer);
        // Debug.DrawRay(origin.position, directionToTarget * distanceToTarget, hit.collider == null ? Color.green : Color.red);
        return hit.collider == null; // True if no obstacle hit
    }

    void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        timeLostPlayer = -1f; // Reset lost timer on any state change
        StopWaitingIfNeeded();
        // Debug.Log($"{gameObject.name} -> {newState}"); // Optional state change log
    }

    // --- Actions ---

    void MoveTowards(Vector3 targetPosition)
    {
        transform.position = Vector2.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
        // Or use Rigidbody velocity/MovePosition if preferred
    }

    void RotateTowards(Vector3 targetPosition)
    {
        Vector2 direction = (targetPosition - transform.position).normalized;
        if (direction != Vector2.zero)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
            // Rotate self (adjust if only a turret part should rotate)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    void Shoot() { /* ... Paste working Shoot logic here ... */ } // Omitted - Use previous corrected Shoot logic

    public void TakeDamage(float amount) { /* ... Paste working TakeDamage logic here ... */ } // Omitted - Use previous corrected TakeDamage logic

    void Die() { /* ... Paste working Die logic here ... */ } // Omitted - Use previous corrected Die logic

    // --- Helpers ---

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player"); // Ensure player has "Player" tag
        playerTarget = playerObj ? playerObj.transform : null; // Assign if found, else null
    }

    Vector3 GetReturnDestination() { return FindNearestWaypoint(); } // Go back to nearest waypoint

    Vector3 FindNearestWaypoint()
    {
        if (patrolWaypoints == null || patrolWaypoints.Count == 0) return transform.position;
        Vector3 nearest = patrolWaypoints[0]; float minDistSqr = (transform.position - nearest).sqrMagnitude;
        for (int i = 1; i < patrolWaypoints.Count; i++)
        {
            float distSqr = (transform.position - patrolWaypoints[i]).sqrMagnitude;
            if (distSqr < minDistSqr) { minDistSqr = distSqr; nearest = patrolWaypoints[i]; }
        }
        return nearest;
    }

    int FindNearestWaypointIndex()
    {
        if (patrolWaypoints == null || patrolWaypoints.Count == 0) return 0;
        int nearestIndex = 0; float minDistSqr = (transform.position - patrolWaypoints[0]).sqrMagnitude;
        for (int i = 1; i < patrolWaypoints.Count; i++)
        {
            float distSqr = (transform.position - patrolWaypoints[i]).sqrMagnitude;
            if (distSqr < minDistSqr) { minDistSqr = distSqr; nearestIndex = i; }
        }
        return nearestIndex;
    }

    // --- Gizmos ---
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.gray; Gizmos.DrawWireSphere(transform.position, loseSightRange); // Use the declared variable
        Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, stoppingDistance);

        if (patrolWaypoints != null && patrolWaypoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolWaypoints.Count; i++)
            {
                Gizmos.DrawSphere(patrolWaypoints[i], 0.3f);
                if (patrolWaypoints.Count > 1)
                {
                    int nextIndex = (i + 1) % patrolWaypoints.Count;
                    Gizmos.DrawLine(patrolWaypoints[i], patrolWaypoints[nextIndex]);
                }
            }
            // Highlight current target waypoint
            if (currentState == EnemyState.Patrolling && !waitingAtWaypoint)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(transform.position, patrolWaypoints[currentWaypointIndex]);
            }
        }
        if (playerTarget != null) { /* ... Paste LOS Gizmo logic here ... */ } // Omitted - Use previous
    }
    // --- END OF OMITTED GIZMOS --- - Please paste the working logic from the previous full script here ---

} // End of EnemyAI class