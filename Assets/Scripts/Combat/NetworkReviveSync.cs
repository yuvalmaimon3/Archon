using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles network synchronization for the player revive system.
/// Attach this alongside NetworkDeathSync on the Player prefab.
///
/// Revive trigger paths:
///   Solo   — a revive item calls RequestReviveServerRpc() when used
///   Co-op  — the room-start system calls RequestReviveServerRpc() on all dead players
///
/// Networked revive flow:
///   1. Any machine calls RequestReviveServerRpc()
///   2. Server validates (player must be dead), then runs the full revive locally:
///        a. Health.Revive()           → restores HP, clears IsDead, fires OnDamaged + OnRevived
///        b. DeathController.TriggerRevive() → re-enables scripts/renderers/colliders,
///                                             restores tag and Rigidbody state
///   3. Server broadcasts TriggerReviveClientRpc() to all clients
///   4. Each client runs DeathController.TriggerRevive() locally
///      (Health is already in sync via the NetworkVariable in NetworkDeathSync)
///
/// Standalone (non-networked) revive:
///   Call Health.Revive() + DeathController.TriggerRevive() directly — no RPC needed.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(DeathController))]
public class NetworkReviveSync : NetworkBehaviour
{
    private Health          _health;
    private DeathController _deathController;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health          = GetComponent<Health>();
        _deathController = GetComponent<DeathController>();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Requests the server to revive this player.
    /// Can be called from any machine — the server is the sole authority.
    ///
    /// RequireOwnership = false so external systems (room manager, item system)
    /// can trigger it without needing to be the player's owner.
    ///
    /// Usage:
    ///   Solo revive item  → player.GetComponent<NetworkReviveSync>().RequestReviveServerRpc()
    ///   Co-op room start  → foreach dead player → RequestReviveServerRpc()
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void RequestReviveServerRpc()
    {
        if (!_health.IsDead)
        {
            Debug.LogWarning($"[NetworkReviveSync] '{name}' is not dead — revive request ignored.");
            return;
        }

        // Run the full revive on the server
        ExecuteReviveOnServer();

        // Broadcast to all clients so they mirror the revive locally
        TriggerReviveClientRpc();

        Debug.Log($"[NetworkReviveSync] Server revived '{name}' — broadcast sent to clients.");
    }

    // ── ClientRpc ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs on all clients when the server broadcasts a revive.
    /// DeathController.TriggerRevive() has its own _isDead guard so the host
    /// (which already ran TriggerRevive inside ExecuteReviveOnServer) silently skips.
    /// Clients re-enable their local components, restore the tag, etc.
    /// Health is already synced via NetworkDeathSync's _syncedHealth NetworkVariable.
    /// </summary>
    [ClientRpc]
    private void TriggerReviveClientRpc()
    {
        _deathController.TriggerRevive();
        Debug.Log($"[NetworkReviveSync] '{name}' revive synced to client.");
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Full server-side revive sequence. Order matters:
    ///   1. Health.Revive() first — restores HP and clears IsDead.
    ///      NetworkDeathSync picks up OnDamaged and updates _syncedHealth so clients get the new HP.
    ///   2. DeathController.TriggerRevive() — re-enables components, restores tag and Rigidbody.
    /// </summary>
    private void ExecuteReviveOnServer()
    {
        _health.Revive();
        _deathController.TriggerRevive();
    }
}
