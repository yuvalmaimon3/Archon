using System;
using UnityEngine;

/// <summary>
/// Tracks the current elemental state of a GameObject — works for both players and enemies.
/// Implements IElementReceiver to accept incoming element applications.
///
/// Currently replaces the stored state on each new application.
/// The previousElement stored before the replace is the natural hook point
/// for reaction logic in a future step (e.g. Fire + Water = reaction).
/// </summary>
public class ElementStatusController : MonoBehaviour, IElementReceiver
{
    // ── Read-only state ──────────────────────────────────────────────────────

    /// <summary>The element currently affecting this object. None = clean state.</summary>
    public ElementType CurrentElement { get; private set; } = ElementType.None;

    /// <summary>Strength of the current element. 0 when no element is active.</summary>
    public float CurrentStrength { get; private set; } = 0f;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired whenever the element state changes (including clears).
    /// Subscribe here for VFX, audio, or UI — no need to poll CurrentElement.
    /// Passes (newElement, newStrength).
    /// </summary>
    public event Action<ElementType, float> OnElementChanged;

    // ── IElementReceiver ─────────────────────────────────────────────────────

    /// <summary>
    /// Applies a new element to this object, replacing the previous state.
    ///
    /// Note: previousElement is captured before the replace — this is the
    /// correct place to add reaction checks in the future (e.g. if
    /// previousElement != None and conflicts with incoming, trigger a reaction).
    /// </summary>
    public void ApplyElement(ElementApplication application)
    {
        // Capture previous element — future reaction logic checks this before replacing
        ElementType previousElement = CurrentElement;

        // Replace current state with the incoming application
        CurrentElement  = application.Element;
        CurrentStrength = application.Strength;

        Debug.Log($"[ElementStatusController] {gameObject.name} — " +
                  $"element set to {CurrentElement} (strength: {CurrentStrength:F1}), " +
                  $"was: {previousElement}");

        // Notify subscribers: VFX, audio, reaction system (when added)
        OnElementChanged?.Invoke(CurrentElement, CurrentStrength);
    }

    /// <summary>
    /// Resets element state to clean (None, 0 strength).
    /// Call this after a reaction consumes the element, or on respawn.
    /// </summary>
    public void ClearElement()
    {
        CurrentElement  = ElementType.None;
        CurrentStrength = 0f;

        Debug.Log($"[ElementStatusController] {gameObject.name} — element cleared.");

        OnElementChanged?.Invoke(CurrentElement, CurrentStrength);
    }
}
