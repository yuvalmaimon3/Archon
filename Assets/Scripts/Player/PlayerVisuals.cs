using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Swaps the player capsule material based on ownership.
/// The prefab already has host_color (grey) as the default material,
/// so we only need to swap to client_color (red) for remote players.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class PlayerVisuals : NetworkBehaviour
{
    [SerializeField] private Material clientMaterial; // assigned in prefab — client_color.mat

    private MeshRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Called by NGO after ownership is assigned.
    /// IsOwner = true → local player, keep the default grey material.
    /// IsOwner = false → remote player, swap to client material (red).
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsOwner && clientMaterial != null)
        {
            _renderer.material = clientMaterial;
            Debug.Log("[PlayerVisuals] Remote player — applied client color.");
        }
        else
        {
            Debug.Log("[PlayerVisuals] Local player — keeping host color.");
        }
    }
}
