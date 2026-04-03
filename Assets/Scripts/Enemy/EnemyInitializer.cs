using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Bootstraps an enemy instance from its EnemyData asset.
/// Runs on the server immediately after the enemy spawns and pushes
/// stats (HP, attack definition) into the other components.
///
/// Responsibilities:
///   - Apply MaxHealth from EnemyData to Health
///   - Apply AttackDefinition from EnemyData to AttackController
///   - Pass EnemyData to EnemyMovement so it knows speed and attack range
///
/// This keeps EnemyData as the single source of truth for all per-type stats.
/// Changing a stat in the asset instantly affects every instance of that enemy.
/// </summary>
public class EnemyInitializer : NetworkBehaviour
{
    [Header("Data")]
    [Tooltip("The enemy type definition. Assign the correct EnemyData asset " +
             "in each prefab variant (e.g. GoblinData for the Goblin variant).")]
    [SerializeField] private EnemyData enemyData;

    // ── Cached component references ──────────────────────────────────────────

    private Health           _health;
    private AttackController _attackController;
    private EnemyMovement    _movement;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health           = GetComponent<Health>();
        _attackController = GetComponent<AttackController>();
        _movement         = GetComponent<EnemyMovement>();

        if (enemyData == null)
            Debug.LogError($"[EnemyInitializer] '{name}' has no EnemyData assigned.", this);
    }

    /// <summary>
    /// Called by NGO when this object enters the network session.
    /// Only the server initializes — clients receive state via sync components.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (enemyData == null) return;

        ApplyStats();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the EnemyData asset assigned to this enemy instance.
    /// Useful for other systems (e.g. loot, room manager) that need to read enemy type.
    /// </summary>
    public EnemyData EnemyData => enemyData;

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Pushes all stats from EnemyData into the relevant components.
    /// Called once on the server when the enemy spawns.
    /// </summary>
    private void ApplyStats()
    {
        // --- Health ---
        if (_health != null)
        {
            // SetMaxHealth resets current HP to the new max — safe to call at spawn before combat begins.
            _health.SetMaxHealth(enemyData.MaxHealth);
        }
        else
        {
            Debug.LogError($"[EnemyInitializer] '{name}' is missing a Health component.", this);
        }

        // --- AttackController ---
        if (_attackController != null)
        {
            if (enemyData.AttackDefinition != null)
                _attackController.SetAttackDefinition(enemyData.AttackDefinition, resetCooldown: true);
            else
                Debug.LogWarning($"[EnemyInitializer] '{name}': EnemyData has no AttackDefinition assigned.", this);
        }
        else
        {
            Debug.LogError($"[EnemyInitializer] '{name}' is missing an AttackController component.", this);
        }

        // --- EnemyMovement ---
        if (_movement != null)
            _movement.Initialize(enemyData);
        else
            Debug.LogError($"[EnemyInitializer] '{name}' is missing an EnemyMovement component.", this);

        Debug.Log($"[EnemyInitializer] '{name}' initialized as '{enemyData.EnemyName}'.");
    }
}
