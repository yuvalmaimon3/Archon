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
/// The reaction damage hit carries no element, so it cannot chain-trigger another reaction.
///
/// Attach this component to any entity that has both Health and ElementStatusController.
/// For networked entities, damage is only applied on the server (matching how projectile
/// hits are processed in Projectile.cs).
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(ElementStatusController))]
public class ReactionDamageHandler : MonoBehaviour
{
    [Header("Reaction Damage")]
    [Tooltip("Multiplier applied to the triggering attack's base damage. 2 = double damage.")]
    [SerializeField] private float reactionDamageMultiplier = 2f;

    // ── Global reaction event ─────────────────────────────────────────────────

    // Fired server-side after this entity takes reaction damage.
    // Args: reaction position, final damage, source player who caused the reaction.
    // Source is null for non-player reactions (traps, environment).
    public static event Action<Vector3, int, GameObject> OnAnyReactionDamage;

    // ── Private references ───────────────────────────────────────────────────

    private Health _health;
    private ElementStatusController _elementStatus;

    // Cached NetworkObject — null on non-networked entities.
    // Used to gate reaction damage to the server only (same pattern as Projectile.cs).
    private NetworkObject _networkObject;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _health        = GetComponent<Health>();
        _elementStatus = GetComponent<ElementStatusController>();
        TryGetComponent(out _networkObject);
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
    /// Called when a reaction is detected on this entity.
    /// Computes reaction damage and applies it as a plain (element-free) hit.
    ///
    /// Networked: only runs on the server so damage authority stays server-side.
    /// Non-networked (solo/editor): always runs.
    /// </summary>
    private void HandleReaction(ReactionResult result)
    {
        // In networked mode, only the server applies damage (matches Projectile.cs authority model)
        if (_networkObject != null && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

        int reactionDamage = Mathf.RoundToInt(result.BaseDamage * reactionDamageMultiplier);

        // Skip zero-damage reactions (e.g. pure element applications with baseDamage = 0).
        // Avoids firing OnDamaged and running the full Health.TakeDamage pipeline for no effect.
        if (reactionDamage <= 0)
        {
            Debug.Log($"[ReactionDamageHandler] {gameObject.name} — " +
                      $"{result.ReactionType} reaction skipped (base damage was 0).");
            return;
        }

        Debug.Log($"[ReactionDamageHandler] {gameObject.name} — " +
                  $"{result.ReactionType} reaction! " +
                  $"Base damage: {result.BaseDamage} × {reactionDamageMultiplier} = {reactionDamage}");

        // Build a plain DamageInfo with no element — prevents recursive reactions
        var reactionDamageInfo = new DamageInfo(
            amount:             reactionDamage,
            source:             null,                // reaction is environmental, no attacker
            hitPoint:           transform.position,
            hitDirection:       Vector3.zero,
            elementApplication: default              // no element = no reaction loop
        );

        _health.TakeDamage(reactionDamageInfo);

        // Broadcast to per-player upgrade effects (e.g. BlastReactionUpgradeEffect).
        // Source is the player whose attack triggered the reaction — null for non-player sources.
        OnAnyReactionDamage?.Invoke(transform.position, reactionDamage, result.Source);
    }
}
