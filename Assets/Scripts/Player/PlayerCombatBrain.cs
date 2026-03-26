using UnityEngine;

/// <summary>
/// Decides when the player attacks and dispatches to the correct executor.
/// Auto-fires toward the nearest tagged enemy each cooldown cycle.
/// Stops attacking automatically when no enemies are present in the scene.
///
/// Responsibilities:
///   - Scan for nearest enemy each frame via Unity tag lookup
///   - Ask AttackController whether the attack cooldown is ready
///   - Route to the correct executor based on AttackType
///   - Mark the cooldown after a successful execution
///
/// Note: when multiplayer is added, guard Update/TryAttack with IsOwner
/// (same pattern used in PlayerMovement) so only the local player triggers attacks.
/// </summary>
public class PlayerCombatBrain : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The attack controller that owns the cooldown and AttackDefinition. " +
             "Auto-resolved from this GameObject if left empty.")]
    [SerializeField] private AttackController attackController;

    [Header("Targeting")]
    [Tooltip("Unity tag that marks enemy GameObjects. Must match the tag applied to enemy prefabs.")]
    [SerializeField] private string enemyTag = "Enemy";

    // The nearest live enemy this frame — null when no enemies exist.
    private Transform _currentTarget;

    // Track the previous target to avoid spamming the log every frame.
    private Transform _lastLoggedTarget;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-resolve so the component works when the reference is not set manually.
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (attackController == null)
            Debug.LogError($"[PlayerCombatBrain] {gameObject.name} has no AttackController assigned or found.", this);
    }

    private void Update()
    {
        // Re-scan each frame so we always aim at the current nearest enemy.
        _currentTarget = FindNearestEnemy();

        if (_currentTarget != null)
            TryAttack();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to fire the current attack toward the active target.
    /// Can be called externally (UI buttons, test scripts).
    /// Does nothing if the cooldown is not ready or no target is set.
    /// </summary>
    public void TryAttack()
    {
        if (attackController == null || _currentTarget == null) return;
        if (!attackController.CanUseAttack()) return;

        AttackDefinition def = attackController.AttackDefinition;
        bool success = ExecuteAttack(def);

        // Only spend the cooldown when the attack actually fired —
        // prevents a missing prefab from wasting the player's cooldown.
        if (success)
            attackController.MarkAttackUsed();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Routes to the correct executor based on the AttackDefinition's AttackType.
    /// Returns true when an attack was successfully dispatched.
    /// </summary>
    private bool ExecuteAttack(AttackDefinition def)
    {
        switch (def.AttackType)
        {
            case AttackType.Projectile:
                // ProjectileAttackExecutor returns the spawned Projectile, or null on failure.
                var projectile = ProjectileAttackExecutor.Execute(transform, GetAttackDirection(), def);
                return projectile != null;

            case AttackType.Melee:
                MeleeAttackExecutor.Execute(transform, def);
                return true;

            case AttackType.Contact:
                // ContactDamageDealer handles this automatically — brain must not trigger it.
                Debug.LogWarning("[PlayerCombatBrain] AttackType.Contact is driven by ContactDamageDealer, not the brain.");
                return false;

            default:
                Debug.LogWarning($"[PlayerCombatBrain] Unhandled AttackType: {def.AttackType}");
                return false;
        }
    }

    /// <summary>
    /// Returns the normalized world-space direction from the player toward the current target.
    /// Y component is zeroed to keep projectiles flying on the horizontal plane.
    /// Falls back to transform.forward when no target is set.
    /// </summary>
    private Vector3 GetAttackDirection()
    {
        if (_currentTarget == null)
            return transform.forward;

        Vector3 dir = _currentTarget.position - transform.position;
        dir.y = 0f; // Stay on the horizontal plane — avoids lobbing projectiles upward.
        return dir == Vector3.zero ? transform.forward : dir.normalized;
    }

    /// <summary>
    /// Scans all GameObjects tagged with enemyTag and returns the Transform of the closest one.
    /// Returns null when no enemies are present in the scene.
    ///
    /// Note: FindGameObjectsWithTag allocates each call — acceptable for a test scene.
    /// Production builds should replace this with a centrally-managed enemy registry.
    /// </summary>
    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        if (enemies.Length == 0) return null;

        Transform nearest = null;
        float nearestSqDist = float.MaxValue;

        foreach (var enemy in enemies)
        {
            float sqDist = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest = enemy.transform;
            }
        }

        // Only log when the target changes — avoids console spam every frame.
        if (nearest != _lastLoggedTarget)
        {
            _lastLoggedTarget = nearest;
            if (nearest != null)
                Debug.Log($"[PlayerCombatBrain] New target: '{nearest.name}' at {Mathf.Sqrt(nearestSqDist):F1}m");
            else
                Debug.Log("[PlayerCombatBrain] No targets — attack stopped.");
        }

        return nearest;
    }
}
