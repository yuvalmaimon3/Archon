using System;
using UnityEngine;

// Owns the attack state and cooldown for a single AttackDefinition.
// Generic — works for both player and enemy without modification.
//
// Does not execute attacks, spawn projectiles, or select targets.
// Callers ask CanUseAttack(), then MarkAttackUsed() after firing.
//
// Level scaling: SetDamageMultiplier() and SetCooldownMultiplier() let
// EnemyInitializer apply level-based scaling without modifying the shared
// AttackDefinition asset. EffectiveDamage and EffectiveCooldown expose
// the final values for executors to use.
public class AttackController : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("The attack this controller manages. Assign an AttackDefinition asset here.")]
    [SerializeField] private AttackDefinition attackDefinition;

    // ── Events ───────────────────────────────────────────────────────────────

    // Fired immediately after an attack is used and the cooldown begins.
    // Subscribe here to react to each shot (e.g. cycling to the next attack definition).
    public event Action OnAttackUsed;

    // Tracks when the next attack is allowed.
    // Using a timestamp (Time.time) avoids a decrement loop in Update.
    private float _nextAttackTime = 0f;

    // Multipliers set by EnemyInitializer (or any other system) to scale
    // the base definition values per enemy level at runtime.
    // Defaults to 1.0 — no scaling until explicitly set.
    private float _damageMultiplier   = 1f;
    private float _cooldownMultiplier = 1f; // < 1.0 = faster attacks (shorter cooldown)

    // ── Read-only state ──────────────────────────────────────────────────────

    // The attack definition assigned to this controller.
    public AttackDefinition AttackDefinition => attackDefinition;

    // True when the cooldown has elapsed and the attack is ready to fire.
    // Does not validate whether a target exists or whether a definition is assigned.
    public bool IsReady => Time.time >= _nextAttackTime;

    // Damage after applying the level multiplier.
    // Executors should use this instead of attackDefinition.Damage directly.
    public int EffectiveDamage => attackDefinition != null
        ? Mathf.Max(0, Mathf.RoundToInt(attackDefinition.Damage * _damageMultiplier))
        : 0;

    // Returns a single rolled damage value for one attack instance.
    // Applies damageVariance from the AttackDefinition around the level-scaled base.
    // variance = 0 → always equals EffectiveDamage, no Random call.
    public int RollDamage()
    {
        if (attackDefinition == null) return 0;

        float variance = attackDefinition.DamageVariance;
        float scaled   = attackDefinition.Damage * _damageMultiplier;

        if (variance <= 0f)
            return Mathf.Max(0, Mathf.RoundToInt(scaled));

        float rolled = Random.Range(scaled * (1f - variance), scaled * (1f + variance));
        return Mathf.Max(0, Mathf.RoundToInt(rolled));
    }

    // Cooldown after applying the level multiplier.
    // < 1.0 multiplier = shorter cooldown = faster attacks.
    public float EffectiveCooldown => attackDefinition != null
        ? Mathf.Max(0.05f, attackDefinition.Cooldown * _cooldownMultiplier)
        : 0f;

    // ── Level scaling API ────────────────────────────────────────────────────

    // The current damage multiplier — read by PlayerUpgradeHandler to compound upgrade bonuses
    // on top of whatever PlayerLevelSystem has already applied (e.g. 1.05^level).
    public float DamageMultiplier => _damageMultiplier;

    // The current cooldown multiplier — read by PlayerUpgradeHandler to compound attack-speed upgrades.
    // Values < 1.0 mean faster attacks (shorter cooldown).
    public float CooldownMultiplier => _cooldownMultiplier;

    // Sets the damage multiplier applied on top of AttackDefinition.Damage.
    // Called by EnemyInitializer when a level is assigned.
    // 1.0 = base damage, 2.0 = double damage.
    public void SetDamageMultiplier(float multiplier)
    {
        _damageMultiplier = Mathf.Max(0f, multiplier);
        Debug.Log($"[AttackController] {gameObject.name} — damage multiplier set to {_damageMultiplier:F2} " +
                  $"(effective damage: {EffectiveDamage}).");
    }

    // Sets the cooldown multiplier applied on top of AttackDefinition.Cooldown.
    // Called by EnemyInitializer when a level is assigned.
    // < 1.0 = faster attacks, 1.0 = base speed.
    public void SetCooldownMultiplier(float multiplier)
    {
        _cooldownMultiplier = Mathf.Max(0.01f, multiplier);
        Debug.Log($"[AttackController] {gameObject.name} — cooldown multiplier set to {_cooldownMultiplier:F2} " +
                  $"(effective cooldown: {EffectiveCooldown:F2}s).");
    }

    // ── Public API ───────────────────────────────────────────────────────────

    // True while attacks are externally blocked (e.g. frozen status).
    public bool IsAttackBlocked { get; private set; }

    // Block/unblock attacks from external systems (freeze, stun, etc.).
    public void BlockAttacks()   => IsAttackBlocked = true;
    public void UnblockAttacks() => IsAttackBlocked = false;

    // Returns true when this controller is fully ready to fire:
    // a definition is assigned, the cooldown has elapsed, and attacks are not blocked.
    public bool CanUseAttack()
    {
        if (attackDefinition == null)
        {
            Debug.LogWarning($"[AttackController] {gameObject.name} has no AttackDefinition assigned.");
            return false;
        }

        return IsReady && !IsAttackBlocked;
    }

    // Replaces the current attack definition at runtime.
    // resetCooldown: true (default) lets the new attack fire immediately — use when swapping
    // attacks as an upgrade or game-start override.
    // resetCooldown: false preserves the running cooldown — use when cycling attacks mid-combat
    // so the swap itself doesn't grant an extra free shot.
    public void SetAttackDefinition(AttackDefinition newDefinition, bool resetCooldown = true)
    {
        attackDefinition = newDefinition;
        if (resetCooldown) _nextAttackTime = 0f;
        Debug.Log($"[AttackController] {gameObject.name} attack changed to '{newDefinition?.AttackId ?? "null"}'" +
                  $"{(resetCooldown ? " (cooldown reset)" : " (cooldown preserved)")}.");
    }

    // Records the attack as used and starts the cooldown.
    // Uses EffectiveCooldown so level scaling is automatically applied.
    // Call this immediately after the attack is executed.
    public void MarkAttackUsed()
    {
        if (attackDefinition == null) return;

        // Use EffectiveCooldown so level-based attack speed scaling is applied.
        _nextAttackTime = Time.time + EffectiveCooldown;

        Debug.Log($"[AttackController] {gameObject.name} used '{attackDefinition.AttackId}'. " +
                  $"Next attack available in {EffectiveCooldown:F2}s.");

        OnAttackUsed?.Invoke();
    }
}
