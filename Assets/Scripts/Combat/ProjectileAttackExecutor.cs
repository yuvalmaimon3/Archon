using Unity.Netcode;
using UnityEngine;

// Stateless utility class responsible for spawning and initializing a Projectile.
// Has one job: given an origin, a direction, and an AttackDefinition, fire a projectile.
//
// Networking:
//   When called on a networked server (host or dedicated), spawns via NetworkObject.Spawn()
//   and calls InitializeClientRpc so all clients simulate the same trajectory.
//   When called in standalone/offline mode, uses Object.Instantiate + Initialize() with
//   a local Destroy timer instead.
//
// Does not manage cooldowns, select targets, or contain damage logic.
// Cooldowns stay in AttackController; target selection stays in the caller (player/enemy AI).
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

        int finalDamage = damageOverride >= 0 ? damageOverride : attackDefinition.Damage;

        // ── Network vs standalone initialization ─────────────────────────────

        bool isNetworkedServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        if (isNetworkedServer)
        {
            // Spawn through NGO so all clients receive and simulate the projectile.
            // Without this, the object only exists locally and is invisible to remote clients.
            projectile.NetworkObject.Spawn();

            // Resolve the source as a NetworkObjectReference so clients can identify the shooter.
            // Falls back to default (empty ref) when the origin has no NetworkObject.
            NetworkObjectReference sourceRef = default;
            if (origin.TryGetComponent<NetworkObject>(out var originNetObj))
                sourceRef = new NetworkObjectReference(originNetObj);

            projectile.InitializeClientRpc(
                damage:          finalDamage,
                sourceRef:       sourceRef,
                direction:       direction,
                speed:           attackDefinition.ProjectileSpeed,
                elementType:     attackDefinition.ElementType,
                elementStrength: attackDefinition.ElementStrength,
                targetTag:       attackDefinition.ProjectileTargetTag
            );
        }
        else
        {
            // Standalone / offline mode — no NGO session active.
            // Local Destroy timer replaces the server lifetime coroutine.
            var elementApplication = new ElementApplication(
                element:  attackDefinition.ElementType,
                strength: attackDefinition.ElementStrength,
                source:   origin.gameObject
            );

            projectile.Initialize(
                damage:             finalDamage,
                source:             origin.gameObject,
                direction:          direction,
                speed:              attackDefinition.ProjectileSpeed,
                elementApplication: elementApplication,
                targetTag:          attackDefinition.ProjectileTargetTag
            );
        }

        Debug.Log($"[ProjectileAttackExecutor] '{origin.name}' fired '{attackDefinition.AttackId}'.");

        return projectile;
    }
}
