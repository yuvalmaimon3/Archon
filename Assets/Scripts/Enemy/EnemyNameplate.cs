using TMPro;
using UnityEngine;

// Drives the name + level label shown above the enemy health bar.
// Reads data from EnemyInitializer and refreshes the TextMeshProUGUI text.
//
// Format displayed:  "Goblin  Lv.3"
//
// Refresh() is called:
//   - In Start() for immediate display in the editor and on clients.
//   - By EnemyInitializer.ApplyStats() on the server after level-scaled stats
//     are applied, so the label always reflects the actual active level.
//
// The label GameObject is found automatically by searching for "NameLabel"
// inside HealthbarCanvas, so no manual wiring is needed per-prefab.
public class EnemyNameplate : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("TextMeshPro inside HealthbarCanvas/NameLabel. " +
             "Left empty — auto-resolved from HealthbarCanvas child on Awake.")]
    [SerializeField] private TextMeshProUGUI nameText;

    // Cached reference to the initializer that owns the name and level data.
    private EnemyInitializer _initializer;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _initializer = GetComponent<EnemyInitializer>();

        // Auto-find the label if not wired in the Inspector.
        if (nameText == null)
            nameText = FindNameLabel();

        if (nameText == null)
            Debug.LogWarning($"[EnemyNameplate] '{name}' could not find a NameLabel TextMeshProUGUI in HealthbarCanvas.", this);
    }

    private void Start()
    {
        // Show the label immediately on first frame — covers the editor preview
        // and clients who receive the prefab before OnNetworkSpawn fires.
        Refresh();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    // Rebuilds the displayed text from the current EnemyData name and level.
    // Called by EnemyInitializer after stats are applied so the label stays in sync.
    public void Refresh()
    {
        if (nameText == null || _initializer == null) return;

        string enemyName = _initializer.EnemyData != null
            ? _initializer.EnemyData.EnemyName
            : gameObject.name;

        nameText.text = $"{enemyName}  Lv.{_initializer.Level}";

        Debug.Log($"[EnemyNameplate] '{name}' label updated → '{nameText.text}'");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    // Searches for HealthbarCanvas/NameLabel among direct children of this transform.
    // Returns the TextMeshProUGUI on that child, or null if not found.
    private TextMeshProUGUI FindNameLabel()
    {
        // HealthbarCanvas is a direct child of the enemy root.
        Transform hbc = transform.Find("HealthbarCanvas");
        if (hbc == null)
        {
            Debug.LogWarning($"[EnemyNameplate] '{name}' has no HealthbarCanvas child.", this);
            return null;
        }

        Transform label = hbc.Find("NameLabel");
        if (label == null)
        {
            Debug.LogWarning($"[EnemyNameplate] '{name}' HealthbarCanvas has no NameLabel child.", this);
            return null;
        }

        return label.GetComponent<TextMeshProUGUI>();
    }
}
