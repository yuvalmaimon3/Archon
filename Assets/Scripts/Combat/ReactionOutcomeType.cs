/// <summary>
/// Describes what happens to the elemental state after a reaction fires.
/// Each reaction decides its own outcome — there is no universal rule.
/// </summary>
public enum ReactionOutcomeType
{
    /// <summary>No reaction — normal element replacement applies.</summary>
    None,

    /// <summary>Both elements are consumed. State resets to None.</summary>
    ClearAll,

    /// <summary>The existing element is kept. The incoming element is discarded.</summary>
    KeepExisting,

    /// <summary>Replaces the existing element with the incoming element and its strength.</summary>
    ReplaceWithIncoming,

    /// <summary>
    /// State is set to a specific element defined inside ReactionResult (e.g. Freeze → Ice).
    /// Use ReactionResult.ResultElement and ResultStrength for the new state values.
    /// </summary>
    ReplaceWithSpecificElement
}
