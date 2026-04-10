using System.Collections.Generic;
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

    // Upgrades this player has already chosen — used to filter non-stackable upgrades from the pool
    private readonly HashSet<UpgradeDefinition> _acquiredUpgrades = new();

    // The random selection currently presented to the owner — used to map the
    // button index back to the pool index when the ServerRpc is sent.
    private UpgradeDefinition[] _pendingChoices;

    // Cached on the server so we don't call FindFirstObjectByType every upgrade.
    private RoomManager _roomManager;

    // Cached UI reference (found once, reused on subsequent level-ups)
    private UpgradeSelectionUI _cachedSelectionUI;
    private bool _uiCached;

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
        if (_levelSystem != null)
            _levelSystem.OnLevelUp += HandleLevelUp;

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

        if (_upgradePool == null || _upgradePool.upgrades.Length == 0)
        {
            Debug.LogWarning("[PlayerUpgradeHandler] No UpgradePool assigned — skipping upgrade selection.");
            SkipUpgradeServerRpc();
            return;
        }

        CacheUI();

        // Pick a random subset, filtering out non-stackable upgrades already acquired
        _pendingChoices = _upgradePool.GetRandomSelection(_choiceCount, _acquiredUpgrades);

        if (_pendingChoices.Length == 0)
        {
            Debug.Log("[PlayerUpgradeHandler] All upgrades acquired — skipping selection.");
            SkipUpgradeServerRpc();
            return;
        }

        if (_cachedSelectionUI != null)
        {
            _cachedSelectionUI.Show(_pendingChoices, OnUpgradeChosen);
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

        var chosen = _pendingChoices[choiceIndex];
        int poolIndex = _upgradePool.IndexOf(chosen);

        if (poolIndex < 0)
        {
            Debug.LogWarning("[PlayerUpgradeHandler] Chosen upgrade not found in pool — skipping.");
            SkipUpgradeServerRpc();
            return;
        }

        // Track locally so next level-up filters non-stackable upgrades
        _acquiredUpgrades.Add(chosen);

        ApplyUpgradeServerRpc(poolIndex);
    }

    // ── UI cache ──────────────────────────────────────────────────────────────

    private void CacheUI()
    {
        if (_uiCached) return;

        _cachedSelectionUI = FindFirstObjectByType<UpgradeSelectionUI>(FindObjectsInactive.Include);
        _uiCached = true;
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

        NotifyUpgradeChosenClientRpc(upgrade.upgradeName);
        NotifyRoomUpgradeDone();
    }

    [ServerRpc]
    private void SkipUpgradeServerRpc()
    {
        Debug.Log($"[PlayerUpgradeHandler] '{name}' upgrade skipped — notifying room manager.");
        NotifyRoomUpgradeDone();
    }

    // ── ClientRpcs ────────────────────────────────────────────────────────────

    [ClientRpc]
    private void NotifyUpgradeChosenClientRpc(string upgradeName)
    {
        Debug.Log($"[PlayerUpgradeHandler] '{name}' chose: {upgradeName}");
    }

    // ── Upgrade application (server only) ────────────────────────────────────

    // Delegates to UpgradeEffectRegistry — each effect type has its own handler class.
    private void ApplyEffect(UpgradeDefinition upgrade)
    {
        var ctx = new UpgradeContext
        {
            GameObject         = gameObject,
            Health             = _health,
            HealthSync         = _healthSync,
            AttackControllers  = _attackControllers,
            Movement           = _movement,
            ProjectileModifiers = _projectileModifiers,
        };

        UpgradeEffectRegistry.TryApply(upgrade, ctx);

        // Handlers may AddComponent for ProjectileModifiers — sync the cached reference
        _projectileModifiers = ctx.ProjectileModifiers;
    }

    // Tells the room manager that this player's upgrade selection is done.
    private void NotifyRoomUpgradeDone()
    {
        if (_roomManager != null)
            _roomManager.NotifyUpgradeChosen();
        else
            Debug.LogWarning("[PlayerUpgradeHandler] RoomManager not found — gate may not open.");
    }
}
