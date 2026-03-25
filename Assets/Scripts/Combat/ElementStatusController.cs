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
    /// Applies a new element. Checks for a reaction first; applies the resolver's
    /// outcome rule if one exists, otherwise stores the incoming element directly.
    /// </summary>
    public void ApplyElement(ElementApplication application)
    {
        ReactionResult result = ReactionResolver.Resolve(
            CurrentElement, CurrentStrength,
            application.Element, application.Strength
        );

        if (result.HasReaction)
        {
            LastReaction = result.ReactionType;

            Debug.Log($"[ElementStatusController] {gameObject.name} — " +
                      $"reaction: {result.ReactionType} " +
                      $"({CurrentElement} + {application.Element}), " +
                      $"outcome: {result.OutcomeType}");

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

            case ReactionOutcomeType.KeepIncoming:
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
