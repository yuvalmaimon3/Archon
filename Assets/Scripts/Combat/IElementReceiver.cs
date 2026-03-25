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
    void ApplyElement(ElementApplication application);
}
