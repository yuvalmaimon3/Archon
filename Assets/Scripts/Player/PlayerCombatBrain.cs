using UnityEngine;

/// <summary>
/// Decides when the player attacks and dispatches to the correct executor.
/// Owns the "when" — not the "how": damage logic lives in the executors and Health.
///
/// Responsibilities:
///   - Read player attack input
///   - Ask AttackController whether the attack is ready
///   - Route to the correct executor based on AttackType
///   - Mark the cooldown after a successful execution
///
/// Note: when multiplayer is added, wrap Update/TryAttack with an IsOwner guard
/// (same pattern used in PlayerMovement) so only the local player triggers attacks.
/// </summary>
public class PlayerCombatBrain : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The attack controller that owns the cooldown and AttackDefinition. " +
             "Auto-resolved from this GameObject if left empty.")]
    [SerializeField] private AttackController attackController;

    [Header("Input")]
    [Tooltip("Key that triggers an attack attempt. Swap for a mobile/UI button call to TryAttack() later.")]
    [SerializeField] private KeyCode attackKey = KeyCode.Space;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-resolve so the component works when the reference is not set manually
        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (attackController == null)
            Debug.LogError($"[PlayerCombatBrain] {gameObject.name} has no AttackController assigned or found.", this);
    }

    private void Update()
    {
        if (Input.GetKeyDown(attackKey))
            TryAttack();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to execute the current attack.
    /// Can be called from external sources (auto-fire timer, UI button) in addition to keyboard input.
    /// </summary>
    public void TryAttack()
    {
        if (attackController == null) return;
        if (!attackController.CanUseAttack()) return;

        AttackDefinition def = attackController.AttackDefinition;

        bool success = ExecuteAttack(def);

        // Only start the cooldown when the attack actually fired —
        // prevents a missing prefab from wasting the player's turn
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
                // ProjectileAttackExecutor returns the spawned Projectile, or null on failure
                var projectile = ProjectileAttackExecutor.Execute(transform, GetAttackDirection(), def);
                return projectile != null;

            case AttackType.Melee:
                MeleeAttackExecutor.Execute(transform, def);
                return true;

            case AttackType.Contact:
                // Contact damage is applied automatically and continuously by ContactDamageDealer
                // on the same GameObject — the brain does not need to trigger it manually
                Debug.LogWarning($"[PlayerCombatBrain] AttackType.Contact is managed by " +
                                 $"ContactDamageDealer, not triggered by the brain.");
                return false;

            default:
                Debug.LogWarning($"[PlayerCombatBrain] Unhandled AttackType: {def.AttackType}");
                return false;
        }
    }

    /// <summary>
    /// Returns the world-space direction this player's attacks travel toward.
    /// Currently uses transform.forward — replace this method's body with aim logic
    /// (e.g. direction to nearest enemy) without changing any other code.
    /// </summary>
    private Vector3 GetAttackDirection()
    {
        return transform.forward;
    }
}
