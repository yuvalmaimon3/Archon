/// <summary>
/// Implemented by any GameObject that can receive damage — players, enemies, destructible objects.
/// Keeps the damage system decoupled: the attacker only needs this interface, not the concrete type.
/// </summary>
public interface IDamageable
{
    /// <summary>
    /// Apply damage to this object.
    /// </summary>
    /// <param name="damageInfo">Full context of the damage event.</param>
    void TakeDamage(DamageInfo damageInfo);
}
