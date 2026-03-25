/// <summary>
/// Pure static class — no state, no MonoBehaviour, no Unity dependencies.
/// Maps element pairs to their reaction result and post-reaction outcome.
///
/// To add a new reaction: add a case to the switch and a private factory method below.
/// Each reaction is responsible for defining its own outcome rule.
/// </summary>
public static class ReactionResolver
{
    /// <summary>
    /// Returns the reaction (if any) between an existing and incoming element.
    /// Strengths are passed in because some reactions (e.g. Freeze) use them to compute result strength.
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
            (ElementType.Fire,  ElementType.Water) => MakeVaporize(),
            (ElementType.Water, ElementType.Fire)  => MakeVaporize(),

            (ElementType.Water, ElementType.Ice)   => MakeFreeze(existingStrength, incomingStrength),
            (ElementType.Ice,   ElementType.Water) => MakeFreeze(existingStrength, incomingStrength),

            (ElementType.Fire,  ElementType.Ice)   => MakeMelt(),
            (ElementType.Ice,   ElementType.Fire)  => MakeMelt(),

            _ => ReactionResult.NoReaction
        };
    }

    // ── Reaction factories ───────────────────────────────────────────────────
    // Each method owns its outcome rule. Change the outcome here without touching
    // the resolver logic or ElementStatusController.

    /// <summary>
    /// Fire + Water: explosive steam reaction.
    /// Both elements are fully consumed — state clears to None.
    /// </summary>
    private static ReactionResult MakeVaporize() => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.Vaporize,
        outcomeType:    ReactionOutcomeType.ClearAll,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );

    /// <summary>
    /// Water + Ice: water freezes into ice.
    /// State becomes Ice with strength averaged from both inputs.
    /// </summary>
    private static ReactionResult MakeFreeze(float existingStrength, float incomingStrength) => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.Freeze,
        outcomeType:    ReactionOutcomeType.ReplaceWithSpecificElement,
        resultElement:  ElementType.Ice,
        resultStrength: (existingStrength + incomingStrength) * 0.5f
    );

    /// <summary>
    /// Fire + Ice: ice melts, both elements consumed.
    /// State clears to None.
    /// </summary>
    private static ReactionResult MakeMelt() => new ReactionResult(
        hasReaction:    true,
        reactionType:   ReactionType.Melt,
        outcomeType:    ReactionOutcomeType.ClearAll,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );
}
