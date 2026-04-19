using UnityEngine;

// Defines a single item that can be equipped by the player.
// Create assets via: Assets → Create → Arcon/Item/Item Definition
[CreateAssetMenu(menuName = "Arcon/Item/Item Definition", fileName = "NewItem")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique string key used in save/load and logs.")]
    public string itemId;

    [Header("Display")]
    public string displayName = "New Item";

    [TextArea(1, 3)]
    public string description = "Describe what this item does.";

    public Sprite icon;

    [Header("Slot & Rarity")]
    public EquipmentSlot slot = EquipmentSlot.Weapon;

    [Tooltip("Visual rarity tier. No gameplay effect yet.")]
    public ItemRarity rarity = ItemRarity.Common;

    [Header("Stat Modifiers")]
    [Tooltip("Stat bonuses granted while this item is equipped.")]
    public ItemStatModifier[] statModifiers = System.Array.Empty<ItemStatModifier>();

    [Header("Weapon Override")]
    [Tooltip("If assigned, equipping this item swaps the player's attack to this definition.")]
    public AttackDefinition attackOverride;

    [Header("Element")]
    [Tooltip("Element affinity of this item. No gameplay effect yet.")]
    public ElementType elementAffinity = ElementType.None;
}
