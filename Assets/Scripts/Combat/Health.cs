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

    [Header("Debug (read-only)")]
    [SerializeField] private int _currentHealth;

    /// <summary>Current health. Always between 0 and MaxHealth.</summary>
    public int CurrentHealth => _currentHealth;

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
        _currentHealth =maxHealth;

        // Optional — not every damageable object has an elemental component
        TryGetComponent(out _elementStatus);
    }

    // ── IDamageable ──────────────────────────────────────────────────────────

    /// <summary>
    /// Applies damage and updates health state.
    /// Ignores incoming hits if already dead — prevents double-death events.
    ///
    /// Reaction suppression: if this hit carries an element that would trigger a reaction,
    /// the direct attack damage is NOT applied. ReactionDamageHandler will apply x2 damage
    /// via a follow-up TakeDamage call (no element) once OnReactionTriggered fires.
    /// This ensures the total damage is the reaction's value only — not attack + reaction.
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        // Ignore hits on a dead object — prevents duplicate Die() calls if
        // multiple projectiles land on the same frame
        if (IsDead) return;

        bool hasElement = damageInfo.ElementApplication.Element != ElementType.None;

        // Check BEFORE applying damage whether this element would trigger a reaction.
        // If yes, suppress direct damage — ReactionDamageHandler handles it with x2 multiplier.
        bool willReact = _elementStatus != null
                         && hasElement
                         && _elementStatus.WouldReact(damageInfo.ElementApplication.Element);

        if (!willReact)
        {
            // Normal hit — apply damage directly
            _currentHealth = Mathf.Clamp(_currentHealth - damageInfo.Amount, 0, maxHealth);

            Debug.Log($"[Health] {gameObject.name} took {damageInfo.Amount} damage — " +
                      $"{CurrentHealth}/{maxHealth} HP remaining.");

            // Notify subscribers (health bar, hit flash, audio)
            OnDamaged?.Invoke(CurrentHealth, maxHealth);
        }
        else
        {
            // Reaction hit — direct damage is suppressed.
            // ReactionDamageHandler will apply x2 damage after OnReactionTriggered fires.
            Debug.Log($"[Health] {gameObject.name} — direct damage suppressed " +
                      $"(reaction triggered, ReactionDamageHandler will apply x{damageInfo.Amount * 2}).");
        }

        // Forward element to ElementStatusController.
        // Pass baseDamage so the ReactionResult carries it for damage calculation.
        if (_elementStatus != null && hasElement)
            _elementStatus.ApplyElement(damageInfo.ElementApplication, damageInfo.Amount);

        // Guard with !IsDead — ReactionDamageHandler's TakeDamage (called synchronously
        // inside ApplyElement above) may have already triggered death via Die().
        if (!IsDead && CurrentHealth == 0)
            Die(damageInfo);
    }

    // ── Network sync ─────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a health value received from the server without processing damage logic.
    /// Used by NetworkDeathSync to keep client health bars in sync with the authoritative
    /// server state. Fires OnDamaged so health bar components react, but does NOT trigger
    /// death — NetworkDeathSync handles death via a separate ClientRpc.
    /// </summary>
    public void ForceSync(int currentHealth)
    {
        _currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        OnDamaged?.Invoke(_currentHealth, maxHealth);
    }

    // ── Test utilities ───────────────────────────────────────────────────────

    /// <summary>
    /// Resets health to full and clears the dead flag.
    /// Used by TestEnemyResetter in the TestReactions scene to allow continuous damage testing.
    /// Not intended for production gameplay — use a proper respawn/revival system there.
    /// </summary>
    public void ResetHealth()
    {
        _currentHealth = maxHealth;
        IsDead = false;

        OnDamaged?.Invoke(_currentHealth, maxHealth);

        Debug.Log($"[Health] {gameObject.name} — health reset to {maxHealth}.");
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
