using UnityEngine;

// Drives enemy animations by hooking into DeathController events.
//
// Setup:
//   1. Add this component to the enemy prefab alongside DeathController.
//   2. Assign the Animator (or leave it to auto-resolve on the same GameObject).
//   3. Set the trigger name to match the parameter in your Animator Controller.
//
// Death animation timing:
//   The enemy stays visible during the death animation.
//   For this to work, do NOT put the enemy's Renderer in DeathController._disableRenderers.
//   Set DeathController._destroyDelay to match the length of your death clip.
//
// Animation not created yet:
//   When no Animator is assigned or the trigger name doesn't exist, the component
//   logs a placeholder message. Wire up the real Animator once the clip is ready.
[DisallowMultipleComponent]
public class EnemyAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("The Animator to drive. Auto-resolved from the same GameObject if left empty.")]
    [SerializeField] private Animator _animator;

    [Header("Death Animation")]
    [Tooltip("Name of the Animator trigger parameter to set when the enemy dies. " +
             "Must match exactly what is defined in the Animator Controller.")]
    [SerializeField] private string _deathTriggerName = "Die";

    // ── Private references ────────────────────────────────────────────────────

    private DeathController _deathController;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-resolve Animator if not assigned in the Inspector.
        if (_animator == null)
            _animator = GetComponent<Animator>();

        _deathController = GetComponent<DeathController>();

        if (_deathController == null)
            Debug.LogWarning($"[EnemyAnimationController] '{name}' has no DeathController — death animation will never trigger.");
    }

    private void OnEnable()
    {
        if (_deathController != null)
            _deathController.OnDied += HandleDeath;
    }

    private void OnDisable()
    {
        if (_deathController != null)
            _deathController.OnDied -= HandleDeath;
    }

    // ── Private handlers ──────────────────────────────────────────────────────

    // Called by DeathController.OnDied the moment the enemy dies.
    // Triggers the death animation if an Animator and valid trigger are configured.
    private void HandleDeath()
    {
        if (_animator == null)
        {
            // No animator assigned — placeholder log until the animation is created.
            Debug.Log($"[EnemyAnimationController] '{name}' died — no Animator assigned yet, skipping death animation.");
            return;
        }

        _animator.SetTrigger(_deathTriggerName);
        Debug.Log($"[EnemyAnimationController] '{name}' triggered death animation '{_deathTriggerName}'.");
    }
}
