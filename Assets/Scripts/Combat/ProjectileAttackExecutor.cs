using UnityEngine;

/// <summary>
/// Stateless utility class responsible for spawning and initializing a Projectile.
/// Has one job: given an origin, a direction, and an AttackDefinition, fire a projectile.
///
/// Does not manage cooldowns, select targets, or contain damage logic.
/// Cooldowns stay in AttackController; target selection stays in the caller (player/enemy AI).
/// </summary>
public static class ProjectileAttackExecutor
{
    // Spawns a projectile at origin and sends it in direction.
    // Returns the spawned Projectile, or null if execution was blocked by a validation failure.
    //
    // damageOverride: when >= 0, replaces attackDefinition.Damage — used by EnemyCombatBrain
    // to apply level-scaled damage (AttackController.EffectiveDamage) without mutating
    // the shared AttackDefinition ScriptableObject asset.
    public static Projectile Execute(Transform origin, Vector3 direction, AttackDefinition attackDefinition, int damageOverride = -1)
    {
        // ── Validation ───────────────────────────────────────────────────────

        if (origin == null)
        {
            Debug.LogError("[ProjectileAttackExecutor] Origin transform is null.");
            return null;
        }

        if (attackDefinition == null)
        {
            Debug.LogError("[ProjectileAttackExecutor] AttackDefinition is null.");
            return null;
        }

        // Guard against using a non-projectile definition by mistake
        if (attackDefinition.AttackType != AttackType.Projectile)
        {
            Debug.LogError($"[ProjectileAttackExecutor] AttackDefinition '{attackDefinition.AttackId}' " +
                           $"is type {attackDefinition.AttackType}, expected Projectile.");
            return null;
        }

        if (attackDefinition.ProjectilePrefab == null)
        {
            Debug.LogError($"[ProjectileAttackExecutor] AttackDefinition '{attackDefinition.AttackId}' " +
                           $"has no ProjectilePrefab assigned.");
            return null;
        }

        if (direction == Vector3.zero)
        {
            Debug.LogWarning("[ProjectileAttackExecutor] Direction is zero — projectile not spawned.");
            return null;
        }

        // ── Spawn ────────────────────────────────────────────────────────────

        Projectile projectile = Object.Instantiate(
            attackDefinition.ProjectilePrefab,
            origin.position,
            Quaternion.LookRotation(direction)   // face the travel direction for correct visuals
        );

        // Build elemental data — Element.None means no element is applied on this hit
        var elementApplication = new ElementApplication(
            element:  attackDefinition.ElementType,
            strength: attackDefinition.ElementStrength,
            source:   origin.gameObject
        );

        // Use the override if provided (level-scaled value from AttackController),
        // otherwise fall back to the base value in the AttackDefinition asset.
        int finalDamage = damageOverride >= 0 ? damageOverride : attackDefinition.Damage;

        projectile.Initialize(
            damage:             finalDamage,
            source:             origin.gameObject,
            direction:          direction,
            speed:              attackDefinition.ProjectileSpeed,
            elementApplication: elementApplication,
            targetTag:          attackDefinition.ProjectileTargetTag
        );

        Debug.Log($"[ProjectileAttackExecutor] '{origin.name}' fired '{attackDefinition.AttackId}'.");

        return projectile;
    }
}
