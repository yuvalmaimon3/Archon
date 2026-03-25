using System;
using UnityEngine;

/// <summary>
/// Generic health component — works for players, enemies, or any damageable object.
/// Implements IDamageable to receive damage events.
///
/// If an ElementStatusController exists on the same GameObject, Health will forward
/// elemental data from incoming hits to it automatically. Health does not own or
/// manage elemental state — it only acts as a bridge.
///
/// Intentionally contains no game-specific logic: no Player/Enemy references,
/// no tag checks, no attack knowledge. Other systems react through events.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    // ── Read-only state ──────────────────────────────────────────────────────

    /// <summary>Maximum health, set in the Inspector.</summary>
    public int MaxHealth => maxHealth;

    /// <summary>Current health. Always between 0 and MaxHealth.</summary>
    public int CurrentHealth { get; private set; }

    /// <summary>True once health reaches zero. Prevents duplicate death events.</summary>
    public bool IsDead { get; private set; }

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired on every successful hit (after damage is applied).
    /// Passes (currentHealth, maxHealth) — use for health bar UI, hit VFX, audio.
    /// </summary>
    public event Action<int, int> OnDamaged;

    /// <summary>
    /// Fired once when health reaches zero.
    /// Subscribe here for death VFX, game-over logic, loot drops, etc.
    /// Passes the final DamageInfo that caused the death.
    /// </summary>
    public event Action<DamageInfo> OnDeath;

    // ── Private references ───────────────────────────────────────────────────

    // Cached once in Awake — null if this GameObject has no elemental component.
    // Avoiding GetComponent per hit keeps TakeDamage allocation-free.
    private ElementStatusController _elementStatus;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        // Start at full health
        CurrentHealth = maxHealth;

        // Optional — not every damageable object has an elemental component
        TryGetComponent(out _elementStatus);
    }

    // ── IDamageable ──────────────────────────────────────────────────────────

    /// <summary>
    /// Applies damage and updates health state.
    /// Ignores incoming hits if already dead — prevents double-death events.
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        // Ignore hits on a dead object — prevents duplicate Die() calls if
        // multiple projectiles land on the same frame
        if (IsDead) return;

        // Subtract damage and clamp to [0, MaxHealth]
        CurrentHealth = Mathf.Clamp(CurrentHealth - damageInfo.Amount, 0, maxHealth);

        Debug.Log($"[Health] {gameObject.name} took {damageInfo.Amount} damage — " +
                  $"{CurrentHealth}/{maxHealth} HP remaining.");

        // Notify subscribers (health bar, hit flash, audio)
        OnDamaged?.Invoke(CurrentHealth, maxHealth);

        // Forward elemental data to ElementStatusController if present and the hit carries an element.
        // Health does not own elemental state — it only passes the data along.
        if (_elementStatus != null && damageInfo.ElementApplication.Element != ElementType.None)
            _elementStatus.ApplyElement(damageInfo.ElementApplication);

        if (CurrentHealth == 0)
            Die(damageInfo);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called once when health hits zero.
    /// Sets IsDead and fires OnDeath — actual destruction/respawn is handled by subscribers.
    /// </summary>
    private void Die(DamageInfo killingBlow)
    {
        IsDead = true;

        Debug.Log($"[Health] {gameObject.name} died. Killed by: {killingBlow.Source?.name ?? "unknown"}");

        OnDeath?.Invoke(killingBlow);
    }
}
