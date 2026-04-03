using Unity.Netcode;
using UnityEngine;

// Abstract base for all enemy movement types.
// Subclasses implement the actual locomotion (NavMesh, flying, etc.).
//
// KnockbackHandler and EnemyInitializer talk only to this base class —
// they don't care whether the enemy walks or flies.
//
// To add a new movement type: extend this class and implement the abstract methods.
public abstract class EnemyMovementBase : NetworkBehaviour
{
    [Header("Movement Config")]
    // Unity tag used to locate players.
    [SerializeField] protected string playerTag = "Player";

    // Cached enemy stats — set by Initialize() from EnemyInitializer.
    protected EnemyData EnemyData { get; private set; }

    // True while a knockback is active — subclasses must pause locomotion when set.
    public bool IsKnockedBack { get; private set; }

    // ── Public API ───────────────────────────────────────────────────────────

    // Called by EnemyInitializer on spawn to provide stats.
    // Subclasses override to apply type-specific settings (agent speed, etc.).
    public virtual void Initialize(EnemyData data)
    {
        EnemyData = data;
        OnInitialized(data);
        Debug.Log($"[{GetType().Name}] '{name}' initialized as '{data.EnemyName}'.");
    }

    // Applies a level-scaled move speed after Initialize() has already run.
    // Called by EnemyInitializer after computing scaled stats so subclasses
    // can update their agent/controller without a full re-initialize.
    // Subclasses override to update the appropriate speed field (e.g. NavMeshAgent.speed).
    public virtual void SetMoveSpeed(float speed) { }

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

    // ── Abstract / virtual hooks for subclasses ──────────────────────────────

    // Called once after EnemyData is assigned — apply type-specific setup here.
    protected abstract void OnInitialized(EnemyData data);

    // Subclass-specific response to knockback starting (e.g. disable NavMeshAgent).
    protected abstract void OnKnockbackStart();

    // Subclass-specific response to knockback ending (e.g. re-enable NavMeshAgent).
    protected abstract void OnKnockbackEnd();

    // ── Shared utility ───────────────────────────────────────────────────────

    // Scans all live players and returns the nearest Transform.
    // Dead players excluded — DeathController removes the player tag on death.
    protected Transform FindNearestPlayer()
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

    // Rotates the enemy to face a world-space position, ignoring Y.
    protected void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        transform.rotation = Quaternion.LookRotation(direction);
    }
}
