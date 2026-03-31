/// <summary>
/// Pure static class — no state, no MonoBehaviour, no Unity dependencies.
/// Maps element pairs to their reaction result and post-reaction outcome.
///
/// To add a new reaction: add both orderings to the switch and a private factory method below.
/// Each reaction is responsible for defining its own outcome rule.
/// </summary>
public static class ReactionResolver
{
    /// <summary>
    /// Returns the reaction (if any) between an existing and incoming element.
    /// Both orderings of each pair are handled — Water+Ice and Ice+Water both yield Frozen.
    /// Returns ReactionResult.NoReaction if no reaction is defined for the pair.
    /// </summary>
    public static ReactionResult Resolve(ElementType existing, float existingStrength,
                                         ElementType incoming, float incomingStrength)
    {
        // No reaction possible if either side has no element
        if (existing == ElementType.None || incoming == ElementType.None)
            return ReactionResult.NoReaction;

        return (existing, incoming) switch
        {
            // Frozen — Water + Ice
            (ElementType.Water,     ElementType.Ice)       => MakeFrozen(),
            (ElementType.Ice,       ElementType.Water)     => MakeFrozen(),

            // Boiling — Water + Fire
            (ElementType.Water,     ElementType.Fire)      => MakeBoiling(),
            (ElementType.Fire,      ElementType.Water)     => MakeBoiling(),

            // Thermal Shock — Ice + Fire
            (ElementType.Ice,       ElementType.Fire)      => MakeThermalShock(),
            (ElementType.Fire,      ElementType.Ice)       => MakeThermalShock(),

            // Arc — Water + Lightning
            (ElementType.Water,     ElementType.Lightning) => MakeArc(),
            (ElementType.Lightning, ElementType.Water)     => MakeArc(),

            // Crack — Ice + Lightning
            (ElementType.Ice,       ElementType.Lightning) => MakeCrack(),
            (ElementType.Lightning, ElementType.Ice)       => MakeCrack(),

            // Plasma — Fire + Lightning
            (ElementType.Fire,      ElementType.Lightning) => MakePlasma(),
            (ElementType.Lightning, ElementType.Fire)      => MakePlasma(),

            _ => ReactionResult.NoReaction
        };
    }

    // ── Reaction factories ───────────────────────────────────────────────────
    // Each method owns its outcome rule. Change the outcome here without touching
    // the resolver logic or ElementStatusController.

    /// <summary>Water + Ice: target freezes solid. Both elements consumed.</summary>
    private static ReactionResult MakeFrozen() => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.Frozen,
        outcomeType:    ReactionOutcomeType.ClearAll,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );

    /// <summary>Water + Fire: target boils. Both elements consumed.</summary>
    private static ReactionResult MakeBoiling() => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.Boiling,
        outcomeType:    ReactionOutcomeType.ClearAll,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );

    /// <summary>Ice + Fire: extreme temperature change. Both elements consumed.</summary>
    private static ReactionResult MakeThermalShock() => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.ThermalShock,
        outcomeType:    ReactionOutcomeType.ClearAll,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );

    /// <summary>Water + Lightning: electricity conducts through water. Both elements consumed.</summary>
    private static ReactionResult MakeArc() => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.Arc,
        outcomeType:    ReactionOutcomeType.ClearAll,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );

    /// <summary>Ice + Lightning: frozen target shatters from electric shock. Both elements consumed.</summary>
    private static ReactionResult MakeCrack() => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.Crack,
        outcomeType:    ReactionOutcomeType.ClearAll,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );

    /// <summary>Fire + Lightning: superheated plasma burst. Both elements consumed.</summary>
    private static ReactionResult MakePlasma() => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.Plasma,
        outcomeType:    ReactionOutcomeType.ClearAll,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );
}
