using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles player movement using WASD keys or a joystick.
///
/// NetworkBehaviour means this script is multiplayer-aware:
/// - Each player in the game has their own copy of this script
/// - Only the player who OWNS this object (i.e. the local player) can move it
/// - Movement is sent to the server so all other players see it too
/// </summary>
[RequireComponent(typeof(Rigidbody))] // automatically adds a Rigidbody if missing
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed    = 8f;  // max movement speed
    [SerializeField] private float acceleration = 20f; // how fast we reach full speed
    [SerializeField] private float deceleration = 15f; // how fast we slow down when no input

    private Rigidbody _rb;      // reference to the physics body
    private Vector2   _inputDir; // stores the current input direction this frame

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Prevent the sphere from tipping or rolling on its own axis
        _rb.freezeRotation = true;
    }

    /// <summary>
    /// Called by NGO (Netcode for GameObjects) when this object is spawned in the network session.
    /// This is different from Awake/Start — it only runs once a network session is active.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // If this is NOT our player (e.g. another player's character), make it kinematic.
        // Kinematic = not driven by physics locally — the server will tell us where it is.
        if (!IsOwner)
            _rb.isKinematic = true;
    }

    /// <summary>
    /// Returns true only when this client is allowed to move the player.
    /// Two conditions must both be true:
    ///   1. The game has been started (Start button was pressed)
    ///   2. This is our own player (not someone else's character)
    /// </summary>
    private bool CanMove => (GameManager.Instance != null && GameManager.Instance.IsGameStarted)
                            && (!IsSpawned || IsOwner);
    // IsSpawned = false means we're not in a network session yet (solo/testing mode)
    // In that case we skip the ownership check so local testing still works

    private void Update()
    {
        if (!CanMove) return;

        // GetAxisRaw reads keyboard (WASD / arrow keys) and joystick left stick simultaneously.
        // Returns -1, 0, or 1 for keyboard; -1.0 to 1.0 for analog joystick.
        _inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // If moving diagonally the raw vector length is ~1.41 — normalize it to keep speed consistent
        if (_inputDir.sqrMagnitude > 1f)
            _inputDir.Normalize();
    }

    private void FixedUpdate()
    {
        // FixedUpdate runs at a fixed timestep (default 50Hz) — always use this for physics
        if (!CanMove) return;

        ApplyMovement(_inputDir);

        // When in a multiplayer session, tell the server what input we pressed
        // so it can validate and sync the position to all other clients
        if (IsSpawned)
            MoveServerRpc(_inputDir);
    }

    /// <summary>
    /// ServerRpc = a method that runs on the SERVER, called from the CLIENT.
    /// The owning client sends their input here; the server re-applies the movement
    /// so the authoritative physics position stays correct for everyone.
    /// </summary>
    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        // The host is both server and client, so it already applied movement in FixedUpdate
        if (IsOwner) return;
        ApplyMovement(input);
    }

    /// <summary>
    /// Core movement logic. Smoothly accelerates toward the target velocity
    /// or decelerates to a stop when there is no input.
    /// </summary>
    private void ApplyMovement(Vector2 input)
    {
        // Convert 2D input (X, Y) to 3D world direction (X, 0, Z) — we never move on Y axis
        Vector3 target = new Vector3(input.x, 0f, input.y) * moveSpeed;

        // Use faster acceleration when pressing a key, slower deceleration when releasing
        float rate = target.sqrMagnitude > 0.001f ? acceleration : deceleration;

        // Only modify horizontal velocity — preserve vertical (gravity / jumping) as-is
        Vector3 currentXZ = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 newXZ     = Vector3.MoveTowards(currentXZ, target, rate * Time.fixedDeltaTime);

        _rb.linearVelocity = new Vector3(newXZ.x, _rb.linearVelocity.y, newXZ.z);
    }
}
