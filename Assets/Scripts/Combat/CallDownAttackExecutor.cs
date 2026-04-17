using UnityEngine;

// Stateless utility that spawns CallDownZone instances at strike locations.
// Has one job: given an origin, a target position, and an AttackDefinition, plant the zones.
//
// The caller (brain/AI) decides the target position — it could be the player's current position,
// a random map location, a predicted position, or anything else. The executor is position-agnostic.
//
// For multi-zone attacks (callDownTargetCount > 1), the first zone lands at targetPosition
// and the rest are scattered randomly within callDownSpreadRadius around it.
public static class CallDownAttackExecutor
{
    // Spawns CallDownZone(s) and arms them. Returns the number of zones successfully spawned.
    //
    // damageOverride: when >= 0, replaces attackDefinition.Damage — pass AttackController.EffectiveDamage
    //                to apply level scaling without mutating the shared AttackDefinition asset.
    public static int Execute(Transform origin, Vector3 targetPosition, AttackDefinition attackDefinition,
                               int damageOverride = -1)
    {
        // ── Validation ───────────────────────────────────────────────────────

        if (origin == null)
        {
            Debug.LogError("[CallDownAttackExecutor] Origin transform is null.");
            return 0;
        }

        if (attackDefinition == null)
        {
            Debug.LogError("[CallDownAttackExecutor] AttackDefinition is null.");
            return 0;
        }

        if (attackDefinition.AttackType != AttackType.CallDown)
        {
            Debug.LogError($"[CallDownAttackExecutor] AttackDefinition '{attackDefinition.AttackId}' " +
                           $"is type {attackDefinition.AttackType}, expected CallDown.");
            return 0;
        }

        if (attackDefinition.CallDownZonePrefab == null)
        {
            Debug.LogError($"[CallDownAttackExecutor] AttackDefinition '{attackDefinition.AttackId}' " +
                           $"has no CallDownZonePrefab assigned.");
            return 0;
        }

        // ── Prepare shared data ──────────────────────────────────────────────

        int finalDamage = damageOverride >= 0 ? damageOverride : attackDefinition.Damage;
        int count       = Mathf.Max(1, attackDefinition.CallDownTargetCount);

        var elementApplication = new ElementApplication(
            element:  attackDefinition.ElementType,
            strength: attackDefinition.ElementStrength,
            source:   origin.gameObject
        );

        // ── Spawn zones ──────────────────────────────────────────────────────

        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 strikePos = targetPosition;

            // First zone at the exact target; additional zones scatter around it.
            if (i > 0)
            {
                Vector2 offset = Random.insideUnitCircle * attackDefinition.CallDownSpreadRadius;
                strikePos = targetPosition + new Vector3(offset.x, 0f, offset.y);
            }

            // Keep y at the target's ground level — avoid floating or sunken zones.
            strikePos.y = targetPosition.y;

            CallDownZone zone = Object.Instantiate(
                attackDefinition.CallDownZonePrefab,
                strikePos,
                Quaternion.identity
            );

            zone.Initialize(
                damage:             finalDamage,
                source:             origin.gameObject,
                warnDuration:       attackDefinition.CallDownWarnDuration,
                aoeRadius:          attackDefinition.CallDownAoeRadius,
                targetTag:          attackDefinition.CallDownTargetTag,
                elementApplication: elementApplication
            );

            spawned++;
        }

        Debug.Log($"[CallDownAttackExecutor] '{origin.name}' spawned {spawned} CallDown zone(s) " +
                  $"around {targetPosition} ('{attackDefinition.AttackId}').");

        return spawned;
    }
}
