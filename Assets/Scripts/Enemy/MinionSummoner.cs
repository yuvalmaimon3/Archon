using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Handles periodic minion summoning for the Summoner enemy.
//
// Design intent:
//   - Runs entirely on the server (NGO). Clients see the result via NGO-spawned NetworkObjects.
//   - Runs INDEPENDENTLY of the combat brain — summons happen on a fixed timer regardless
//     of what the brain is doing (attacking, retreating, repositioning).
//   - Tracks active minions by NetworkObject reference so we can enforce a max cap.
//     Dead or despawned minions are pruned from the list before each summon wave.
//   - Implements IDeathHandler so DeathController automatically stops summons on death.
//
// Usage (prefab setup):
//   - Attach to the Summoner prefab alongside SummonerBrain and SummonerMovement.
//   - Assign the minionPrefab (e.g. Goblin prefab with NetworkObject).
//   - Tune summonInterval, minionsPerWave, spawnRadius, and maxActiveMinions in the Inspector.
public class MinionSummoner : NetworkBehaviour, IDeathHandler
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Summon Settings")]
    [Tooltip("The minion prefab to spawn. Must have a NetworkObject component.")]
    [SerializeField] private GameObject minionPrefab;

    [Tooltip("Seconds between each summon wave.")]
    [Min(0.5f)]
    [SerializeField] private float summonInterval = 5f;

    [Tooltip("How many minions to attempt spawning per wave.")]
    [Min(1)]
    [SerializeField] private int minionsPerWave = 2;

    [Tooltip("Radius around the summoner in which minions are scattered on spawn.")]
    [Min(0f)]
    [SerializeField] private float spawnRadius = 2f;

    [Tooltip("Max search radius passed to NavMesh.SamplePosition when finding a valid spawn point.")]
    [Min(0.5f)]
    [SerializeField] private float navMeshSearchRadius = 2f;

    [Tooltip("Maximum number of concurrently active summoned minions. " +
             "Summon waves are skipped when this cap is reached.")]
    [Min(1)]
    [SerializeField] private int maxActiveMinions = 6;

    [Header("Level Scaling")]
    [Tooltip("Level applied to each spawned minion via EnemyInitializer. " +
             "Typically set to match or trail the summoner's own level.")]
    [Min(1)]
    [SerializeField] private int minionLevel = 1;

    // ── Private state ────────────────────────────────────────────────────────

    // Server-side timestamp for the next summon wave (Time.time).
    private float _nextSummonTime;

    // Tracks all NetworkObjects this summoner has spawned.
    // Null references (destroyed minions) are pruned before each wave.
    private readonly List<NetworkObject> _activeMinions = new();

    // Set to true by OnDeath() — stops further summons without destroying this component.
    private bool _isDead;

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Start the first summon after one full interval so minions don't spawn instantly.
        _nextSummonTime = Time.time + summonInterval;

        Debug.Log($"[MinionSummoner] '{name}' ready — first summon in {summonInterval:F1}s.");
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Update()
    {
        // Summon logic is server-only — clients never call this.
        if (!IsServer) return;
        if (_isDead) return;
        if (minionPrefab == null) return;
        if (Time.time < _nextSummonTime) return;

        TrySummonWave();

        // Schedule next wave regardless of whether this wave spawned anything
        // (even if capped, we check again after the full interval).
        _nextSummonTime = Time.time + summonInterval;
    }

    // ── IDeathHandler ────────────────────────────────────────────────────────

    // Called automatically by DeathController when the summoner's HP reaches zero.
    // Existing minions are NOT despawned — they continue fighting independently.
    public void OnDeath()
    {
        _isDead = true;
        _activeMinions.Clear();

        Debug.Log($"[MinionSummoner] '{name}' died — no further summons.");
    }

    // ── Private logic ────────────────────────────────────────────────────────

    // Checks the cap, then spawns up to minionsPerWave new minions.
    private void TrySummonWave()
    {
        PruneDeadMinions();

        int slotsAvailable = maxActiveMinions - _activeMinions.Count;

        if (slotsAvailable <= 0)
        {
            Debug.Log($"[MinionSummoner] '{name}' — summon wave skipped: " +
                      $"cap of {maxActiveMinions} active minions reached.");
            return;
        }

        int toSpawn = Mathf.Min(minionsPerWave, slotsAvailable);

        Debug.Log($"[MinionSummoner] '{name}' — summoning {toSpawn} minion(s) " +
                  $"({_activeMinions.Count}/{maxActiveMinions} currently active).");

        for (int i = 0; i < toSpawn; i++)
        {
            SpawnMinion(i);
        }
    }

    // Instantiates and NGO-spawns a single minion at a valid NavMesh position near the summoner.
    // Applies level before spawning so OnNetworkSpawn on the minion sees the right level.
    private void SpawnMinion(int index)
    {
        // Find a valid NavMesh point — retries random scatter positions, falls back to origin.
        if (!TryFindNavMeshPosition(out Vector3 spawnPos))
        {
            Debug.LogWarning($"[MinionSummoner] '{name}' — minion {index} skipped: " +
                             $"no valid NavMesh position found near {transform.position}.");
            return;
        }

        GameObject minion = Instantiate(minionPrefab, spawnPos, Quaternion.identity);

        // Apply level BEFORE NGO spawn — EnemyInitializer reads it in OnNetworkSpawn.
        var initializer = minion.GetComponent<EnemyInitializer>();
        if (initializer != null)
            initializer.SetLevel(minionLevel);
        else
            Debug.LogWarning($"[MinionSummoner] minionPrefab '{minionPrefab.name}' has no EnemyInitializer — " +
                             "minion will spawn at default level.");

        var netObj = minion.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[MinionSummoner] minionPrefab '{minionPrefab.name}' has no NetworkObject. " +
                           $"Minion {index} destroyed.");
            Destroy(minion);
            return;
        }

        netObj.Spawn(destroyWithScene: true);

        // Track this minion so we can enforce the cap on future waves.
        _activeMinions.Add(netObj);

        Debug.Log($"[MinionSummoner] '{name}' — minion {index} spawned at {spawnPos}.");
    }

    // Tries random scatter positions within spawnRadius, snapping each to the NavMesh.
    // Falls back to sampling the summoner's own position before giving up.
    private bool TryFindNavMeshPosition(out Vector3 result)
    {
        const int maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 circle    = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        // Last resort: sample at the summoner's own position
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit fallback, navMeshSearchRadius, NavMesh.AllAreas))
        {
            result = fallback.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    // Removes null (destroyed) or despawned NetworkObject entries from the tracking list.
    // Called before each wave to keep the count accurate.
    private void PruneDeadMinions()
    {
        int before = _activeMinions.Count;
        _activeMinions.RemoveAll(m => m == null || !m.IsSpawned);
        int pruned = before - _activeMinions.Count;

        if (pruned > 0)
            Debug.Log($"[MinionSummoner] '{name}' — pruned {pruned} dead minion reference(s).");
    }
}
