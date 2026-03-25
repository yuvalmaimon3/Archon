using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles the visual appearance of each player capsule.
/// Colours the local player grey and all remote players red,
/// so you can instantly tell who you are controlling in a multiplayer session.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class PlayerVisuals : NetworkBehaviour
{
    // Colours used for local and remote players
    private static readonly Color LocalPlayerColor  = new Color(0.5f, 0.5f, 0.5f); // grey
    private static readonly Color RemotePlayerColor = new Color(0.85f, 0.1f, 0.1f); // red

    private MeshRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Called by NGO after the object is spawned on the network.
    /// IsOwner is true only on the client that owns this player — the local player.
    /// We use .material (not .sharedMaterial) so each instance gets its own copy
    /// and we don't accidentally recolor all players at once.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        Color color = IsOwner ? LocalPlayerColor : RemotePlayerColor;
        _renderer.material.color = color;
        Debug.Log($"[PlayerVisuals] Spawned — IsOwner={IsOwner}, color={color}");
    }
}
