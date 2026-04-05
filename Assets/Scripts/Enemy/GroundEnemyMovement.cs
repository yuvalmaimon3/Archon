using UnityEngine;
using UnityEngine.AI;

// Ground enemy movement using Unity NavMeshAgent.
// Pathfinds around walls and obstacles to reach the player.
// Stops when within attack range so EnemyCombatBrain can take over.
//
// Knockback: disables the agent and makes Rigidbody physical for the duration,
// then warps the agent back onto the NavMesh on recovery.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class GroundEnemyMovement : EnemyMovementBase
{
    // How often (seconds) to refresh the nav destination.
    // Updating every frame is wasteful — 0.1s is smooth enough on mobile.
    [SerializeField] private float destinationUpdateInterval = 0.1f;

    private NavMeshAgent _agent;
    private Rigidbody    _rb;

    private Transform _target;
    private float     _nextDestinationUpdate;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb    = GetComponent<Rigidbody>();

        // Rigidbody is kinematic during normal movement — NavMeshAgent owns the transform.
        _rb.isKinematic    = true;
        _rb.freezeRotation = true;

        // Rotation is handled manually to match movement direction.
        _agent.updateRotation = false;
        _agent.updateUpAxis   = false;
    }

    public override void OnNetworkSpawn()
    {
        // Only server drives enemy movement — clients get position via NetworkTransform.
        if (!IsServer)
        {
            _agent.enabled = false;
            return;
        }

        // Snap to NavMesh on spawn — prevents "SetDestination on inactive agent" errors
        // when the enemy is placed slightly off the baked mesh surface.
        _agent.Warp(transform.position);

        Debug.Log($"[GroundEnemyMovement] '{name}' spawned on server.");
    }

    private void Update()
    {
        if (!IsServer) return;
        if (EnemyData == null) return;
        if (IsKnockedBack) return;

        _target = FindNearestPlayer();

        if (_target == null)
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            return;
        }

        float distance = Vector3.Distance(transform.position, _target.position);

        if (distance <= EnemyData.AttackRange)
        {
            // In attack range — stop and face the target.
            if (_agent.isOnNavMesh) _agent.ResetPath();
            FaceTarget(_target.position);
        }
        else
        {
            // Throttle SetDestination calls to save CPU on mobile.
            if (Time.time >= _nextDestinationUpdate)
            {
                if (_agent.isOnNavMesh)
                    _agent.SetDestination(_target.position);
                _nextDestinationUpdate = Time.time + destinationUpdateInterval;
            }

            if (_agent.velocity.sqrMagnitude > 0.01f)
                FaceTarget(transform.position + _agent.velocity);
        }
    }

    // ── EnemyMovementBase overrides ──────────────────────────────────────────

    protected override void OnInitialized(EnemyData data)
    {
        _agent.speed            = data.MoveSpeed;
        _agent.stoppingDistance = data.AttackRange;
    }

    // Applies a level-scaled speed override to the NavMeshAgent.
    // Called by EnemyInitializer after ComputeStats() to reflect level growth.
    public override void SetMoveSpeed(float speed)
    {
        _agent.speed = Mathf.Max(0f, speed);
    }

    // Stops the NavMeshAgent immediately on death.
    // The agent keeps pathfinding even when the MonoBehaviour is disabled —
    // explicit reset + disable is required to prevent the enemy from
    // sliding toward the player after health hits zero.
    protected override void OnDeathCleanup()
    {
        if (_agent.isOnNavMesh) _agent.ResetPath();
        _agent.enabled = false;
    }

    // Disable agent and hand control to Rigidbody for physics-driven knockback.
    protected override void OnKnockbackStart()
    {
        if (_agent.isOnNavMesh) _agent.ResetPath();
        _agent.enabled  = false;
        _rb.isKinematic = false;
    }

    // Re-enable agent and snap back onto the NavMesh after knockback.
    protected override void OnKnockbackEnd()
    {
        _rb.isKinematic    = true;
        _rb.linearVelocity = Vector3.zero;

        _agent.enabled = true;
        // Warp re-snaps the agent to the nearest valid NavMesh position.
        _agent.Warp(transform.position);

        Debug.Log($"[GroundEnemyMovement] '{name}' agent re-enabled after knockback.");
    }
}
