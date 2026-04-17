using Unity.Netcode;
using UnityEngine;

// Abstract base for all enemy movement types.
// Subclasses implement the actual locomotion (NavMesh, flying, etc.).
//
// KnockbackHandler and EnemyInitializer talk only to this base class —
// they don't care whether the enemy walks or flies.
//
// Implements IDeathHandler so movement stops the instant the entity dies —
// called by DeathController before scripts are disabled, preventing the NavMeshAgent
// (or any locomotion system) from continuing to run after health hits zero.
//
// To add a new movement type: extend this class and implement the abstract methods.
public abstract class EnemyMovementBase : NetworkBehaviour, IDeathHandler
{
    [Header("Movement Config")]
    // Unity tag used to locate players.
    [SerializeField] protected string playerTag = "Player";

    // Cached enemy stats — set by Initialize() from EnemyInitializer.
    protected EnemyData EnemyData { get; private set; }

    // Current effective move speed (set by EnemyInitializer via SetMoveSpeed).
    // Read by ElementStatusEffects to apply and restore Ice slow.
    public float ScaledSpeed { get; private set; }

    // True while a knockback is active — subclasses must pause locomotion when set.
    public bool IsKnockedBack { get; private set; }

    // True while an electro stun is active — pauses self-movement but allows knockback.
    protected bool IsMovementSuspended { get; private set; }

    // Combined block flag: subclasses use this instead of checking IsKnockedBack directly.
    protected bool IsBlocked => IsKnockedBack || IsMovementSuspended;

    // ── Public API ───────────────────────────────────────────────────────────

    // Called by EnemyInitializer on spawn to provide stats.
    // Subclasses override to apply type-specific settings (agent speed, etc.).
    public virtual void Initialize(EnemyData data)
    {
        EnemyData    = data;
        ScaledSpeed  = data.MoveSpeed;
        OnInitialized(data);
        Debug.Log($"[{GetType().Name}] '{name}' initialized as '{data.EnemyName}'.");
    }

    // Applies a level-scaled move speed. Updates ScaledSpeed so effect systems
    // (e.g. ice slow) can read and restore the correct base.
    // Subclasses MUST call base.SetMoveSpeed(speed) before their own logic.
    public virtual void SetMoveSpeed(float speed)
    {
        ScaledSpeed = speed;
    }

    // Pauses self-directed movement (electro stun). Knockback can still apply.
    // Subclasses override OnMovementSuspended to stop their locomotion system immediately.
    public void SuspendMovement()
    {
        IsMovementSuspended = true;
        OnMovementSuspended();
    }

    // Resumes self-directed movement after a stun ends.
    public void ResumeMovement()
    {
        IsMovementSuspended = false;
        OnMovementResumed();
    }

    // Override in NavMesh subclasses to reset the agent path on stun start.
    protected virtual void OnMovementSuspended() { }
    protected virtual void OnMovementResumed()   { }

    // Called by KnockbackHandler when knockback starts.
    // Subclasses should suspend movement and hand off control to Rigidbody.
    public void StartKnockback()
    {
        IsKnockedBack = true;
        OnKnockbackStart();
    }

    // Called by KnockbackHandler when knockback ends.
    // Subclasses should resume normal movement.
    public void EndKnockback()
    {
        IsKnockedBack = false;
        OnKnockbackEnd();
    }

    // ── IDeathHandler ────────────────────────────────────────────────────────

    // Called by DeathController (Step 1) the moment health hits zero.
    // Stops all locomotion immediately — before scripts are disabled in Step 2 —
    // so the enemy never continues moving after it dies.
    public void OnDeath()
    {
        OnDeathCleanup();
        Debug.Log($"[{GetType().Name}] '{name}' movement stopped on death.");
    }

    // Override in subclasses to perform type-specific death cleanup.
    // Default: no-op (sufficient for Update/FixedUpdate-based movers
    // since DeathController will disable the script in Step 2).
    // NavMeshAgent-based movers must override to stop the agent explicitly —
    // the agent keeps pathfinding even when the MonoBehaviour is disabled.
    protected virtual void OnDeathCleanup() { }

    // ── Abstract / virtual hooks for subclasses ──────────────────────────────

    // Called once after EnemyData is assigned — apply type-specific setup here.
    protected abstract void OnInitialized(EnemyData data);

    // Subclass-specific response to knockback starting (e.g. disable NavMeshAgent).
    protected abstract void OnKnockbackStart();

    // Subclass-specific response to knockback ending (e.g. re-enable NavMeshAgent).
    protected abstract void OnKnockbackEnd();

    // ── Shared utility ───────────────────────────────────────────────────────

    // Delegates to the shared PlayerFinder utility.
    // Kept as a protected method so subclasses can call it without knowing about the static helper.
    protected Transform FindNearestPlayer()
    {
        return PlayerFinder.FindNearest(transform.position, playerTag);
    }

    // Rotates the enemy to face a world-space position, ignoring Y.
    protected void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.LookRotation(direction);
    }
}
