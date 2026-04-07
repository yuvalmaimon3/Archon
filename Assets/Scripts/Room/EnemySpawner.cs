using System;
using System.Collections.Generic;
using UnityEngine;

// Spawns enemies for a round and tracks how many remain alive.
// When the last enemy dies, fires OnAllEnemiesDefeated.
//
// Spawn position priority:
//   1. SpawnPoints (Transform[]) — explicit anchors placed in the scene
//   2. Random positions within the room bounds (fallback when no points are assigned)
//
// RoomManager calls SetSpawnBounds() after the room is generated so random placement
// stays inside the room.
public class EnemySpawner : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("Explicit spawn anchors. If empty, enemies spawn at random positions in the room.")]
    [SerializeField] private Transform[] _spawnPoints;

    [Tooltip("Minimum distance from room centre to spawn an enemy (keeps player start clear).")]
    [SerializeField] private float _centreExclusionRadius = 4f;

    // ── Events ────────────────────────────────────────────────────────────────

    // Fired when every enemy from the current spawn batch has been defeated.
    public event Action OnAllEnemiesDefeated;

    // Fired each time an enemy dies. Passes the number still alive.
    public event Action<int> OnEnemyDefeated;

    // ── State ─────────────────────────────────────────────────────────────────

    // Room dimensions used for random placement. Set via SetSpawnBounds().
    private float _roomWidth;
    private float _roomLength;
    private float _wallMargin = 2f;

    // How many enemies from the current batch are still alive.
    private int _aliveCount;

    // Tracks all currently spawned enemy instances for cleanup.
    private readonly List<GameObject> _spawnedEnemies = new();

    // ── Public API ────────────────────────────────────────────────────────────

    // Configures the play area for random spawn placement.
    // Call this after generating the room (before the first SpawnRound).
    public void SetSpawnBounds(float width, float length, float wallMargin = 2f)
    {
        _roomWidth  = width;
        _roomLength = length;
        _wallMargin = wallMargin;
    }

    // Clears any survivors from the previous round and spawns the new round's enemies.
    // Hooks death events on every spawned enemy to track alive count.
    public void SpawnRound(RoundConfig config)
    {
        ClearSpawnedEnemies();
        _aliveCount = 0;

        if (config.Enemies == null || config.Enemies.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] Round config has no enemies — completing immediately.");
            OnAllEnemiesDefeated?.Invoke();
            return;
        }

        foreach (var entry in config.Enemies)
        {
            if (entry.EnemyPrefab == null)
            {
                Debug.LogWarning("[EnemySpawner] EnemySpawnEntry has null prefab — skipped.");
                continue;
            }

            for (int i = 0; i < entry.Count; i++)
                SpawnEnemy(entry.EnemyPrefab);
        }

        // Edge case: all prefabs were null, complete immediately.
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

    // Instantiates one enemy at a valid position and hooks its death event.
    private void SpawnEnemy(GameObject prefab)
    {
        Vector3 pos      = ChooseSpawnPosition();
        float   yRotation = UnityEngine.Random.Range(0f, 360f);
        var     instance  = Instantiate(prefab, pos, Quaternion.Euler(0f, yRotation, 0f));

        _spawnedEnemies.Add(instance);

        // Hook death — prefer DeathController.OnDied (fires after full cleanup sequence),
        // fall back to Health.OnDeath if no DeathController present.
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
            Debug.LogWarning($"[EnemySpawner] '{instance.name}' has no DeathController or Health — cannot track death. Not counted.");
        }
    }

    // Called whenever a tracked enemy dies.
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

    // Returns a position from explicit spawn points or a random in-bounds position.
    private Vector3 ChooseSpawnPosition()
    {
        if (_spawnPoints != null && _spawnPoints.Length > 0)
            return _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)].position;

        return RandomPositionInBounds();
    }

    // Generates a random XZ position inside the room, keeping away from walls and centre.
    private Vector3 RandomPositionInBounds()
    {
        float hw = _roomWidth  / 2f - _wallMargin;
        float hl = _roomLength / 2f - _wallMargin;

        Vector3 pos;
        int     attempts = 0;

        // Retry up to 20 times to clear the centre exclusion zone.
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
