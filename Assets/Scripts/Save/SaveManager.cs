using UnityEngine;

// Pure data persistence layer — reads and writes SaveData to PlayerPrefs via JSON.
// Does not know about players or game flow; callers decide when to load/save.
// Singleton so any system can reach it without a scene reference.
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SaveKey = "ArconSave_v1";

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public bool HasSaveData => PlayerPrefs.HasKey(SaveKey);

    // Serializes SaveData to JSON and writes it to PlayerPrefs.
    public void Save(SaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log($"[SaveManager] Saved — gold:{data.gold} " +
                  $"items:{data.inventoryItemIds?.Count ?? 0} " +
                  $"weapon:'{data.equippedWeaponId}'");
    }

    // Returns deserialized SaveData from PlayerPrefs, or a fresh default if none exists.
    public SaveData Load()
    {
        if (!HasSaveData)
        {
            Debug.Log("[SaveManager] No save found — returning fresh SaveData.");
            return new SaveData();
        }

        string json = PlayerPrefs.GetString(SaveKey);
        var data = JsonUtility.FromJson<SaveData>(json);
        Debug.Log($"[SaveManager] Loaded — gold:{data.gold} " +
                  $"items:{data.inventoryItemIds?.Count ?? 0} " +
                  $"weapon:'{data.equippedWeaponId}'");
        return data;
    }

    // Wipes all save data. Useful for testing or a "new game" button.
    public void DeleteSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        Debug.Log("[SaveManager] Save data deleted.");
    }
}
