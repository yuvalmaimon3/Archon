using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Listens for elemental reactions on this entity and applies reaction damage.
///
/// When a reaction fires (e.g. Fire + Ice = Melt), the triggering attack's damage
/// is suppressed by Health to avoid double-counting. This handler applies a
/// separate damage hit scaled by reactionDamageMultiplier (default 2x).
///
/// Arc reaction: 1.5x to primary + chains to all nearby wet enemies (full pipeline).
/// The reaction damage hit carries no element, so it cannot chain-trigger another reaction.
///
/// Attach this component to any entity that has both Health and ElementStatusController.
/// For networked entities, damage is only applied on the server (matching how projectile
/// hits are processed in Projectile.cs).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(ElementStatusController))]
public class ReactionDamageHandler : MonoBehaviour
{
    [Header("Reaction Damage")]
    [Tooltip("Multiplier applied to the triggering attack's base damage. 2 = double damage.")]
    [SerializeField] private float reactionDamageMultiplier = 2f;

    [Header("Arc Reaction")]
    [Tooltip("Damage multiplier for Arc (overrides general multiplier). Applied to primary and all chained wet enemies.")]
    [SerializeField] private float arcDamageMultiplier = 1.5f;
    [Tooltip("Radius to scan for nearby wet enemies when Arc triggers.")]
    [SerializeField] private float arcAoeRadius = 8f;
    [SerializeField] private string arcEnemyTag = "Enemy";

    // ── Global reaction event ─────────────────────────────────────────────────

    // Fired server-side after this entity takes reaction damage.
    // Args: reaction position, final damage, source player who caused the reaction.
    // Source is null for non-player reactions (traps, environment).
    public static event Action<Vector3, int, GameObject> OnAnyReactionDamage;

    // ── Private references ───────────────────────────────────────────────────

    private Health _health;
    private ElementStatusController _elementStatus;

    // Cached NetworkObject — null on non-networked entities.
    private NetworkObject _networkObject;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private bool _subscribed;

    private void Awake()
    {
        CacheReferences();
        Subscribe();
    }

    private void OnEnable()
    {
        CacheReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        CacheReferences();
        Subscribe();
    }

    private void CacheReferences()
    {
        if (_health == null) _health = GetComponent<Health>();
        if (_elementStatus == null) _elementStatus = GetComponent<ElementStatusController>();
        if (_networkObject == null) TryGetComponent(out _networkObject);
    }

    private void Subscribe()
    {
        if (_subscribed || _elementStatus == null) return;
        _elementStatus.OnReactionTriggered += HandleReaction;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || _elementStatus == null) return;
        _elementStatus.OnReactionTriggered -= HandleReaction;
        _subscribed = false;
    }

    // ── Reaction handling ────────────────────────────────────────────────────

    private void HandleReaction(ReactionResult result)
    {
        if (_networkObject != null && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

        float multiplier = result.ReactionType == ReactionType.Arc ? arcDamageMultiplier : reactionDamageMultiplier;
        int reactionDamage = Mathf.RoundToInt(result.BaseDamage * multiplier);

        if (reactionDamage <= 0)
        {
            Debug.Log($"[ReactionDamageHandler] {gameObject.name} — " +
                      $"{result.ReactionType} reaction skipped (base damage was 0).");
            return;
        }

        Debug.Log($"[ReactionDamageHandler] {gameObject.name} — " +
                  $"{result.ReactionType} reaction! " +
                  $"Base damage: {result.BaseDamage} × {multiplier} = {reactionDamage}");

        var reactionDamageInfo = new DamageInfo(
            amount:             reactionDamage,
            source:             null,
            hitPoint:           transform.position,
            hitDirection:       Vector3.zero,
            elementApplication: default,
            isCritical:         result.IsCritical
        );

        _health.TakeDamage(reactionDamageInfo);
        OnAnyReactionDamage?.Invoke(transform.position, reactionDamage, result.Source);

        if (result.ReactionType == ReactionType.Arc)
            ChainArcToNearbyWetEnemies(result);
    }

    // Finds all wet (Water element) enemies within arcAoeRadius and triggers the full
    // Arc reaction pipeline on each — same damage, VFX, and audio as the primary hit.
    // OverlapSphere is 3D so flying enemies are included. Chain terminates naturally:
    // Arc uses ClearAll, so reacted enemies lose Water and won't react again.
    private void ChainArcToNearbyWetEnemies(ReactionResult result)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, arcAoeRadius);

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            if (!hit.CompareTag(arcEnemyTag)) continue;
            if (!hit.TryGetComponent<ElementStatusController>(out var enemyElement)) continue;
            if (enemyElement.CurrentElement != ElementType.Water) continue;

            Debug.Log($"[ReactionDamageHandler] Arc chain → {hit.gameObject.name}");

            // Apply Lightning to the wet enemy — triggers full Arc reaction on their pipeline
            var lightningApplication = new ElementApplication(ElementType.Lightning, 1f, result.Source);
            enemyElement.ApplyElement(lightningApplication, result.BaseDamage, result.IsCritical);
        }
    }
}
