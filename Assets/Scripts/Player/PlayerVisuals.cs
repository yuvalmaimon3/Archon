using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages the player capsule's visual appearance.
///
/// Two layers:
///   1. Element tinting — colors the capsule by the player's assigned element (when PlayerElementSetup exists)
///   2. Ownership fallback — host vs client material swap (legacy, used when no element setup)
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class PlayerVisuals : NetworkBehaviour
{
    [SerializeField] private Material clientMaterial; // assigned in prefab — client_color.mat

    private MeshRenderer _renderer;
    private PlayerElementSetup _elementSetup;
    private Material _originalMaterial;

    private void Awake()
    {
        _renderer = GetComponent<MeshRenderer>();
        _originalMaterial = _renderer.material;
        TryGetComponent(out _elementSetup);
    }

    public override void OnNetworkSpawn()
    {
        if (_elementSetup != null)
        {
            _elementSetup.OnElementAssigned += ApplyElementColor;

            // If element already assigned (host case), apply immediately
            if (_elementSetup.AssignedElement != ElementType.None)
                ApplyElementColor(_elementSetup.AssignedElement);
        }
        else
        {
            // No element setup — fall back to legacy host/client color
            ApplyOwnershipColor();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_elementSetup != null)
            _elementSetup.OnElementAssigned -= ApplyElementColor;
    }

    /// <summary>Tints the player capsule to match the assigned element color.</summary>
    private void ApplyElementColor(ElementType element)
    {
        if (element == ElementType.None) return;

        Color color = ElementVisualConfig.Get(element).primary;

        // Create unique material instance for this player
        _renderer.material = new Material(_originalMaterial);
        _renderer.material.color = color;
        _renderer.material.SetColor("_BaseColor", color);

        string label = IsOwner ? "local" : "remote";
        Debug.Log($"[PlayerVisuals] {name} ({label}) — tinted to {element}.");
    }

    /// <summary>Legacy ownership-based color swap when no element system is present.</summary>
    private void ApplyOwnershipColor()
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
