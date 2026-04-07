using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Wraith-specific ghost movement: flies directly toward the player in a straight line,
/// ignoring all terrain and obstacles (ghost / phase-through behavior).
///
/// The Wraith hovers at a fixed height above the player's Y while approaching.
/// It stops when within AttackRange so the trigger collider overlaps the player
/// and ContactDamageDealer can tick.
///
/// Ghost setup requires:
///   - NavMeshAgent disabled on the prefab (no NavMesh constraint)
///   - CapsuleCollider set to isTrigger (no physics blocking)
///   - Kinematic Rigidbody (no gravity, direct transform movement)
///
/// Server-only locomotion; clients receive position via NetworkTransform.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WraithMovement : EnemyMovementBase
{
    [Header("Movement Settings")]
    [Tooltip("Height above the player's Y position the Wraith tries to maintain while chasing.")]
    [SerializeField] private float hoverHeight = 2f;

    private Rigidbody _rb;

    // Level-scaled speed set by EnemyInitializer.SetMoveSpeed().
    // Defaults to -1 (unset) so Update falls back to EnemyData.MoveSpeed until scaling is applied.
    private float _scaledMoveSpeed = -1f;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Ghost movement: no gravity, no physics forces — direct transform control.
        _rb.useGravity     = false;
        _rb.isKinematic    = true;
        _rb.freezeRotation = true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        Debug.Log($"[WraithMovement] '{name}' spawned on server.");
    }

    private void Update()
    {
        if (!IsServer) return;
        if (EnemyData == null) return;
        if (IsKnockedBack) return;

        Transform target = FindNearestPlayer();
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);

        // Stop and face the player once within attack range —
        // the trigger collider will overlap and ContactDamageDealer handles the rest.
        if (distance <= EnemyData.AttackRange)
        {
            FaceTarget(target.position);
            return;
        }

        MoveTowardTarget(target.position);
    }

    // ── EnemyMovementBase overrides ──────────────────────────────────────────

    protected override void OnInitialized(EnemyData data)
    {
        // Speed is applied via SetMoveSpeed() after this call.
    }

    // Stores the level-scaled move speed from EnemyInitializer.
    public override void SetMoveSpeed(float speed)
    {
        _scaledMoveSpeed = Mathf.Max(0f, speed);
    }

    // Stop movement immediately on death — disables this component so Update stops running.
    // Without this override the Wraith keeps chasing for the DeathController destroy delay.
    protected override void OnDeathCleanup()
    {
        enabled = false;
        _rb.linearVelocity = Vector3.zero;
        Debug.Log($"[WraithMovement] '{name}' movement stopped on death.");
    }

    // Knockback: switch to physical mode so forces can be applied.
    protected override void OnKnockbackStart()
    {
        _rb.isKinematic = false;
        _rb.useGravity  = false; // Keep gravity off — Wraith floats even when hit.
    }

    // Knockback recovery: return to kinematic direct-movement mode.
    protected override void OnKnockbackEnd()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic    = true;
        Debug.Log($"[WraithMovement] '{name}' recovered from knockback.");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    // Moves directly toward the target position, hovering at the configured height above them.
    // Uses direct transform.position (kinematic) so walls and NavMesh are completely ignored.
    private void MoveTowardTarget(Vector3 targetPosition)
    {
        // Hover target: match the player's X/Z but stay above them at hoverHeight.
        Vector3 destination = new Vector3(targetPosition.x, targetPosition.y + hoverHeight, targetPosition.z);

        float speed = _scaledMoveSpeed >= 0f ? _scaledMoveSpeed : EnemyData.MoveSpeed;

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            speed * Time.deltaTime
        );

        FaceTarget(targetPosition);
    }
}
