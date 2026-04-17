using Unity.Netcode;
using UnityEngine;

// Movement for a ball-shaped enemy that rolls around in random directions.
// Uses Rigidbody physics (non-kinematic) rather than NavMesh, so the sphere
// naturally rolls across the terrain and collides with walls.
//
// Pattern of motion:
//   - Every `directionChangeInterval` seconds, picks a new random XZ direction.
//   - Applies a continuous force in that direction while below `maxSpeed`.
//   - Speed cap prevents the ball from accelerating forever; linear drag ensures
//     it decelerates naturally when force direction changes.
//
// Knockback is physics-compatible: since the Rigidbody is always non-kinematic,
// KnockbackHandler can add an impulse at any time. We just pause our own force
// application during the knockback window so they don't fight each other.
//
// NetworkBehaviour is inherited via EnemyMovementBase. All movement logic runs
// server-only; position is replicated to clients through NetworkTransform.
[RequireComponent(typeof(Rigidbody))]
public class RollingEnemyMovement : EnemyMovementBase
{
    [Header("Rolling Settings")]
    [Tooltip("Continuous force applied each FixedUpdate in the current roll direction.")]
    [SerializeField] private float rollForce = 15f;

    [Tooltip("Maximum horizontal speed. No force is added once the ball exceeds this.")]
    [SerializeField] private float maxSpeed = 5f;

    [Tooltip("How often (seconds) the ball picks a new random roll direction.")]
    [SerializeField] private float directionChangeInterval = 2f;

    [Tooltip("Linear drag applied to the Rigidbody — controls deceleration between direction changes.")]
    [SerializeField] private float rollingDrag = 1.5f;

    // ── Private state ─────────────────────────────────────────────────────────

    private Rigidbody _rb;

    // Current world-space XZ direction the ball is rolling toward.
    private Vector3 _rollDirection;

    // Time.time when the next direction change should occur.
    private float _nextDirectionChangeTime;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Rolling is always physics-driven — never kinematic.
        _rb.isKinematic = false;
        _rb.linearDamping = rollingDrag;

        // Allow the sphere to rotate freely so it visually rolls.
        // No rotation constraints needed — a sphere on flat ground won't tip.
        _rb.freezeRotation = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Clients observe position via NetworkTransform; only server steers.
        if (!IsServer)
        {
            enabled = false;
            return;
        }

        // Start rolling immediately in a random direction on spawn.
        PickNewDirection();
        Debug.Log($"[RollingEnemyMovement] '{name}' spawned — initial roll direction: {_rollDirection}");
    }

    // FixedUpdate keeps physics forces in sync with the physics step.
    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (EnemyData == null) return;
        if (IsBlocked) return;

        // Switch to a new random direction on the scheduled interval.
        if (Time.time >= _nextDirectionChangeTime)
            PickNewDirection();

        // Only push if we haven't reached the speed cap yet.
        // Checking horizontal speed prevents the ball from infinitely accelerating
        // on flat ground while still allowing gravity to act vertically.
        float horizontalSpeedSq = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).sqrMagnitude;
        if (horizontalSpeedSq < maxSpeed * maxSpeed)
            _rb.AddForce(_rollDirection * rollForce, ForceMode.Force);
    }

    // ── EnemyMovementBase overrides ───────────────────────────────────────────

    // Called by EnemyInitializer once EnemyData is ready.
    // We apply drag here in case it was serialized with a different value before awake.
    protected override void OnInitialized(EnemyData data)
    {
        _rb.linearDamping = rollingDrag;
        Debug.Log($"[RollingEnemyMovement] '{name}' initialized — maxSpeed:{maxSpeed} drag:{rollingDrag}");
    }

    // Maps the level-scaled move speed to the horizontal speed cap.
    // EnemyInitializer calls this after ComputeStats() so higher-level rollers
    // can be faster without touching the rollForce.
    public override void SetMoveSpeed(float speed)
    {
        base.SetMoveSpeed(speed);
        maxSpeed = Mathf.Max(0f, speed);
        Debug.Log($"[RollingEnemyMovement] '{name}' maxSpeed updated to {maxSpeed:F2}.");
    }

    // Knockback start: stop applying our own force so the knockback impulse
    // is not resisted. The Rigidbody stays non-kinematic — KnockbackHandler
    // adds its impulse directly and that's fine.
    protected override void OnKnockbackStart()
    {
        // Nothing to disable — IsKnockedBack check in FixedUpdate handles the pause.
        Debug.Log($"[RollingEnemyMovement] '{name}' rolling paused (knockback).");
    }

    // Knockback end: pick a new direction and resume rolling.
    // We avoid resuming the old direction so the ball doesn't charge straight
    // back at whatever sent it flying.
    protected override void OnKnockbackEnd()
    {
        PickNewDirection();
        Debug.Log($"[RollingEnemyMovement] '{name}' rolling resumed after knockback, new dir: {_rollDirection}");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    // Picks a random direction in the XZ plane and schedules the next change.
    private void PickNewDirection()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        _rollDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        _nextDirectionChangeTime = Time.time + directionChangeInterval;
    }
}
