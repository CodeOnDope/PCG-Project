using UnityEngine;
using UnityEngine.InputSystem; // Use the new Input System
using System.Collections;
using System.Collections.Generic; // Required for Lists and Interfaces

// Ensure the Player GameObject has these components
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    #region Variables

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private InputActionReference moveActionReference;

    [Header("Aiming Input (for Shooting Direction)")]
    [SerializeField] private InputActionReference lookActionReference; // Assign Look Action Asset here
    private Camera mainCamera; // Cached reference to the main camera

    [Header("Shooting")]
    [SerializeField] private GameObject bulletPrefab; // Assign your Bullet Prefab
    [SerializeField] private Transform firePoint; // Assign the child GameObject where bullets spawn
    [SerializeField] private float fireRate = 4f; // Bullets per second
    [SerializeField] private float bulletSpeed = 15f; // Speed for bullets
    [SerializeField] private InputActionReference fireActionReference; // Assign Fire Action Asset here
    private float nextFireTime = 0f;
    private bool isFiring = false; // Tracks if fire button is held

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth; // Current health of the player

    [Header("Dash (Optional Ability)")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;
    [SerializeField] private InputActionReference dashActionReference; // Assign Dash Action Asset
    private bool isDashing = false;
    private float lastDashTime = -10f; // Allow dashing immediately

    [Header("Interaction (Optional Ability)")]
    [SerializeField] private float interactRange = 1.5f;
    [SerializeField] private LayerMask interactableLayer; // Set this layer in the Inspector for interactable objects
    [SerializeField] private InputActionReference interactActionReference; // Assign Interact Action Asset

    // Component References
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private Vector2 lookInput; // Still read mouse/aim position

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Cache component references
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main; // Ensure your main camera has the "MainCamera" tag

        // Basic validation / fallback for firepoint
        if (firePoint == null && transform.childCount > 0) {
             firePoint = transform.GetChild(0);
             Debug.LogWarning($"Player Fire Point not assigned. Using child '{firePoint.name}' as fallback.");
        }
        if (firePoint == null) {
            Debug.LogError("Player Fire Point not assigned and no child found! Please assign or create one.");
            GameObject fp = new GameObject("FirePoint");
            fp.transform.SetParent(transform);
            fp.transform.localPosition = Vector3.up * 0.5f; // Assumes player faces Up
            firePoint = fp.transform;
        }
    }

    private void Start()
    {
        // Initialize Health
        currentHealth = maxHealth;
        // OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Ensure Rigidbody rotation is frozen (CRITICAL FOR NO TILT)
        if (rb != null) {
             rb.constraints |= RigidbodyConstraints2D.FreezeRotation; // Force freeze rotation
             Debug.Log($"Player Start: Rigidbody Freeze Rotation Z = {rb.constraints.HasFlag(RigidbodyConstraints2D.FreezeRotation)}");
        } else {
             Debug.LogError("Player Rigidbody2D not found in Awake!");
        }
    }

    private void OnEnable()
    {
        // Enable actions if using InputActionReference approach and direct enabling
         moveActionReference?.action.Enable();
         lookActionReference?.action.Enable();
         fireActionReference?.action.Enable();
         dashActionReference?.action.Enable();
         interactActionReference?.action.Enable();
    }

    private void OnDisable()
    {
        // Disable actions
        moveActionReference?.action.Disable();
        lookActionReference?.action.Disable();
        fireActionReference?.action.Disable();
        dashActionReference?.action.Disable();
        interactActionReference?.action.Disable();
        // Stop movement when disabled
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        if (isDashing) return; // Don't process other inputs while dashing
        HandleInput(); // Reads inputs into variables
        // HandleAiming(); // Aiming rotation removed
        HandleShooting();
        HandleDashInput();
        HandleInteractionInput();
    }

    // Ensure FixedUpdate calls the single HandleMovement method
    private void FixedUpdate()
    {
        // Physics-based updates should happen in FixedUpdate
        HandleMovement();
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        // Read input values from actions
        if (moveActionReference != null && moveActionReference.action.enabled)
             moveInput = moveActionReference.action.ReadValue<Vector2>();
        else moveInput = Vector2.zero;

        if (lookActionReference != null && lookActionReference.action.enabled)
             lookInput = lookActionReference.action.ReadValue<Vector2>(); // Read mouse screen position
        else lookInput = Vector2.zero;

        if (fireActionReference != null && fireActionReference.action.enabled)
             isFiring = fireActionReference.action.IsPressed(); // Check if fire button is held
        else isFiring = false;
    }

    #endregion

    #region Movement

    // --- THIS IS THE ONLY HandleMovement METHOD DEFINITION ---
    private void HandleMovement()
    {
        if (isDashing) return; // Don't allow regular movement while dashing
    
        // Apply velocity directly for responsive top-down movement
        if (rb != null) // Safety check for Rigidbody
            rb.linearVelocity = moveInput.normalized * moveSpeed;
    }
    // --- End HandleMovement ---

    // --- HandleAiming() method entirely removed ---

    #endregion

    #region Shooting (Corrected Velocity)

    private void HandleShooting()
    {
        if (isFiring && Time.time >= nextFireTime) // Check fire input AND cooldown
        {
            // Also check if necessary components exist
            if (bulletPrefab != null && firePoint != null && mainCamera != null)
            {
                nextFireTime = Time.time + (1f / fireRate); // Reset cooldown
                Shoot();
            }
        }
    }

    private void Shoot()
    {
        // --- Pre-checks ---
        if (bulletPrefab == null) { Debug.LogError("Shoot: Bullet Prefab not assigned!"); return; }
        if (firePoint == null) { Debug.LogError("Shoot: Fire Point not assigned!"); return; }
        if (mainCamera == null) { Debug.LogError("Shoot: Main Camera reference is missing!"); return; }

        // --- Calculate aim direction at the moment of firing ---
        Vector3 mouseScreenPosition = Mouse.current.position.ReadValue(); // Get the mouse position in screen space
        mouseScreenPosition.z = mainCamera.WorldToScreenPoint(firePoint.position).z; // Use firePoint's Z for projection plane
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition); // Convert to world space
        Vector2 aimDirection = ((Vector2)mouseWorldPosition - (Vector2)firePoint.position).normalized; // Calculate direction

        if (aimDirection == Vector2.zero)
        {
            aimDirection = transform.up; // Fallback to player's world up if aiming at self
        }

        // Calculate rotation FOR THE BULLET SPRITE (assumes sprite points UP)
        float bulletAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg - 90f;
        Quaternion bulletRotation = Quaternion.Euler(0, 0, bulletAngle);

        // Instantiate the bullet
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, bulletRotation);

        // Set velocity
        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();
        if (bulletRb != null)
        {
            bulletRb.linearVelocity = aimDirection * bulletSpeed; // Use .velocity to set bullet speed
        }
        else
        {
            Debug.LogWarning($"Bullet prefab '{bulletPrefab.name}' is missing a Rigidbody2D.");
        }
    }

    #endregion

    #region Health & Damage

    public void TakeDamage(int damage)
    {
        if (isDashing || currentHealth <= 0) return;
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        Debug.Log($"{gameObject.name} took {damage} damage. HP: {currentHealth}/{maxHealth}");
        // OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0) { Die(); }
        // StartCoroutine(FlashRed());
    }

    private void Die()
    {
        Debug.Log("Player Died!");
        // OnDied?.Invoke();
        this.enabled = false;
        if(rb != null) rb.linearVelocity = Vector2.zero;
        // Add death effects, game over logic etc.
         gameObject.SetActive(false);
    }

    public void Heal(int amount)
    {
        if (currentHealth <= 0) return;
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        // OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"{gameObject.name} healed {amount}. HP: {currentHealth}/{maxHealth}");
    }

    #endregion

    #region Optional Abilities (Dash & Interact)

    private void HandleDashInput()
    {
        if (dashActionReference != null && dashActionReference.action.triggered && Time.time >= lastDashTime + dashCooldown && !isDashing) {
            StartCoroutine(PerformDash());
        }
    }

    private IEnumerator PerformDash()
    {
        isDashing = true;
        lastDashTime = Time.time;
        float originalDrag = rb.linearDamping;
        Vector2 dashDirection = moveInput.normalized;
        if (dashDirection == Vector2.zero) { dashDirection = transform.up; }

        rb.linearDamping = 0f;
        rb.linearVelocity = dashDirection * dashSpeed;
        // Start dash effects

        yield return new WaitForSeconds(dashDuration);

        // End dash effects
        if(isDashing) { // Check if still dashing
            rb.linearVelocity = Vector2.zero;
            rb.linearDamping = originalDrag;
            isDashing = false;
        }
    }

    private void HandleInteractionInput()
    {
        if (interactActionReference != null && interactActionReference.action.triggered) {
            PerformInteractionCheck();
        }
    }

    private void PerformInteractionCheck()
    {
        Collider2D[] nearbyObjects = Physics2D.OverlapCircleAll(transform.position, interactRange, interactableLayer);
        Collider2D closestObject = null;
        float closestDistSqr = float.MaxValue;
        foreach (Collider2D col in nearbyObjects) {
            if (col.gameObject == this.gameObject) continue;
            float distSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distSqr < closestDistSqr) { closestDistSqr = distSqr; closestObject = col; }
        }

        if (closestObject != null) {
            IInteractable interactable = closestObject.GetComponentInParent<IInteractable>();
            if (interactable != null) {
                Debug.Log($"Interacting with {closestObject.name}");
                interactable.Interact(gameObject);
            }
        }
    }

    // Define Interface needed for interaction check (can be in its own C# file: IInteractable.cs)
    public interface IInteractable
    {
        void Interact(GameObject interactor);
        string GetInteractText(); // Optional
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);

        if (firePoint != null) {
             Gizmos.color = Color.red;
             Gizmos.DrawSphere(firePoint.position, 0.1f);
        }
    }

    #endregion

} // End of PlayerController class