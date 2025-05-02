using UnityEngine;

// ██████╗  ██████╗  ██████╗    ██╗     ███████╗██╗   ██╗███████╗██╗         ██████╗ ███████╗███╗   ██╗
// ██╔══██╗██╔════╝ ██╔════╝    ██║     ██╔════╝██║   ██║██╔════╝██║        ██╔════╝ ██╔════╝████╗  ██║
// ██████╔╝██║      ██║  ███╗   ██║     █████╗  ██║   ██║█████╗  ██║        ██║  ███╗█████╗  ██╔██╗ ██║
// ██╔═══╝ ██║      ██║   ██║   ██║     ██╔══╝  ╚██╗ ██╔╝██╔══╝  ██║        ██║   ██║██╔══╝  ██║╚██╗██║
// ██║     ╚██████╗ ╚██████╔╝   ███████╗███████╗ ╚████╔╝ ███████╗███████╗   ╚██████╔╝███████╗██║ ╚████║
// ╚═╝      ╚═════╝  ╚═════╝    ╚══════╝╚══════╝  ╚═══╝  ╚══════╝╚══════╝    ╚═════╝ ╚══════╝╚═╝  ╚═══╝
//
// PCG Level Generator for Unity
// Copyright (c) 2025 Dineshkumar & Kamalanathan
// Version: 1.0.0

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothSpeed = 0.125f;
    public Vector2 offset = Vector2.zero; // Changed to Vector2 since we only need X and Y offset

    [Tooltip("Fixed Z position for the camera")]
    public float fixedZPosition = -5f;

    private void Start()
    {
        // Automatically find the Player GameObject and assign it to target
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                Debug.LogWarning("Player GameObject not found in the scene. Make sure it is tagged as 'Player'.");
            }
        }

        // Set the initial Z position
        Vector3 position = transform.position;
        position.z = fixedZPosition;
        transform.position = position;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Create desired position using only X and Y from target, with fixed Z
        Vector3 desiredPosition = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            fixedZPosition
        );

        // Smooth movement
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Ensure Z is always exactly fixedZPosition
        transform.position = smoothedPosition;
    }
}