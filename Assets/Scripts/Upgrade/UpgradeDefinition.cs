using UnityEngine;

// High-level category used to group and filter upgrades in the selection UI.
public enum UpgradeCategory
{
    Attack,   // Offensive upgrades (damage, attack speed, etc.)
    Defense,  // Defensive upgrades (armour, block, etc.)
    Health,   // HP-related upgrades (max HP, healing, etc.)
    Utility,  // Movement, cooldown, and other miscellaneous upgrades
    Reaction, // Upgrades that modify or amplify elemental reaction effects
}

// The type of stat effect this upgrade applies when chosen.
// Each value maps directly to a handler case in PlayerUpgradeHandler.
public enum UpgradeEffectType
{
    MaxHpFlat,          // Adds a flat amount to the player's max HP
    HealPercent,        // Heals a percentage of the player's max HP (value = 0.5 → 50%)
    DamagePercent,      // Multiplies the current damage multiplier by (1 + value): e.g. value=0.10 → +10%
    MoveSpeedFlat,      // Adds a flat bonus to the player's move speed
    MoveSpeedPercent,   // Multiplies current speed by (1 + value): e.g. value=0.10 → +10%
    AttackSpeedPercent, // Reduces attack cooldown by 'value' fraction: value=0.20 → 20% faster attacks
    ProjectileSplit,    // On enemy hit, splits the projectile into 3: forward, +value°, -value° (value = angle in degrees, e.g. 45)
    BlastReaction,      // On any reaction, AoE-damages all enemies within value units (value = blast radius, e.g. 2)
    LifeSteal,          // Each projectile hit heals the firing player by value% of the damage dealt (value = fraction, e.g. 0.1 = 10%)
}

// Defines a single upgrade option that can appear in the upgrade selection dialog.
// Create assets via: Assets → Create → Arcon/Upgrade/Upgrade Definition
[CreateAssetMenu(menuName = "Arcon/Upgrade/Upgrade Definition", fileName = "NewUpgrade")]
public class UpgradeDefinition : ScriptableObject
{
    [Header("Display")]
    [Tooltip("Short name shown on the upgrade button (e.g. 'Iron Will').")]
    public string upgradeName = "New Upgrade";

    [Tooltip("One-line description shown below the upgrade name on the button.")]
    [TextArea(1, 3)]
    public string description = "Describe what this upgrade does.";

    [Tooltip("Icon shown on the upgrade button. Leave empty to hide the icon slot.")]
    public Sprite icon;

    [Header("Category")]
    [Tooltip("High-level group for this upgrade (used for display filtering).")]
    public UpgradeCategory category = UpgradeCategory.Utility;

    [Header("Effect")]
    [Tooltip("Which stat this upgrade improves.")]
    public UpgradeEffectType effectType = UpgradeEffectType.MaxHpFlat;

    [Tooltip("Prefab spawned by upgrades that create a world object on activation " +
             "(e.g. BlastReaction spawns a ReactionExplosion). Leave empty for stat-only upgrades.")]
    public GameObject effectPrefab;

    [Header("Stacking")]
    [Tooltip("If false, this upgrade is removed from the pool after being chosen once. " +
             "Stat upgrades (damage, HP, speed) are usually stackable; " +
             "behaviour upgrades (Shotgun, BlastReaction) are usually not.")]
    public bool stackable = true;

    [Tooltip("Magnitude of the effect. Meaning depends on effectType:\n" +
             "  MaxHpFlat          → flat HP added (e.g. 20)\n" +
             "  HealPercent        → fraction of max HP healed (e.g. 0.5 = 50%)\n" +
             "  DamagePercent      → fraction added to damage multiplier (e.g. 0.10 = +10%)\n" +
             "  MoveSpeedFlat      → flat units/sec added to move speed (e.g. 1.0)\n" +
             "  MoveSpeedPercent   → fraction added multiplicatively (e.g. 0.10 = +10%)\n" +
             "  AttackSpeedPercent → fraction by which cooldown is reduced (e.g. 0.20 = 20% faster)\n" +
             "  ProjectileSplit    → angle in degrees between split projectiles (e.g. 45)\n" +
             "  BlastReaction      → blast radius in units around the reaction position (e.g. 2)\n" +
             "  LifeSteal          → fraction of damage dealt healed per projectile hit (e.g. 0.1 = 10%)")]
    [Min(0f)]
    public float value = 1f;
}
