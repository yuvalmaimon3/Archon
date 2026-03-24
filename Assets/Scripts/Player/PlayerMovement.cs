using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Network-aware player movement.
/// Only the owning client reads input; movement is applied locally and
/// reconciled server-side via ServerRpc so all peers see the result.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed   = 8f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 15f;

    private Rigidbody _rb;
    private Vector2   _inputDir;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
    }

    public override void OnNetworkSpawn()
    {
        // Non-owners don't need a kinematic body — NetworkTransform drives their position.
        if (!IsOwner)
            _rb.isKinematic = true;
    }

    // Movement requires the game to be started AND this client to own the object.
    private bool CanMove => (GameManager.Instance != null && GameManager.Instance.IsGameStarted)
                            && (!IsSpawned || IsOwner);

    private void Update()
    {
        if (!CanMove) return;

        // Unity's Input.GetAxisRaw works for both WASD and joystick axes.
        _inputDir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // Normalize only when magnitude > 1 to preserve analog stick pressure.
        if (_inputDir.sqrMagnitude > 1f)
            _inputDir.Normalize();
    }

    private void FixedUpdate()
    {
        if (!CanMove) return;

        ApplyMovement(_inputDir);

        // Only send to server when in a live network session.
        if (IsSpawned)
            MoveServerRpc(_inputDir);
    }

    // The server applies the same movement logic so authoritative physics are correct.
    [ServerRpc]
    private void MoveServerRpc(Vector2 input)
    {
        if (IsOwner) return; // host already applied it locally
        ApplyMovement(input);
    }

    private void ApplyMovement(Vector2 input)
    {
        Vector3 target = new Vector3(input.x, 0f, input.y) * moveSpeed;
        float rate = target.sqrMagnitude > 0.001f ? acceleration : deceleration;

        Vector3 currentXZ = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 newXZ     = Vector3.MoveTowards(currentXZ, target, rate * Time.fixedDeltaTime);

        _rb.linearVelocity = new Vector3(newXZ.x, _rb.linearVelocity.y, newXZ.z);
    }
}
