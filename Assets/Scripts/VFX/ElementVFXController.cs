using UnityEngine;

/// <summary>
/// Manages element aura and reaction VFX on a damageable target.
/// Attach alongside ElementStatusController on any GameObject that can receive elements.
///
/// Subscribes to ElementStatusController events:
///   - OnElementChanged  → shows/swaps/clears the element aura
///   - OnReactionTriggered → plays the reaction burst VFX
///
/// Purely cosmetic — does not modify game state. Safe to add or remove.
/// </summary>
[RequireComponent(typeof(ElementStatusController))]
public class ElementVFXController : MonoBehaviour
{
    private ElementStatusController _elementStatus;

    // Currently active aura particle system. Null when no element is applied.
    private ParticleSystem _currentAura;
    private ElementType _currentAuraElement = ElementType.None;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    private void Awake()
    {
        _elementStatus = GetComponent<ElementStatusController>();
    }

    private void OnEnable()
    {
        if (_elementStatus != null)
        {
            _elementStatus.OnElementChanged += HandleElementChanged;
            _elementStatus.OnReactionTriggered += HandleReactionTriggered;
        }
    }

    private void OnDisable()
    {
        if (_elementStatus != null)
        {
            _elementStatus.OnElementChanged -= HandleElementChanged;
            _elementStatus.OnReactionTriggered -= HandleReactionTriggered;
        }
        DestroyCurrentAura();
    }

    // ── Event handlers ─────────────────────────────────────────────────────

    /// <summary>
    /// Called whenever the element on this target changes.
    /// Creates, swaps, or destroys the aura particle effect.
    /// </summary>
    private void HandleElementChanged(ElementType newElement, float strength)
    {
        // Same element — no visual change needed
        if (newElement == _currentAuraElement) return;

        DestroyCurrentAura();

        if (newElement == ElementType.None)
        {
            Debug.Log($"[ElementVFXController] {gameObject.name} — aura cleared.");
            return;
        }

        // Create new aura for the applied element
        _currentAura = ElementVFXBuilder.BuildElementAura(newElement, transform);
        _currentAuraElement = newElement;

        Debug.Log($"[ElementVFXController] {gameObject.name} — showing {newElement} aura.");
    }

    /// <summary>
    /// Called when two elements react on this target.
    /// Plays the reaction burst VFX — stronger and more noticeable than a hit.
    /// </summary>
    private void HandleReactionTriggered(ReactionResult result)
    {
        ElementVFXBuilder.BuildReactionVFX(result.ReactionType, transform.position);
        Debug.Log($"[ElementVFXController] {gameObject.name} — reaction burst: {result.ReactionType}.");
    }

    // ── Private ────────────────────────────────────────────────────────────

    private void DestroyCurrentAura()
    {
        if (_currentAura != null)
        {
            Destroy(_currentAura.gameObject);
            _currentAura = null;
        }
        _currentAuraElement = ElementType.None;
    }
}
