using Unity.Netcode;
using UnityEngine;

// Bootstraps an enemy instance from its EnemyData asset.
// Runs on the server once the enemy spawns and pushes stats into other components.
//
// This keeps EnemyData as the single source of truth for all per-type stats.
// Changing a value in the asset immediately affects every instance of that enemy type.
public class EnemyInitializer : NetworkBehaviour
{
    [Header("Data")]
    [Tooltip("The enemy type definition. Assign the matching EnemyData asset " +
             "in each prefab variant (e.g. GoblinData for the Goblin variant).")]
    [SerializeField] private EnemyData enemyData;

    private Health            _health;
    private AttackController  _attackController;
    private EnemyMovementBase _movement;

    // Returns the EnemyData for this enemy — useful for loot, room manager, etc.
    public EnemyData EnemyData => enemyData;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health           = GetComponent<Health>();
        _attackController = GetComponent<AttackController>();
        _movement         = GetComponent<EnemyMovementBase>(); // works for Ground or Flying

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

    // ── Private ──────────────────────────────────────────────────────────────

    // Pushes all stats from EnemyData into the relevant components.
    private void ApplyStats()
    {
        // Health
        if (_health != null)
            _health.SetMaxHealth(enemyData.MaxHealth);
        else
            Debug.LogError($"[EnemyInitializer] '{name}' missing Health component.", this);

        // AttackController
        if (_attackController != null)
        {
            if (enemyData.AttackDefinition != null)
                _attackController.SetAttackDefinition(enemyData.AttackDefinition, resetCooldown: true);
            else
                Debug.LogWarning($"[EnemyInitializer] '{name}' EnemyData has no AttackDefinition.", this);
        }
        else
        {
            Debug.LogError($"[EnemyInitializer] '{name}' missing AttackController component.", this);
        }

        // Movement — works for both GroundEnemyMovement and FlyingEnemyMovement
        if (_movement != null)
            _movement.Initialize(enemyData);
        else
            Debug.LogError($"[EnemyInitializer] '{name}' missing EnemyMovementBase component.", this);

        Debug.Log($"[EnemyInitializer] '{name}' initialized as '{enemyData.EnemyName}'.");
    }
}
