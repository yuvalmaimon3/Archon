/// <summary>
/// Pure static class — no state, no MonoBehaviour, no Unity dependencies.
/// Maps element pairs to their reaction result and post-reaction outcome.
/// Covers all 6 pairwise combinations of the 4 core elements.
///
/// To add a new reaction: add a case to the switch and a private factory method.
/// Each reaction owns its outcome rule (clear, keep, replace).
/// </summary>
public static class ReactionResolver
{
    /// <summary>
    /// Returns the reaction between an existing and incoming element.
    /// Returns ReactionResult.NoReaction if no reaction applies (same element, None, etc.).
    /// </summary>
    public static ReactionResult Resolve(ElementType existing, float existingStrength,
                                         ElementType incoming, float incomingStrength)
    {
        if (existing == ElementType.None || incoming == ElementType.None)
            return ReactionResult.NoReaction;

        // Same element refreshes — no reaction
        if (existing == incoming)
            return ReactionResult.NoReaction;

        // Normalize pair so (A,B) and (B,A) hit the same case
        var (first, second) = existing < incoming
            ? (existing, incoming)
            : (incoming, existing);

        float avgStrength = (existingStrength + incomingStrength) * 0.5f;

        return (first, second) switch
        {
            // Fire (1) + Water (2)
            (ElementType.Fire, ElementType.Water)     => MakeVaporize(),
            // Fire (1) + Ice (3)
            (ElementType.Fire, ElementType.Ice)       => MakeMelt(),
            // Fire (1) + Lightning (4)
            (ElementType.Fire, ElementType.Lightning)  => MakeOverload(),
            // Water (2) + Ice (3)
            (ElementType.Water, ElementType.Ice)       => MakeFreeze(avgStrength),
            // Water (2) + Lightning (4)
            (ElementType.Water, ElementType.Lightning)  => MakeElectrocharge(),
            // Ice (3) + Lightning (4)
            (ElementType.Ice, ElementType.Lightning)   => MakeSuperconduct(),

            _ => ReactionResult.NoReaction
        };
    }

    // ── Reaction factories ─────────────────────────────────────────────────────

    /// <summary>Fire + Water: explosive steam. Both consumed.</summary>
    private static ReactionResult MakeVaporize() => new ReactionResult(
        true, ReactionType.Vaporize, ReactionOutcomeType.ClearAll,
        ElementType.None, 0f
    );

    /// <summary>Fire + Ice: thermal shock. Both consumed.</summary>
    private static ReactionResult MakeMelt() => new ReactionResult(
        true, ReactionType.Melt, ReactionOutcomeType.ClearAll,
        ElementType.None, 0f
    );

    /// <summary>Fire + Lightning: explosive overcharge. Both consumed.</summary>
    private static ReactionResult MakeOverload() => new ReactionResult(
        true, ReactionType.Overload, ReactionOutcomeType.ClearAll,
        ElementType.None, 0f
    );

    /// <summary>Water + Ice: target freezes. State becomes Ice with averaged strength.</summary>
    private static ReactionResult MakeFreeze(float avgStrength) => new ReactionResult(
        true, ReactionType.Freeze, ReactionOutcomeType.ReplaceWithSpecificElement,
        ElementType.Ice, avgStrength
    );

    /// <summary>Water + Lightning: electric surge. Both consumed.</summary>
    private static ReactionResult MakeElectrocharge() => new ReactionResult(
        true, ReactionType.Electrocharge, ReactionOutcomeType.ClearAll,
        ElementType.None, 0f
    );

    /// <summary>Lightning + Ice: crackling frost shatter. Both consumed.</summary>
    private static ReactionResult MakeSuperconduct() => new ReactionResult(
        true, ReactionType.Superconduct, ReactionOutcomeType.ClearAll,
        ElementType.None, 0f
    );
}
