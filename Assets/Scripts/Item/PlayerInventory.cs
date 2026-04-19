using System.Collections.Generic;
using UnityEngine;

// Stores the player's owned items locally. No networking — inventory is private per-player.
// PlayerEquipment reads from this to validate equip requests.
public class PlayerInventory : MonoBehaviour
{
    private readonly List<ItemDefinition> _items = new();

    public IReadOnlyList<ItemDefinition> Items => _items;

    public void AddItem(ItemDefinition item)
    {
        if (item == null) return;
        _items.Add(item);
        Debug.Log($"[PlayerInventory] '{name}' received: {item.displayName}");
    }

    // Returns true if the item was found and removed.
    public bool RemoveItem(ItemDefinition item)
    {
        bool removed = _items.Remove(item);
        if (removed)
            Debug.Log($"[PlayerInventory] '{name}' removed: {item.displayName}");
        return removed;
    }

    public bool HasItem(ItemDefinition item) => _items.Contains(item);
}
