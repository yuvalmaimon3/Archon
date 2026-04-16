using UnityEngine;
using UnityEngine.AI;

// NavMesh-based movement that alternates between wandering and standing still.
// Cycle: move for moveDuration → stand for standDuration → repeat.
//
// Standing = no NavMesh destination. The agent stays enabled so physics
// forces (knockback, pushback) apply normally in both states.
// EnemyCombatBrain fires independently regardless of movement state.
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

    [Header("Move / Stand Cycle")]
    [Tooltip("Seconds spent moving toward a random waypoint before stopping.")]
    [Min(0.1f)]
    [SerializeField] private float moveDuration = 2f;

    [Tooltip("Seconds spent standing still before moving again.")]
    [Min(0f)]
    [SerializeField] private float standDuration = 2f;

    private NavMeshAgent _agent;
    private Rigidbody    _rb;

    private enum WanderState { Moving, Standing }
    private WanderState _state;
    private float       _stateTimer; // counts down; state switches when it hits 0

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
        EnterMoving();

        Debug.Log($"[RandomEnemyMovement] '{name}' spawned — cycle: move {moveDuration}s / stand {standDuration}s.");
    }

    private void Update()
    {
        if (!IsServer) return;
        if (IsKnockedBack) return;
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        _stateTimer -= Time.deltaTime;

        switch (_state)
        {
            case WanderState.Moving:
                if (_agent.velocity.sqrMagnitude > 0.01f)
                    FaceTarget(transform.position + _agent.velocity);

                // Pick a new waypoint immediately if the current one is reached mid-phase
                if (!_agent.pathPending && _agent.remainingDistance <= waypointThreshold)
                    PickNewWaypoint();

                if (_stateTimer <= 0f)
                    EnterStanding();
                break;

            case WanderState.Standing:
                if (_stateTimer <= 0f)
                    EnterMoving();
                break;
        }
    }

    // ── EnemyMovementBase overrides ──────────────────────────────────────────

    protected override void OnInitialized(EnemyData data)
    {
        _agent.speed            = data.MoveSpeed;
        _agent.stoppingDistance = 0f;
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

    // KnockbackHandler calls these — disabling/re-enabling the agent handles both
    // Moving and Standing states correctly; the cycle resumes after knockback ends.
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

        // Resume whichever state was active — timer continues from where it left off
        if (_state == WanderState.Moving)
            PickNewWaypoint();

        Debug.Log($"[RandomEnemyMovement] '{name}' agent re-enabled after knockback.");
    }

    // ── State transitions ────────────────────────────────────────────────────

    private void EnterMoving()
    {
        _state      = WanderState.Moving;
        _stateTimer = moveDuration;
        PickNewWaypoint();
    }

    private void EnterStanding()
    {
        _state      = WanderState.Standing;
        _stateTimer = standDuration;
        if (_agent.isOnNavMesh) _agent.ResetPath(); // stop moving; agent stays enabled
    }

    // ── Wander ───────────────────────────────────────────────────────────────

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

        Debug.LogWarning($"[RandomEnemyMovement] '{name}' — no valid waypoint found near {transform.position}.");
    }
}
