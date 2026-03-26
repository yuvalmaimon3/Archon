/// <summary>
/// Identifies which elemental reaction was triggered.
/// None means no reaction occurred — elements were simply stored.
/// Covers all 6 pairwise combinations of the 4 core elements (Fire, Water, Lightning, Ice).
/// </summary>
public enum ReactionType
{
    None,

    Vaporize,      // Fire + Water     — steam burst, both consumed
    Melt,          // Fire + Ice       — thermal shock, both consumed
    Overload,      // Fire + Lightning — explosive overcharge, both consumed
    Freeze,        // Water + Ice      — ice crystal formation, becomes Ice
    Electrocharge, // Water + Lightning — electric surge, both consumed
    Superconduct   // Lightning + Ice  — crackling frost shatter, both consumed
}
