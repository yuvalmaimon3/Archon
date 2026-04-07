using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Bridges the local Health + DeathController components with the network layer.
/// Attach this to any NetworkObject that also has Health and DeathController.
///
/// The core problem it solves:
///   Health.TakeDamage is only called on the server (projectile hits are server-authoritative).
///   Without this component, client health bars never update and corpses remain fully alive
///   on all non-server machines — colliders active, AI running, visually intact.
///
/// What it does:
///   Server  → syncs CurrentHealth to all clients via a NetworkVariable (health bars stay live).
///   Server  → calls TriggerDeathClientRpc so every client runs its local DeathController.
///   Clients → apply synced health to the local Health component for UI feedback.
///   Clients → run their local death sequence on receiving the RPC (disable components,
///              go kinematic, change tag, etc.) — one source of truth, no independent divergence.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(DeathController))]
public class NetworkDeathSync : NetworkBehaviour
{
    // Authoritative health value replicated server → all clients.
    // Clients read it; only the server writes.
    // Only used when no NetworkHealthSync is present — avoids double-syncing
    // the same value through two NetworkVariables.
    private readonly NetworkVariable<int> _syncedHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private Health _health;
    private DeathController _deathController;

    // True when NetworkHealthSync is handling health replication on the same object.
    // When true, this component only handles death sync and skips health sync entirely.
    private bool _healthSyncedExternally;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health = GetComponent<Health>();
        _deathController = GetComponent<DeathController>();

        // If NetworkHealthSync is present, it already owns a NetworkVariable for health.
        // Skip our own health sync to avoid double-replication and wasted bandwidth.
        _healthSyncedExternally = GetComponent<NetworkHealthSync>() != null;
    }

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Called on all machines when the NetworkObject enters the session.
    /// Server wires into Health events; clients react to the NetworkVariable.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Only sync health ourselves if no NetworkHealthSync is handling it
            if (!_healthSyncedExternally)
            {
                _syncedHealth.Value = _health.CurrentHealth;
                _health.OnDamaged += OnServerHealthChanged;
            }

            _health.OnDeath += OnServerDeath;
        }
        else if (!_healthSyncedExternally)
        {
            // React to server health pushes — keeps health bars current on clients.
            _syncedHealth.OnValueChanged += OnClientHealthSynced;

            // Apply the current value immediately in case we joined after damage was dealt.
            _health.ForceSync(_syncedHealth.Value);
        }
    }

    /// <summary>
    /// Called on all machines when the NetworkObject leaves the session.
    /// Prevents stale event subscriptions after despawn.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            if (!_healthSyncedExternally)
                _health.OnDamaged -= OnServerHealthChanged;

            _health.OnDeath -= OnServerDeath;
        }
        else if (!_healthSyncedExternally)
        {
            _syncedHealth.OnValueChanged -= OnClientHealthSynced;
        }
    }

    // ── Server handlers ──────────────────────────────────────────────────────

    /// <summary>
    /// Fires on server each time health changes. Writes the new value to the NetworkVariable
    /// so all clients receive it and update their local health bars.
    /// </summary>
    private void OnServerHealthChanged(int currentHealth, int maxHealth)
    {
        _syncedHealth.Value = currentHealth;
    }

    /// <summary>
    /// Fires on server when health reaches zero. Broadcasts to all clients
    /// so they can run their local death sequence (disabling components,
    /// kinematic, tag change, etc.).
    /// </summary>
    private void OnServerDeath(DamageInfo killingBlow)
    {
        TriggerDeathClientRpc();
    }

    // ── ClientRpc ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs on all clients (and host) when the server broadcasts death.
    /// The server skips this because DeathController already ran via Health.OnDeath.
    /// Clients call TriggerDeath which executes the full death ghost sequence locally.
    /// </summary>
    [ClientRpc]
    private void TriggerDeathClientRpc()
    {
        // Server already handled death via its own Health.OnDeath → DeathController chain.
        if (IsServer) return;

        _deathController.TriggerDeath();
        Debug.Log($"[NetworkDeathSync] '{name}' death sequence synced to client.");
    }

    // ── Client handlers ──────────────────────────────────────────────────────

    /// <summary>
    /// Fires on clients each time the server updates _syncedHealth.
    /// Calls ForceSync to update the local Health value and fire OnDamaged
    /// so the health bar reacts — without re-processing any damage logic.
    /// </summary>
    private void OnClientHealthSynced(int previousValue, int newValue)
    {
        _health.ForceSync(newValue);
        Debug.Log($"[NetworkDeathSync] '{name}' health synced on client: {previousValue} → {newValue}.");
    }
}
