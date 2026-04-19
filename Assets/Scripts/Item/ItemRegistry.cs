using System.Collections.Generic;
using UnityEngine;

// Ordered catalogue of all ItemDefinitions in the game.
// Provides string-ID lookup (save/load) and integer index lookup (networking).
// Create a single registry asset: Assets → Create → Arcon/Item/Item Registry
[CreateAssetMenu(menuName = "Arcon/Item/Item Registry", fileName = "ItemRegistry")]
public class ItemRegistry : ScriptableObject
{
    [Tooltip("All items in the game. Array index is the network-safe reference.")]
    public ItemDefinition[] items = System.Array.Empty<ItemDefinition>();

    // Built lazily to avoid repeated linear scans.
    private Dictionary<string, ItemDefinition> _idLookup;

    // Returns the item at the given index, or null if out of range.
    public ItemDefinition GetByIndex(int index)
    {
        if (index < 0 || index >= items.Length) return null;
        return items[index];
    }

    // Returns the network-safe index of an item, or -1 if not found.
    public int IndexOf(ItemDefinition item)
    {
        for (int i = 0; i < items.Length; i++)
            if (items[i] == item) return i;
        return -1;
    }

    // Returns the item matching the given ID, or null if not found.
    public ItemDefinition GetById(string id)
    {
        BuildLookupIfNeeded();
        _idLookup.TryGetValue(id, out var item);
        return item;
    }

    private void BuildLookupIfNeeded()
    {
        if (_idLookup != null) return;

        _idLookup = new Dictionary<string, ItemDefinition>(items.Length);
        foreach (var item in items)
        {
            if (item == null) continue;
            if (!_idLookup.TryAdd(item.itemId, item))
                Debug.LogWarning($"[ItemRegistry] Duplicate itemId '{item.itemId}' — second entry ignored.");
        }
    }
}
