/// <summary>
/// Defines how an attack is delivered to its target.
/// Used by AttackDefinition to determine which executor handles the attack.
/// </summary>
public enum AttackType
{
    /// <summary>Fires a physical projectile toward the target.</summary>
    Projectile,

    /// <summary>Deals damage in a short range around the attacker (swing, slash, etc.).</summary>
    Melee,

    /// <summary>Deals damage on direct physical contact (e.g. enemy body touch, trap).</summary>
    Contact,

    /// <summary>
    /// Marks one or more ground locations with a warning indicator, then strikes with AOE damage after a delay.
    /// Think: fire from the ground, lightning from above, meteor shower.
    /// </summary>
    CallDown,

    /// <summary>
    /// Spawns one or more enemy instances at valid NavMesh positions near the attacker.
    /// Summoned enemies are fully independent — they pathfind, attack, and die on their own.
    /// </summary>
    Summoning
}
