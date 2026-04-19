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

    private Rigidbody _rb;
    private Vector2   _inputDir;

    private float _originalSpeed;   // inspector value, never modified — used for ResetSpeed
    private float _baseSpeed;       // accumulated flat bonuses on top of _originalSpeed
    private float _speedMultiplier = 1f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _originalSpeed = moveSpeed;
        _baseSpeed = moveSpeed;
    }

    /// <summary>
    /// Called by NGO (Netcode for GameObjects) when this object is spawned in the network session.
    /// This is different from Awake/Start — it only runs once a network session is active.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        // The SERVER (host) must run physics for ALL players — both its own and clients'.
        //   If the client's Rigidbody were kinematic on the server, MoveServerRpc → ApplyMovement
        //   would try to set velocity on a kinematic body, which has NO effect.
        //   The server would then broadcast a stationary position via NetworkTransform.
        //
        // Non-server machines (clients) are kinematic for all objects because the server
        // is authoritative — positions arrive via NetworkTransform, not local physics.
        if (!IsServer)
            _rb.isKinematic = true;

        Debug.Log($"[PlayerMovement] Spawned — IsOwner:{IsOwner} IsServer:{IsServer} IsHost:{IsHost} Kinematic:{_rb.isKinematic}");
    }

    /// <summary>
    /// Returns true only when this client is allowed to move the player.
    /// We only check ownership — if this player is spawned it means the game is already running.
    /// IsOwner is true only on the machine that controls this specific player.
    /// Note: IsGameStarted is NOT checked here because it is only set on the host, not synced
    /// to clients, so checking it would permanently block client movement.
    /// </summary>
    private bool CanMove => IsOwner;

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

        // Only the server applies physics directly — client Rigidbodies are kinematic.
        // Calling ApplyMovement on a kinematic body (client-side) would log a warning and do nothing.
        if (IsServer)
            ApplyMovement(_inputDir);

        // Send input to the server so it can move this player for everyone.
        // The host already moved via ApplyMovement above — skip the redundant RPC.
        if (IsSpawned && !IsServer)
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

    // Flat bonus — adds a fixed amount to current speed.
    public void AddSpeedBonus(float bonus)
    {
        _baseSpeed = Mathf.Max(0f, _baseSpeed + bonus);
        moveSpeed  = _baseSpeed * _speedMultiplier;
        Debug.Log($"[PlayerMovement] '{name}' speed +{bonus} flat → {moveSpeed:F2}");
    }

    // Percent bonus — each call multiplies current speed by (1 + fraction), stacks multiplicatively.
    public void AddSpeedMultiplier(float fraction)
    {
        _speedMultiplier *= (1f + fraction);
        moveSpeed = _baseSpeed * _speedMultiplier;
        Debug.Log($"[PlayerMovement] '{name}' speed ×{_speedMultiplier:F3} → {moveSpeed:F2}");
    }

    // Reverses a previous AddSpeedMultiplier call. Used by PlayerEquipment on item unequip.
    public void RemoveSpeedMultiplier(float fraction)
    {
        float divisor = 1f + fraction;
        _speedMultiplier = divisor > 0f ? _speedMultiplier / divisor : 1f;
        moveSpeed = _baseSpeed * _speedMultiplier;
        Debug.Log($"[PlayerMovement] '{name}' speed multiplier removed — ×{_speedMultiplier:F3} → {moveSpeed:F2}");
    }

    // Resets speed to the inspector base value, clearing all flat and percent bonuses.
    // Used for full stat recomputation (e.g. re-applying all upgrades from scratch).
    public void ResetSpeed()
    {
        _baseSpeed       = _originalSpeed;
        _speedMultiplier = 1f;
        moveSpeed        = _originalSpeed;
        Debug.Log($"[PlayerMovement] '{name}' speed reset to base {moveSpeed:F2}");
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
