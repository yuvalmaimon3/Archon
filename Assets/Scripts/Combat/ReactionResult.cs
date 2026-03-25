/// <summary>
/// Immutable result returned by ReactionResolver for a single element interaction.
/// Carries both the reaction identity and the post-reaction elemental state instructions.
/// </summary>
public readonly struct ReactionResult
{
    /// <summary>True if a reaction was triggered. False means normal element replacement.</summary>
    public readonly bool HasReaction;

    /// <summary>Which reaction fired. None if HasReaction is false.</summary>
    public readonly ReactionType ReactionType;

    /// <summary>What to do with elemental state after the reaction.</summary>
    public readonly ReactionOutcomeType OutcomeType;

    /// <summary>
    /// The element to store when OutcomeType is ReplaceWithSpecificElement.
    /// Ignored for all other outcome types.
    /// </summary>
    public readonly ElementType ResultElement;

    /// <summary>
    /// The strength to store when OutcomeType is ReplaceWithSpecificElement.
    /// Ignored for all other outcome types.
    /// </summary>
    public readonly float ResultStrength;

    public ReactionResult(bool hasReaction, ReactionType reactionType,
                          ReactionOutcomeType outcomeType,
                          ElementType resultElement, float resultStrength)
    {
        HasReaction    = hasReaction;
        ReactionType   = reactionType;
        OutcomeType    = outcomeType;
        ResultElement  = resultElement;
        ResultStrength = resultStrength;
    }

    /// <summary>
    /// Convenience constant for the no-reaction case.
    /// Returned by ReactionResolver when no reaction is defined for the element pair.
    /// </summary>
    public static ReactionResult NoReaction => new ReactionResult(
        hasReaction:   false,
        reactionType:  ReactionType.None,
        outcomeType:   ReactionOutcomeType.None,
        resultElement: ElementType.None,
        resultStrength: 0f
    );
}
