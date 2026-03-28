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
    Contact
}
