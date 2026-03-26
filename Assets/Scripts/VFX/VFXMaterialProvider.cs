using UnityEngine;

/// <summary>
/// Provides a single shared particle material for all VFX.
/// Reusing one material reduces draw calls and material instances on mobile.
/// Cached on first access — lives for the entire session.
/// </summary>
public static class VFXMaterialProvider
{
    private static Material _particleMat;

    /// <summary>
    /// Returns an additive/transparent particle material.
    /// Tries URP particle shader first, falls back to built-in.
    /// </summary>
    public static Material Get()
    {
        if (_particleMat != null) return _particleMat;

        // Try shaders in order of preference: URP → Standard → Legacy
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");

        if (shader == null)
        {
            Debug.LogError("[VFXMaterialProvider] No particle shader found. VFX may not render.");
            _particleMat = new Material(Shader.Find("Sprites/Default"));
            return _particleMat;
        }

        _particleMat = new Material(shader);

        // Configure for additive transparent blending
        _particleMat.SetFloat("_Surface", 1f);  // Transparent
        _particleMat.SetFloat("_Blend", 1f);    // Additive
        _particleMat.renderQueue = 3000;         // Transparent queue

        return _particleMat;
    }
}
