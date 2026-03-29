using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Overrides the host player's attack definition at game start.
/// Attach to the Player prefab. When the host spawns, it swaps
/// the AttackController's definition to the specified override.
///
/// Used for testing specific projectile types (e.g., Fireball)
/// without changing the default attack for all players.
/// </summary>
public class HostAttackOverride : NetworkBehaviour
{
    [Header("Host Override")]
    [Tooltip("Attack definition the host player will use. Leave null to keep the default.")]
    [SerializeField] private AttackDefinition hostAttackOverride;

    [Tooltip("AttackController to override. Auto-resolved if left empty.")]
    [SerializeField] private AttackController attackController;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Only override for the host player (server + owner)
        if (!IsServer || !IsOwner) return;

        if (hostAttackOverride == null)
        {
            Debug.LogWarning("[HostAttackOverride] No override attack assigned — keeping default.");
            return;
        }

        if (attackController == null)
            attackController = GetComponent<AttackController>();

        if (attackController == null)
        {
            Debug.LogError("[HostAttackOverride] No AttackController found on this GameObject.", this);
            return;
        }

        // Swap the attack definition to the fireball (or whatever override is assigned)
        attackController.SetAttackDefinition(hostAttackOverride);
        Debug.Log($"[HostAttackOverride] Host player attack overridden to '{hostAttackOverride.AttackId}'.");
    }
}
