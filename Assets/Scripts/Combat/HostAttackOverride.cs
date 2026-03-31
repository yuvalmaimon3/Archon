using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Cycles the host player's attack through a list of attack definitions, advancing
/// to the next one after each shot. Used for testing elemental reactions without
/// manually swapping attacks — e.g. Fire → Ice → Fire → Ice continuously.
///
/// Attach to the Player prefab. Only active on the host (server + owner).
/// Assign the attack cycle list in the Inspector (e.g. [FireballAttack, IceAttack]).
/// </summary>
public class HostAttackOverride : NetworkBehaviour
{
    [Header("Attack Cycle")]
    [Tooltip("Attacks to cycle through in order. After the last one, loops back to the first. " +
             "E.g. [FireballAttack, IceAttack] fires Fire then Ice alternately.")]
    [SerializeField] private AttackDefinition[] attackCycle;

    [Tooltip("AttackController to override. Auto-resolved if left empty.")]
    [SerializeField] private AttackController attackController;

    // ── Private state ────────────────────────────────────────────────────────

    // Index of the attack that will fire next
    private int _currentIndex = 0;

    // Whether this override is active (host only)
    private bool _isActive = false;

    // ── NetworkBehaviour ─────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only the host (server + owner) runs the attack cycle
        if (!IsServer || !IsOwner) return;

        if (attackCycle == null || attackCycle.Length == 0)
        {
            Debug.LogWarning("[HostAttackOverride] attackCycle is empty — no override applied.");
            return;
        }

        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (attackController == null)
        {
            Debug.LogError("[HostAttackOverride] No AttackController found on this GameObject.", this);
            return;
        }

        _isActive = true;
        _currentIndex = 0;

        // Apply the first attack immediately
        ApplyCurrent();

        // Advance to the next attack after each shot
        attackController.OnAttackUsed += AdvanceToNext;

        Debug.Log($"[HostAttackOverride] Host attack cycle started with {attackCycle.Length} attacks.");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (_isActive && attackController != null)
            attackController.OnAttackUsed -= AdvanceToNext;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves to the next attack in the cycle (wraps around).
    /// Called by AttackController.OnAttackUsed after each shot.
    /// </summary>
    private void AdvanceToNext()
    {
        _currentIndex = (_currentIndex + 1) % attackCycle.Length;
        ApplyCurrent();
    }

    /// <summary>Applies the attack at the current cycle index to the AttackController.</summary>
    private void ApplyCurrent()
    {
        AttackDefinition next = attackCycle[_currentIndex];
        attackController.SetAttackDefinition(next);

        Debug.Log($"[HostAttackOverride] Next attack: '{next?.AttackId ?? "null"}' " +
                  $"(index {_currentIndex}/{attackCycle.Length - 1}).");
    }
}
