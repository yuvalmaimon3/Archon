using UnityEngine;

// Flying enemy movement — moves in a straight line toward the player,
// ignoring walls and obstacles entirely.
//
// Maintains a configurable hover height above the player's Y position
// so the enemy appears to float rather than hug the ground.
//
// Knockback: applies force in 3D (including Y) since flying enemies
// are not constrained to a NavMesh surface.
[RequireComponent(typeof(Rigidbody))]
public class FlyingEnemyMovement : EnemyMovementBase
{
    // Height above the player's Y position the enemy tries to maintain while chasing.
    [SerializeField] private float hoverHeight = 2f;

    // How smoothly the enemy adjusts its Y position toward the hover height.
    // Higher = snappier vertical adjustment.
    [SerializeField] private float verticalLerpSpeed = 3f;

    private Rigidbody _rb;
    private Transform _target;

    // Level-scaled speed set by EnemyInitializer.SetMoveSpeed().
    // Defaults to -1 (unset) so Update falls back to EnemyData.MoveSpeed until scaling is applied.
    private float _scaledMoveSpeed = -1f;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Flying enemies use direct transform movement — disable gravity.
        _rb.useGravity     = false;
        _rb.isKinematic    = true;
        _rb.freezeRotation = true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        Debug.Log($"[FlyingEnemyMovement] '{name}' spawned on server.");
    }

    private void Update()
    {
        if (!IsServer) return;
        if (EnemyData == null) return;
        if (IsBlocked) return;

        _target = FindNearestPlayer();

        if (_target == null)
            return;

        float distance = Vector3.Distance(transform.position, _target.position);

        if (distance <= EnemyData.AttackRange)
        {
            // In attack range — stop and face target.
            FaceTarget(_target.position);
            return;
        }

        MoveTowardTarget(_target.position);
    }

    // ── EnemyMovementBase overrides ──────────────────────────────────────────

    protected override void OnInitialized(EnemyData data)
    {
        // No agent to configure — speed is applied via SetMoveSpeed() after this call.
    }

    // Stores the level-scaled move speed from EnemyInitializer.
    // Without this override, the base no-op silently discards the scaled value
    // and Update would always use the unscaled EnemyData.MoveSpeed.
    public override void SetMoveSpeed(float speed)
    {
        base.SetMoveSpeed(speed);
        _scaledMoveSpeed = Mathf.Max(0f, speed);
    }

    // On knockback start: make Rigidbody physical so forces can be applied.
    // Flying knockback includes Y so the enemy can be pushed upward/downward.
    protected override void OnKnockbackStart()
    {
        _rb.isKinematic = false;
        _rb.useGravity  = false; // Still no gravity — just physics forces
    }

    // On knockback end: return to kinematic direct-movement mode.
    protected override void OnKnockbackEnd()
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic    = true;

        Debug.Log($"[FlyingEnemyMovement] '{name}' recovered from knockback.");
    }

    // ── Private ──────────────────────────────────────────────────────────────

    // Moves directly toward the target position, hovering at the configured height.
    private void MoveTowardTarget(Vector3 targetPosition)
    {
        // Target Y = player Y + hover height
        float targetY    = targetPosition.y + hoverHeight;
        Vector3 destination = new Vector3(targetPosition.x, targetY, targetPosition.z);

        // Use level-scaled speed if set, otherwise fall back to base EnemyData speed.
        float speed = _scaledMoveSpeed >= 0f ? _scaledMoveSpeed : EnemyData.MoveSpeed;

        // Move via transform — kinematic Rigidbody, no physics solver needed
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            speed * Time.deltaTime
        );

        // Face the player (ignore hover offset for rotation — looks more natural)
        FaceTarget(targetPosition);
    }
}
