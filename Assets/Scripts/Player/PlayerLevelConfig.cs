using UnityEngine;

/// <summary>
/// ScriptableObject that defines the player leveling curve and per-level-up bonuses.
/// One asset shared across all player instances — change values here to tune the game feel.
///
/// Usage:
///   Create via Assets → Create → Arcon → Player → Level Config
///   Assign the asset to PlayerLevelSystem on the Player prefab.
/// </summary>
[CreateAssetMenu(menuName = "Arcon/Player/Level Config", fileName = "PlayerLevelConfig")]
public class PlayerLevelConfig : ScriptableObject
{
    [Header("Level Settings")]
    [Tooltip("Maximum level the player can reach. Must match the length of expPerLevel array.")]
    [Min(2)]
    public int maxLevel = 10;

    [Header("EXP Requirements")]
    [Tooltip("EXP required to advance each level.\n" +
             "Index 0 = EXP needed to go from level 1 to 2.\n" +
             "Index 1 = EXP needed to go from level 2 to 3, etc.\n" +
             "Array must contain (maxLevel - 1) entries.\n\n" +
             "Tuned for 25 EXP per room:\n" +
             "  L1→2: 25  (1 room)\n" +
             "  L2→3: 50  (2 rooms)\n" +
             "  L3→4: 75  (3 rooms)\n" +
             "  L4→5: 100 (4 rooms)\n" +
             "  L5→6: 150 (6 rooms)\n" +
             "  L6→7: 200 (8 rooms)\n" +
             "  L7→8: 275 (11 rooms)\n" +
             "  L8→9: 375 (15 rooms)\n" +
             "  L9→10: 500 (20 rooms)")]
    public int[] expPerLevel = { 25, 50, 75, 100, 150, 200, 275, 375, 500 };

    [Header("Level-Up Bonuses")]
    [Tooltip("Percentage added to max HP on each level-up. 0.05 = 5%.")]
    [Range(0f, 0.5f)]
    public float maxHpBonusPercent = 0.05f;

    [Tooltip("Percentage added to attack damage on each level-up. 0.05 = 5%.")]
    [Range(0f, 0.5f)]
    public float damageBonusPercent = 0.05f;

    [Tooltip("Fraction of new max HP restored as healing on each level-up. 0.20 = 20%.")]
    [Range(0f, 1f)]
    public float healOnLevelUpPercent = 0.20f;

    // ── Public API ───────────────────────────────────────────────────────────

    // Returns EXP required to advance from currentLevel to the next level.
    // Returns int.MaxValue when already at max level (caller should guard with IsMaxLevel).
    public int GetExpRequired(int currentLevel)
    {
        int index = currentLevel - 1;

        if (index < 0 || index >= expPerLevel.Length)
        {
            // Level is at or beyond the cap — no more EXP needed
            return int.MaxValue;
        }

        return expPerLevel[index];
    }

    // Validates the config in the Editor — catches array/maxLevel mismatches early.
    private void OnValidate()
    {
        int expected = maxLevel - 1;
        if (expPerLevel != null && expPerLevel.Length != expected)
        {
            Debug.LogWarning(
                $"[PlayerLevelConfig] '{name}': expPerLevel has {expPerLevel.Length} entries " +
                $"but maxLevel={maxLevel} requires {expected} entries. " +
                $"Resize the array to avoid missing or unused thresholds.");
        }
    }
}
