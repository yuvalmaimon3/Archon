using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Bridges the local Health component to NGO's NetworkVariable replication system.
/// Attach alongside Health on any networked entity (players, networked enemies).
///
/// How the sync works:
///   - Server subscribes to Health.OnDamaged and writes the new value to _syncedHealth.
///   - NGO automatically replicates _syncedHealth to all connected clients.
///   - All machines fire OnHealthChanged (C# event) when the value changes.
///   - The Healthbar subscribes to OnHealthChanged — zero polling, event-driven.
///
/// Why not embed a NetworkVariable directly in Health?
///   Health.cs is used for non-networked entities (enemies, destructibles).
///   Mixing NGO into Health would force every damageable object to carry a NetworkObject.
///   This component keeps the networking concern separate and opt-in.
/// </summary>
[RequireComponent(typeof(Health))]
public class NetworkHealthSync : NetworkBehaviour
{
    private Health _health;

    // Server writes; all clients read. Default 0 is overwritten in OnNetworkSpawn.
    private readonly NetworkVariable<int> _syncedHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Read-only state ──────────────────────────────────────────────────────

    /// <summary>Current health synced from the server. Always up to date on all clients.</summary>
    public int CurrentHealth => _syncedHealth.Value;

    /// <summary>
    /// Max health from the local Health component.
    /// Not synced — all clients share the same prefab so the value is always identical.
    /// </summary>
    public int MaxHealth => _health != null ? _health.MaxHealth : 0;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired on all machines whenever health changes (currentHealth, maxHealth).
    /// Also fired once on network spawn so subscribers initialize with the correct state.
    /// Subscribe here for health bars, hit VFX, audio reactions, etc.
    /// </summary>
    public event Action<int, int> OnHealthChanged;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health = GetComponent<Health>();
    }

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Called on all machines when the NetworkObject is spawned.
    /// Server initializes the synced value; all machines subscribe to future changes.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialize at full health — only the server owns the write permission.
            _syncedHealth.Value = _health.MaxHealth;

            // Subscribe to local damage events. Only the server calls TakeDamage
            // (Projectile.OnTriggerEnter has an IsServer guard), so this is server-only.
            _health.OnDamaged += OnDamagedServer;
        }

        // All machines react to replicated value changes.
        _syncedHealth.OnValueChanged += OnSyncedValueChanged;

        // Fire once immediately so any subscriber that registered before spawn gets the
        // current state. Also handles late-joining clients whose initial value is already non-max.
        OnHealthChanged?.Invoke(_syncedHealth.Value, MaxHealth);

        Debug.Log($"[NetworkHealthSync] '{name}' spawned — health:{_syncedHealth.Value}/{MaxHealth}, IsServer:{IsServer}");
    }

    /// <summary>Called on all machines when the NetworkObject is despawned.</summary>
    public override void OnNetworkDespawn()
    {
        if (IsServer)
            _health.OnDamaged -= OnDamagedServer;

        _syncedHealth.OnValueChanged -= OnSyncedValueChanged;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called on the server only when the local Health takes damage.
    /// Writes the new value to the NetworkVariable, triggering replication to all clients.
    /// </summary>
    private void OnDamagedServer(int currentHealth, int maxHealth)
    {
        _syncedHealth.Value = currentHealth;

        Debug.Log($"[NetworkHealthSync] '{name}' health synced: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Called on all machines when the NetworkVariable value changes.
    /// Forwards to the C# event so local listeners (Healthbar, UI) update without polling.
    /// </summary>
    private void OnSyncedValueChanged(int oldValue, int newValue)
    {
        OnHealthChanged?.Invoke(newValue, MaxHealth);
    }
}
