using System;
using System.Collections.Generic;
using UnityEngine;

// Spawns enemy groups for a round and tracks how many enemies remain alive.
// When the last enemy dies, fires OnAllEnemiesDefeated.
//
// SERVER-ONLY: This component is only ever called from RoomManager while IsServer.
// Clients never call SpawnRound() — NGO propagates spawned NetworkObjects to them automatically.
//
// Spawn position priority:
//   1. SpawnPoints (Transform[]) — explicit anchors placed in the scene
//   2. Random positions within the room bounds (fallback when none assigned)
public class EnemySpawner : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Explicit spawn anchors. If empty, enemies spawn at random positions inside the room.")]
    [SerializeField] private Transform[] _spawnPoints;

    [Tooltip("Minimum distance from room centre when using random placement (keeps player start clear).")]
    [SerializeField] private float _centreExclusionRadius = 4f;

    // ── Events ────────────────────────────────────────────────────────────────

    // Fired on the server when every enemy in the current batch has been defeated.
    public event Action OnAllEnemiesDefeated;

    // Fired on the server each time an enemy dies. Passes the remaining alive count.
    public event Action<int> OnEnemyDefeated;

    // ── State (server-side only) ───────────────────────────────────────────────

    // Room bounds for random placement. Set via SetSpawnBounds() after room generation.
    private float _roomWidth;
    private float _roomLength;
    private float _wallMargin = 2f;

    // How many enemies from the current round batch are still alive.
    private int _aliveCount;

    // All enemy instances currently alive in the scene — for cleanup between rounds.
    private readonly List<GameObject> _spawnedEnemies = new();

    // ── Public API ────────────────────────────────────────────────────────────

    // Configures the play area for random spawn placement.
    // Call after generating the room, before the first SpawnRound.
    public void SetSpawnBounds(float width, float length, float wallMargin = 2f)
    {
        _roomWidth  = width;
        _roomLength = length;
        _wallMargin = wallMargin;
    }

    // Clears any survivors from the previous round, then spawns all groups for the new round.
    // Each SpawnGroup defines an enemy type and how many of that type to create.
    // Only valid to call on the server — NGO syncs spawned NetworkObjects to clients.
    public void SpawnRound(RoundConfig config)
    {
        ClearSpawnedEnemies();
        _aliveCount = 0;

        if (config.SpawnGroups == null || config.SpawnGroups.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] Round config has no spawn groups — completing immediately.");
            OnAllEnemiesDefeated?.Invoke();
            return;
        }

        // Spawn every group: for each group, instantiate Count enemies of that type.
        foreach (var group in config.SpawnGroups)
        {
            if (group.EnemyPrefab == null)
            {
                Debug.LogWarning("[EnemySpawner] SpawnGroup has null prefab — skipped.");
                continue;
            }

            for (int i = 0; i < group.Count; i++)
                SpawnEnemy(group.EnemyPrefab);
        }

        // Edge case: all prefabs were null, nothing actually spawned.
        if (_aliveCount == 0)
        {
            Debug.LogWarning("[EnemySpawner] No valid enemies spawned — firing AllDefeated immediately.");
            OnAllEnemiesDefeated?.Invoke();
        }
    }

    // Destroys any surviving enemies from the previous round.
    // Called automatically by SpawnRound; can also be called externally on room reset.
    public void ClearSpawnedEnemies()
    {
        foreach (var enemy in _spawnedEnemies)
            if (enemy != null) Destroy(enemy);

        _spawnedEnemies.Clear();
        _aliveCount = 0;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    // Instantiates one enemy and hooks its death event for alive-count tracking.
    private void SpawnEnemy(GameObject prefab)
    {
        Vector3 pos      = ChooseSpawnPosition();
        float   rotation = UnityEngine.Random.Range(0f, 360f);
        var     instance = Instantiate(prefab, pos, Quaternion.Euler(0f, rotation, 0f));

        _spawnedEnemies.Add(instance);

        // Prefer DeathController.OnDied (fires after full cleanup) over Health.OnDeath.
        bool hooked = false;

        if (instance.TryGetComponent<DeathController>(out var dc))
        {
            dc.OnDied += HandleEnemyDied;
            hooked = true;
        }
        else if (instance.TryGetComponent<Health>(out var hp))
        {
            hp.OnDeath += _ => HandleEnemyDied();
            hooked = true;
        }

        if (hooked)
        {
            _aliveCount++;
            Debug.Log($"[EnemySpawner] Spawned '{instance.name}' at {pos}. Alive: {_aliveCount}");
        }
        else
        {
            Debug.LogWarning($"[EnemySpawner] '{instance.name}' has no DeathController or Health — " +
                             "cannot track death, not counted.");
        }
    }

    // Called on the server whenever a tracked enemy dies.
    private void HandleEnemyDied()
    {
        _aliveCount = Mathf.Max(0, _aliveCount - 1);
        OnEnemyDefeated?.Invoke(_aliveCount);

        Debug.Log($"[EnemySpawner] Enemy defeated — {_aliveCount} remaining.");

        if (_aliveCount == 0)
        {
            Debug.Log("[EnemySpawner] All enemies defeated.");
            OnAllEnemiesDefeated?.Invoke();
        }
    }

    // Returns a position from explicit spawn points, or random in-bounds if none are set.
    private Vector3 ChooseSpawnPosition()
    {
        if (_spawnPoints != null && _spawnPoints.Length > 0)
            return _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)].position;

        return RandomPositionInBounds();
    }

    // Generates a random XZ position inside the room, away from walls and the player spawn centre.
    private Vector3 RandomPositionInBounds()
    {
        float hw = _roomWidth  / 2f - _wallMargin;
        float hl = _roomLength / 2f - _wallMargin;

        Vector3 pos;
        int     attempts = 0;

        do
        {
            pos = new Vector3(
                UnityEngine.Random.Range(-hw, hw),
                0f,
                UnityEngine.Random.Range(-hl, hl));
            attempts++;
        }
        while (new Vector2(pos.x, pos.z).magnitude < _centreExclusionRadius && attempts < 20);

        return pos;
    }
}
