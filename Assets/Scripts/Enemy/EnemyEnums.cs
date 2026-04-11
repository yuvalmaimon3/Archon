// Shared enums describing enemy archetype, locomotion, and combat behavior.
// Referenced by EnemyData so each enemy type can be categorized uniformly.

// Broad combat archetype. Drives spawner mixing and UI grouping later.
public enum EnemyRole
{
    Melee,
    Ranged,
    Tank,
    Caster,
    Support,
    Boss
}

// How the enemy physically moves through the world.
// Used by movement components and animation selection.
public enum EnemyMovementStyle
{
    Walk,
    Run,
    Fly,
    Hop,
    Teleport
}

// High-level combat behavior. Consumed by the enemy's combat brain to
// pick between engagement patterns.
public enum EnemyBehavior
{
    Aggressive,
    Defensive,
    Patrol,
    Ambush,
    Support
}
