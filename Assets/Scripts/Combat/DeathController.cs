using System;
using UnityEngine;

/// <summary>
/// Orchestrates entity death. Designed to work identically for both enemies and players.
///
/// On death (triggered by Health.OnDeath):
///   1. Calls OnDeath() on every IDeathHandler found in the hierarchy — custom per-component cleanup.
///   2. Disables every Behaviour listed in _disableOnDeath (scripts, renderers, etc.).
///   3. Disables every Collider listed in _disableColliders — projectiles and physics pass through.
///   4. Fires OnDied — external listeners (animation system, VFX, loot, game-over screen) hook here.
///
/// The entity is intentionally kept alive as a "ghost":
///   - No Destroy() call here — destruction, respawn, or pooling belongs to a higher-level system.
///
/// Requires a Health component on the same GameObject.
/// </summary>
[RequireComponent(typeof(Health))]
public class DeathController : MonoBehaviour
{
    [Header("Disable on Death — Behaviours")]
    [Tooltip("Scripts, renderers, and other Behaviours to disable on death " +
             "(AI brain, combat, movement, health bar, mesh renderer, etc.).")]
    [SerializeField] private Behaviour[] _disableOnDeath;

    [Header("Disable on Death — Colliders")]
    [Tooltip("Physics colliders to disable on death so projectiles and physics pass through the corpse.")]
    [SerializeField] private Collider[] _disableColliders;

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
    /// custom handlers → component shutdown → collider shutdown → external event.
    /// </summary>
    private void HandleDeath(DamageInfo killingBlow)
    {
        // Step 1 — let each component do its own cleanup (clear targets, stop coroutines, etc.)
        var handlers = GetComponentsInChildren<IDeathHandler>();
        foreach (var handler in handlers)
            handler.OnDeath();

        // Step 2 — disable behaviours (scripts, renderers) — removes them from Unity's update loop
        int behaviourCount = 0;
        if (_disableOnDeath != null)
        {
            foreach (var behaviour in _disableOnDeath)
            {
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                    behaviourCount++;
                }
            }
        }

        // Step 3 — disable colliders so projectiles and physics pass through the corpse
        int colliderCount = 0;
        if (_disableColliders != null)
        {
            foreach (var col in _disableColliders)
            {
                if (col != null)
                {
                    col.enabled = false;
                    colliderCount++;
                }
            }
        }

        Debug.Log($"[DeathController] '{name}' entered ghost state — " +
                  $"notified {handlers.Length} handler(s), " +
                  $"disabled {behaviourCount} behaviour(s), {colliderCount} collider(s).");

        // Step 4 — notify external systems (animation, VFX, loot, etc.)
        OnDied?.Invoke();
    }
}
