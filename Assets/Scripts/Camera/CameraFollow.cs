using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Makes the camera smoothly follow a target (the player).
/// The camera keeps a fixed offset from the player — it doesn't rotate, just moves.
///
/// The angle and starting position are set directly in the scene (not in code).
/// This script just maintains that offset while the player moves.
///
/// In multiplayer, it always follows the LOCAL player (the one this machine owns),
/// not just any player in the scene.
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
        // If no target was assigned in the Inspector, try to find the local player now
        if (target == null)
            TryFindLocalPlayer();

        if (target != null)
            _offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        // LateUpdate runs after all Update() calls — important for cameras so the player
        // has already moved before we reposition the camera, preventing jitter

        // Player not yet found — keep searching every frame until NGO spawns it
        if (target == null)
        {
            TryFindLocalPlayer();
            if (target == null) return;
        }

        // Where the camera should be this frame
        Vector3 desired = target.position + _offset;

        // Lerp = Linear Interpolation: smoothly moves from current position toward desired
        // smoothSpeed * Time.deltaTime controls how fast it catches up each frame
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Finds the local (owned) player and locks the camera onto it.
    ///
    /// In multiplayer there are multiple "Player" objects in the scene.
    /// We must follow only the one this machine controls — not another player's capsule.
    ///
    /// Priority:
    ///   1. NGO LocalClient.PlayerObject — the authoritative local player reference.
    ///   2. FindWithTag("Player") — fallback for offline / single-player use.
    /// </summary>
    private void TryFindLocalPlayer()
    {
        // NGO path: works for both host (clientId=0) and regular clients
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObj != null)
            {
                target  = playerObj.transform;
                _offset = transform.position - target.position;
                Debug.Log($"[CameraFollow] Locked onto local NGO player: {target.name}");
                return;
            }
        }

        // Fallback: no network session active — find by tag (single-player / editor preview)
        var fallback = GameObject.FindWithTag("Player");
        if (fallback != null)
        {
            target  = fallback.transform;
            _offset = transform.position - target.position;
            Debug.Log($"[CameraFollow] Locked onto tagged player (fallback): {target.name}");
        }
    }
}
