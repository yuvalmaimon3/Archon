// Equipment slot — determines where an item can be equipped.
public enum EquipmentSlot
{
    Weapon, // Only slot for now
}

// Visual rarity tier — field exists on ItemDefinition but has no gameplay effect yet.
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
}

// Stat effect an item applies when equipped.
// Each value maps to a handler in ItemEffectRegistry.
public enum ItemEffectType
{
    MaxHpFlat,          // Adds flat HP to max HP
    DamagePercent,      // Multiplies damage multiplier by (1 + value): 0.10 → +10%
    AttackSpeedPercent, // Reduces attack cooldown by value fraction: 0.20 → 20% faster
    MoveSpeedFlat,      // Adds flat units/sec to move speed
    MoveSpeedPercent,   // Multiplies move speed by (1 + value): 0.10 → +10%
    CritChance,         // Adds flat crit chance: 0.10 → +10%
    CritDamage,         // Adds flat crit damage bonus: 0.25 → +25%
}
