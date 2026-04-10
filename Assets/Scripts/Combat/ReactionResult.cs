using UnityEngine;

/// <summary>
/// Immutable result returned by ReactionResolver for a single element interaction.
/// Carries both the reaction identity and the post-reaction elemental state instructions.
///
/// BaseDamage and Source are attached by ElementStatusController (not the resolver) because
/// damage/attribution context comes from Health, not from element pair logic.
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

    /// <summary>
    /// The raw damage of the attack that triggered this reaction.
    /// Set by Health before forwarding to ElementStatusController — used by
    /// ReactionDamageHandler to compute reaction damage (e.g. x2).
    /// 0 if no damage context (e.g. pure element application without an attack).
    /// </summary>
    public readonly int BaseDamage;

    // The player who triggered this reaction (from ElementApplication.Source).
    // Used by per-player upgrade effects (e.g. BlastReaction) to only fire for
    // reactions caused by their owner's attacks.
    public readonly GameObject Source;

    public ReactionResult(bool hasReaction, ReactionType reactionType,
                          ReactionOutcomeType outcomeType,
                          ElementType resultElement, float resultStrength,
                          int baseDamage = 0, GameObject source = null)
    {
        HasReaction    = hasReaction;
        ReactionType   = reactionType;
        OutcomeType    = outcomeType;
        ResultElement  = resultElement;
        ResultStrength = resultStrength;
        BaseDamage     = baseDamage;
        Source         = source;
    }

    /// <summary>
    /// Returns a copy with BaseDamage set.
    /// Used by ElementStatusController to attach the triggering attack's damage.
    /// </summary>
    public ReactionResult WithBaseDamage(int baseDamage) => new ReactionResult(
        HasReaction, ReactionType, OutcomeType, ResultElement, ResultStrength, baseDamage, Source
    );

    // Returns a copy with Source set (the player whose attack triggered the reaction).
    public ReactionResult WithSource(GameObject source) => new ReactionResult(
        HasReaction, ReactionType, OutcomeType, ResultElement, ResultStrength, BaseDamage, source
    );

    /// <summary>
    /// Convenience constant for the no-reaction case.
    /// Returned by ReactionResolver when no reaction is defined for the element pair.
    /// </summary>
    public static ReactionResult NoReaction => new ReactionResult(
        hasReaction:    false,
        reactionType:   ReactionType.None,
        outcomeType:    ReactionOutcomeType.None,
        resultElement:  ElementType.None,
        resultStrength: 0f
    );
}
