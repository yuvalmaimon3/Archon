using UnityEngine;

// Tracks the player's critical hit stats and performs crit rolls.
// Crit is rolled once per base attack; the critted damage is baked into DamageInfo.Amount
// so reactions and upgrade effects automatically inherit the bonus.
//
// Network: MonoBehaviour — crit stats are per-player but do not need to be synced
// to all clients. The server (which owns damage authority for projectiles) reads this
// component directly on the player's NetworkObject.
public class PlayerCritHandler : MonoBehaviour
{
    [Header("Crit Stats")]
    [Tooltip("Base chance for an attack to be a critical hit (0–1).")]
    [SerializeField] private float baseCritChance = 0.50f;   // 50%

    [Tooltip("Bonus damage multiplier on a critical hit. 0.5 = +50% damage.")]
    [SerializeField] private float baseCritDamage = 0.50f;   // +50%

    // Current runtime values — start at base, modified by upgrades later.
    private float _critChance;
    private float _critDamage;

    public float CritChance    => _critChance;
    public float CritDamage    => _critDamage;

    // Total multiplier applied to damage on a crit (e.g. 1.5 at +50% crit damage).
    public float CritMultiplier => 1f + _critDamage;

    private void Awake()
    {
        _critChance = baseCritChance;
        _critDamage = baseCritDamage;
    }

    // Returns true if this attack is a critical hit. Call once per attack chain.
    public bool RollCrit() => Random.value < _critChance;

    // ── Upgrade hooks (for future crit upgrades) ─────────────────────────────

    public void AddCritChance(float amount)
    {
        _critChance = Mathf.Clamp01(_critChance + amount);
        Debug.Log($"[PlayerCritHandler] {gameObject.name} crit chance → {_critChance * 100f:F1}%");
    }

    public void AddCritDamage(float amount)
    {
        _critDamage = Mathf.Max(0f, _critDamage + amount);
        Debug.Log($"[PlayerCritHandler] {gameObject.name} crit damage → +{_critDamage * 100f:F0}%");
    }

    // Resets crit stats to their inspector base values.
    // Used for full stat recomputation (e.g. re-applying all upgrades from scratch).
    public void ResetCritStats()
    {
        _critChance = baseCritChance;
        _critDamage = baseCritDamage;
        Debug.Log($"[PlayerCritHandler] {gameObject.name} crit stats reset to base — " +
                  $"chance:{_critChance * 100f:F1}% damage:+{_critDamage * 100f:F0}%");
    }
}
