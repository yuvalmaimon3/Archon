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
        if (IsKnockedBack) return;

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
        // No agent to configure — speed is read directly from EnemyData in Update.
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

        // Direction to destination (full 3D — flying enemies move up/down freely)
        Vector3 direction = (destination - transform.position).normalized;

        // Move via transform — kinematic Rigidbody, no physics solver needed
        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            EnemyData.MoveSpeed * Time.deltaTime
        );

        // Face the player (ignore hover offset for rotation — looks more natural)
        FaceTarget(targetPosition);
    }
}
