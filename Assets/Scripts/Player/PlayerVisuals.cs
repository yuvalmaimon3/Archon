using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles the visual appearance of each player capsule.
/// Local player (the one you control) → grey.
/// Remote players (other clients) → red.
///
/// Uses OnNetworkSpawn so it runs after NGO assigns ownership,
/// which is required to correctly read IsOwner.
/// Supports both URP (_BaseColor) and Built-in/Standard (_Color) shaders.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class PlayerVisuals : NetworkBehaviour
{
    private static readonly Color LocalPlayerColor  = new Color(0.5f, 0.5f, 0.5f); // grey
    private static readonly Color RemotePlayerColor = new Color(0.85f, 0.1f, 0.1f); // red

    private MeshRenderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
    }

    /// <summary>
    /// Called by NGO after the NetworkObject is fully spawned and ownership is assigned.
    /// IsOwner = true means this instance is the local player.
    /// We create a material instance (.material) to avoid changing all players at once.
    /// We set both _BaseColor (URP) and _Color (Built-in) so it works regardless of render pipeline.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        Color color = IsOwner ? LocalPlayerColor : RemotePlayerColor;

        // .material creates a per-instance copy automatically — safe to modify
        Material mat = _renderer.material;
        mat.SetColor("_BaseColor", color); // URP / Lit shader
        mat.SetColor("_Color", color);     // Built-in / Standard shader

        Debug.Log($"[PlayerVisuals] Spawned — IsOwner={IsOwner}, color={color}");
    }
}
