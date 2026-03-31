using System;
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

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired immediately after an attack is used and the cooldown begins.
    /// Subscribe here to react to each shot (e.g. cycling to the next attack definition).
    /// </summary>
    public event Action OnAttackUsed;

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
    /// Replaces the current attack definition at runtime.
    /// Used by systems like HostAttackOverride to swap attacks dynamically.
    /// </summary>
    public void SetAttackDefinition(AttackDefinition newDefinition)
    {
        attackDefinition = newDefinition;
        _nextAttackTime = 0f; // Reset cooldown so the new attack can fire immediately
        Debug.Log($"[AttackController] {gameObject.name} attack changed to '{newDefinition?.AttackId ?? "null"}'.");
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

        OnAttackUsed?.Invoke();
    }
}
