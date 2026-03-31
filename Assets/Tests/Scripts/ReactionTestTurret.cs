using UnityEngine;

/// <summary>
/// Fires projectiles at a target enemy in a cyclic pattern, alternating between two attack definitions.
/// Designed for the TestReactions scene to validate all elemental reaction combinations with no time limit.
///
/// Each turret tests one reaction pair (e.g. Ice → Fire for Thermal Shock).
/// The cycle repeats indefinitely: A → B → A → B → ...
/// </summary>
public class ReactionTestTurret : MonoBehaviour
{
    [Header("Attack Cycle")]
    [Tooltip("Attack definitions to cycle through in order. Typically 2 entries: first element then second element.")]
    [SerializeField] private AttackDefinition[] _attackCycle;

    [Header("Target")]
    [Tooltip("The enemy Transform to aim and fire at.")]
    [SerializeField] private Transform _target;

    [Header("Timing")]
    [Tooltip("Seconds between each shot.")]
    [SerializeField] private float _shotInterval = 1f;

    [Header("Info")]
    [Tooltip("Human-readable label shown in debug logs to identify this turret's reaction test.")]
    [SerializeField] private string _reactionLabel;

    // ── Private state ────────────────────────────────────────────────────────

    private int _currentIndex;
    private float _nextShotTime;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Update()
    {
        if (_target == null || _attackCycle == null || _attackCycle.Length == 0) return;
        if (Time.time < _nextShotTime) return;

        FireNext();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Fires the next attack in the cycle toward the target, then advances the cycle index.
    /// </summary>
    private void FireNext()
    {
        AttackDefinition attack = _attackCycle[_currentIndex];

        if (attack == null)
        {
            Debug.LogWarning($"[ReactionTestTurret] {_reactionLabel}: attack at index {_currentIndex} is null — skipping.");
            AdvanceCycle();
            return;
        }

        Vector3 direction = (_target.position - transform.position).normalized;
        Projectile fired = ProjectileAttackExecutor.Execute(transform, direction, attack);

        if (fired != null)
            Debug.Log($"[ReactionTestTurret] {_reactionLabel} — shot {_currentIndex + 1}/{_attackCycle.Length}: {attack.ElementType}");

        AdvanceCycle();
    }

    /// <summary>Moves to the next index in the cycle and schedules the next shot.</summary>
    private void AdvanceCycle()
    {
        _currentIndex = (_currentIndex + 1) % _attackCycle.Length;
        _nextShotTime = Time.time + _shotInterval;
    }
}
