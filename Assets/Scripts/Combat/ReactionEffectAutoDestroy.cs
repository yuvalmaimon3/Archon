using UnityEngine;

/// <summary>
/// Self-destructs this GameObject after a configurable duration.
/// Attach to any reaction VFX prefab so it cleans itself up without
/// needing a pool or external manager.
/// </summary>
public class ReactionEffectAutoDestroy : MonoBehaviour
{
    [Tooltip("Seconds before this GameObject destroys itself.")]
    [SerializeField] private float lifetime = 1f;

    private void OnEnable()
    {
        Destroy(gameObject, lifetime);
    }
}
