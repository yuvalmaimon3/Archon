using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

// Handles periodic minion summoning for the Summoner enemy.
//
// Summoning cycle:
//   1. Summon a full batch (minionsPerWave).
//   2. Wait until every minion in the batch is dead.
//   3. Start cooldown (summonInterval).
//   4. Repeat.
//
// Only one batch is ever on the field at a time. No new summons happen while
// any minion from the last batch is still alive.
//
// Runs server-only. Clients see results via NGO-spawned NetworkObjects.
public class MinionSummoner : NetworkBehaviour, IDeathHandler
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Summon Settings")]
    [Tooltip("The minion prefab to spawn. Must have a NetworkObject component.")]
    [SerializeField] private GameObject minionPrefab;

    [Tooltip("Seconds to wait after the last batch minion dies before summoning again.")]
    [Min(0.5f)]
    [SerializeField] private float summonInterval = 5f;

    [Tooltip("How many minions to spawn per batch.")]
    [Min(1)]
    [SerializeField] private int minionsPerWave = 2;

    [Tooltip("Radius around the summoner in which minions scatter on spawn.")]
    [Min(0f)]
    [SerializeField] private float spawnRadius = 2f;

    [Tooltip("Max search radius for NavMesh.SamplePosition when finding a spawn point.")]
    [Min(0.5f)]
    [SerializeField] private float navMeshSearchRadius = 2f;

    [Tooltip("Spawn minions within a 180° forward arc instead of all around.")]
    [SerializeField] private bool spawnInFront = false;

    [Header("Level Scaling")]
    [Tooltip("Level applied to each spawned minion via EnemyInitializer.")]
    [Min(1)]
    [SerializeField] private int minionLevel = 1;

    // ── Private state ────────────────────────────────────────────────────────

    // Current batch of live minions. Pruned every Update tick.
    private readonly List<NetworkObject> _activeMinions = new();

    // True while at least one minion from the current batch is alive.
    private bool _batchAlive;

    // Cooldown timer after the last batch member dies.
    private float _cooldownEndTime;

    private bool _isDead;

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // Initial delay before the first summon.
        _cooldownEndTime = Time.time + summonInterval;

        Debug.Log($"[MinionSummoner] '{name}' ready — first summon in {summonInterval:F1}s.");
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Update()
    {
        if (!IsServer) return;
        if (_isDead) return;
        if (minionPrefab == null) return;

        if (_batchAlive)
        {
            PruneDeadMinions();

            // All batch minions have died — start cooldown.
            if (_activeMinions.Count == 0)
            {
                _batchAlive      = false;
                _cooldownEndTime = Time.time + summonInterval;

                Debug.Log($"[MinionSummoner] '{name}' — batch cleared. " +
                          $"Next summon in {summonInterval:F1}s.");
            }
        }
        else if (Time.time >= _cooldownEndTime)
        {
            SummonBatch();
        }
    }

    // ── IDeathHandler ────────────────────────────────────────────────────────

    public void OnDeath()
    {
        _isDead = true;
        _activeMinions.Clear();

        Debug.Log($"[MinionSummoner] '{name}' died — no further summons.");
    }

    // ── Private logic ────────────────────────────────────────────────────────

    private void SummonBatch()
    {
        Debug.Log($"[MinionSummoner] '{name}' — summoning batch of {minionsPerWave}.");

        for (int i = 0; i < minionsPerWave; i++)
            SpawnMinion(i);

        // Mark batch alive even if some individual spawns failed,
        // as long as at least one succeeded.
        _batchAlive = _activeMinions.Count > 0;
    }

    private void SpawnMinion(int index)
    {
        if (!TryFindNavMeshPosition(out Vector3 spawnPos))
        {
            Debug.LogWarning($"[MinionSummoner] '{name}' — minion {index} skipped: " +
                             $"no valid NavMesh position near {transform.position}.");
            return;
        }

        GameObject minion = Instantiate(minionPrefab, spawnPos, Quaternion.identity);

        var initializer = minion.GetComponent<EnemyInitializer>();
        if (initializer != null)
            initializer.SetLevel(minionLevel);

        var netObj = minion.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[MinionSummoner] '{minionPrefab.name}' has no NetworkObject — minion destroyed.");
            Destroy(minion);
            return;
        }

        netObj.Spawn(destroyWithScene: true);
        _activeMinions.Add(netObj);

        Debug.Log($"[MinionSummoner] '{name}' — minion {index} spawned at {spawnPos}.");
    }

    private bool TryFindNavMeshPosition(out Vector3 result)
    {
        const int maxAttempts = 10;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 candidate = spawnInFront ? GetFrontArcCandidate() : GetCircleCandidate();

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }

        // Fallback: sample at summoner's own position.
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit fallback, navMeshSearchRadius, NavMesh.AllAreas))
        {
            result = fallback.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }

    private Vector3 GetCircleCandidate()
    {
        Vector2 circle = Random.insideUnitCircle * spawnRadius;
        return transform.position + new Vector3(circle.x, 0f, circle.y);
    }

    private Vector3 GetFrontArcCandidate()
    {
        float   angle = Random.Range(-90f, 90f);
        float   dist  = Random.Range(0f, spawnRadius);
        Vector3 dir   = Quaternion.Euler(0f, angle, 0f) * transform.forward;
        return transform.position + dir * dist;
    }

    private void PruneDeadMinions()
    {
        int before = _activeMinions.Count;
        _activeMinions.RemoveAll(m => m == null || !m.IsSpawned);
        int pruned = before - _activeMinions.Count;

        if (pruned > 0)
            Debug.Log($"[MinionSummoner] '{name}' — pruned {pruned} dead minion(s).");
    }
}
