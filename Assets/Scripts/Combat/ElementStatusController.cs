using System;
using UnityEngine;

/// <summary>
/// Tracks the current elemental state of a GameObject — works for both players and enemies.
/// Implements IElementReceiver to accept incoming element applications.
///
/// On each new application:
///   1. Asks ReactionResolver whether existing + incoming elements react.
///   2. If no reaction — stores the incoming element normally.
///   3. If a reaction — applies the outcome rule returned by the resolver
///      (clear, keep, replace, or replace with a specific element).
///
/// Does not own reaction damage or VFX — fires OnReactionTriggered so
/// other systems can respond without coupling to this class.
/// </summary>
public class ElementStatusController : MonoBehaviour, IElementReceiver
{
    // ── Read-only state ──────────────────────────────────────────────────────

    /// <summary>The element currently affecting this object. None = clean state.</summary>
    public ElementType CurrentElement { get; private set; } = ElementType.None;

    /// <summary>Strength of the current element. 0 when no element is active.</summary>
    public float CurrentStrength { get; private set; } = 0f;

    /// <summary>
    /// The last reaction that was triggered on this object.
    /// ReactionType.None if no reaction has occurred yet.
    /// </summary>
    public ReactionType LastReaction { get; private set; } = ReactionType.None;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired whenever the elemental state changes (including clears and post-reaction states).
    /// Passes (newElement, newStrength).
    /// </summary>
    public event Action<ElementType, float> OnElementChanged;

    /// <summary>
    /// Fired when two elements react.
    /// Passes the full ReactionResult — subscribers use it to apply bonus damage, VFX, audio, etc.
    /// </summary>
    public event Action<ReactionResult> OnReactionTriggered;

    // ── IElementReceiver ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if applying the given element would trigger a reaction with the current element.
    /// Does NOT modify state — used by Health to decide whether to suppress direct damage before
    /// forwarding the element application.
    /// </summary>
    public bool WouldReact(ElementType incomingElement)
    {
        if (CurrentElement == ElementType.None || incomingElement == ElementType.None) return false;
        // Strength value doesn't affect whether a reaction occurs, only outcome details
        return ReactionResolver.Resolve(CurrentElement, CurrentStrength, incomingElement, 1f).HasReaction;
    }

    /// <summary>
    /// Applies a new element. Checks for a reaction first; applies the resolver's
    /// outcome rule if one exists, otherwise stores the incoming element directly.
    ///
    /// baseDamage — the raw damage of the attack that applied this element.
    /// Attached to the ReactionResult so subscribers (e.g. ReactionDamageHandler)
    /// can compute reaction damage without needing to reach back into the attack.
    /// Pass 0 for pure element applications that carry no damage context.
    /// </summary>
    public void ApplyElement(ElementApplication application, int baseDamage = 0,
                             bool isCritical = false)
    {
        ReactionResult result = ReactionResolver.Resolve(
            CurrentElement, CurrentStrength,
            application.Element, application.Strength
        );

        if (result.HasReaction)
        {
            LastReaction = result.ReactionType;

            // Attach the triggering attack's damage, source player, and crit flag
            result = result.WithBaseDamage(baseDamage);
            result = result.WithSource(application.Source);
            result = result.WithIsCritical(isCritical);

            Debug.Log($"[ElementStatusController] {gameObject.name} — " +
                      $"reaction: {result.ReactionType} " +
                      $"({CurrentElement} + {application.Element}), " +
                      $"outcome: {result.OutcomeType}, baseDamage: {baseDamage}");

            ApplyOutcome(result, application);

            // Notify subscribers — damage bonus, VFX, audio all live outside this class
            OnReactionTriggered?.Invoke(result);
        }
        else
        {
            // No reaction — store incoming element normally
            CurrentElement  = application.Element;
            CurrentStrength = application.Strength;

            Debug.Log($"[ElementStatusController] {gameObject.name} — " +
                      $"element set to {CurrentElement} (strength: {CurrentStrength:F1})");
        }

        OnElementChanged?.Invoke(CurrentElement, CurrentStrength);
    }

    /// <summary>
    /// Resets element state to clean (None, 0 strength).
    /// Call this on respawn or after external systems consume the element.
    /// </summary>
    public void ClearElement()
    {
        CurrentElement  = ElementType.None;
        CurrentStrength = 0f;

        Debug.Log($"[ElementStatusController] {gameObject.name} — element cleared.");

        OnElementChanged?.Invoke(CurrentElement, CurrentStrength);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies the post-reaction elemental state based on the outcome rule
    /// defined by the resolver. Each reaction controls its own aftermath.
    /// </summary>
    private void ApplyOutcome(ReactionResult result, ElementApplication incoming)
    {
        switch (result.OutcomeType)
        {
            case ReactionOutcomeType.ClearAll:
                // Both elements consumed — reset to clean state
                CurrentElement  = ElementType.None;
                CurrentStrength = 0f;
                break;

            case ReactionOutcomeType.KeepExisting:
                // Existing element survives — incoming is discarded
                break;

            case ReactionOutcomeType.ReplaceWithIncoming:
                // Store the incoming element as the new state
                CurrentElement  = incoming.Element;
                CurrentStrength = incoming.Strength;
                break;

            case ReactionOutcomeType.ReplaceWithSpecificElement:
                // Reaction produces a specific resulting element (e.g. Freeze → Ice)
                CurrentElement  = result.ResultElement;
                CurrentStrength = result.ResultStrength;
                break;
        }
    }
}
