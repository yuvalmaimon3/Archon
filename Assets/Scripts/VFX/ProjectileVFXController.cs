using UnityEngine;

/// <summary>
/// Attach to the Projectile prefab. Builds element-colored VFX at spawn time
/// and spawns a hit impact effect on collision.
///
/// Does not affect gameplay — purely cosmetic. Safe to remove without breaking combat.
///
/// Usage flow:
///   1. Projectile is spawned and InitializeClientRpc / Initialize is called
///   2. Projectile calls SetVisuals(element, shape) on this component
///   3. This component calls ElementVFXBuilder to create body + trail particles
///   4. On hit, Projectile calls PlayHitEffect(position) on this component
///   5. This component spawns a one-shot hit burst at the impact point
/// </summary>
public class ProjectileVFXController : MonoBehaviour
{
    // The root of the spawned projectile VFX (body + trail particles).
    // Destroyed automatically when the projectile is destroyed.
    private GameObject _vfxRoot;

    // Cached for hit VFX
    private ElementType _element = ElementType.None;

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds projectile body + trail VFX for the given element and shape.
    /// Called once after the projectile is initialized.
    /// </summary>
    public void SetVisuals(ElementType element, ProjectileShape shape)
    {
        _element = element;

        // Destroy previous VFX if re-initialized (shouldn't happen, but safe)
        if (_vfxRoot != null)
            Destroy(_vfxRoot);

        _vfxRoot = ElementVFXBuilder.BuildProjectileVFX(shape, element, transform);

        // Tint the projectile mesh if it has a renderer
        TintMesh(element);

        Debug.Log($"[ProjectileVFXController] {element} {shape} visuals applied.");
    }

    /// <summary>
    /// Spawns a one-shot hit impact effect at the given world position.
    /// Called by Projectile on collision, just before the projectile is destroyed.
    /// </summary>
    public void PlayHitEffect(Vector3 hitPoint)
    {
        if (_element == ElementType.None) return;
        ElementVFXBuilder.BuildHitVFX(_element, hitPoint);
    }

    // ── Private ────────────────────────────────────────────────────────────

    /// <summary>Tints the projectile mesh to match the element. Uses PropertyBlock to avoid material copies.</summary>
    private void TintMesh(ElementType element)
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) return;

        Color color = ElementVisualConfig.Get(element).primary;
        var block = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(block);
    }
}
