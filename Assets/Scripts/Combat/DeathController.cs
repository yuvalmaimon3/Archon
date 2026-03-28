using System;
using UnityEngine;

/// <summary>
/// Orchestrates entity death. Designed to work identically for both enemies and players.
///
/// On death (triggered by Health.OnDeath):
///   1. Calls OnDeath() on every IDeathHandler found in the hierarchy — custom per-component cleanup.
///   2. Disables every MonoBehaviour listed in _disableOnDeath — stops AI, combat, movement, etc.
///   3. Fires OnDied — external listeners (animation system, VFX, loot, game-over screen) hook here.
///
/// The entity is intentionally kept alive as a "ghost":
///   - No Destroy() call here — destruction, respawn, or pooling belongs to a higher-level system.
///   - Physics and rendering remain active unless explicitly listed in _disableOnDeath.
///
/// Requires a Health component on the same GameObject.
/// </summary>
[RequireComponent(typeof(Health))]
public class DeathController : MonoBehaviour
{
    [Header("Disable on Death")]
    [Tooltip("Behaviours to disable when this entity dies (AI brain, combat, movement, etc.). " +
             "These go silent immediately — no Update, no coroutine overhead.")]
    [SerializeField] private MonoBehaviour[] _disableOnDeath;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired once when this entity dies.
    /// Subscribe here for death animation, VFX, loot drops, game-over logic, etc.
    /// </summary>
    public event Action OnDied;

    // ── Private references ───────────────────────────────────────────────────

    private Health _health;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        _health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        _health.OnDeath -= HandleDeath;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Triggered by Health.OnDeath. Runs the full death sequence:
    /// custom handlers → component shutdown → external event.
    /// </summary>
    private void HandleDeath(DamageInfo killingBlow)
    {
        // Step 1 — let each component do its own cleanup (clear targets, stop coroutines, etc.)
        var handlers = GetComponentsInChildren<IDeathHandler>();
        foreach (var handler in handlers)
            handler.OnDeath();

        // Step 2 — shut down listed behaviours (removes them from Unity's Update loop)
        foreach (var behaviour in _disableOnDeath)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }

        int disabledCount = _disableOnDeath?.Length ?? 0;
        Debug.Log($"[DeathController] '{name}' entered ghost state — " +
                  $"notified {handlers.Length} handler(s), disabled {disabledCount} behaviour(s).");

        // Step 3 — notify external systems (animation, VFX, loot, etc.)
        OnDied?.Invoke();
    }
}
