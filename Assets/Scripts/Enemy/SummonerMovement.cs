using UnityEngine;
using UnityEngine.AI;

// NavMesh-based movement for the Summoner enemy.
// Brain-controlled: exposes MoveTo(), Stop(), and FaceToward() so SummonerBrain
// can direct positioning based on its state machine (reposition / attack / retreat).
//
// Knockback handling follows the standard pattern: disable the agent, hand off to
// Rigidbody during knockback, re-enable and warp back to NavMesh afterward.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class SummonerMovement : EnemyMovementBase
{
    private NavMeshAgent _agent;
    private Rigidbody    _rb;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _rb    = GetComponent<Rigidbody>();

        // Rigidbody is kinematic during normal movement — NavMeshAgent owns the transform.
        _rb.isKinematic    = true;
        _rb.freezeRotation = true;

        // Rotation is handled explicitly by SummonerBrain via FaceToward().
        _agent.updateRotation = false;
        _agent.updateUpAxis   = false;
    }

    public override void OnNetworkSpawn()
    {
        // Only the server drives enemy movement — clients receive position via NetworkTransform.
        if (!IsServer)
        {
            _agent.enabled = false;
            return;
        }

        Debug.Log($"[SummonerMovement] '{name}' spawned on server.");
    }

    // ── EnemyMovementBase overrides ──────────────────────────────────────────

    // Applies data from EnemyData at initialization time.
    // Base speed is set here; EnemyInitializer overrides it with the level-scaled value immediately after.
    protected override void OnInitialized(EnemyData data)
    {
        _agent.speed = data.MoveSpeed;
    }

    // Sets a level-scaled move speed on the NavMeshAgent.
    // Called by EnemyInitializer after computing scaled stats.
    public override void SetMoveSpeed(float speed)
    {
        _agent.speed = Mathf.Max(0f, speed);
    }

    // Disables NavMeshAgent and hands control to Rigidbody during knockback.
    protected override void OnKnockbackStart()
    {
        _agent.ResetPath();
        _agent.enabled  = false;
        _rb.isKinematic = false;
    }

    // Re-enables NavMeshAgent and snaps back to NavMesh after knockback ends.
    protected override void OnKnockbackEnd()
    {
        _rb.isKinematic    = true;
        _rb.linearVelocity = Vector3.zero;

        _agent.enabled = true;
        // Warp re-snaps the agent to the nearest valid NavMesh position after physics moved it.
        _agent.Warp(transform.position);

        Debug.Log($"[SummonerMovement] '{name}' agent re-enabled after knockback.");
    }

    // ── Brain-controlled movement API ────────────────────────────────────────

    // Orders the agent to path toward a world-space destination.
    // Called each Update by SummonerBrain while repositioning or retreating.
    public void MoveTo(Vector3 destination)
    {
        if (!_agent.enabled) return;
        _agent.SetDestination(destination);
    }

    // Stops all NavMesh pathfinding — used when the summoner is in Attack state.
    public void Stop()
    {
        if (!_agent.enabled) return;
        _agent.ResetPath();
    }

    // Rotates the summoner to face a world-space position, ignoring Y.
    // Exposes the protected FaceTarget() from EnemyMovementBase.
    public void FaceToward(Vector3 position)
    {
        FaceTarget(position);
    }
}
