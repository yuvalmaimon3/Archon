using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Assigns a random element and projectile shape to the player on network spawn.
/// Server picks both, syncs to all clients via NetworkVariables.
///
/// For testing: every player gets one random element (Fire/Water/Lightning/Ice)
/// and one random projectile shape. These override whatever is on the AttackDefinition
/// so different players have visually distinct attacks.
///
/// Other scripts read these values:
///   - PlayerCombatBrain → overrides element + shape when spawning projectiles
///   - PlayerVisuals     → tints the player capsule by element color
/// </summary>
public class PlayerElementSetup : NetworkBehaviour
{
    /// <summary>Server-authoritative element assignment. Clients can read but not write.</summary>
    private NetworkVariable<ElementType> _assignedElement = new NetworkVariable<ElementType>(
        ElementType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>Server-authoritative projectile shape assignment.</summary>
    private NetworkVariable<ProjectileShape> _assignedShape = new NetworkVariable<ProjectileShape>(
        ProjectileShape.Orb,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>The element assigned to this player.</summary>
    public ElementType AssignedElement => _assignedElement.Value;

    /// <summary>The projectile shape assigned to this player.</summary>
    public ProjectileShape AssignedShape => _assignedShape.Value;

    /// <summary>Fired on all clients when the element is assigned. Used by PlayerVisuals.</summary>
    public event System.Action<ElementType> OnElementAssigned;

    // The 4 core elements available for random assignment
    private static readonly ElementType[] PlayableElements = {
        ElementType.Fire,
        ElementType.Water,
        ElementType.Lightning,
        ElementType.Ice
    };

    // All 10 projectile shapes available for random assignment
    private static readonly ProjectileShape[] AllShapes = {
        ProjectileShape.Arrow,
        ProjectileShape.Orb,
        ProjectileShape.Fireball,
        ProjectileShape.Shard,
        ProjectileShape.Bolt,
        ProjectileShape.Spear,
        ProjectileShape.Needle,
        ProjectileShape.WaveShot,
        ProjectileShape.BurstPellet,
        ProjectileShape.SpinningDisc
    };

    // ── NGO lifecycle ──────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        // Subscribe to value changes for late-joining clients
        _assignedElement.OnValueChanged += HandleElementChanged;

        if (IsServer)
        {
            // Server picks random element and shape for this player
            _assignedElement.Value = PlayableElements[Random.Range(0, PlayableElements.Length)];
            _assignedShape.Value   = AllShapes[Random.Range(0, AllShapes.Length)];

            Debug.Log($"[PlayerElementSetup] Server assigned {_assignedElement.Value} + " +
                      $"{_assignedShape.Value} to '{name}' (Client {OwnerClientId}).");
        }

        // Fire event immediately if value was already set (host case)
        if (_assignedElement.Value != ElementType.None)
        {
            OnElementAssigned?.Invoke(_assignedElement.Value);
            Debug.Log($"[PlayerElementSetup] {name} → {_assignedElement.Value} {_assignedShape.Value} " +
                      $"(IsOwner:{IsOwner}).");
        }
    }

    public override void OnNetworkDespawn()
    {
        _assignedElement.OnValueChanged -= HandleElementChanged;
    }

    private void HandleElementChanged(ElementType prev, ElementType next)
    {
        Debug.Log($"[PlayerElementSetup] {name} element: {prev} → {next}.");
        OnElementAssigned?.Invoke(next);
    }
}
