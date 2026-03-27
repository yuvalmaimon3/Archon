using UnityEngine;

/// <summary>
/// Runtime controller for the Fireball prefab.
///
/// Responsibilities:
///   - Flickers the fire point light via Perlin noise for a live, organic look.
///   - Pulses the light color between deep orange and hot white (anime feel).
///   - Provides Extinguish() to gracefully stop all particles on hit.
///   - Provides SetLightColor() to tint the light for elemental reactions.
///
/// Attach this to the root "Fireball" GameObject.
/// The FireLight child object is auto-detected on Start if not assigned.
/// </summary>
public class FireballController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Light Flicker")]
    [Tooltip("Point light child that simulates fire glow. Auto-found if null.")]
    [SerializeField] private Light _fireLight;

    [Tooltip("Resting light intensity.")]
    [SerializeField] private float _baseLightIntensity = 3.5f;

    [Tooltip("How much intensity varies above/below base per flicker cycle.")]
    [SerializeField] private float _flickerAmount = 1.2f;

    [Tooltip("How fast the light flickers. Higher = more nervous flame.")]
    [SerializeField] private float _flickerSpeed = 7f;

    [Header("Light Color Pulse")]
    [Tooltip("Lerp the light color between these two values for an anime 'living fire' look.")]
    [SerializeField] private bool  _colorPulse   = true;
    [SerializeField] private Color _lightColorLow  = new Color(1f, 0.30f, 0.04f); // deep ember orange
    [SerializeField] private Color _lightColorHigh = new Color(1f, 0.82f, 0.35f); // hot yellow-white

    [Header("Auto Destroy")]
    [Tooltip("When true, destroys this GameObject after 'lifetime' seconds.")]
    [SerializeField] private bool  _autoDestroy = false;
    [SerializeField] private float _lifetime    = 5f;

    // ── Private ───────────────────────────────────────────────────────────────

    // Independent Perlin offsets so each fireball flickers differently.
    private float _noiseOffsetIntensity;
    private float _noiseOffsetColor;

    // Accumulated time scaled by flicker speed.
    private float _noiseTime;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    /// <summary>
    /// Randomises Perlin offsets so every fireball instance flickers differently.
    /// Auto-locates the FireLight child if not assigned in the Inspector.
    /// </summary>
    private void Start()
    {
        _noiseOffsetIntensity = Random.Range(0f, 100f);
        _noiseOffsetColor     = Random.Range(0f, 100f);

        // Auto-locate light in children if Inspector reference is empty.
        if (_fireLight == null)
            _fireLight = GetComponentInChildren<Light>();

        if (_fireLight == null)
            Debug.LogWarning("[FireballController] No Light found in children. Flicker disabled.");

        if (_autoDestroy)
            Destroy(gameObject, _lifetime);

        Debug.Log($"[FireballController] Initialized on '{name}'.");
    }

    /// <summary>
    /// Every frame: advances the Perlin noise time and drives intensity + color flicker.
    /// Runs only on the owner client (or server) — purely cosmetic, no network sync needed.
    /// </summary>
    private void Update()
    {
        if (_fireLight == null) return;

        _noiseTime += Time.deltaTime * _flickerSpeed;

        // Intensity: maps Perlin [0,1] → [base - flicker, base + flicker]
        float nIntensity = Mathf.PerlinNoise(_noiseTime, _noiseOffsetIntensity);
        _fireLight.intensity = _baseLightIntensity + (nIntensity - 0.5f) * 2f * _flickerAmount;

        // Color pulse: slower independent noise track
        if (_colorPulse)
        {
            float nColor = Mathf.PerlinNoise(_noiseTime * 0.55f, _noiseOffsetColor);
            _fireLight.color = Color.Lerp(_lightColorLow, _lightColorHigh, nColor);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Gracefully stops all child particle emitters and turns off the light.
    /// Call just before destroying the projectile so particles finish naturally.
    /// </summary>
    public void Extinguish()
    {
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (_fireLight != null)
            _fireLight.enabled = false;

        Debug.Log($"[FireballController] '{name}' extinguished.");
    }

    /// <summary>
    /// Tints the fire light to the given color.
    /// Useful for elemental reactions (e.g. Freeze reaction tints the light blue).
    /// Automatically derives a darker variant for the low-pulse color.
    /// </summary>
    public void SetLightColor(Color color)
    {
        _lightColorHigh = color;
        _lightColorLow  = color * 0.55f;

        if (_fireLight != null)
            _fireLight.color = color;

        Debug.Log($"[FireballController] '{name}' light color → {color}.");
    }
}
