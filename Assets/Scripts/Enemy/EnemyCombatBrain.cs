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
/// <summary>
/// IDeathHandler implementation: clears the attack target so no stale reference remains
/// after this component is disabled by DeathController.
/// </summary>
public class EnemyCombatBrain : MonoBehaviour, IDeathHandler
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

    // Cached Health of the current target — checked each frame to detect when the player dies.
    // Avoids GetComponent per frame; populated whenever a new target is assigned.
    private Health _targetHealth;

    // Tracks the previously logged target to avoid spamming the console every frame.
    private Transform _lastLoggedTarget;

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

        // Re-scan every frame so the enemy always aims at the nearest live player.
        // This handles coop (multiple players) and late-joins — target switches
        // automatically when a closer player appears or the current target dies.
        target = FindNearestPlayer();

        if (target == null) return;

        if (IsTargetInRange())
            TryAttack();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Assigns or replaces the current attack target at runtime.
    /// Also caches the target's Health component so Update can check IsDead cheaply.
    /// Call this from a targeting system or room spawner when the target is known.
    /// </summary>
    public void SetTarget(Transform newTarget) => SetTargetInternal(newTarget);

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
    /// Scans all live players and returns the nearest one's Transform.
    /// Uses FindGameObjectsWithTag to support coop (multiple players) — dead players
    /// are excluded automatically because DeathController changes their tag to "Untagged".
    /// Returns null when no players exist yet (handles NGO late-join).
    ///
    /// Note: FindGameObjectsWithTag allocates each call — acceptable for a test scene.
    /// Production builds should replace this with a centrally-managed player registry.
    /// </summary>
    private Transform FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
        if (players.Length == 0)
        {
            SetTargetInternal(null);
            return null;
        }

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

        // Cache Health whenever the target changes — avoids GetComponent every frame.
        if (nearest != target)
            SetTargetInternal(nearest);

        // Log only on change to avoid console spam every frame.
        if (nearest != _lastLoggedTarget)
        {
            _lastLoggedTarget = nearest;
            if (nearest != null)
                Debug.Log($"[EnemyCombatBrain] '{name}' targeting '{nearest.name}' " +
                          $"at {Mathf.Sqrt(nearestSqDist):F1}m.");
            else
                Debug.Log($"[EnemyCombatBrain] '{name}' — no players found.");
        }

        return nearest;
    }

    /// <summary>
    /// Updates the internal target and caches its Health component in one place.
    /// </summary>
    private void SetTargetInternal(Transform newTarget)
    {
        target        = newTarget;
        _targetHealth = newTarget != null ? newTarget.GetComponent<Health>() : null;
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
                // Contact damage is driven by ContactDamageDealer, not the brain — nothing to do.
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

    // ── IDeathHandler ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DeathController when this entity dies.
    /// Clears the target so no stale Transform reference remains after the component is disabled.
    /// </summary>
    public void OnDeath()
    {
        SetTargetInternal(null);
        _lastLoggedTarget = null;
        Debug.Log($"[EnemyCombatBrain] '{name}' target cleared on death.");
    }
}
