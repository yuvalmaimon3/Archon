using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space health bar that smoothly drains and changes color based on current HP.
///
/// Billboard: rotates every frame to face the local camera.
///
/// Health source (auto-discovered from the parent hierarchy):
///   1. NetworkHealthSync — networked entities (players). Event-driven, synced from server.
///   2. Health            — non-networked entities (enemies). OnDamaged event + poll fallback.
///
/// IMPORTANT — fill update is intentionally placed BEFORE the camera null-check.
/// The camera is only needed for billboard rotation. A missing camera must never
/// block the health bar drain animation.
/// </summary>
public class Healthbar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The fill Image. Must be Type=Filled, FillMethod=Horizontal, FillOrigin=Left.")]
    [SerializeField] private Image _fillImage;

    [Header("Settings")]
    [Tooltip("Drain speed in fill-units per second. 2 = full-to-empty in 0.5 s.")]
    [SerializeField] private float _drainSpeed = 2f;

    // ── Private state ────────────────────────────────────────────────────────

    // Target fill ratio [0..1]. Set by damage events; fillAmount animates toward it.
    private float _target = 1f;

    // Billboard camera — only needed for rotation, not for fill updates.
    private Camera _cam;

    // Health sources — one will be non-null depending on entity type.
    private NetworkHealthSync _healthSync; // players (networked)
    private Health             _health;    // enemies (non-networked)

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        _cam = Camera.main;

        if (_fillImage == null)
        {
            Debug.LogError($"[Healthbar] '_fillImage' not assigned on '{gameObject.name}'. " +
                           "Assign the Fill Image in the Inspector.", this);
            return;
        }

        // Priority 1 — NetworkHealthSync (players).
        _healthSync = GetComponentInParent<NetworkHealthSync>();
        if (_healthSync != null)
        {
            _healthSync.OnHealthChanged += OnHealthChanged;
            if (_healthSync.IsSpawned)
                SnapFill(_healthSync.MaxHealth, _healthSync.CurrentHealth);
            Debug.Log($"[Healthbar] '{name}' → NetworkHealthSync on '{_healthSync.name}'.");
            return;
        }

        // Priority 2 — plain Health (enemies).
        _health = GetComponentInParent<Health>();
        if (_health != null)
        {
            _health.OnDamaged += OnHealthChanged;
            SnapFill(_health.MaxHealth, _health.CurrentHealth);
            Debug.Log($"[Healthbar] '{name}' → Health on '{_health.name}' " +
                      $"({_health.CurrentHealth}/{_health.MaxHealth} HP).");
            return;
        }

        Debug.LogWarning($"[Healthbar] No health source found above '{name}'.", this);
    }

    private void OnDestroy()
    {
        if (_healthSync != null) _healthSync.OnHealthChanged -= OnHealthChanged;
        if (_health     != null) _health.OnDamaged           -= OnHealthChanged;
    }

    private void Update()
    {
        if (_fillImage == null) return;

        // Poll fallback for enemies — keeps bar in sync even if an event was missed.
        if (_health != null)
            _target = _health.MaxHealth > 0 ? (float)_health.CurrentHealth / _health.MaxHealth : 0f;

        // ── Fill + color — no camera needed ──────────────────────────────────
        _fillImage.fillAmount = Mathf.MoveTowards(_fillImage.fillAmount, _target, _drainSpeed * Time.deltaTime);

        // ── Billboard — camera needed ─────────────────────────────────────────
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>Instantly snaps fill and color to the current health (no animation).</summary>
    private void SnapFill(int maxHealth, int currentHealth)
    {
        _target = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
        if (_fillImage != null)
        {
            _fillImage.fillAmount = _target;
        }
    }

    /// <summary>Called by both NetworkHealthSync.OnHealthChanged and Health.OnDamaged.</summary>
    private void OnHealthChanged(int currentHealth, int maxHealth)
    {
        _target = maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }
}
