using UnityEngine;

/// <summary>
/// Plays visual and audio feedback when the player levels up.
/// Attach to the same GameObject as PlayerLevelSystem.
///
/// Current state: base implementation — particle burst + optional audio.
/// TODO: Add character animation trigger, screen flash, floating level-up text popup.
/// </summary>
public class LevelUpEffect : MonoBehaviour
{
    [Header("VFX")]
    [Tooltip("Particle system that bursts on level-up. Assign a child particle effect.")]
    [SerializeField] private ParticleSystem _burstParticle;

    [Header("Audio")]
    [Tooltip("AudioSource used to play the level-up sound. Optional.")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Sound clip played on level-up. Optional.")]
    [SerializeField] private AudioClip _levelUpSound;

    [Header("Debug")]
    [Tooltip("Log level-up events to the console for testing.")]
    [SerializeField] private bool _debugLog = true;

    // ── Private ───────────────────────────────────────────────────────────────

    private PlayerLevelSystem _levelSystem;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _levelSystem = GetComponent<PlayerLevelSystem>();

        if (_levelSystem == null)
            Debug.LogError($"[LevelUpEffect] No PlayerLevelSystem found on '{name}'.", this);
    }

    private void OnEnable()
    {
        if (_levelSystem != null)
            _levelSystem.OnLevelUp += HandleLevelUp;
    }

    private void OnDisable()
    {
        if (_levelSystem != null)
            _levelSystem.OnLevelUp -= HandleLevelUp;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // Triggered on all clients by PlayerLevelSystem.TriggerLevelUpEffectsClientRpc.
    private void HandleLevelUp(int newLevel)
    {
        if (_debugLog)
            Debug.Log($"[LevelUpEffect] '{name}' reached level {newLevel}! Playing effects.");

        PlayParticle();
        PlaySound();

        // TODO: Trigger character animation ("LevelUp" trigger on Animator)
        // TODO: Spawn floating text "+LEVEL UP!" above the player
        // TODO: Play screen-space flash or vignette effect
    }

    // Plays the burst particle system from the beginning.
    private void PlayParticle()
    {
        if (_burstParticle == null) return;

        // Stop + clear first so it restarts cleanly if the player levels up in quick succession
        _burstParticle.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
        _burstParticle.Play(withChildren: true);
    }

    // Plays the level-up sound one-shot on the assigned AudioSource.
    private void PlaySound()
    {
        if (_audioSource == null || _levelUpSound == null) return;
        _audioSource.PlayOneShot(_levelUpSound);
    }
}
