using System.Collections.Generic;
using UnityEngine;

// Holds the full set of upgrades available in the game.
// At level-up, PlayerUpgradeHandler asks this pool for a random subset to present to the player.
//
// Create via: Assets → Create → Arcon/Upgrade/Upgrade Pool
// Assign the asset to PlayerUpgradeHandler on the Player prefab.
[CreateAssetMenu(menuName = "Arcon/Upgrade/Upgrade Pool", fileName = "UpgradePool")]
public class UpgradePool : ScriptableObject
{
    [Tooltip("All available upgrade definitions. PlayerUpgradeHandler picks a random subset at level-up.")]
    public UpgradeDefinition[] upgrades = System.Array.Empty<UpgradeDefinition>();

    // Returns 'count' distinct upgrades chosen at random from the pool.
    // Non-stackable upgrades already in 'acquired' are excluded from the selection.
    // If the pool has fewer eligible entries than 'count', all eligible entries are returned.
    public UpgradeDefinition[] GetRandomSelection(int count, HashSet<UpgradeDefinition> acquired = null)
    {
        if (upgrades == null || upgrades.Length == 0)
            return System.Array.Empty<UpgradeDefinition>();

        // Build a filtered list: exclude non-stackable upgrades the player already owns
        var available = new List<UpgradeDefinition>();
        for (int i = 0; i < upgrades.Length; i++)
        {
            var u = upgrades[i];
            if (u == null) continue;

            bool alreadyOwned = acquired != null && acquired.Contains(u);
            if (!u.stackable && alreadyOwned)
                continue;

            available.Add(u);
        }

        if (available.Count == 0)
            return System.Array.Empty<UpgradeDefinition>();

        count = Mathf.Min(count, available.Count);
        var selected = new UpgradeDefinition[count];

        for (int i = 0; i < count; i++)
        {
            int index   = Random.Range(0, available.Count);
            selected[i] = available[index];
            available.RemoveAt(index);
        }

        return selected;
    }

    // Returns the index of the given upgrade in the pool, or -1 if not found.
    // Used by PlayerUpgradeHandler to send the pool index (not a reference) over the network.
    public int IndexOf(UpgradeDefinition upgrade)
    {
        if (upgrade == null) return -1;

        for (int i = 0; i < upgrades.Length; i++)
            if (upgrades[i] == upgrade) return i;

        return -1;
    }
}
