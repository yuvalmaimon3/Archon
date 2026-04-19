using Unity.Netcode;
using UnityEngine;

// Handles save/load lifecycle on the player.
// Owner-side: loads on spawn, saves when the game stops or app quits.
// Server-side: applies gold via ServerRpc (PlayerCurrency is server-authoritative).
public class PlayerSaveHandler : NetworkBehaviour
{
    [Tooltip("Shared ItemRegistry asset — needed to resolve saved item IDs back to ItemDefinitions.")]
    [SerializeField] private ItemRegistry _itemRegistry;

    private PlayerInventory  _inventory;
    private PlayerEquipment  _equipment;
    private PlayerCurrency   _currency;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _equipment = GetComponent<PlayerEquipment>();
        _currency  = GetComponent<PlayerCurrency>();
    }

    public override void OnNetworkSpawn()
    {
        // Each player loads their own save data on their own machine.
        if (!IsOwner) return;

        if (SaveManager.Instance == null)
        {
            Debug.LogWarning("[PlayerSaveHandler] No SaveManager in scene — skipping load.");
            return;
        }

        SaveData data = SaveManager.Instance.Load();
        ApplySaveData(data);

        // Subscribe to game-stop event so we save at the end of every run.
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStopped += HandleGameStopped;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStopped -= HandleGameStopped;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    // Called when the game round ends.
    private void HandleGameStopped()
    {
        SaveCurrent();
    }

    // Safety net: also save when the application closes.
    private void OnApplicationQuit()
    {
        if (IsOwner) SaveCurrent();
    }

    private void SaveCurrent()
    {
        if (SaveManager.Instance == null) return;

        var data = new SaveData
        {
            gold              = _currency?.Gold ?? 0,
            equippedWeaponId  = _equipment?.EquippedWeapon?.itemId ?? "",
        };

        if (_inventory != null)
            foreach (var item in _inventory.Items)
                data.inventoryItemIds.Add(item.itemId);

        SaveManager.Instance.Save(data);
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    private void ApplySaveData(SaveData data)
    {
        // Inventory is local — populate directly on the owning client.
        if (_inventory != null && _itemRegistry != null && data.inventoryItemIds != null)
        {
            foreach (var id in data.inventoryItemIds)
            {
                var item = _itemRegistry.GetById(id);
                if (item != null)
                    _inventory.AddItem(item);
                else
                    Debug.LogWarning($"[PlayerSaveHandler] Unknown item id '{id}' — skipped.");
            }
        }

        // Gold is server-authoritative — send via ServerRpc.
        if (data.gold > 0)
            LoadGoldServerRpc(data.gold);

        // Equipment goes through PlayerEquipment which sends its own ServerRpc.
        if (!string.IsNullOrEmpty(data.equippedWeaponId) && _itemRegistry != null)
        {
            var weapon = _itemRegistry.GetById(data.equippedWeaponId);
            if (weapon != null)
                _equipment?.EquipWeapon(weapon);
            else
                Debug.LogWarning($"[PlayerSaveHandler] Saved weapon id '{data.equippedWeaponId}' not found in registry.");
        }

        Debug.Log($"[PlayerSaveHandler] '{name}' save data applied — " +
                  $"gold:{data.gold} items:{data.inventoryItemIds?.Count ?? 0} " +
                  $"weapon:'{data.equippedWeaponId}'");
    }

    // Server applies initial gold from the owner's save file.
    [ServerRpc]
    private void LoadGoldServerRpc(int gold)
    {
        _currency?.AddGold(gold);
    }
}
