using Unity.Netcode;
using UnityEngine;

// Listens for reactions caused by THIS player and spawns a ReactionExplosion at the reaction position.
// The prefab owns all explosion behaviour: damage, knockback, VFX, and lifetime.
//
// Added to the player GameObject server-side when the Blast Reaction upgrade is chosen.
// Network: MonoBehaviour — only runs on the server (ApplyUpgradeServerRpc guard).
//          The explosion prefab is a NetworkObject and replicates to all clients for VFX.
public class BlastReactionUpgradeEffect : MonoBehaviour
{
    // ── Config (set via SetConfig before use) ─────────────────────────────────

    // Prefab that contains ReactionExplosion + NetworkObject + particle systems.
    private ReactionExplosion _explosionPrefab;

    // Radius passed to ReactionExplosion.InitializeServer each spawn.
    private float _blastRadius = 2f;

    // ── Public API ────────────────────────────────────────────────────────────

    // Called by PlayerUpgradeHandler after AddComponent to configure this effect.
    public void SetConfig(ReactionExplosion prefab, float blastRadius)
    {
        _explosionPrefab = prefab;
        _blastRadius     = blastRadius;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        ReactionDamageHandler.OnAnyReactionDamage += HandleBlast;
    }

    private void OnDisable()
    {
        ReactionDamageHandler.OnAnyReactionDamage -= HandleBlast;
    }

    // ── Blast spawn ───────────────────────────────────────────────────────────

    // Fires server-side when any enemy triggers a reaction.
    // Only spawns an explosion if THIS player caused the reaction.
    private void HandleBlast(Vector3 reactionPosition, int reactionDamage, GameObject source)
    {
        // Only trigger for reactions caused by this player's attacks
        if (source != gameObject) return;

        if (_explosionPrefab == null)
        {
            Debug.LogWarning("[BlastReactionUpgradeEffect] No explosion prefab assigned — blast skipped.");
            return;
        }

        var explosion = Instantiate(_explosionPrefab, reactionPosition, Quaternion.identity);

        // Pass damage and radius before Spawn so OnNetworkSpawn can apply them immediately.
        explosion.InitializeServer(reactionDamage, _blastRadius);

        explosion.NetworkObject.Spawn();

        Debug.Log($"[BlastReactionUpgradeEffect] Spawned explosion at {reactionPosition} " +
                  $"(radius:{_blastRadius}, damage:{reactionDamage}).");
    }
}
