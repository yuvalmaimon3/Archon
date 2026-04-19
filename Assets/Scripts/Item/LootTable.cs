using UnityEngine;

// Defines what drops when an enemy dies. Extension point for item drops, gold, etc.
// Create assets via: Assets → Create → Arcon/Item/Loot Table
[CreateAssetMenu(menuName = "Arcon/Item/Loot Table", fileName = "NewLootTable")]
public class LootTable : ScriptableObject
{
    [Header("Gold")]
    [Tooltip("Gold range dropped on death (inclusive). Set both to the same value for a fixed amount.")]
    [Min(0)] public int goldMin = 1;
    [Min(0)] public int goldMax = 5;

    // Returns a random gold amount within the configured range.
    public int RollGold() => Random.Range(goldMin, goldMax + 1);
}
