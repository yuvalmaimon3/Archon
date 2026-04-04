using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour that applies repeated damage to every IDamageable target
/// that stays in trigger contact with this object.
///
/// Unlike the static projectile and melee executors, contact damage is sustained
/// over time — it needs the Unity lifecycle (OnTriggerEnter/Exit, Update),
/// which is why this is a MonoBehaviour rather than a static class.
///
/// Multiple simultaneous contacts are supported: each target gets its own
/// independent tick timer so they can never steal ticks from each other.
///
/// Setup requirements:
///   - A Collider set to Is Trigger on this GameObject (or a child)
///   - An AttackDefinition with AttackType = Contact assigned in the Inspector
/// </summary>
[RequireComponent(typeof(Collider))]
public class ContactDamageDealer : MonoBehaviour
{
    [Header("Attack")]
    [Tooltip("Must be a Contact-type AttackDefinition.")]
    [SerializeField] private AttackDefinition attackDefinition;

    // The attack definition driving this dealer's damage and tick interval.
    public AttackDefinition AttackDefinition => attackDefinition;

    // Level-scaled damage multiplier set by EnemyInitializer.
    // Applied in ApplyDamageTo() so the shared AttackDefinition asset is never mutated.
    // 1.0 by default = no scaling until EnemyInitializer provides a level.
    private float _damageMultiplier = 1f;

    // ── Active contacts ──────────────────────────────────────────────────────
    // Maps each active target to the Time.time when it is next eligible for a tick.
    // Using IDamageable as key keeps this decoupled from any specific component type.
    private readonly Dictionary<IDamageable, float> _activeTargets = new();

    // Reused each Update to avoid allocating a new List every frame.
    // Snapshot of keys is needed because a tick can kill a target, modifying the dictionary.
    private readonly List<IDamageable> _tickSnapshot = new();

    // Collects targets whose underlying GameObject was destroyed without OnTriggerExit.
    // Cleaned up at the end of each Update pass.
    private readonly List<IDamageable> _staleTargets = new();

    // Cached element application — rebuilt in Awake so it is not reconstructed each tick
    private ElementApplication _elementApplication;

    // ── Public API ───────────────────────────────────────────────────────────

    // Sets the damage multiplier applied on top of AttackDefinition.Damage each tick.
    // Called by EnemyInitializer after computing scaled stats for the enemy's level.
    public void SetDamageMultiplier(float multiplier)
    {
        _damageMultiplier = Mathf.Max(0f, multiplier);
        Debug.Log($"[ContactDamageDealer] {gameObject.name} — damage multiplier set to {_damageMultiplier:F2}.");
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (attackDefinition == null)
        {
            Debug.LogError($"[ContactDamageDealer] {gameObject.name} has no AttackDefinition assigned.", this);
            return;
        }

        if (attackDefinition.AttackType != AttackType.Contact)
        {
            Debug.LogError($"[ContactDamageDealer] {gameObject.name} — AttackDefinition " +
                           $"'{attackDefinition.AttackId}' is type {attackDefinition.AttackType}, expected Contact.", this);
            return;
        }

        // Cache element application once; element data never changes at runtime
        _elementApplication = new ElementApplication(
            element:  attackDefinition.ElementType,
            strength: attackDefinition.ElementStrength,
            source:   gameObject
        );
    }

    private void Update()
    {
        if (attackDefinition == null || _activeTargets.Count == 0) return;

        // Snapshot keys into a reusable list to avoid dictionary-modification during iteration
        _tickSnapshot.Clear();
        _tickSnapshot.AddRange(_activeTargets.Keys);

        _staleTargets.Clear();

        foreach (IDamageable target in _tickSnapshot)
        {
            // If the target's GameObject was destroyed (e.g. enemy despawned),
            // OnTriggerExit never fires. Mark it for removal instead of crashing.
            var targetComponent = target as Component;
            if (targetComponent == null)
            {
                _staleTargets.Add(target);
                continue;
            }

            // Target may have been removed by OnTriggerExit between the snapshot and now
            if (!_activeTargets.TryGetValue(target, out float nextTickTime)) continue;

            if (Time.time < nextTickTime) continue;

            // Schedule the next tick before applying damage —
            // prevents a re-entry issue if TakeDamage triggers another event
            _activeTargets[target] = Time.time + attackDefinition.ContactTickInterval;

            ApplyDamageTo(target);
        }

        // Clean up targets whose GameObjects were destroyed without triggering OnTriggerExit
        foreach (IDamageable stale in _staleTargets)
            _activeTargets.Remove(stale);
    }

    // ── Trigger callbacks ────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!IsValidTarget(other, out IDamageable damageable)) return;

        if (_activeTargets.ContainsKey(damageable)) return;

        // Tick immediately on first contact, then follow the interval
        _activeTargets[damageable] = 0f;

        Debug.Log($"[ContactDamageDealer] {gameObject.name} — contact started with '{other.gameObject.name}'.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidTarget(other, out IDamageable damageable)) return;

        _activeTargets.Remove(damageable);

        Debug.Log($"[ContactDamageDealer] {gameObject.name} — contact ended with '{other.gameObject.name}'.");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the collider belongs to a valid damageable target
    /// (not the source itself, has IDamageable, definition is ready).
    /// </summary>
    private bool IsValidTarget(Collider other, out IDamageable damageable)
    {
        damageable = null;

        if (attackDefinition == null) return false;

        // Exclude this object and any of its children (e.g. multi-collider setups)
        if (other.transform == transform || other.transform.IsChildOf(transform)) return false;

        return other.TryGetComponent(out damageable);
    }

    /// <summary>
    /// Builds a DamageInfo for the target and calls TakeDamage.
    /// HitPoint and HitDirection are approximated from transform positions.
    /// </summary>
    private void ApplyDamageTo(IDamageable target)
    {
        var targetComponent = target as Component;
        Vector3 hitPoint    = targetComponent != null ? targetComponent.transform.position : transform.position;
        Vector3 hitDir      = targetComponent != null
            ? (targetComponent.transform.position - transform.position).normalized
            : transform.forward;

        // Apply level multiplier — base damage from asset × multiplier set by EnemyInitializer.
        int finalDamage = Mathf.Max(0, Mathf.RoundToInt(attackDefinition.Damage * _damageMultiplier));

        var damageInfo = new DamageInfo(
            amount:             finalDamage,
            source:             gameObject,
            hitPoint:           hitPoint,
            hitDirection:       hitDir,
            elementApplication: _elementApplication
        );

        target.TakeDamage(damageInfo);

        Debug.Log($"[ContactDamageDealer] {gameObject.name} — ticked '{(targetComponent != null ? targetComponent.gameObject.name : "unknown")}' " +
                  $"for {finalDamage} damage.");
    }
}
