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
/// Max health sync:
///   - _syncedMaxHealth mirrors Health.maxHealth across all clients.
///   - Required because maxHealth can change at runtime (e.g., player level-up).
///   - Clients update their local Health component when _syncedMaxHealth changes so
///     the Healthbar always computes the correct fill ratio.
///
/// Why not embed NetworkVariables directly in Health?
///   Health.cs is used for non-networked entities (enemies, destructibles).
///   Mixing NGO into Health would force every damageable object to carry a NetworkObject.
///   This component keeps the networking concern separate and opt-in.
/// </summary>
[RequireComponent(typeof(Health))]
public class NetworkHealthSync : NetworkBehaviour
{
    private Health _health;

    // Authoritative current health — server writes, all clients read.
    private readonly NetworkVariable<int> _syncedHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Authoritative max health — server writes, all clients read.
    // Allows runtime max-health changes (level-up HP boost) to propagate to all clients.
    private readonly NetworkVariable<int> _syncedMaxHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Read-only state ──────────────────────────────────────────────────────

    /// <summary>Current health synced from the server. Always up to date on all clients.</summary>
    public int CurrentHealth => _syncedHealth.Value;

    /// <summary>
    /// Max health synced from the server.
    /// Falls back to the local Health component before the NetworkObject is spawned.
    /// </summary>
    public int MaxHealth => IsSpawned ? _syncedMaxHealth.Value : (_health != null ? _health.MaxHealth : 0);

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
    /// Server initializes the synced values; all machines subscribe to future changes.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialize at full health and current max — only the server owns write permission.
            _syncedHealth.Value    = _health.MaxHealth;
            _syncedMaxHealth.Value = _health.MaxHealth;

            // Subscribe to local damage events. Only the server calls TakeDamage
            // (Projectile.OnTriggerEnter has an IsServer guard), so this is server-only.
            _health.OnDamaged += OnDamagedServer;
        }

        // All machines react to replicated value changes.
        _syncedHealth.OnValueChanged    += OnSyncedHealthChanged;
        _syncedMaxHealth.OnValueChanged += OnSyncedMaxHealthChanged;

        // Fire once immediately so any subscriber that registered before spawn gets the
        // current state. Also handles late-joining clients whose initial value is already non-max.
        OnHealthChanged?.Invoke(_syncedHealth.Value, MaxHealth);

        Debug.Log($"[NetworkHealthSync] '{name}' spawned — " +
                  $"health:{_syncedHealth.Value}/{_syncedMaxHealth.Value}, IsServer:{IsServer}");
    }

    /// <summary>Called on all machines when the NetworkObject is despawned.</summary>
    public override void OnNetworkDespawn()
    {
        if (IsServer)
            _health.OnDamaged -= OnDamagedServer;

        _syncedHealth.OnValueChanged    -= OnSyncedHealthChanged;
        _syncedMaxHealth.OnValueChanged -= OnSyncedMaxHealthChanged;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the synced max health value so all clients reflect the new cap.
    /// Call this on the server whenever Health.maxHealth changes (e.g., player level-up).
    /// Clients will receive the change and update their local Health component via
    /// OnSyncedMaxHealthChanged, keeping Healthbar fill ratios correct everywhere.
    /// </summary>
    public void UpdateSyncedMaxHealth(int newMax)
    {
        if (!IsServer) return;
        _syncedMaxHealth.Value = Mathf.Max(1, newMax);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    // Called on the server only when the local Health takes damage.
    // Writes the new value to the NetworkVariable, triggering replication to all clients.
    private void OnDamagedServer(int currentHealth, int maxHealth)
    {
        _syncedHealth.Value = currentHealth;

        Debug.Log($"[NetworkHealthSync] '{name}' health synced: {currentHealth}/{maxHealth}");
    }

    // Called on all machines when the current health NetworkVariable changes.
    // Forwards to the C# event so local listeners (Healthbar, UI) update without polling.
    private void OnSyncedHealthChanged(int oldValue, int newValue)
    {
        OnHealthChanged?.Invoke(newValue, MaxHealth);
    }

    // Called on all machines when the max health NetworkVariable changes.
    // On clients: updates the local Health component so Healthbar fill ratios stay correct.
    // On the server: Health.maxHealth was already updated directly (via AdjustMaxHealth),
    // so we only need to fire the UI event.
    private void OnSyncedMaxHealthChanged(int oldMax, int newMax)
    {
        if (!IsServer && _health != null)
        {
            // Sync client Health component — this fires Health.OnDamaged which
            // keeps any direct Health subscribers (non-player entities) in sync too.
            _health.ClientSync(newMax, _syncedHealth.Value);
        }

        // Notify UI (Healthbar) on all machines with the updated max.
        OnHealthChanged?.Invoke(_syncedHealth.Value, newMax);
    }
}
