using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Broadcasts elemental reaction VFX to all connected clients.
///
/// Problem solved:
///   Projectile hits are processed server-side only (Projectile.cs line 152).
///   This means OnReactionTriggered only fires on the server — clients never
///   see reaction VFX because ReactionVFXSpawner's event handler never runs.
///
/// How it works:
///   1. Server: OnReactionTriggered fires → this component calls SpawnReactionVFXClientRpc.
///   2. ClientRpc runs on ALL clients (NGO behaviour).
///   3. Server skips its own ClientRpc execution — it already spawned VFX via the
///      local event in ReactionVFXSpawner.
///   4. Each client calls ReactionVFXSpawner.SpawnVFX() locally — same visual result.
///
/// Attach this component to any networked entity that already has both
/// ReactionVFXSpawner and ElementStatusController.
/// </summary>
[RequireComponent(typeof(ElementStatusController))]
[RequireComponent(typeof(ReactionVFXSpawner))]
public class ReactionVFXNetworkSync : NetworkBehaviour
{
    // ── Private references ───────────────────────────────────────────────────

    private ElementStatusController _elementStatus;
    private ReactionVFXSpawner _vfxSpawner;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _elementStatus = GetComponent<ElementStatusController>();
        _vfxSpawner    = GetComponent<ReactionVFXSpawner>();
    }

    private void OnEnable()
    {
        _elementStatus.OnReactionTriggered += OnReactionTriggered;
    }

    private void OnDisable()
    {
        _elementStatus.OnReactionTriggered -= OnReactionTriggered;
    }

    // ── Reaction handling ────────────────────────────────────────────────────

    /// <summary>
    /// Called locally when this entity has a reaction. Only the server proceeds
    /// — it broadcasts the reaction type to all clients via ClientRpc.
    /// Non-networked entities (no NetworkObject) are ignored because IsSpawned is false.
    /// </summary>
    private void OnReactionTriggered(ReactionResult result)
    {
        // Only the server has authority to broadcast
        if (!IsServer) return;

        // Guard: entity may not be networked (e.g. standalone / test scene without NGO)
        if (!IsSpawned) return;

        Debug.Log($"[ReactionVFXNetworkSync] {gameObject.name} — " +
                  $"broadcasting {result.ReactionType} VFX to all clients.");

        SpawnReactionVFXClientRpc(result.ReactionType);
    }

    /// <summary>
    /// Runs on every connected client (and the host).
    /// Host skips execution — ReactionVFXSpawner already handled it via the local event.
    /// Pure clients spawn the VFX locally so all players see the same visual feedback.
    /// </summary>
    [ClientRpc]
    private void SpawnReactionVFXClientRpc(ReactionType reactionType)
    {
        // Server/host already spawned VFX through the local OnReactionTriggered event
        if (IsServer) return;

        Debug.Log($"[ReactionVFXNetworkSync] {gameObject.name} — " +
                  $"client spawning {reactionType} VFX (received from server).");

        _vfxSpawner.SpawnVFX(reactionType);
    }
}
