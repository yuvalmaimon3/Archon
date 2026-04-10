using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages the player's EXP and level progression.
/// Server-authoritative: level and EXP are replicated via NetworkVariables so all clients
/// display accurate state.
///
/// On level-up the system applies three stat bonuses immediately (server side):
///   1. Max HP +5%   — via Health.AdjustMaxHealth + NetworkHealthSync.UpdateSyncedMaxHealth
///   2. Damage +5%   — cumulative multiplier applied to all AttackControllers
///   3. Heal 20%     — partial HP restore equal to 20% of the new max HP
///
/// Additionally fires events for VFX (LevelUpEffect) and UI (ExpBar).
///
/// EXP is granted by the room system at room completion — see AddExperience().
/// Skill dialog on level-up is handled by a separate system (LevelUpSkillDialog).
/// </summary>
public class PlayerLevelSystem : NetworkBehaviour
{
    [Header("Configuration")]
    [Tooltip("Level curve and bonus values. Assign the PlayerLevelConfig asset here.")]
    [SerializeField] private PlayerLevelConfig _config;

    // ── NetworkVariables ─────────────────────────────────────────────────────

    // Current player level. Starts at 1, caps at _config.maxLevel.
    private readonly NetworkVariable<int> _level = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // EXP accumulated toward the next level.
    // Resets to 0 when a level-up occurs (or stays at 0 at max level).
    private readonly NetworkVariable<int> _currentExp = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Component references ─────────────────────────────────────────────────

    private Health            _health;
    private NetworkHealthSync _healthSync;
    private AttackController[] _attackControllers;  // update all controllers (primary + any extras)

    // ── Events ───────────────────────────────────────────────────────────────

    // Fires on all clients when the player levels up.
    // Passes the new level — subscribe for VFX, skill dialog, audio.
    public event Action<int> OnLevelUp;

    // Fires on all clients when EXP or level changes.
    // Passes (currentExp, expRequiredForNextLevel) — subscribe for the EXP bar UI.
    public event Action<int, int> OnExpChanged;

    // ── Read-only properties ─────────────────────────────────────────────────

    public int  CurrentLevel  => _level.Value;
    public int  CurrentExp    => _currentExp.Value;
    public int  ExpRequired   => _config != null ? _config.GetExpRequired(_level.Value) : 0;
    public bool IsMaxLevel    => _config != null && _level.Value >= _config.maxLevel;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health            = GetComponent<Health>();
        _healthSync        = GetComponent<NetworkHealthSync>();
        _attackControllers = GetComponents<AttackController>();

        if (_config == null)
            Debug.LogError($"[PlayerLevelSystem] No PlayerLevelConfig assigned on '{name}'.", this);
        if (_health == null)
            Debug.LogError($"[PlayerLevelSystem] No Health component found on '{name}'.", this);
    }

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        // Subscribe for UI updates on all machines
        _level.OnValueChanged      += OnLevelValueChanged;
        _currentExp.OnValueChanged += OnExpValueChanged;

        // Fire initial events so ExpBar and other UI initialize with the correct state
        OnExpChanged?.Invoke(_currentExp.Value, ExpRequired);

        Debug.Log($"[PlayerLevelSystem] '{name}' spawned — " +
                  $"level {_level.Value}, EXP {_currentExp.Value}/{ExpRequired}");
    }

    public override void OnNetworkDespawn()
    {
        _level.OnValueChanged      -= OnLevelValueChanged;
        _currentExp.OnValueChanged -= OnExpValueChanged;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Grants EXP to this player. Must be called on the server.
    /// Intended to be called by the room manager at room completion.
    /// Processes one or more level-ups immediately if the EXP thresholds are crossed.
    /// Does nothing if the player is already at max level.
    /// </summary>
    public void AddExperience(int amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"[PlayerLevelSystem] AddExperience must be called on the server. " +
                             $"Use AddExperienceServerRpc from a client instead.");
            return;
        }

        ProcessExperience(amount);
    }

    /// <summary>
    /// ServerRpc variant of AddExperience for callers that don't own the server.
    /// RequireOwnership = false lets any machine grant EXP to this player.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AddExperienceServerRpc(int amount)
    {
        ProcessExperience(amount);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // Core EXP processing logic. Only runs on the server.
    private void ProcessExperience(int amount)
    {
        if (_config == null || IsMaxLevel) return;

        int exp = _currentExp.Value + amount;

        // Process level-ups one at a time in case the EXP gain spans multiple levels
        while (!IsMaxLevel)
        {
            int needed = _config.GetExpRequired(_level.Value);
            if (exp < needed) break;

            exp -= needed;
            ApplyLevelUp();
        }

        // Write the remaining EXP (0 when at max level)
        _currentExp.Value = IsMaxLevel ? 0 : exp;

        Debug.Log($"[PlayerLevelSystem] '{name}' gained {amount} EXP — " +
                  $"level {_level.Value}, EXP {_currentExp.Value}/{ExpRequired}.");
    }

    // Applies all stat bonuses for one level-up. Server only.
    private void ApplyLevelUp()
    {
        _level.Value++;

        // ── HP boost: +maxHpBonusPercent% of current max ─────────────────────
        int newMaxHp = Mathf.RoundToInt(_health.MaxHealth * (1f + _config.maxHpBonusPercent));

        // Update the server's local Health component
        _health.AdjustMaxHealth(newMaxHp);

        // Push the new max to clients via NetworkHealthSync so Healthbar fill ratios
        // stay correct on all machines
        _healthSync?.UpdateSyncedMaxHealth(newMaxHp);

        // ── HP heal: +healOnLevelUpPercent% of new max ───────────────────────
        // Heal is applied after max HP is set so the new ceiling is in effect
        int healAmount = Mathf.RoundToInt(newMaxHp * _config.healOnLevelUpPercent);
        _health.Heal(healAmount);
        // Note: Heal fires Health.OnDamaged which NetworkHealthSync catches server-side
        // and updates _syncedHealth — so the healed HP is automatically propagated to clients.

        // ── Damage boost: +damageBonusPercent% per level ────────────────────
        // Applied incrementally so upgrade bonuses stacked on the multiplier are preserved.
        float levelBonus = 1f + _config.damageBonusPercent;
        foreach (var ac in _attackControllers)
            ac.SetDamageMultiplier(ac.DamageMultiplier * levelBonus);

        // ── Visual effects: fire on all clients ─────────────────────────────
        TriggerLevelUpEffectsClientRpc(_level.Value);

        Debug.Log($"[PlayerLevelSystem] '{name}' leveled up to {_level.Value}! " +
                  $"MaxHP: {newMaxHp}, Heal: {healAmount}, DmgBonus: +{_config.damageBonusPercent * 100f:F0}%");
    }

    // Fires on ALL clients (including the host) to trigger local visual/audio effects.
    // Stats are applied server-side only; this RPC is only for presentation.
    [ClientRpc]
    private void TriggerLevelUpEffectsClientRpc(int newLevel)
    {
        // Fire the event — LevelUpEffect and skill dialog systems subscribe here
        OnLevelUp?.Invoke(newLevel);

        Debug.Log($"[PlayerLevelSystem] Level-up effects triggered on '{name}' → level {newLevel}.");
    }

    private void OnLevelValueChanged(int oldLevel, int newLevel)
    {
        // ExpRequired changes when the level changes — update EXP bar threshold
        OnExpChanged?.Invoke(_currentExp.Value, ExpRequired);
    }

    private void OnExpValueChanged(int oldExp, int newExp)
    {
        OnExpChanged?.Invoke(newExp, ExpRequired);
    }
}
