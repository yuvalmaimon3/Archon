using UnityEngine;

/// <summary>
/// Owns the attack state and cooldown for a single AttackDefinition.
/// Generic — works for both player and enemy without modification.
///
/// Does not execute attacks, spawn projectiles, or select targets.
/// Callers ask CanUseAttack(), then MarkAttackUsed() after firing.
/// </summary>
public class AttackController : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("The attack this controller manages. Assign an AttackDefinition asset here.")]
    [SerializeField] private AttackDefinition attackDefinition;

    // Tracks when the next attack is allowed.
    // Using a timestamp (Time.time) avoids a decrement loop in Update.
    private float _nextAttackTime = 0f;

    // ── Read-only state ──────────────────────────────────────────────────────

    /// <summary>The attack definition assigned to this controller.</summary>
    public AttackDefinition AttackDefinition => attackDefinition;

    /// <summary>
    /// True when the cooldown has elapsed and the attack is ready to fire.
    /// Does not validate whether a target exists or whether a definition is assigned.
    /// </summary>
    public bool IsReady => Time.time >= _nextAttackTime;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when this controller is fully ready to fire:
    /// a definition is assigned and the cooldown has elapsed.
    /// Future conditions (ammo, charge, status effects) belong here, not in IsReady.
    /// </summary>
    public bool CanUseAttack()
    {
        if (attackDefinition == null)
        {
            Debug.LogWarning($"[AttackController] {gameObject.name} has no AttackDefinition assigned.");
            return false;
        }

        return IsReady;
    }

    /// <summary>
    /// Records the attack as used and starts the cooldown.
    /// Call this immediately after the attack is executed.
    /// </summary>
    public void MarkAttackUsed()
    {
        if (attackDefinition == null) return;

        _nextAttackTime = Time.time + attackDefinition.Cooldown;

        Debug.Log($"[AttackController] {gameObject.name} used '{attackDefinition.AttackId}'. " +
                  $"Next attack available in {attackDefinition.Cooldown:F2}s.");
    }
}
