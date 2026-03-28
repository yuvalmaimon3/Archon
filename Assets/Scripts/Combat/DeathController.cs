using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Orchestrates entity death. Designed to work identically for both enemies and players,
/// in both standalone and networked (NGO) sessions.
///
/// On death (triggered by Health.OnDeath or TriggerDeath from NetworkDeathSync):
///   1. Guard: runs only once per entity lifetime (_isDead flag).
///   2. Calls OnDeath() on every IDeathHandler found in the hierarchy — custom per-component cleanup.
///   3. Disables every MonoBehaviour listed in _disableOnDeath (AI, combat, movement, etc.).
///   4. Disables every Renderer listed in _disableRenderers (mesh, skinned mesh, etc.).
///   5. Disables every Collider listed in _disableColliders — projectiles and physics pass through.
///   6. Removes the entity tag so targeting systems skip the corpse immediately.
///   7. Freezes Rigidbody (kinematic) so the corpse stays where it died.
///   8. Fires OnDied — hook here for animation, VFX, loot, game-over.
///   9. Schedules destruction:
///        - NetworkObject + IsServer  → NGO Despawn propagates to all clients automatically.
///        - NetworkObject + !IsServer → do nothing — waits for server-driven Despawn.
///        - Non-networked            → Destroy() locally.
///        - _autoDestroy = false     → never destroyed (players awaiting revive).
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

    [Tooltip("When false the GameObject is never auto-destroyed after death — " +
             "use this for players so a future revive system can restore them.")]
    [SerializeField] private bool _autoDestroy = true;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired once when this entity dies, before the destroy timer starts.
    /// Subscribe here for death animation, VFX, loot drops, game-over logic, etc.
    /// </summary>
    public event Action OnDied;

    // ── Private state ────────────────────────────────────────────────────────

    private Health _health;

    // Guards against double-execution: HandleDeath can be called via Health.OnDeath
    // (server) or TriggerDeath (client via NetworkDeathSync RPC). Only runs once.
    private bool _isDead;

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

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers the full death sequence from outside — used by NetworkDeathSync
    /// to execute death on client machines that don't receive Health.OnDeath directly.
    /// Safe to call multiple times; subsequent calls are ignored via the _isDead guard.
    /// </summary>
    public void TriggerDeath()
    {
        HandleDeath(default);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full death sequence. Called from Health.OnDeath (server) or
    /// TriggerDeath (clients via NetworkDeathSync ClientRpc).
    /// </summary>
    private void HandleDeath(DamageInfo killingBlow)
    {
        // Guard: only run once — prevents double-execution when both Health.OnDeath
        // and a direct TriggerDeath call arrive on the same frame.
        if (_isDead) return;
        _isDead = true;

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

        // Step 5 — remove entity tag so targeting systems skip the corpse immediately.
        // FindGameObjectsWithTag("Enemy") / FindGameObjectWithTag("Player") return null
        // for this object from here on — cheaper than a per-frame IsDead check.
        gameObject.tag = "Untagged";

        // Step 6 — freeze physics: kinematic stops gravity and forces so the corpse
        // stays exactly where it died. Rigidbody is kept available for future ragdoll.
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic     = true;
        }

        Debug.Log($"[DeathController] '{name}' ghost state — " +
                  $"handlers:{handlers.Length} scripts:{scriptCount} " +
                  $"renderers:{rendererCount} colliders:{colliderCount}.");

        // Step 7 — notify external systems (animation, VFX, loot, etc.)
        OnDied?.Invoke();

        // Step 8 — schedule destruction
        if (_autoDestroy)
            ScheduleDestruction();
    }

    /// <summary>
    /// Determines the correct destruction path based on whether this is a NetworkObject.
    ///
    /// NetworkObject + IsServer  → NGO Despawn after delay — propagates destroy to ALL clients.
    /// NetworkObject + !IsServer → do nothing — the server's Despawn will clean up this client.
    /// Non-networked             → Destroy() locally after delay.
    ///
    /// This ensures exactly one machine initiates destruction per entity.
    /// </summary>
    private void ScheduleDestruction()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            // Only the server owns the lifetime — it despawns and NGO propagates to clients.
            // IsServer lives on NetworkBehaviour/NetworkManager, not NetworkObject.
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                StartCoroutine(DespawnAfterDelay(netObj));
            // Client: does nothing — awaits the server-driven NGO Despawn.
            return;
        }

        // Non-networked (standalone / offline): destroy locally.
        Destroy(gameObject, _destroyDelay);
    }

    /// <summary>
    /// Waits for _destroyDelay seconds then despawns the NetworkObject.
    /// NGO propagates the destroy to all connected clients automatically.
    /// </summary>
    private IEnumerator DespawnAfterDelay(NetworkObject netObj)
    {
        yield return new WaitForSeconds(_destroyDelay);
        if (netObj != null && netObj.IsSpawned)
        {
            Debug.Log($"[DeathController] '{name}' server despawn after {_destroyDelay}s.");
            netObj.Despawn(destroy: true);
        }
    }
}
