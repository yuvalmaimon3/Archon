using UnityEngine;

/// <summary>
/// Runtime controller for the Fireball prefab.
///
/// Manages two independent lights:
///   - FireLight      : warm outer glow — slow organic flicker (Perlin noise)
///   - InnerCoreLight : blue-white magical core — fast subtle heartbeat (separate Perlin track)
///
/// Both lights are located by GameObject name in children.
/// Provides Extinguish() and SetLightColor() for gameplay integration.
/// </summary>
public class FireballController : MonoBehaviour
{
    // ── Inspector — Outer Fire Light ──────────────────────────────────────────

    [Header("Outer Fire Light")]
    [Tooltip("Warm point light driven by slow organic flicker. Auto-found by name 'FireLight'.")]
    [SerializeField] private Light _fireLight;

    [Tooltip("Base (resting) intensity of the outer fire light.")]
    [SerializeField] private float _baseLightIntensity = 4f;

    [Tooltip("How much the outer light intensity varies per flicker.")]
    [SerializeField] private float _flickerAmount = 1.5f;

    [Tooltip("Speed of the outer light Perlin noise track.")]
    [SerializeField] private float _flickerSpeed = 6f;

    [Header("Outer Light Color Pulse")]
    [Tooltip("Pulse outer light color between deep orange and hot yellow.")]
    [SerializeField] private bool  _colorPulse    = true;
    [SerializeField] private Color _lightColorLow  = new Color(1f, 0.28f, 0.03f); // deep ember orange
    [SerializeField] private Color _lightColorHigh = new Color(1f, 0.80f, 0.30f); // hot golden-yellow

    // ── Inspector — Inner Core Light ──────────────────────────────────────────

    [Header("Inner Core Light (Magical)")]
    [Tooltip("Small bright blue-white light at the magical core. Auto-found by name 'InnerCoreLight'.")]
    [SerializeField] private Light _innerCoreLight;

    [Tooltip("Base intensity of the inner core light.")]
    [SerializeField] private float _innerCoreBaseIntensity = 8f;

    [Tooltip("Subtle flicker amplitude for the inner core (magical heartbeat).")]
    [SerializeField] private float _innerCoreFlicker = 2.5f;

    // ── Inspector — Auto Destroy ──────────────────────────────────────────────

    [Header("Auto Destroy")]
    [Tooltip("Destroy this GameObject after 'lifetime' seconds.")]
    [SerializeField] private bool  _autoDestroy = false;
    [SerializeField] private float _lifetime    = 5f;

    // ── Private ───────────────────────────────────────────────────────────────

    // Independent Perlin offsets so every fireball instance is unique.
    private float _noiseOffsetFire;
    private float _noiseOffsetColor;
    private float _noiseOffsetCore;

    // Accumulated time — each light track runs at a different speed.
    private float _noiseTime;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    /// <summary>
    /// Locates both lights by child GameObject name.
    /// Randomises Perlin offsets so instances never flicker in sync.
    /// </summary>
    private void Start()
    {
        _noiseOffsetFire  = Random.Range(0f, 100f);
        _noiseOffsetColor = Random.Range(0f, 100f);
        _noiseOffsetCore  = Random.Range(0f, 100f);

        // Look up lights by exact child name for reliable results.
        if (_fireLight == null)
        {
            var fireChild = transform.Find("FireLight");
            _fireLight = fireChild != null ? fireChild.GetComponent<Light>() : GetComponentInChildren<Light>();
        }

        if (_innerCoreLight == null)
        {
            var coreChild = transform.Find("InnerCoreLight");
            if (coreChild != null) _innerCoreLight = coreChild.GetComponent<Light>();
        }

        if (_autoDestroy)
            Destroy(gameObject, _lifetime);

        Debug.Log($"[FireballController] Initialized. FireLight={_fireLight != null}, " +
                  $"InnerCoreLight={_innerCoreLight != null}");
    }

    /// <summary>
    /// Drives both lights every frame:
    ///   - FireLight: slow (~6 Hz) organic flicker + color pulse.
    ///   - InnerCoreLight: fast (~15 Hz) subtle heartbeat (independent track).
    /// </summary>
    private void Update()
    {
        _noiseTime += Time.deltaTime;

        // ── Outer fire light (slow, dramatic) ────────────────────────────────
        if (_fireLight != null)
        {
            float t = _noiseTime * _flickerSpeed;
            float n = Mathf.PerlinNoise(t, _noiseOffsetFire);
            _fireLight.intensity = _baseLightIntensity + (n - 0.5f) * 2f * _flickerAmount;

            if (_colorPulse)
            {
                float nc = Mathf.PerlinNoise(t * 0.5f, _noiseOffsetColor);
                _fireLight.color = Color.Lerp(_lightColorLow, _lightColorHigh, nc);
            }
        }

        // ── Inner core light (fast, subtle — magical heartbeat) ───────────────
        if (_innerCoreLight != null)
        {
            float tc = _noiseTime * 14f; // faster track
            float nc = Mathf.PerlinNoise(tc, _noiseOffsetCore);
            _innerCoreLight.intensity = _innerCoreBaseIntensity + (nc - 0.5f) * 2f * _innerCoreFlicker;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Stops all child particle emitters and disables both lights.
    /// Call just before destroying the projectile on impact.
    /// </summary>
    public void Extinguish()
    {
        foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        if (_fireLight != null)       _fireLight.enabled       = false;
        if (_innerCoreLight != null)  _innerCoreLight.enabled  = false;

        Debug.Log($"[FireballController] '{name}' extinguished.");
    }

    /// <summary>
    /// Tints both lights to the given color (for elemental reactions).
    /// The inner core stays slightly cooler (multiply by 0.6 and shift toward white).
    /// </summary>
    public void SetLightColor(Color color)
    {
        _lightColorHigh = color;
        _lightColorLow  = color * 0.5f;

        // Keep the inner core bluer/whiter to suggest heat even with a color override.
        if (_innerCoreLight != null)
            _innerCoreLight.color = Color.Lerp(color, Color.white, 0.4f);

        if (_fireLight != null)
            _fireLight.color = color;

        Debug.Log($"[FireballController] '{name}' light color → {color}.");
    }
}
