using System.Collections;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

// Test-only component. Configures the scene for upgrade window testing without
// touching any base scripts. Remove this GameObject to restore normal behaviour.
//
// What it does:
//   1. Before the host starts — injects a runtime RoomConfig: 3 rounds, 1 skeleton each.
//   2. After the host/server is live — sets player max HP to _playerHp.
//   3. After _expGrantDelay seconds — grants enough EXP to trigger a level-up,
//      which opens the upgrade selection window with all available upgrades.
//   4. After each round starts — waits 2 frames then overrides all spawned enemy HPs to _enemyHp.
//
// Network: MonoBehaviour — lives only on the host (test scenes use TestSceneAutoHost).
public class UpgradeTestSetup : MonoBehaviour
{
    [Header("Enemy")]
    [Tooltip("SkeletonArcher prefab to spawn once per round.")]
    [SerializeField] private GameObject _skeletonPrefab;

    [Tooltip("HP override applied to every enemy after it spawns.")]
    [SerializeField] private int _enemyHp = 500;

    [Header("Player")]
    [Tooltip("Max HP set on the player after the host starts.")]
    [SerializeField] private int _playerHp = 10000;

    [Header("Level-up trigger")]
    [Tooltip("Seconds after host start before EXP is granted.")]
    [SerializeField] private float _expGrantDelay = 0.2f;

    [Tooltip("EXP amount granted — 25 is exactly enough for level 1→2 with default config.")]
    [SerializeField] private int _expAmount = 25;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        InjectTestRoomConfig();
    }

    private void Start()
    {
        StartCoroutine(WaitForHostThenSetup());
    }

    private void OnDestroy()
    {
        // Unsubscribe from round events to avoid dangling delegate
        var rm = FindFirstObjectByType<RoomManager>();
        if (rm != null)
            rm.OnRoundStarted -= OnRoundStarted;
    }

    // ── Room config injection ────────────────────────────────────────────────

    // Builds a RoomConfig at runtime and forces it into RoomManager via reflection.
    // Must run in Awake so the value is ready before TestSceneAutoHost starts the host
    // (which triggers RoomManager.OnNetworkSpawn where _config is first read).
    private void InjectTestRoomConfig()
    {
        var roomManager = FindFirstObjectByType<RoomManager>();
        if (roomManager == null)
        {
            Debug.LogWarning("[UpgradeTestSetup] RoomManager not found — config injection skipped.");
            return;
        }

        var group = new SpawnGroup { EnemyPrefab = _skeletonPrefab, Count = 1 };

        var testConfig = ScriptableObject.CreateInstance<RoomConfig>();
        testConfig.Rounds = new RoundConfig[]
        {
            new RoundConfig { TimerDuration = 60f, SpawnGroups = new[] { group } },
            new RoundConfig { TimerDuration = 60f, SpawnGroups = new[] { group } },
            new RoundConfig { TimerDuration = 0f,  SpawnGroups = new[] { group } },
        };

        var field = typeof(RoomManager).GetField(
            "_config", BindingFlags.NonPublic | BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(roomManager, testConfig);
            Debug.Log("[UpgradeTestSetup] Test RoomConfig injected: 3 rounds × 1 skeleton.");
        }
        else
        {
            Debug.LogError("[UpgradeTestSetup] '_config' field not found on RoomManager.");
        }
    }

    // ── Post-host setup coroutine ────────────────────────────────────────────

    private IEnumerator WaitForHostThenSetup()
    {
        // Wait until TestSceneAutoHost has called StartHost()
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        // Let OnNetworkSpawn run on all freshly spawned NetworkBehaviours
        yield return null;
        yield return null;

        SetPlayerHp();

        // Subscribe to round-start so each new batch of enemies gets overridden
        var rm = FindFirstObjectByType<RoomManager>();
        if (rm != null)
            rm.OnRoundStarted += OnRoundStarted;

        yield return new WaitForSeconds(_expGrantDelay);

        GrantExpToPlayer();
    }

    // ── Player HP ────────────────────────────────────────────────────────────

    private void SetPlayerHp()
    {
        var levelSystem = FindFirstObjectByType<PlayerLevelSystem>();
        if (levelSystem == null)
        {
            Debug.LogWarning("[UpgradeTestSetup] Player not found — HP not set.");
            return;
        }

        var health = levelSystem.GetComponent<Health>();
        var sync   = levelSystem.GetComponent<NetworkHealthSync>();

        // SetMaxHealth resets currentHealth to the new max and fires OnDamaged,
        // which NetworkHealthSync.OnDamagedServer picks up and syncs _syncedHealth.
        if (health != null)
            health.SetMaxHealth(_playerHp);

        // Push the new max to all clients via the NetworkVariable.
        if (sync != null)
            sync.UpdateSyncedMaxHealth(_playerHp);

        Debug.Log($"[UpgradeTestSetup] Player HP set to {_playerHp}.");
    }

    // ── EXP grant ────────────────────────────────────────────────────────────

    private void GrantExpToPlayer()
    {
        var levelSystem = FindFirstObjectByType<PlayerLevelSystem>();
        if (levelSystem == null)
        {
            Debug.LogWarning("[UpgradeTestSetup] Player not found — EXP not granted.");
            return;
        }

        // AddExperience is server-authoritative — safe to call directly since we're the host.
        levelSystem.AddExperience(_expAmount);
        Debug.Log($"[UpgradeTestSetup] Granted {_expAmount} EXP — upgrade window should appear.");
    }

    // ── Enemy HP override ────────────────────────────────────────────────────

    // Fired by RoomManager on all clients when a new round begins.
    // We wait two frames for EnemyInitializer.OnNetworkSpawn (which calls ApplyStats)
    // to finish, then force all enemy HPs to the test value.
    private void OnRoundStarted(int roundIndex, int totalRounds)
    {
        StartCoroutine(OverrideEnemyHp());
    }

    private IEnumerator OverrideEnemyHp()
    {
        yield return null;
        yield return null;

        var enemies = FindObjectsByType<EnemyInitializer>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var enemy in enemies)
        {
            if (!enemy.TryGetComponent<Health>(out var hp)) continue;
            if (hp.MaxHealth == _enemyHp) continue;

            hp.SetMaxHealth(_enemyHp);
            count++;
        }

        if (count > 0)
            Debug.Log($"[UpgradeTestSetup] Overrode HP → {_enemyHp} on {count} enemy/enemies.");
    }
}
