using UnityEngine;

// Activates/deactivates per-element VFX GameObjects when the element state changes.
// Runs on ALL clients (subscribes to ElementStateNetworkSync, not the server-only controller).
//
// Assign VFX prefab instances as children of this enemy and drag them into the slots below.
// Leave slots empty until real VFX are ready — missing slots are silently ignored.
[RequireComponent(typeof(ElementStateNetworkSync))]
public class ElementVFXController : MonoBehaviour
{
    [Header("Element VFX — assign child GameObjects here when ready")]
    [SerializeField] private GameObject fireVFX;
    [SerializeField] private GameObject iceVFX;
    [SerializeField] private GameObject electroVFX;
    [SerializeField] private GameObject waterVFX;

    private ElementStateNetworkSync _networkSync;

    private void Awake()
    {
        _networkSync = GetComponent<ElementStateNetworkSync>();

        // Start with all VFX off.
        SetAllVFX(false);
    }

    private void OnEnable()
    {
        _networkSync.OnElementChanged += OnElementChanged;
    }

    private void OnDisable()
    {
        _networkSync.OnElementChanged -= OnElementChanged;
    }

    private void OnElementChanged(ElementType element, float strength)
    {
        SetAllVFX(false);

        if (element == ElementType.None) return;

        GameObject vfx = element switch
        {
            ElementType.Fire      => fireVFX,
            ElementType.Ice       => iceVFX,
            ElementType.Lightning => electroVFX,
            ElementType.Water     => waterVFX,
            _                     => null
        };

        if (vfx != null)
            vfx.SetActive(true);
    }

    private void SetAllVFX(bool active)
    {
        if (fireVFX    != null) fireVFX.SetActive(active);
        if (iceVFX     != null) iceVFX.SetActive(active);
        if (electroVFX != null) electroVFX.SetActive(active);
        if (waterVFX   != null) waterVFX.SetActive(active);
    }
}
