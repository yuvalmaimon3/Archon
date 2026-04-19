using Unity.Netcode;
using UnityEngine;

// Tracks the player's gold. Server-authoritative — NetworkVariable ensures
// all clients always see the correct amount (needed for future shop UI).
public class PlayerCurrency : NetworkBehaviour
{
    private readonly NetworkVariable<int> _gold = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int Gold => _gold.Value;

    public override void OnNetworkSpawn()
    {
        _gold.OnValueChanged += OnGoldChanged;
    }

    public override void OnNetworkDespawn()
    {
        _gold.OnValueChanged -= OnGoldChanged;
    }

    // Adds gold to this player. Must be called on the server (e.g. from GoldDropper).
    public void AddGold(int amount)
    {
        if (!IsServer) return;
        if (amount <= 0) return;

        _gold.Value += amount;
        Debug.Log($"[PlayerCurrency] '{name}' +{amount} gold → {_gold.Value} total");
    }

    // Deducts gold. Returns true if the player had enough and gold was spent.
    // Server-only — for future shop system.
    public bool SpendGold(int amount)
    {
        if (!IsServer || amount <= 0) return false;

        if (_gold.Value < amount)
        {
            Debug.Log($"[PlayerCurrency] '{name}' can't afford {amount} gold (has {_gold.Value})");
            return false;
        }

        _gold.Value -= amount;
        Debug.Log($"[PlayerCurrency] '{name}' spent {amount} gold → {_gold.Value} remaining");
        return true;
    }

    private void OnGoldChanged(int oldVal, int newVal)
    {
        Debug.Log($"[PlayerCurrency] '{name}' gold: {oldVal} → {newVal}");
    }
}
