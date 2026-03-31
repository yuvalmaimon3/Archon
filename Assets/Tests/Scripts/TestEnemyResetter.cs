using UnityEngine;

/// <summary>
/// Resets the enemy's health and elemental state after death so the test cycle continues indefinitely.
/// Without this, Health.IsDead blocks all incoming damage once the enemy reaches 0 HP,
/// stopping the reaction test for that enemy permanently.
///
/// Attach this to all test dummy enemies in the TestReactions scene alongside Health and ElementStatusController.
/// </summary>
[RequireComponent(typeof(Health))]
public class TestEnemyResetter : MonoBehaviour
{
    [Tooltip("Seconds to wait after death before resetting. Gives reaction VFX time to play out.")]
    [SerializeField] private float _resetDelay = 1.5f;

    // ── Private references ───────────────────────────────────────────────────

    private Health _health;

    // Optional — not every test dummy has an elemental component, but most will.
    private ElementStatusController _elementStatus;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health = GetComponent<Health>();
        TryGetComponent(out _elementStatus);
    }

    private void OnEnable()
    {
        _health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        _health.OnDeath -= HandleDeath;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void HandleDeath(DamageInfo _)
    {
        // Delay allows any death-triggered VFX or logs to complete before wiping state
        Invoke(nameof(ResetEnemy), _resetDelay);
    }

    /// <summary>
    /// Restores health to full and clears elemental state so this dummy can receive damage again.
    /// </summary>
    private void ResetEnemy()
    {
        _health.ResetHealth();
        _elementStatus?.ClearElement();

        Debug.Log($"[TestEnemyResetter] {gameObject.name} — reset to full health for continued testing.");
    }
}
