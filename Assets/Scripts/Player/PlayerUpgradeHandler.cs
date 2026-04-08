using Unity.Netcode;
using UnityEngine;

// Bridges PlayerLevelSystem level-up events to the upgrade selection UI and
// applies the chosen upgrade on the server.
//
// Network split:
//   Client (owner) — receives OnLevelUp via ClientRpc, shows UpgradeSelectionUI
//                    only on the machine that controls this player.
//   Server         — receives the chosen upgrade index via ServerRpc, applies
//                    the stat effect, and notifies RoomManager that this player
//                    has finished choosing.
//
// In co-op each player independently sees their own upgrade dialog on their own screen.
// RoomManager tracks a counter (_pendingUpgradeChoices) so the gate waits for all
// players who leveled up before opening.
public class PlayerUpgradeHandler : NetworkBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Upgrades")]
    [Tooltip("Pool of all available upgrades. Assign the shared UpgradePool asset here.")]
    [SerializeField] private UpgradePool _upgradePool;

    [Tooltip("How many upgrade options to present at level-up.")]
    [SerializeField] [Min(1)] private int _choiceCount = 3;

    // ── Component references ─────────────────────────────────────────────────

    private PlayerLevelSystem         _levelSystem;
    private Health                    _health;
    private NetworkHealthSync         _healthSync;
    private AttackController[]        _attackControllers;
    private PlayerMovement            _movement;
    private PlayerProjectileModifiers _projectileModifiers;

    // ── Runtime state ────────────────────────────────────────────────────────

    // The random selection currently presented to the owner — used to map the
    // button index back to the pool index when the ServerRpc is sent.
    private UpgradeDefinition[] _pendingChoices;

    // Cached on the server so we don't call FindFirstObjectByType every upgrade.
    private RoomManager _roomManager;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _levelSystem          = GetComponent<PlayerLevelSystem>();
        _health               = GetComponent<Health>();
        _healthSync           = GetComponent<NetworkHealthSync>();
        _attackControllers    = GetComponents<AttackController>();
        _movement             = GetComponent<PlayerMovement>();
        _projectileModifiers  = GetComponent<PlayerProjectileModifiers>();

        if (_levelSystem == null)
            Debug.LogError($"[PlayerUpgradeHandler] No PlayerLevelSystem on '{name}'.", this);
    }

    public override void OnNetworkSpawn()
    {
        // Subscribe to level-up on all clients — the handler checks IsOwner internally
        if (_levelSystem != null)
            _levelSystem.OnLevelUp += HandleLevelUp;

        // Cache RoomManager reference on the server (only the server calls NotifyUpgradeChosen)
        if (IsServer)
            _roomManager = FindFirstObjectByType<RoomManager>();
    }

    public override void OnNetworkDespawn()
    {
        if (_levelSystem != null)
            _levelSystem.OnLevelUp -= HandleLevelUp;
    }

    // ── Level-up flow ─────────────────────────────────────────────────────────

    // Fires on ALL clients (triggered by PlayerLevelSystem.TriggerLevelUpEffectsClientRpc).
    // Only the owning client shows the selection UI — others ignore this call.
    private void HandleLevelUp(int newLevel)
    {
        if (!IsOwner) return;

        Debug.Log($"[PlayerUpgradeHandler] '{name}' reached level {newLevel} — showing upgrade dialog.");

        // No pool or no upgrades → skip selection, still notify server so the gate can open
        if (_upgradePool == null || _upgradePool.upgrades.Length == 0)
        {
            Debug.LogWarning("[PlayerUpgradeHandler] No UpgradePool assigned — skipping upgrade selection.");
            SkipUpgradeServerRpc();
            return;
        }

        // Test UI (all upgrades) takes priority over the normal 3-choice UI when present
        var testUI = FindFirstObjectByType<UpgradeAllSelectionUI>(FindObjectsInactive.Include);
        if (testUI != null)
        {
            _pendingChoices = _upgradePool.upgrades;
            testUI.Show(_pendingChoices, OnUpgradeChosen);
            return;
        }

        // Normal flow: pick a random subset of upgrades to present
        _pendingChoices = _upgradePool.GetRandomSelection(_choiceCount);

        var ui = FindFirstObjectByType<UpgradeSelectionUI>(FindObjectsInactive.Include);
        if (ui != null)
        {
            ui.Show(_pendingChoices, OnUpgradeChosen);
        }
        else
        {
            // No UI in scene — auto-pick the first option so the gate isn't permanently blocked
            Debug.LogWarning("[PlayerUpgradeHandler] No UpgradeSelectionUI found — auto-choosing first upgrade.");
            OnUpgradeChosen(0);
        }
    }

    // Called by UpgradeSelectionUI when the player clicks a button.
    // Sends the pool index to the server for authoritative application.
    private void OnUpgradeChosen(int choiceIndex)
    {
        if (_pendingChoices == null || choiceIndex < 0 || choiceIndex >= _pendingChoices.Length)
        {
            Debug.LogWarning("[PlayerUpgradeHandler] Invalid choice index — skipping.");
            SkipUpgradeServerRpc();
            return;
        }

        int poolIndex = _upgradePool.IndexOf(_pendingChoices[choiceIndex]);

        if (poolIndex < 0)
        {
            Debug.LogWarning("[PlayerUpgradeHandler] Chosen upgrade not found in pool — skipping.");
            SkipUpgradeServerRpc();
            return;
        }

        ApplyUpgradeServerRpc(poolIndex);
    }

    // ── ServerRpcs ────────────────────────────────────────────────────────────

    // Owner sends the chosen pool index to the server. Server applies the effect and
    // decrements the room manager's pending upgrade counter.
    [ServerRpc]
    private void ApplyUpgradeServerRpc(int poolIndex)
    {
        if (_upgradePool == null || poolIndex < 0 || poolIndex >= _upgradePool.upgrades.Length)
        {
            Debug.LogError($"[PlayerUpgradeHandler] Invalid pool index {poolIndex} received on server.");
            NotifyRoomUpgradeDone();
            return;
        }

        var upgrade = _upgradePool.upgrades[poolIndex];

        Debug.Log($"[PlayerUpgradeHandler] '{name}' applying upgrade: {upgrade.upgradeName}");

        ApplyEffect(upgrade);

        // Let all clients log / show a short feedback
        NotifyUpgradeChosenClientRpc(upgrade.upgradeName);

        NotifyRoomUpgradeDone();
    }

    // Called when the player skips (no pool configured, or UI missing).
    // Still notifies the room manager so the gate isn't stuck waiting.
    [ServerRpc]
    private void SkipUpgradeServerRpc()
    {
        Debug.Log($"[PlayerUpgradeHandler] '{name}' upgrade skipped — notifying room manager.");
        NotifyRoomUpgradeDone();
    }

    // ── ClientRpcs ────────────────────────────────────────────────────────────

    // Logs the chosen upgrade on all clients (hook here for VFX / floating text later).
    [ClientRpc]
    private void NotifyUpgradeChosenClientRpc(string upgradeName)
    {
        Debug.Log($"[PlayerUpgradeHandler] '{name}' chose: {upgradeName}");
        // TODO: play a short VFX or floating text "+{upgradeName}" above the player
    }

    // ── Upgrade application (server only) ────────────────────────────────────

    // Applies the effect of the chosen upgrade directly to the player's components.
    // Only runs on the server — all networked state is updated through existing sync paths.
    private void ApplyEffect(UpgradeDefinition upgrade)
    {
        switch (upgrade.effectType)
        {
            case UpgradeEffectType.MaxHpFlat:
            {
                // Increase max HP and sync the new cap to all clients
                int newMax = _health.MaxHealth + Mathf.Max(1, (int)upgrade.value);
                _health.AdjustMaxHealth(newMax);
                _healthSync?.UpdateSyncedMaxHealth(newMax);
                Debug.Log($"[PlayerUpgradeHandler] MaxHP +{(int)upgrade.value} → {newMax}");
                break;
            }

            case UpgradeEffectType.HealPercent:
            {
                // Restore a fraction of max HP (heal fires Health.OnDamaged which NetworkHealthSync catches)
                int healAmount = Mathf.RoundToInt(_health.MaxHealth * upgrade.value);
                _health.Heal(healAmount);
                Debug.Log($"[PlayerUpgradeHandler] Healed {healAmount} HP ({upgrade.value * 100f:F0}% of max)");
                break;
            }

            case UpgradeEffectType.DamagePercent:
            {
                // Compound the existing damage multiplier (works alongside PlayerLevelSystem's scaling)
                foreach (var ac in _attackControllers)
                {
                    float newMult = ac.DamageMultiplier * (1f + upgrade.value);
                    ac.SetDamageMultiplier(newMult);
                    Debug.Log($"[PlayerUpgradeHandler] Damage ×{newMult:F3} on '{ac.gameObject.name}'");
                }
                break;
            }

            case UpgradeEffectType.MoveSpeedFlat:
            {
                if (_movement != null)
                {
                    _movement.AddSpeedBonus(upgrade.value);
                    Debug.Log($"[PlayerUpgradeHandler] Move speed +{upgrade.value}");
                }
                break;
            }

            case UpgradeEffectType.AttackSpeedPercent:
            {
                // Reduce each attack controller's cooldown multiplier by 'value' fraction.
                // Compounded multiplicatively so multiple upgrades stack properly.
                // e.g. 20% faster twice: 1.0 × 0.80 × 0.80 = 0.64 (36% total reduction).
                foreach (var ac in _attackControllers)
                {
                    float newMult = ac.CooldownMultiplier * (1f - upgrade.value);
                    ac.SetCooldownMultiplier(newMult);
                    Debug.Log($"[PlayerUpgradeHandler] Attack speed +{upgrade.value * 100f:F0}% on '{ac.gameObject.name}' " +
                              $"→ cooldown ×{newMult:F3} (effective: {ac.EffectiveCooldown:F2}s)");
                }
                break;
            }

            case UpgradeEffectType.BlastReaction:
            {
                // Add BlastReactionUpgradeEffect if not already present, then configure it.
                // value = blast radius in world units (e.g. 2).
                // effectPrefab = ReactionExplosion prefab assigned in the UpgradeDefinition asset.
                if (upgrade.effectPrefab == null || !upgrade.effectPrefab.TryGetComponent<ReactionExplosion>(out var explosionPrefab))
                {
                    Debug.LogError("[PlayerUpgradeHandler] BlastReaction upgrade has no ReactionExplosion prefab assigned.");
                    break;
                }

                var blast = gameObject.GetComponent<BlastReactionUpgradeEffect>()
                            ?? gameObject.AddComponent<BlastReactionUpgradeEffect>();
                blast.SetConfig(explosionPrefab, upgrade.value);
                Debug.Log($"[PlayerUpgradeHandler] Blast Reaction enabled — radius:{upgrade.value}u");
                break;
            }

            case UpgradeEffectType.ProjectileSplit:
            {
                // Add PlayerProjectileModifiers if not already present, then enable split.
                // value = the angle in degrees between the forward shot and each angled shot.
                if (_projectileModifiers == null)
                    _projectileModifiers = gameObject.AddComponent<PlayerProjectileModifiers>();

                _projectileModifiers.SplitOnHit        = true;
                _projectileModifiers.SplitAngleDegrees = upgrade.value;
                Debug.Log($"[PlayerUpgradeHandler] Shotgun split enabled — angle:{upgrade.value}°");
                break;
            }

            case UpgradeEffectType.LifeSteal:
            {
                // value = heal fraction per hit (e.g. 0.01 = 1% of max HP).
                // Heal is applied server-side in Projectile.OnTriggerEnter for each enemy hit.
                // In co-op each player's projectiles only reference their own source, so
                // lifesteal always heals the correct player.
                if (_projectileModifiers == null)
                    _projectileModifiers = gameObject.AddComponent<PlayerProjectileModifiers>();

                _projectileModifiers.LifeSteal         = true;
                _projectileModifiers.LifeStealFraction = upgrade.value;
                Debug.Log($"[PlayerUpgradeHandler] Life steal enabled — {upgrade.value * 100f:F0}% per hit");
                break;
            }

            default:
                Debug.LogWarning($"[PlayerUpgradeHandler] Unknown UpgradeEffectType: {upgrade.effectType}");
                break;
        }
    }

    // Tells the room manager that this player's upgrade selection is done.
    // Server only — RoomManager.NotifyUpgradeChosen() decrements the pending counter.
    private void NotifyRoomUpgradeDone()
    {
        if (_roomManager != null)
            _roomManager.NotifyUpgradeChosen();
        else
            Debug.LogWarning("[PlayerUpgradeHandler] RoomManager not found — gate may not open.");
    }
}
