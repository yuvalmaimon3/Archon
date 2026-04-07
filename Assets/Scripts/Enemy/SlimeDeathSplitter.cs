using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles the Slime's death-split mechanic: spawns a number of smaller slimes
/// around the death position when the parent slime dies.
///
/// Implements IDeathHandler so DeathController calls OnDeath() automatically.
/// Only the server spawns the mini-slimes — NGO propagates them to all clients.
///
/// Set isMiniSlime = true on small slime prefabs to prevent infinite recursion.
/// </summary>
public class SlimeDeathSplitter : NetworkBehaviour, IDeathHandler
{
    [Header("Split Settings")]
    [Tooltip("Prefab to spawn on death. Must have a NetworkObject component.")]
    [SerializeField] private GameObject smallSlimePrefab;

    [Tooltip("How many mini-slimes to spawn when this slime dies.")]
    [Min(1)]
    [SerializeField] private int spawnCount = 2;

    [Tooltip("Radius around the death point within which mini-slimes are scattered.")]
    [Min(0f)]
    [SerializeField] private float spawnRadius = 0.5f;

    [Header("Identity")]
    [Tooltip("True for mini-slimes — prevents them from splitting again on death.")]
    [SerializeField] private bool isMiniSlime = false;

    // ── IDeathHandler ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by DeathController when this entity's HP reaches zero.
    /// Mini-slimes skip this entirely. Full slimes spawn children on the server.
    /// </summary>
    public void OnDeath()
    {
        // Mini-slimes do not split — stops infinite recursion
        if (isMiniSlime) return;

        // Only the server authorizes spawning
        if (!IsServer) return;

        if (smallSlimePrefab == null)
        {
            Debug.LogWarning($"[SlimeDeathSplitter] '{name}' — smallSlimePrefab is not assigned. No mini-slimes spawned.");
            return;
        }

        SpawnMiniSlimes();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates and NGO-spawns mini-slimes at random positions near the death point.
    /// Each mini-slime is offset by a random direction within spawnRadius to avoid overlap.
    /// </summary>
    private void SpawnMiniSlimes()
    {
        Vector3 deathPosition = transform.position;

        for (int i = 0; i < spawnCount; i++)
        {
            // Random horizontal offset within a circle — keeps slimes on the ground plane
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos     = deathPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            GameObject miniSlime = Instantiate(smallSlimePrefab, spawnPos, Quaternion.identity);

            var netObj = miniSlime.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError($"[SlimeDeathSplitter] smallSlimePrefab '{smallSlimePrefab.name}' " +
                               $"has no NetworkObject. Mini-slime {i} destroyed.");
                Destroy(miniSlime);
                continue;
            }

            netObj.Spawn(destroyWithScene: true);

            Debug.Log($"[SlimeDeathSplitter] '{name}' spawned mini-slime {i} at {spawnPos}.");
        }
    }
}
