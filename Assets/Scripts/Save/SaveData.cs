using System;
using System.Collections.Generic;

// Persistent player data that survives across runs.
// Serialized to JSON via JsonUtility and stored in PlayerPrefs.
[Serializable]
public class SaveData
{
    public int gold = 0;
    public List<string> inventoryItemIds = new();  // itemId of each owned item
    public string equippedWeaponId = "";           // itemId of equipped weapon ("" = none)
}
