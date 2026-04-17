using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Stateless utility that spawns enemy instances at valid NavMesh positions.
// Has one job: given an origin and an AttackDefinition, summon the configured enemies.
//
// Position strategy:
//   Picks a random point within summoningSpreadRadius around the origin,
//   then snaps it to the nearest NavMesh surface via NavMesh.SamplePosition.
//   Retries up to MaxAttempts before falling back to the origin itself.
//   If the origin also fails the NavMesh check, that summon is skipped with a warning.
//
// Networking:
//   Server: spawns via NetworkObject.Spawn() so all clients receive the new enemy.
//   Standalone: plain Instantiate — no NGO session active.
public static class SummoningAttackExecutor
{
    private const int MaxNavMeshAttempts = 10;

    // Spawns enemies defined by the AttackDefinition near origin.
    // Returns the number of enemies successfully spawned.
    public static int Execute(Transform origin, AttackDefinition attackDefinition)
    {
        // ── Validation ───────────────────────────────────────────────────────

        if (origin == null)
        {
            Debug.LogError("[SummoningAttackExecutor] Origin transform is null.");
            return 0;
        }

        if (attackDefinition == null)
        {
            Debug.LogError("[SummoningAttackExecutor] AttackDefinition is null.");
            return 0;
        }

        if (attackDefinition.AttackType != AttackType.Summoning)
        {
            Debug.LogError($"[SummoningAttackExecutor] AttackDefinition '{attackDefinition.AttackId}' " +
                           $"is type {attackDefinition.AttackType}, expected Summoning.");
            return 0;
        }

        if (attackDefinition.SummoningPrefab == null)
        {
            Debug.LogError($"[SummoningAttackExecutor] AttackDefinition '{attackDefinition.AttackId}' " +
                           $"has no SummoningPrefab assigned.");
            return 0;
        }

        // ── Spawn loop ───────────────────────────────────────────────────────

        int count   = Mathf.Max(1, attackDefinition.SummoningCount);
        int spawned = 0;

        bool isNetworkedServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        for (int i = 0; i < count; i++)
        {
            if (!TryFindNavMeshPosition(origin.position, attackDefinition.SummoningSpreadRadius,
                                        attackDefinition.SummoningNavMeshSearchRadius, out Vector3 spawnPos))
            {
                Debug.LogWarning($"[SummoningAttackExecutor] '{origin.name}' — summon {i} skipped: " +
                                 $"no valid NavMesh position found near {origin.position} " +
                                 $"(spread:{attackDefinition.SummoningSpreadRadius}, " +
                                 $"searchRadius:{attackDefinition.SummoningNavMeshSearchRadius}).");
                continue;
            }

            GameObject minion = Object.Instantiate(
                attackDefinition.SummoningPrefab,
                spawnPos,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            );

            // Apply level before network spawn — EnemyInitializer reads it in OnNetworkSpawn.
            var initializer = minion.GetComponent<EnemyInitializer>();
            if (initializer != null)
                initializer.SetLevel(attackDefinition.SummoningMinionLevel);
            else
                Debug.LogWarning($"[SummoningAttackExecutor] Prefab '{attackDefinition.SummoningPrefab.name}' " +
                                 "has no EnemyInitializer — spawned at default level.");

            if (isNetworkedServer)
            {
                var netObj = minion.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(destroyWithScene: true);
                }
                else
                {
                    Debug.LogError($"[SummoningAttackExecutor] Prefab '{attackDefinition.SummoningPrefab.name}' " +
                                   "has no NetworkObject — destroy and skip in multiplayer mode.");
                    Object.Destroy(minion);
                    continue;
                }
            }

            spawned++;

            Debug.Log($"[SummoningAttackExecutor] '{origin.name}' summoned '{minion.name}' " +
                      $"at {spawnPos} (level {attackDefinition.SummoningMinionLevel}).");
        }

        Debug.Log($"[SummoningAttackExecutor] '{origin.name}' — {spawned}/{count} minion(s) summoned " +
                  $"('{attackDefinition.AttackId}').");

        return spawned;
    }

    // ── NavMesh sampling ─────────────────────────────────────────────────────

    // Tries random points within spreadRadius, snapping each to the NavMesh.
    // Falls back to sampling origin directly before giving up.
    private static bool TryFindNavMeshPosition(Vector3 origin, float spreadRadius,
                                                float searchRadius, out Vector3 result)
    {
        // Attempt random scatter positions first
        for (int attempt = 0; attempt < MaxNavMeshAttempts; attempt++)
        {
            Vector2 circle    = Random.insideUnitCircle * spreadRadius;
            Vector3 candidate = origin + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, searchRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        // Last resort: sample directly at origin
        if (NavMesh.SamplePosition(origin, out NavMeshHit fallback, searchRadius, NavMesh.AllAreas))
        {
            result = fallback.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }
}
