using UnityEngine;
using UnityEngine.AI;

// NavMesh-based movement that wanders to random positions continuously.
// The enemy never chases the player directly — it moves erratically across
// the field while EnemyCombatBrain handles shooting independently when
// the player enters attack range.
//
// Networking: NetworkBehaviour via EnemyMovementBase.
// Server drives the agent; clients receive position via NetworkTransform.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class RandomEnemyMovement : EnemyMovementBase
{
    [Header("Wander")]
    [Tooltip("How far from the current position to pick the next random waypoint.")]
    [SerializeField] private float wanderRadius = 12f;

    [Tooltip("Distance at which the current waypoint is considered reached.")]
    [Min(0.1f)]
    [SerializeField] private float waypointThreshold = 0.6f;

    [Tooltip("Max search radius passed to NavMesh.SamplePosition when finding a waypoint.")]
    [Min(0.5f)]
    [SerializeField] private float navMeshSearchRadius = 3f;

    private NavMeshAgent _agent;
    private Rigidbody    _rb;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb    = GetComponent<Rigidbody>();

        _rb.isKinematic    = true;
        _rb.freezeRotation = true;

        _agent.updateRotation = false;
        _agent.updateUpAxis   = false;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            _agent.enabled = false;
            return;
        }

        _agent.Warp(transform.position);
        PickNewWaypoint(); // Start moving immediately

        Debug.Log($"[RandomEnemyMovement] '{name}' spawned — beginning random wander.");
    }

    private void Update()
    {
        if (!IsServer) return;
        if (IsKnockedBack) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        // Face the direction of travel
        if (_agent.velocity.sqrMagnitude > 0.01f)
            FaceTarget(transform.position + _agent.velocity);

        // Pick a new waypoint when the current one is reached
        if (!_agent.pathPending && _agent.remainingDistance <= waypointThreshold)
            PickNewWaypoint();
    }

    // ── EnemyMovementBase overrides ──────────────────────────────────────────

    protected override void OnInitialized(EnemyData data)
    {
        _agent.speed            = data.MoveSpeed;
        _agent.stoppingDistance = 0f; // Never stops — always wanders
    }

    public override void SetMoveSpeed(float speed)
    {
        _agent.speed = Mathf.Max(0f, speed);
    }

    protected override void OnDeathCleanup()
    {
        if (_agent.isOnNavMesh) _agent.ResetPath();
        _agent.enabled = false;
    }

    protected override void OnKnockbackStart()
    {
        if (_agent.isOnNavMesh) _agent.ResetPath();
        _agent.enabled  = false;
        _rb.isKinematic = false;
    }

    protected override void OnKnockbackEnd()
    {
        _rb.isKinematic    = true;
        _rb.linearVelocity = Vector3.zero;
        _agent.enabled     = true;
        _agent.Warp(transform.position);
        PickNewWaypoint();

        Debug.Log($"[RandomEnemyMovement] '{name}' agent re-enabled after knockback.");
    }

    // ── Wander logic ─────────────────────────────────────────────────────────

    // Samples a random position within wanderRadius and sets it as the destination.
    // Retries several times; if all fail, stays in place for this tick.
    private void PickNewWaypoint()
    {
        const int maxAttempts = 10;

        for (int i = 0; i < maxAttempts; i++)
        {
            Vector2 circle    = Random.insideUnitCircle * wanderRadius;
            Vector3 candidate = transform.position + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
                return;
            }
        }

        Debug.LogWarning($"[RandomEnemyMovement] '{name}' — could not find valid waypoint near {transform.position}.");
    }
}
