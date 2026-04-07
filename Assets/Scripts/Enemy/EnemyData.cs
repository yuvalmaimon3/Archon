using UnityEngine;

// Holds the computed stats for a specific enemy level.
// Produced by EnemyData.ComputeStats(level) — used by EnemyInitializer to push
// scaled values into Health, AttackController, and movement components.
public struct ScaledEnemyStats
{
    public int   maxHealth;
    public float moveSpeed;
    // Multipliers applied to the AttackDefinition values at runtime.
    // 1.0 = base value, 2.0 = double, etc.
    public float damageMultiplier;
    // < 1.0 means attacks are faster (shorter cooldown), > 1.0 means slower.
    public float cooldownMultiplier;
}

// ScriptableObject that defines the base stats for a single enemy type.
// Create one asset per enemy type (e.g. GoblinData, SkeletonData).
//
// Each enemy prefab (or prefab variant) references its own EnemyData asset,
// so changing stats for one type does not affect others.
//
// Scaling: set the per-stat scaling fields to control how this enemy type grows
// with level. All scaling defaults to 0 (no scaling) until rates are decided.
// Formula: scaledValue = baseValue * (1 + scalingPerLevel * (level - 1))
// Attack speed scales the cooldown inversely: cooldown / (1 + scaling * (level - 1))
//
// Create via: Assets → Create → Arcon → Enemy → Enemy Data
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Arcon/Enemy/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name of this enemy type (used in logs and future UI).")]
    [SerializeField] private string enemyName = "Enemy";

    [Header("Stats")]
    [Tooltip("Maximum hit points for this enemy type at level 1.")]
    [Min(1)]
    [SerializeField] private int maxHealth = 50;

    [Tooltip("Movement speed in units per second at level 1.")]
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

    [Header("Level Scaling")]
    [Tooltip("HP growth per level as a fraction of base HP. " +
             "0 = no scaling, 0.1 = +10% per level. Rates TBD.")]
    [Min(0f)]
    [SerializeField] private float healthScalingPerLevel = 0f;

    [Tooltip("Move speed growth per level as a fraction of base speed. " +
             "0 = no scaling, 0.05 = +5% per level. Rates TBD.")]
    [Min(0f)]
    [SerializeField] private float moveSpeedScalingPerLevel = 0f;

    [Tooltip("Damage growth per level as a fraction of base damage. " +
             "0 = no scaling, 0.1 = +10% per level. Rates TBD.")]
    [Min(0f)]
    [SerializeField] private float damageScalingPerLevel = 0f;

    [Tooltip("Attack speed growth per level. Positive values reduce cooldown (faster attacks). " +
             "0 = no scaling, 0.05 = 5% faster per level. Rates TBD.")]
    [Min(0f)]
    [SerializeField] private float attackSpeedScalingPerLevel = 0f;

    // ── Read-only base stat properties ───────────────────────────────────────

    // Display name of this enemy type.
    public string EnemyName => enemyName;

    // Maximum hit points at level 1.
    public int MaxHealth => maxHealth;

    // Movement speed in units per second at level 1.
    public float MoveSpeed => moveSpeed;

    // Distance at which the enemy stops moving and begins attacking.
    // Used by EnemyMovement to determine when to halt the chase.
    public float AttackRange => attackRange;

    // Attack definition assigned to the enemy's AttackController on spawn.
    public AttackDefinition AttackDefinition => attackDefinition;

    // ── Level scaling ────────────────────────────────────────────────────────

    // Computes the scaled stats for the given level.
    // Level 1 returns base values (multipliers = 1.0). All scaling fields default
    // to 0, so enemies with unconfigured data are unaffected until rates are tuned.
    //
    // HP / MoveSpeed / Damage formula: base * (1 + scaling * (level - 1))
    // AttackSpeed formula (cooldown reduction): base / (1 + scaling * (level - 1))
    //   — dividing the cooldown makes higher levels attack faster.
    public ScaledEnemyStats ComputeStats(int level)
    {
        // Clamp to minimum level 1 so callers can't accidentally produce negative growth.
        int   lvl = Mathf.Max(1, level);
        float t   = lvl - 1; // 0 at level 1, grows linearly

        // Damage and cooldown are stored as multipliers that AttackController applies
        // to the base AttackDefinition values at runtime.
        float damageMultiplier   = 1f + damageScalingPerLevel * t;

        // Attack speed scaling reduces cooldown: divisor grows with level.
        // Clamped to avoid division by zero (divisor always >= 1).
        float cooldownDivisor    = Mathf.Max(1f, 1f + attackSpeedScalingPerLevel * t);
        float cooldownMultiplier = 1f / cooldownDivisor;

        return new ScaledEnemyStats
        {
            maxHealth         = Mathf.Max(1, Mathf.RoundToInt(maxHealth  * (1f + healthScalingPerLevel     * t))),
            moveSpeed         = Mathf.Max(0f, moveSpeed                  * (1f + moveSpeedScalingPerLevel  * t)),
            damageMultiplier  = damageMultiplier,
            cooldownMultiplier = cooldownMultiplier,
        };
    }
}
