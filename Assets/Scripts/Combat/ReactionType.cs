/// <summary>
/// Identifies which elemental reaction was triggered.
/// None means no reaction occurred — elements were simply stored.
/// </summary>
public enum ReactionType
{
    None,
    Vaporize, // Fire + Water
    Freeze,   // Water + Ice
    Melt      // Fire + Ice
}
