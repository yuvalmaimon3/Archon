using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Stateless utility class responsible for applying a melee hit to all valid targets
/// within a sphere around the attacker.
///
/// Does not manage cooldowns, select targets, or contain damage logic beyond
/// passing DamageInfo into each target's IDamageable component.
/// Cooldowns stay in AttackController; the caller (player/AI) decides when to execute.
/// </summary>
public static class MeleeAttackExecutor
{
    // Reused across calls to avoid per-frame allocation from OverlapSphere
    private static readonly Collider[] _overlapBuffer = new Collider[32];

    /// <summary>
    /// Detects all IDamageable targets within the melee radius and applies damage to each.
    /// The source object is always excluded. Each valid target is damaged exactly once.
    /// </summary>
    /// <param name="origin">Center of the hit sphere and the damage source reference.</param>
    /// <param name="attackDefinition">Data asset that defines the attack stats and radius.</param>
    public static void Execute(Transform origin, AttackDefinition attackDefinition)
    {
        // ── Validation ───────────────────────────────────────────────────────

        if (origin == null)
        {
            Debug.LogError("[MeleeAttackExecutor] Origin transform is null.");
            return;
        }

        if (attackDefinition == null)
        {
            Debug.LogError("[MeleeAttackExecutor] AttackDefinition is null.");
            return;
        }

        if (attackDefinition.AttackType != AttackType.Melee)
        {
            Debug.LogError($"[MeleeAttackExecutor] AttackDefinition '{attackDefinition.AttackId}' " +
                           $"is type {attackDefinition.AttackType}, expected Melee.");
            return;
        }

        // ── Overlap detection ────────────────────────────────────────────────

        // Non-alloc version writes into the shared buffer and returns the hit count
        int hitCount = Physics.OverlapSphereNonAlloc(
            origin.position,
            attackDefinition.MeleeRadius,
            _overlapBuffer
        );

        // Collect valid damageable targets first, then apply damage.
        // This prevents a kill (and potential Destroy) on the first hit from
        // affecting the collider buffer mid-loop.
        var targets = new List<IDamageable>(hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _overlapBuffer[i];

            // Skip the source object and any of its children
            if (col.transform == origin || col.transform.IsChildOf(origin))
                continue;

            if (col.TryGetComponent<IDamageable>(out var damageable))
                targets.Add(damageable);
        }

        if (targets.Count == 0)
        {
            Debug.Log($"[MeleeAttackExecutor] '{origin.name}' swung '{attackDefinition.AttackId}' — no targets hit.");
            return;
        }

        // ── Apply damage ─────────────────────────────────────────────────────

        var elementApplication = new ElementApplication(
            element:  attackDefinition.ElementType,
            strength: attackDefinition.ElementStrength,
            source:   origin.gameObject
        );

        foreach (IDamageable target in targets)
        {
            // Cast to Component for position and name — IDamageable alone cannot provide these
            var targetComponent = target as Component;
            Vector3 hitPoint    = targetComponent != null ? targetComponent.transform.position : origin.position;
            Vector3 hitDir      = targetComponent != null
                ? (targetComponent.transform.position - origin.position).normalized
                : origin.forward;

            var damageInfo = new DamageInfo(
                amount:             attackDefinition.Damage,
                source:             origin.gameObject,
                hitPoint:           hitPoint,
                hitDirection:       hitDir,
                elementApplication: elementApplication
            );

            target.TakeDamage(damageInfo);
        }

        Debug.Log($"[MeleeAttackExecutor] '{origin.name}' hit {targets.Count} target(s) " +
                  $"with '{attackDefinition.AttackId}'.");
    }
}
