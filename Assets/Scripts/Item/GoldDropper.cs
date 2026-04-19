using Unity.Netcode;
using UnityEngine;

// Awards gold to the killing player when this enemy dies.
// Server-only: subscribes to Health.OnDeath and calls PlayerCurrency.AddGold.
// Add this component to enemy prefabs alongside a LootTable asset.
[RequireComponent(typeof(Health))]
public class GoldDropper : MonoBehaviour
{
    [SerializeField] private LootTable _lootTable;

    private Health _health;

    private void Awake() => _health = GetComponent<Health>();

    private void OnEnable()  => _health.OnDeath += HandleDeath;
    private void OnDisable() => _health.OnDeath -= HandleDeath;

    private void HandleDeath(DamageInfo killingBlow)
    {
        // In networked sessions only the server runs this — PlayerCurrency is server-authoritative.
        // In standalone (no NetworkManager) we always run.
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworked && !NetworkManager.Singleton.IsServer) return;

        if (_lootTable == null)
        {
            Debug.LogWarning($"[GoldDropper] '{name}' has no LootTable assigned — no gold dropped.");
            return;
        }

        int gold = _lootTable.RollGold();
        if (gold <= 0) return;

        // DamageInfo.Source is the player GameObject (set by Projectile / melee executors).
        GameObject source = killingBlow.Source;
        if (source == null)
        {
            Debug.Log($"[GoldDropper] '{name}' — no kill source, gold lost.");
            return;
        }

        var currency = source.GetComponent<PlayerCurrency>();
        if (currency == null)
        {
            Debug.Log($"[GoldDropper] '{name}' — source '{source.name}' has no PlayerCurrency.");
            return;
        }

        currency.AddGold(gold);
        Debug.Log($"[GoldDropper] '{name}' dropped {gold} gold → '{source.name}'");
    }
}
