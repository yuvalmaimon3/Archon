using UnityEngine;

/// <summary>
/// Immutable data payload sent when applying an element to a target.
/// Passed by value — readonly ensures no field is accidentally mutated after creation.
/// </summary>
public readonly struct ElementApplication
{
    /// <summary>The element being applied.</summary>
    public readonly ElementType Element;

    /// <summary>
    /// How strongly the element is applied.
    /// Higher strength may affect reaction intensity or override weaker elements in the future.
    /// </summary>
    public readonly float Strength;

    /// <summary>
    /// The GameObject that triggered this application (player, projectile, trap, etc.).
    /// Useful for attribution — e.g. crediting kill assists or filtering friendly fire.
    /// </summary>
    public readonly GameObject Source;

    public ElementApplication(ElementType element, float strength, GameObject source)
    {
        Element  = element;
        Strength = strength;
        Source   = source;
    }
}
