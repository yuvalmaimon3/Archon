/// <summary>
/// Implemented by any GameObject that can receive elemental applications — players, enemies, objects.
/// Keeps the element system decoupled: the sender only needs this interface, not the concrete type.
/// </summary>
public interface IElementReceiver
{
    /// <summary>
    /// Apply an element to this receiver.
    /// </summary>
    /// <param name="application">The element data to apply.</param>
    /// <param name="baseDamage">
    /// The raw damage of the attack that carried this element application.
    /// Passed through to the ReactionResult so ReactionDamageHandler can compute
    /// reaction damage (e.g. x2). Use 0 for pure element applications with no attack context.
    /// </param>
    void ApplyElement(ElementApplication application, int baseDamage = 0);
}
