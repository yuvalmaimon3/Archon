using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space EXP bar that smoothly fills as the player gains experience.
/// Positioned above the HP bar in the player's world-space canvas.
///
/// Billboard: rotates every frame to face the local camera, same as Healthbar.
///
/// Data source: PlayerLevelSystem — subscribes to OnExpChanged events.
/// Initializes safely on NGO spawn via a poll fallback in Update.
/// </summary>
public class ExpBar : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The fill Image. Must be Type=Filled, FillMethod=Horizontal, FillOrigin=Left.")]
    [SerializeField] private Image _fillImage;

    [Header("Settings")]
    [Tooltip("Fill speed in units per second. Higher = faster animation.")]
    [SerializeField] private float _fillSpeed = 3f;

    // ── Private state ────────────────────────────────────────────────────────

    // Target fill ratio [0..1]. Animated toward via MoveTowards each frame.
    private float _target = 0f;

    // True once we've received at least one EXP value from the level system.
    private bool _initialized = false;

    private Camera             _cam;
    private PlayerLevelSystem  _levelSystem;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        _cam = Camera.main;

        if (_fillImage == null)
        {
            Debug.LogError($"[ExpBar] '_fillImage' not assigned on '{gameObject.name}'. " +
                           "Assign the Fill Image in the Inspector.", this);
            return;
        }

        // Auto-discover the level system from the player hierarchy
        _levelSystem = GetComponentInParent<PlayerLevelSystem>();

        if (_levelSystem == null)
        {
            Debug.LogWarning($"[ExpBar] No PlayerLevelSystem found above '{name}'.", this);
            return;
        }

        _levelSystem.OnExpChanged += OnExpChanged;

        // Initialize immediately if the NetworkObject is already spawned
        if (_levelSystem.IsSpawned)
            SnapFill(_levelSystem.CurrentExp, _levelSystem.ExpRequired);

        Debug.Log($"[ExpBar] '{name}' → PlayerLevelSystem on '{_levelSystem.name}'.");
    }

    private void OnDestroy()
    {
        if (_levelSystem != null)
            _levelSystem.OnExpChanged -= OnExpChanged;
    }

    private void Update()
    {
        if (_fillImage == null) return;

        // Poll fallback: initialize once the NetworkObject becomes available.
        // Handles the case where Start() ran before OnNetworkSpawn on the level system.
        if (!_initialized && _levelSystem != null && _levelSystem.IsSpawned)
        {
            SnapFill(_levelSystem.CurrentExp, _levelSystem.ExpRequired);
        }

        // Animate fill toward target
        _fillImage.fillAmount = Mathf.MoveTowards(
            _fillImage.fillAmount, _target, _fillSpeed * Time.deltaTime);

        // Billboard: rotate to face the camera (camera check is free, no allocations)
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    // Instantly snaps the fill to the current EXP ratio (no animation).
    // Called on first initialization to avoid the bar "filling up" from zero.
    private void SnapFill(int currentExp, int expRequired)
    {
        _target = expRequired > 0 ? (float)currentExp / expRequired : 1f;

        if (_fillImage != null)
            _fillImage.fillAmount = _target;

        _initialized = true;
    }

    // Called by PlayerLevelSystem.OnExpChanged — updates the animated target.
    private void OnExpChanged(int currentExp, int expRequired)
    {
        // When max level is reached, expRequired will be int.MaxValue — show full bar
        if (expRequired <= 0 || expRequired == int.MaxValue)
        {
            _target = 1f;
            return;
        }

        _target = (float)currentExp / expRequired;
        _initialized = true;
    }
}
