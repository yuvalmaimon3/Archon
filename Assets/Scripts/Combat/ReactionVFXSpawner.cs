using System;
using UnityEngine;

/// <summary>
/// Spawns a visual effect prefab when an elemental reaction is triggered on this entity.
///
/// Each ReactionType maps to a separate VFX prefab assigned in the Inspector.
/// The spawned prefab is expected to auto-destroy itself (e.g. via a ParticleSystem
/// or an Animator that calls Destroy on its last frame).
///
/// Attach to any entity that has an ElementStatusController.
/// </summary>
[RequireComponent(typeof(ElementStatusController))]
public class ReactionVFXSpawner : MonoBehaviour
{
    /// <summary>
    /// Maps a single reaction type to its visual effect prefab.
    /// </summary>
    [Serializable]
    public struct ReactionVFXEntry
    {
        [Tooltip("The reaction type this entry handles.")]
        public ReactionType ReactionType;

        [Tooltip("Prefab to spawn when this reaction triggers. Should auto-destroy itself.")]
        public GameObject VFXPrefab;
    }

    [Header("Reaction VFX")]
    [Tooltip("Assign one entry per reaction type you want to show a VFX for. " +
             "Reactions with no entry here will trigger silently.")]
    [SerializeField] private ReactionVFXEntry[] reactionVFXEntries;

    [Tooltip("Vertical offset above the entity's pivot to spawn the VFX. " +
             "Adjust so the effect appears centered on the model.")]
    [SerializeField] private float spawnHeightOffset = 1f;

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
    /// Finds the VFX entry for the triggered reaction and spawns it at this entity's position.
    /// </summary>
    private void HandleReaction(ReactionResult result)
    {
        GameObject prefab = FindPrefab(result.ReactionType);
        if (prefab == null)
        {
            Debug.Log($"[ReactionVFXSpawner] {gameObject.name} — " +
                      $"no VFX assigned for {result.ReactionType}, skipping.");
            return;
        }

        Vector3 spawnPosition = transform.position + Vector3.up * spawnHeightOffset;
        Instantiate(prefab, spawnPosition, Quaternion.identity);

        Debug.Log($"[ReactionVFXSpawner] {gameObject.name} — " +
                  $"spawned {result.ReactionType} VFX at {spawnPosition}.");
    }

    /// <summary>
    /// Returns the VFX prefab assigned to the given reaction type, or null if none is set.
    /// </summary>
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
