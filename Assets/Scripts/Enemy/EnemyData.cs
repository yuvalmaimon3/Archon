using UnityEngine;

/// <summary>
/// ScriptableObject that defines the base stats for a single enemy type.
/// Create one asset per enemy type (e.g. GoblinData, SkeletonData).
///
/// Each enemy prefab (or prefab variant) references its own EnemyData asset,
/// so changing stats for one type does not affect others.
///
/// Create via: Assets → Create → Arcon → Enemy → Enemy Data
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Arcon/Enemy/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name of this enemy type (used in logs and future UI).")]
    [SerializeField] private string enemyName = "Enemy";

    [Header("Stats")]
    [Tooltip("Maximum hit points for this enemy type.")]
    [Min(1)]
    [SerializeField] private int maxHealth = 50;

    [Tooltip("Movement speed in units per second.")]
    [Min(0f)]
    [SerializeField] private float moveSpeed = 3f;

    [Tooltip("Distance at which the enemy stops chasing and starts attacking. " +
             "Should match (or be slightly less than) the AttackDefinition range.")]
    [Min(0f)]
    [SerializeField] private float attackRange = 2f;

    [Header("Combat")]
    [Tooltip("The attack definition used by this enemy type. " +
             "Assigned to the AttackController at runtime by EnemyInitializer.")]
    [SerializeField] private AttackDefinition attackDefinition;

    // ── Read-only properties ─────────────────────────────────────────────────

    /// <summary>Display name of this enemy type.</summary>
    public string EnemyName => enemyName;

    /// <summary>Maximum hit points.</summary>
    public int MaxHealth => maxHealth;

    /// <summary>Movement speed in units per second.</summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>
    /// Distance at which the enemy stops moving and begins attacking.
    /// Used by EnemyMovement to determine when to halt the chase.
    /// </summary>
    public float AttackRange => attackRange;

    /// <summary>Attack definition assigned to the enemy's AttackController on spawn.</summary>
    public AttackDefinition AttackDefinition => attackDefinition;
}
