using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Server-authoritative enemy movement using NavMeshAgent.
// Chases the nearest living player and stops when within attack range.
//
// Works with KnockbackHandler: when the enemy is knocked back,
// this script pauses and lets KnockbackHandler take over Rigidbody physics.
// Movement resumes automatically once the knockback ends.
//
// Requires: NavMeshAgent, Rigidbody (kinematic during normal movement),
//           EnemyData assigned via EnemyInitializer.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : NetworkBehaviour
{
    [Header("Config")]
    // Enemy stats asset — assigned at runtime by EnemyInitializer.
    [SerializeField] private EnemyData enemyData;

    // Unity tag used to locate players.
    [SerializeField] private string playerTag = "Player";

    // How often (seconds) to refresh the nav destination.
    // Updating every frame is wasteful — 0.1s is smooth enough on mobile.
    [SerializeField] private float destinationUpdateInterval = 0.1f;

    private NavMeshAgent   _agent;
    private Rigidbody      _rb;
    private KnockbackHandler _knockback;

    private Transform _target;
    private float     _nextDestinationUpdate;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _agent    = GetComponent<NavMeshAgent>();
        _rb       = GetComponent<Rigidbody>();
        _knockback = GetComponent<KnockbackHandler>();

        // Rigidbody is kinematic during normal movement — NavMeshAgent owns the transform.
        // KnockbackHandler will temporarily disable the agent and make it non-kinematic.
        _rb.isKinematic    = true;
        _rb.freezeRotation = true;

        // Agent rotation is handled manually so we can match NavMesh movement direction.
        _agent.updateRotation = false;
        _agent.updateUpAxis   = false;
    }

    public override void OnNetworkSpawn()
    {
        // Only the server drives enemy movement.
        // Clients receive position updates via NetworkTransform.
        if (!IsServer)
        {
            _agent.enabled = false;
            return;
        }

        Debug.Log($"[EnemyMovement] '{name}' spawned on server.");
    }

    private void Update()
    {
        if (!IsServer) return;
        if (enemyData == null) return;

        // Pause movement while knocked back — KnockbackHandler is in control.
        if (_knockback != null && _knockback.IsKnockedBack) return;

        _target = FindNearestPlayer();

        if (_target == null)
        {
            _agent.ResetPath();
            return;
        }

        float distance = Vector3.Distance(transform.position, _target.position);

        if (distance <= enemyData.AttackRange)
        {
            // In attack range — stop moving, face the target.
            _agent.ResetPath();
            FaceTarget(_target.position);
        }
        else
        {
            // Update destination on interval to save CPU.
            if (Time.time >= _nextDestinationUpdate)
            {
                _agent.SetDestination(_target.position);
                _nextDestinationUpdate = Time.time + destinationUpdateInterval;
            }

            // Face movement direction while chasing.
            if (_agent.velocity.sqrMagnitude > 0.01f)
                FaceTarget(transform.position + _agent.velocity);
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    // Called by EnemyInitializer after spawn to apply EnemyData stats to the agent.
    public void Initialize(EnemyData data)
    {
        enemyData = data;

        _agent.speed           = data.MoveSpeed;
        _agent.stoppingDistance = data.AttackRange;

        Debug.Log($"[EnemyMovement] '{name}' initialized — speed:{data.MoveSpeed} stoppingDist:{data.AttackRange}");
    }

    // Disables the NavMeshAgent so KnockbackHandler can apply Rigidbody forces.
    // Called by KnockbackHandler when knockback starts.
    public void DisableAgentForKnockback()
    {
        _agent.ResetPath();
        _agent.enabled  = false;
        _rb.isKinematic = false;
    }

    // Re-enables the NavMeshAgent after knockback ends.
    // Called by KnockbackHandler once the enemy settles.
    public void ReEnableAgent()
    {
        _rb.isKinematic = true;
        _rb.linearVelocity = Vector3.zero;

        // Warp agent to current position so it re-snaps to the NavMesh correctly.
        _agent.enabled = true;
        _agent.Warp(transform.position);

        Debug.Log($"[EnemyMovement] '{name}' agent re-enabled after knockback.");
    }

    // ── Private ──────────────────────────────────────────────────────────────

    // Finds the nearest living player. Dead players are excluded because
    // DeathController changes their tag away from playerTag.
    private Transform FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
        if (players.Length == 0) return null;

        Transform nearest       = null;
        float     nearestSqDist = float.MaxValue;

        foreach (var p in players)
        {
            float sqDist = (p.transform.position - transform.position).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest       = p.transform;
            }
        }

        return nearest;
    }

    // Rotates the enemy to face a world-space position, ignoring Y axis.
    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.LookRotation(direction);
    }
}
