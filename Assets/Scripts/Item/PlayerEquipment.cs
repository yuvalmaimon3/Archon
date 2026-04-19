using Unity.Netcode;
using UnityEngine;

// Manages the player's equipped items. Applies and reverses stat bonuses on equip/unequip.
//
// NetworkBehaviour: equipped state must be visible to all clients — weapon swaps
// affect attack visuals and damage is server-authoritative.
//
// Network split:
//   Owner     → sends EquipWeaponServerRpc / UnequipWeaponServerRpc
//   Server    → applies/reverses stat changes, updates _equippedWeaponIndex
//   All clients → read _equippedWeaponIndex for future UI and animation sync
//
// Stat removal uses delta-reversal: each modifier is reversed individually
// (divide multiplicative bonuses, subtract additive bonuses). This works
// correctly for the single weapon slot — full recomputation can be added
// later if multiple slots or stacking edge cases require it.
public class PlayerEquipment : NetworkBehaviour
{
    [Header("Registry")]
    [Tooltip("Shared ItemRegistry asset — required for network index lookup.")]
    [SerializeField] private ItemRegistry _itemRegistry;

    // Synced index into ItemRegistry. -1 = nothing equipped.
    private readonly NetworkVariable<int> _equippedWeaponIndex = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Component references ─────────────────────────────────────────────────

    private PlayerInventory           _inventory;
    private PlayerUpgradeHandler      _upgradeHandler;
    private Health                    _health;
    private NetworkHealthSync         _healthSync;
    private AttackController[]        _attackControllers;
    private PlayerMovement            _movement;
    private PlayerCritHandler         _critHandler;
    private PlayerProjectileModifiers _projectileModifiers;

    // Original attack per controller — restored when a weapon item is unequipped.
    private AttackDefinition[] _originalAttackDefs;

    // Equipped weapon definition on the server. Null = nothing equipped.
    private ItemDefinition _equippedWeapon;

    // ── Read-only ────────────────────────────────────────────────────────────

    public ItemDefinition EquippedWeapon    => _equippedWeapon;
    public bool           HasWeaponEquipped => _equippedWeapon != null;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _inventory           = GetComponent<PlayerInventory>();
        _upgradeHandler      = GetComponent<PlayerUpgradeHandler>();
        _health              = GetComponent<Health>();
        _healthSync          = GetComponent<NetworkHealthSync>();
        _attackControllers   = GetComponents<AttackController>();
        _movement            = GetComponent<PlayerMovement>();
        _critHandler         = GetComponent<PlayerCritHandler>();
        _projectileModifiers = GetComponent<PlayerProjectileModifiers>();

        // Snapshot original attack definitions so we can restore them on unequip.
        _originalAttackDefs = new AttackDefinition[_attackControllers.Length];
        for (int i = 0; i < _attackControllers.Length; i++)
            _originalAttackDefs[i] = _attackControllers[i].AttackDefinition;
    }

    // ── Public API (owner-callable) ──────────────────────────────────────────

    // Equip an item by its registry index. Call from the owning client.
    public void EquipWeapon(int itemIndex)
    {
        if (!IsOwner) return;
        EquipWeaponServerRpc(itemIndex);
    }

    // Convenience overload — resolves registry index automatically.
    public void EquipWeapon(ItemDefinition item)
    {
        if (!IsOwner || _itemRegistry == null) return;
        int index = _itemRegistry.IndexOf(item);
        if (index < 0)
        {
            Debug.LogWarning($"[PlayerEquipment] '{name}' item '{item.displayName}' not in registry.");
            return;
        }
        EquipWeaponServerRpc(index);
    }

    // Unequip the current weapon. Call from the owning client.
    public void UnequipWeapon()
    {
        if (!IsOwner) return;
        UnequipWeaponServerRpc();
    }

    // ── ServerRpcs ────────────────────────────────────────────────────────────

    [ServerRpc]
    private void EquipWeaponServerRpc(int itemIndex)
    {
        if (_itemRegistry == null)
        {
            Debug.LogError($"[PlayerEquipment] '{name}' — no ItemRegistry assigned.");
            return;
        }

        ItemDefinition item = _itemRegistry.GetByIndex(itemIndex);
        if (item == null || item.slot != EquipmentSlot.Weapon)
        {
            Debug.LogWarning($"[PlayerEquipment] '{name}' invalid weapon index {itemIndex}.");
            return;
        }

        // Replace any currently equipped weapon before applying the new one.
        if (_equippedWeapon != null)
            RemoveWeaponStats(_equippedWeapon);

        _equippedWeapon = item;
        _equippedWeaponIndex.Value = itemIndex;
        ApplyWeaponStats(item);

        Debug.Log($"[PlayerEquipment] '{name}' equipped: {item.displayName}");
    }

    [ServerRpc]
    private void UnequipWeaponServerRpc()
    {
        if (_equippedWeapon == null) return;

        Debug.Log($"[PlayerEquipment] '{name}' unequipped: {_equippedWeapon.displayName}");

        RemoveWeaponStats(_equippedWeapon);
        _equippedWeapon = null;
        _equippedWeaponIndex.Value = -1;
    }

    // ── Stat application ──────────────────────────────────────────────────────

    private void ApplyWeaponStats(ItemDefinition item)
    {
        var ctx = BuildContext();
        ItemEffectRegistry.ApplyAll(item, ctx);
        _projectileModifiers = ctx.ProjectileModifiers;

        if (item.attackOverride != null)
        {
            foreach (var ac in _attackControllers)
                ac.SetAttackDefinition(item.attackOverride);
            Debug.Log($"[PlayerEquipment] '{name}' attack → '{item.attackOverride.AttackId}'");
        }
    }

    // Reverses all stat bonuses granted by an item — called on unequip or weapon swap.
    private void RemoveWeaponStats(ItemDefinition item)
    {
        foreach (var modifier in item.statModifiers)
            ReverseModifier(modifier);

        if (item.attackOverride != null)
        {
            for (int i = 0; i < _attackControllers.Length; i++)
            {
                _attackControllers[i].SetAttackDefinition(_originalAttackDefs[i]);
                Debug.Log($"[PlayerEquipment] '{name}' attack restored on '{_attackControllers[i].gameObject.name}'");
            }
        }
    }

    // Reverses a single stat modifier — mirrors each handler in ItemEffectRegistry.
    private void ReverseModifier(ItemStatModifier modifier)
    {
        switch (modifier.effectType)
        {
            case ItemEffectType.MaxHpFlat:
                int newMax = Mathf.Max(1, _health.MaxHealth - (int)modifier.value);
                _health.AdjustMaxHealth(newMax);
                _healthSync?.UpdateSyncedMaxHealth(newMax);
                Debug.Log($"[Item Remove] MaxHP -{(int)modifier.value} → {newMax}");
                break;

            case ItemEffectType.DamagePercent:
                foreach (var ac in _attackControllers)
                {
                    float newMult = ac.DamageMultiplier / (1f + modifier.value);
                    ac.SetDamageMultiplier(Mathf.Max(0f, newMult));
                    Debug.Log($"[Item Remove] Damage ×{newMult:F3} on '{ac.gameObject.name}'");
                }
                break;

            case ItemEffectType.AttackSpeedPercent:
                foreach (var ac in _attackControllers)
                {
                    float divisor = 1f - modifier.value;
                    if (divisor <= 0f) break; // guard against >= 100% reduction
                    float newMult = ac.CooldownMultiplier / divisor;
                    ac.SetCooldownMultiplier(newMult);
                    Debug.Log($"[Item Remove] Attack speed -{modifier.value * 100f:F0}% on '{ac.gameObject.name}' → cooldown ×{newMult:F3}");
                }
                break;

            case ItemEffectType.MoveSpeedFlat:
                _movement?.AddSpeedBonus(-modifier.value);
                Debug.Log($"[Item Remove] Move speed -{modifier.value}");
                break;

            case ItemEffectType.MoveSpeedPercent:
                _movement?.RemoveSpeedMultiplier(modifier.value);
                Debug.Log($"[Item Remove] Move speed -{modifier.value * 100f:F0}%");
                break;

            case ItemEffectType.CritChance:
                _critHandler?.AddCritChance(-modifier.value);
                Debug.Log($"[Item Remove] Crit chance -{modifier.value * 100f:F1}%");
                break;

            case ItemEffectType.CritDamage:
                _critHandler?.AddCritDamage(-modifier.value);
                Debug.Log($"[Item Remove] Crit damage -{modifier.value * 100f:F0}%");
                break;
        }
    }

    private UpgradeContext BuildContext() => new()
    {
        GameObject          = gameObject,
        Health              = _health,
        HealthSync          = _healthSync,
        AttackControllers   = _attackControllers,
        Movement            = _movement,
        ProjectileModifiers = _projectileModifiers,
        CritHandler         = _critHandler,
    };
}
