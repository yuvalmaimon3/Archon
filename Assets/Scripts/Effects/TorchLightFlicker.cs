using UnityEngine;

/// <summary>
/// Simulates natural torch flame flicker by randomly varying
/// a Point Light's intensity and range each frame using Perlin noise.
/// Attach to the same GameObject as the torch's Point Light.
/// </summary>
[RequireComponent(typeof(Light))]
public class TorchLightFlicker : MonoBehaviour
{
    [Header("Intensity")]
    [Tooltip("Center intensity around which the light flickers.")]
    [SerializeField] private float baseIntensity = 2.5f;

    [Tooltip("Maximum deviation from base intensity.")]
    [SerializeField] private float intensityVariance = 0.6f;

    [Header("Range")]
    [Tooltip("Center range around which the light flickers.")]
    [SerializeField] private float baseRange = 4f;

    [Tooltip("Maximum deviation from base range.")]
    [SerializeField] private float rangeVariance = 0.5f;

    [Header("Speed")]
    [Tooltip("How fast the noise scrolls. Higher = more erratic flicker.")]
    [SerializeField] private float flickerSpeed = 8f;

    private Light _light;

    // Two independent noise offsets so intensity and range don't track each other
    private float _intensityOffset;
    private float _rangeOffset;

    private void Awake()
    {
        _light = GetComponent<Light>();
        // Random offsets so multiple torches in a scene don't flicker in sync
        _intensityOffset = Random.Range(0f, 100f);
        _rangeOffset     = Random.Range(0f, 100f);
    }

    private void Update()
    {
        float t = Time.time * flickerSpeed;

        // Perlin noise returns [0,1] — remap to [-1,1] then scale by variance
        float intensityNoise = (Mathf.PerlinNoise(t, _intensityOffset) - 0.5f) * 2f;
        float rangeNoise     = (Mathf.PerlinNoise(t, _rangeOffset)     - 0.5f) * 2f;

        _light.intensity = baseIntensity + intensityNoise * intensityVariance;
        _light.range     = baseRange     + rangeNoise     * rangeVariance;
    }
}
