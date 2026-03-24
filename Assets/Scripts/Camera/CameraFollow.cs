using UnityEngine;

/// <summary>
/// Makes the camera smoothly follow a target (the player).
/// The camera keeps a fixed offset from the player — it doesn't rotate, just moves.
///
/// The angle and starting position are set directly in the scene (not in code).
/// This script just maintains that offset while the player moves.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target; // the player transform to follow

    [Header("Follow")]
    [SerializeField] private float smoothSpeed = 10f; // higher = snappier follow, lower = more lag

    // The distance and direction from the target to the camera, calculated once at start
    private Vector3 _offset;

    private void Start()
    {
        // If no target was assigned in the Inspector, try to find the player automatically by tag
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        // Lock in the offset based on where the camera is placed in the scene
        // This means you can position/angle the camera however you want in the Editor
        // and this script will maintain that exact relative position
        if (target != null)
            _offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        // LateUpdate runs after all Update() calls — important for cameras so the player
        // has already moved before we reposition the camera, preventing jitter

        // Player doesn't exist yet (NGO hasn't spawned it) — keep searching every frame
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            // Found the spawned player — lock in the offset from the camera's current position
            target  = player.transform;
            _offset = transform.position - target.position;
        }

        // Where the camera should be this frame
        Vector3 desired = target.position + _offset;

        // Lerp = Linear Interpolation: smoothly moves from current position toward desired
        // smoothSpeed * Time.deltaTime controls how fast it catches up each frame
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}
