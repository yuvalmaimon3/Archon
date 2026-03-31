/// <summary>
/// Identifies which elemental reaction was triggered.
/// None means no reaction occurred — elements were simply stored.
/// </summary>
public enum ReactionType
{
    None,
    Frozen,       // Water + Ice
    Boiling,      // Water + Fire
    ThermalShock, // Ice + Fire
    Arc,          // Water + Lightning
    Crack,        // Ice + Lightning
    Plasma,       // Fire + Lightning
}
