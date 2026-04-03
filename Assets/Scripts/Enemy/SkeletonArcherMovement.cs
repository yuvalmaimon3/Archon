using UnityEngine;
using UnityEngine.AI;

// Ground movement for the Skeleton Archer.
// Unlike GroundEnemyMovement (which auto-chases the player every frame),
// this component is brain-controlled: it exposes MoveTo() and Stop() so
// SkeletonArcherBrain can direct movement based on its positioning logic
// (reposition, attack in place, or retreat).
//
// Knockback handling is identical to GroundEnemyMovement.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class SkeletonArcherMovement : EnemyMovementBase
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

        // Rotation is handled by the brain via FaceToward().
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

        Debug.Log($"[SkeletonArcherMovement] '{name}' spawned on server.");
    }

    // ── EnemyMovementBase overrides ──────────────────────────────────────────

    // Applies stats from EnemyData at spawn time.
    // Move speed is set here and can be overridden afterward by EnemyInitializer
    // via SetMoveSpeed() once level scaling is applied.
    protected override void OnInitialized(EnemyData data)
    {
        _agent.speed = data.MoveSpeed;
        // No stopping distance — the brain decides when to stop.
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
        // Warp re-snaps the agent to the nearest valid NavMesh position.
        _agent.Warp(transform.position);

        Debug.Log($"[SkeletonArcherMovement] '{name}' agent re-enabled after knockback.");
    }

    // ── Brain-controlled movement API ────────────────────────────────────────

    // Orders the agent to path toward a world-space destination.
    // Called each Update by SkeletonArcherBrain while repositioning or retreating.
    public void MoveTo(Vector3 destination)
    {
        if (!_agent.enabled) return;
        _agent.SetDestination(destination);
    }

    // Stops all NavMesh pathfinding — used when the archer is in Attack state.
    public void Stop()
    {
        if (!_agent.enabled) return;
        _agent.ResetPath();
    }

    // Rotates the archer to face a world-space position, ignoring Y.
    // Exposes the protected FaceTarget() from the base class so SkeletonArcherBrain
    // can call it without needing direct access to NavMesh internals.
    public void FaceToward(Vector3 position)
    {
        FaceTarget(position);
    }
}
