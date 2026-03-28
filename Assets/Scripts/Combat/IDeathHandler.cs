/// <summary>
/// Implement on any component that needs custom cleanup when its entity dies.
///
/// DeathController auto-discovers every IDeathHandler in the GameObject hierarchy
/// and calls OnDeath() once when health reaches zero.
///
/// Use for: clearing targets, cancelling coroutines, stopping AI state machines, etc.
/// For simply disabling a component on death, use DeathController._disableOnDeath instead.
/// </summary>
public interface IDeathHandler
{
    /// <summary>Called once when the entity's health reaches zero.</summary>
    void OnDeath();
}
