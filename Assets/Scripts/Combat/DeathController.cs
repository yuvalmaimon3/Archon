using System;
using UnityEngine;

/// <summary>
/// Orchestrates entity death. Designed to work identically for both enemies and players.
///
/// On death (triggered by Health.OnDeath):
///   1. Calls OnDeath() on every IDeathHandler found in the hierarchy — custom per-component cleanup.
///   2. Disables every MonoBehaviour listed in _disableOnDeath (AI, combat, movement, etc.).
///   3. Disables every Renderer listed in _disableRenderers (mesh, skinned mesh, etc.).
///   4. Disables every Collider listed in _disableColliders — projectiles and physics pass through.
///   5. Fires OnDied — hook here for animation, VFX, loot, game-over.
///   6. Destroys the GameObject after _destroyDelay seconds.
///
/// Requires a Health component on the same GameObject.
/// </summary>
[RequireComponent(typeof(Health))]
public class DeathController : MonoBehaviour
{
    [Header("Disable on Death")]
    [Tooltip("Scripts and behaviours to disable (AI brain, combat, movement, health bar, etc.).")]
    [SerializeField] private MonoBehaviour[] _disableOnDeath;

    [Tooltip("Renderers to disable (MeshRenderer, SkinnedMeshRenderer, etc.).")]
    [SerializeField] private Renderer[] _disableRenderers;

    [Tooltip("Colliders to disable so projectiles and physics pass through the corpse.")]
    [SerializeField] private Collider[] _disableColliders;

    [Header("Cleanup")]
    [Tooltip("Seconds after death before the GameObject is destroyed. Set to 0 to destroy immediately.")]
    [Min(0f)]
    [SerializeField] private float _destroyDelay = 3f;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired once when this entity dies, before the destroy timer starts.
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
    /// IDeathHandler cleanup → script shutdown → renderer hide → collider off → destroy timer.
    /// </summary>
    private void HandleDeath(DamageInfo killingBlow)
    {
        // Step 1 — custom per-component cleanup (clear targets, stop coroutines, etc.)
        var handlers = GetComponentsInChildren<IDeathHandler>();
        foreach (var handler in handlers)
            handler.OnDeath();

        // Step 2 — disable scripts/behaviours (stops Update loops, coroutines, etc.)
        int scriptCount = 0;
        if (_disableOnDeath != null)
            foreach (var mb in _disableOnDeath)
                if (mb != null) { mb.enabled = false; scriptCount++; }

        // Step 3 — hide renderers (body disappears until death animation is added)
        int rendererCount = 0;
        if (_disableRenderers != null)
            foreach (var r in _disableRenderers)
                if (r != null) { r.enabled = false; rendererCount++; }

        // Step 4 — disable colliders so projectiles pass through the corpse
        int colliderCount = 0;
        if (_disableColliders != null)
            foreach (var col in _disableColliders)
                if (col != null) { col.enabled = false; colliderCount++; }

        // Step 5 — freeze physics: kinematic stops gravity and forces so the corpse
        // stays exactly where it died. Rigidbody is kept available for future ragdoll/animation.
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Debug.Log($"[DeathController] '{name}' ghost state — " +
                  $"handlers:{handlers.Length} scripts:{scriptCount} " +
                  $"renderers:{rendererCount} colliders:{colliderCount}. " +
                  $"Destroy in {_destroyDelay}s.");

        // Step 6 — notify external systems (animation, VFX, loot, etc.)
        OnDied?.Invoke();

        // Step 7 — schedule destruction
        Destroy(gameObject, _destroyDelay);
    }
}
