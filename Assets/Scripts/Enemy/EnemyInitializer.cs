using Unity.Netcode;
using UnityEngine;

// Bootstraps an enemy instance from its EnemyData asset and an assigned level.
// Runs on the server once the enemy spawns and pushes scaled stats into other components.
//
// This keeps EnemyData as the single source of truth for base stats and scaling config.
// EnemyInitializer owns the per-instance level — different enemies of the same type
// can be at different levels (e.g. room 10 mixing level 8 and level 12 enemies).
//
// Usage by a spawn system:
//   var go  = Instantiate(enemyPrefab);
//   go.GetComponent<EnemyInitializer>().SetLevel(12);
//   go.GetComponent<NetworkObject>().Spawn();
//
// If SetLevel() is not called, the enemy defaults to level 1 (base stats).
public class EnemyInitializer : NetworkBehaviour
{
    [Header("Data")]
    [Tooltip("The enemy type definition. Assign the matching EnemyData asset " +
             "in each prefab variant (e.g. GoblinData for the Goblin variant).")]
    [SerializeField] private EnemyData enemyData;

    [Header("Level")]
    [Tooltip("Starting level for this enemy instance. Can be overridden before spawn " +
             "via SetLevel() — e.g. by a room spawner to mix levels within a room.")]
    [SerializeField] [Min(1)] private int level = 1;

    // ── Cached component references ───────────────────────────────────────────

    private Health              _health;
    private AttackController    _attackController;
    private EnemyMovementBase   _movement;
    private ContactDamageDealer _contactDamageDealer; // optional — not all enemies use contact damage
    private EnemyNameplate      _nameplate;           // optional — updates name/level label after stat apply

    // ── Read-only properties ─────────────────────────────────────────────────

    // Returns the EnemyData for this enemy — useful for loot, room manager, etc.
    public EnemyData EnemyData => enemyData;

    // Current level of this enemy instance.
    public int Level => level;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health              = GetComponent<Health>();
        _attackController    = GetComponent<AttackController>();
        // Use the first ENABLED EnemyMovementBase — prefab variants (e.g. Wraith) disable the
        // base GroundEnemyMovement and add their own component, so GetComponent() alone would
        // return the disabled base component and skip initialization entirely.
        var allMovements = GetComponents<EnemyMovementBase>();
        _movement = System.Array.Find(allMovements, m => m.enabled) ?? (allMovements.Length > 0 ? allMovements[0] : null);
        _contactDamageDealer = GetComponent<ContactDamageDealer>(); // optional
        _nameplate           = GetComponent<EnemyNameplate>();       // optional

        if (enemyData == null)
            Debug.LogError($"[EnemyInitializer] '{name}' has no EnemyData assigned.", this);
    }

    // Called by NGO on spawn — only server applies stats.
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (enemyData == null) return;

        ApplyStats();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    // Sets the level for this enemy instance.
    // Call this BEFORE spawning the NetworkObject so stats are applied on OnNetworkSpawn.
    // If called after spawn (already on network), stats are re-applied immediately.
    // Levels below 1 are clamped to 1 (base stats).
    public void SetLevel(int newLevel)
    {
        level = Mathf.Max(1, newLevel);
        Debug.Log($"[EnemyInitializer] '{name}' level set to {level}.");

        // Re-apply stats if already spawned (hot swap for runtime testing).
        if (IsSpawned && IsServer)
            ApplyStats();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    // Computes level-scaled stats from EnemyData and pushes them into the
    // relevant components. All stat scaling lives in EnemyData.ComputeStats().
    private void ApplyStats()
    {
        ScaledEnemyStats stats = enemyData.ComputeStats(level);

        Debug.Log($"[EnemyInitializer] '{name}' applying level {level} stats — " +
                  $"HP:{stats.maxHealth} SPD:{stats.moveSpeed:F2} " +
                  $"DMGx:{stats.damageMultiplier:F2} CDx:{stats.cooldownMultiplier:F2}");

        // Health
        if (_health != null)
            _health.SetMaxHealth(stats.maxHealth);
        else
            Debug.LogError($"[EnemyInitializer] '{name}' missing Health component.", this);

        // AttackController — assign definition first, then apply level multipliers.
        if (_attackController != null)
        {
            if (enemyData.AttackDefinition != null)
                _attackController.SetAttackDefinition(enemyData.AttackDefinition, resetCooldown: true);
            else
                Debug.LogWarning($"[EnemyInitializer] '{name}' EnemyData has no AttackDefinition.", this);

            _attackController.SetDamageMultiplier(stats.damageMultiplier);
            _attackController.SetCooldownMultiplier(stats.cooldownMultiplier);
        }
        else
        {
            Debug.LogError($"[EnemyInitializer] '{name}' missing AttackController component.", this);
        }

        // Movement — initialize from data first (sets AttackRange, base speed),
        // then override with the level-scaled speed.
        if (_movement != null)
        {
            _movement.Initialize(enemyData);
            _movement.SetMoveSpeed(stats.moveSpeed);
        }
        else
        {
            Debug.LogError($"[EnemyInitializer] '{name}' missing EnemyMovementBase component.", this);
        }

        // ContactDamageDealer — optional, only present on contact-attack enemies.
        if (_contactDamageDealer != null)
            _contactDamageDealer.SetDamageMultiplier(stats.damageMultiplier);

        // Nameplate — refresh name + level label after stats are applied so it shows the correct level.
        _nameplate?.Refresh();
    }
}
