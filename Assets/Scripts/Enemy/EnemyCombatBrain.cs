using UnityEngine;

/// <summary>
/// Decides when the enemy attacks and dispatches to the correct executor.
/// Owns the "when" — not the "how": damage logic lives in the executors and Health.
///
/// Responsibilities:
///   - Poll attack readiness and target range each frame
///   - Route to the correct executor based on AttackType
///   - Mark the cooldown after a successful execution
///
/// Target assignment is intentionally simple — assign via Inspector or
/// replace SetTarget() calls from a future targeting/AI system without
/// touching any of the attack logic here.
/// </summary>
public class EnemyCombatBrain : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The attack controller that owns the cooldown and AttackDefinition. " +
             "Auto-resolved from this GameObject if left empty.")]
    [SerializeField] private AttackController attackController;

    [Header("Target")]
    [Tooltip("The Transform this enemy attacks toward. Leave empty to auto-find by 'Player' tag at runtime.")]
    [SerializeField] private Transform target;

    [Tooltip("Unity tag used to locate the player when no target is assigned.")]
    [SerializeField] private string playerTag = "Player";

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (attackController == null)
            Debug.LogError($"[EnemyCombatBrain] {gameObject.name} has no AttackController assigned or found.", this);
    }

    private void Update()
    {
        if (attackController == null) return;

        // Auto-find player each frame until one is found.
        // This handles cases where the player spawns after the enemy (e.g. NGO late-join).
        if (target == null)
            target = FindPlayer();

        if (target == null) return;

        if (IsTargetInRange())
            TryAttack();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Assigns or replaces the current attack target at runtime.
    /// Call this from a targeting system or room spawner when the target is known.
    /// </summary>
    public void SetTarget(Transform newTarget) => target = newTarget;

    /// <summary>
    /// Attempts to execute the current attack toward the active target.
    /// Can be called externally (e.g. from a state machine) in addition to the Update loop.
    /// </summary>
    public void TryAttack()
    {
        if (attackController == null || target == null) return;
        if (!attackController.CanUseAttack()) return;

        AttackDefinition def = attackController.AttackDefinition;

        bool success = ExecuteAttack(def);

        if (success)
            attackController.MarkAttackUsed();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Searches the scene for a GameObject with the player tag.
    /// Returns null if none exists yet (player may not have spawned).
    /// </summary>
    private Transform FindPlayer()
    {
        var playerGO = GameObject.FindGameObjectWithTag(playerTag);
        if (playerGO != null)
            Debug.Log($"[EnemyCombatBrain] '{name}' locked on player '{playerGO.name}'.");
        return playerGO != null ? playerGO.transform : null;
    }

    /// <summary>
    /// Returns true when the target is within the attack's configured range.
    /// </summary>
    private bool IsTargetInRange()
    {
        AttackDefinition def = attackController.AttackDefinition;
        if (def == null) return false;

        return Vector3.Distance(transform.position, target.position) <= def.Range;
    }

    /// <summary>
    /// Routes to the correct executor based on the AttackDefinition's AttackType.
    /// Returns true when an attack was successfully dispatched.
    /// </summary>
    private bool ExecuteAttack(AttackDefinition def)
    {
        switch (def.AttackType)
        {
            case AttackType.Projectile:
                var projectile = ProjectileAttackExecutor.Execute(transform, GetAttackDirection(), def);
                return projectile != null;

            case AttackType.Melee:
                MeleeAttackExecutor.Execute(transform, def);
                return true;

            case AttackType.Contact:
                // Contact damage is applied automatically by ContactDamageDealer on this GameObject —
                // the brain does not trigger it manually
                Debug.LogWarning($"[EnemyCombatBrain] AttackType.Contact is managed by " +
                                 $"ContactDamageDealer, not triggered by the brain.");
                return false;

            default:
                Debug.LogWarning($"[EnemyCombatBrain] Unhandled AttackType: {def.AttackType}");
                return false;
        }
    }

    /// <summary>
    /// Returns the world-space direction from this enemy to the current target.
    /// Replace this method's body with prediction or lead-aim logic without
    /// changing any other code.
    /// </summary>
    private Vector3 GetAttackDirection()
    {
        return (target.position - transform.position).normalized;
    }
}
