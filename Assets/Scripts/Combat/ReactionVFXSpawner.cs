using System;
using UnityEngine;

/// <summary>
/// Spawns a visual effect prefab when an elemental reaction is triggered on this entity.
///
/// Each ReactionType maps to a separate VFX prefab assigned in the Inspector.
/// Spawns one copy in front and one behind the entity so the effect wraps the model.
/// The spawned prefab must have a ReactionEffectAutoDestroy component (or equivalent)
/// to clean itself up.
///
/// Attach to any entity that has an ElementStatusController.
/// </summary>
[RequireComponent(typeof(ElementStatusController))]
public class ReactionVFXSpawner : MonoBehaviour
{
    /// <summary>Maps a single reaction type to its visual effect prefab.</summary>
    [Serializable]
    public struct ReactionVFXEntry
    {
        [Tooltip("The reaction type this entry handles.")]
        public ReactionType ReactionType;

        [Tooltip("Prefab to spawn when this reaction triggers. Must auto-destroy itself.")]
        public GameObject VFXPrefab;
    }

    [Header("Reaction VFX")]
    [Tooltip("Assign one entry per reaction type. Reactions with no entry trigger silently.")]
    [SerializeField] private ReactionVFXEntry[] reactionVFXEntries;

    [Header("Spawn Placement")]
    [Tooltip("Vertical offset above the entity's pivot so the effect sits on the model.")]
    [SerializeField] private float spawnHeightOffset = 1f;

    [Tooltip("Distance in front of and behind the entity where the two copies are placed.")]
    [SerializeField] private float spawnDepthOffset = 0.5f;

    // ── Private ──────────────────────────────────────────────────────────────

    private ElementStatusController _elementStatus;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _elementStatus = GetComponent<ElementStatusController>();
    }

    private void OnEnable()
    {
        _elementStatus.OnReactionTriggered += HandleReaction;
    }

    private void OnDisable()
    {
        _elementStatus.OnReactionTriggered -= HandleReaction;
    }

    // ── Reaction handling ────────────────────────────────────────────────────

    /// <summary>
    /// Called by the local OnReactionTriggered event (server / standalone).
    /// Delegates to SpawnVFX so the spawning logic lives in one place.
    /// </summary>
    private void HandleReaction(ReactionResult result)
    {
        SpawnVFX(result.ReactionType);
    }

    /// <summary>
    /// Spawns two VFX copies — one in front, one behind the entity —
    /// facing outward from the entity's forward direction.
    ///
    /// Public so that ReactionVFXNetworkSync can call this on clients
    /// after receiving the ClientRpc broadcast from the server.
    /// </summary>
    public void SpawnVFX(ReactionType reactionType)
    {
        GameObject prefab = FindPrefab(reactionType);
        if (prefab == null)
        {
            Debug.Log($"[ReactionVFXSpawner] {gameObject.name} — " +
                      $"no VFX assigned for {reactionType}, skipping.");
            return;
        }

        Vector3 center  = transform.position + Vector3.up * spawnHeightOffset;
        Vector3 forward = transform.forward;

        // Front copy — parented to this entity so it moves with it
        Vector3 frontPos = center + forward * spawnDepthOffset;
        Instantiate(prefab, frontPos, Quaternion.LookRotation(forward), transform);

        // Back copy — parented to this entity so it moves with it
        Vector3 backPos = center - forward * spawnDepthOffset;
        Instantiate(prefab, backPos, Quaternion.LookRotation(-forward), transform);

        Debug.Log($"[ReactionVFXSpawner] {gameObject.name} — " +
                  $"spawned {reactionType} VFX (front: {frontPos}, back: {backPos}).");
    }

    /// <summary>Returns the VFX prefab for the given reaction, or null if none assigned.</summary>
    private GameObject FindPrefab(ReactionType reactionType)
    {
        foreach (ReactionVFXEntry entry in reactionVFXEntries)
        {
            if (entry.ReactionType == reactionType)
                return entry.VFXPrefab;
        }
        return null;
    }
}
