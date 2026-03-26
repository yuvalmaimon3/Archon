using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar that smoothly drains toward the target fill amount.
///
/// Billboard behavior: rotates each frame to face the local camera, so the bar always
/// reads correctly regardless of the player's rotation or position.
///
/// Auto-discovers NetworkHealthSync in its parent hierarchy and subscribes to health
/// change events — no manual wiring required as long as the parent has the component.
///
/// Visual setup required on the prefab (HealthbarCanvas):
///   - Canvas in World Space render mode
///   - _healthbarSprite: an Image set to Type = Filled, FillMethod = Horizontal, FillOrigin = Left
/// </summary>
public class Healthbar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The fill Image. Must have Image.Type = Filled and FillMethod = Horizontal.")]
    [SerializeField] private Image _healthbarSprite;

    [Header("Settings")]
    [Tooltip("How fast the bar fill moves toward the target value (fill units per second). " +
             "Higher = faster drain animation.")]
    [SerializeField] private float _reduceSpeed = 2f;

    // Target fill [0..1] — written by UpdateHealthBar, smoothly approached in Update.
    private float _target = 1f;

    // Local machine's main camera — used for the billboard rotation.
    private Camera _cam;

    // Found in parent hierarchy on Start — null if parent has no NetworkHealthSync.
    private NetworkHealthSync _healthSync;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        _cam = Camera.main;

        // Look up the sync component in the player (or any entity) hierarchy.
        _healthSync = GetComponentInParent<NetworkHealthSync>();
        if (_healthSync == null)
        {
            Debug.LogWarning($"[Healthbar] No NetworkHealthSync found above '{gameObject.name}'. " +
                             "Health bar will not update automatically.", this);
            return;
        }

        _healthSync.OnHealthChanged += OnHealthChanged;

        // If the component was already spawned before Start ran (e.g. a player that exists
        // in the scene from the start), pull the current value right now.
        if (_healthSync.IsSpawned)
            UpdateHealthBar(_healthSync.MaxHealth, _healthSync.CurrentHealth);
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent callbacks after this object is destroyed.
        if (_healthSync != null)
            _healthSync.OnHealthChanged -= OnHealthChanged;
    }

    private void Update()
    {
        // Camera may not be ready on the first frame — retry until available.
        if (_cam == null)
        {
            _cam = Camera.main;
            return;
        }

        // Billboard: make the bar face the local camera every frame.
        // Using LookRotation(pos - camPos) points +Z away from the camera,
        // which causes the Canvas face (+Z side) to look toward the camera.
        transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

        // Smoothly approach the target fill — produces the satisfying drain effect.
        _healthbarSprite.fillAmount = Mathf.MoveTowards(
            _healthbarSprite.fillAmount, _target, _reduceSpeed * Time.deltaTime);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the target fill amount from raw health values.
    /// The bar drains smoothly to the new target on subsequent Update frames.
    /// </summary>
    public void UpdateHealthBar(float maxHealth, float currentHealth)
    {
        _target = maxHealth > 0f ? currentHealth / maxHealth : 0f;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void OnHealthChanged(int currentHealth, int maxHealth)
    {
        UpdateHealthBar(maxHealth, currentHealth);
    }
}
