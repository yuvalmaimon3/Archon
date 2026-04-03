using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Syncs the elemental state of this entity to all connected clients via NGO NetworkVariables.
///
/// Problem:
///   ElementStatusController is a plain MonoBehaviour — its state only exists on the
///   server (hit logic runs server-side only). Clients have no way to know what element
///   is currently applied to an entity, so element-based VFX and UI indicators
///   would never show on client machines.
///
/// How it works:
///   Server  → subscribes to ElementStatusController.OnElementChanged
///           → writes new element + strength atomically to _syncedState NetworkVariable
///           → NGO replicates the value to all clients automatically
///   Clients → subscribe to _syncedState.OnValueChanged
///           → fire local OnElementChanged event
///           → VFX spawners, UI indicators, etc. react as if the change happened locally
///
/// The event signature matches ElementStatusController.OnElementChanged (ElementType, float)
/// so the same subscriber code works on both the server and clients.
///
/// This mirrors the same pattern used by NetworkHealthSync for health values.
///
/// Attach alongside ElementStatusController on any networked entity (players, enemies).
/// </summary>
[RequireComponent(typeof(ElementStatusController))]
public class ElementStateNetworkSync : NetworkBehaviour
{
    // ── Nested type ──────────────────────────────────────────────────────────

    /// <summary>
    /// Atomic snapshot of element type + strength.
    /// Using a struct NetworkVariable avoids the two-variable ordering problem:
    /// if element and strength were separate variables, clients could briefly see
    /// a mismatched state between the two callbacks.
    /// </summary>
    private struct ElementSnapshot : INetworkSerializable, IEquatable<ElementSnapshot>
    {
        public int   Element;   // ElementType cast to int — enums are not directly serializable
        public float Strength;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Element);
            serializer.SerializeValue(ref Strength);
        }

        public bool Equals(ElementSnapshot other) =>
            Element == other.Element && Mathf.Approximately(Strength, other.Strength);
    }

    // ── Private fields ───────────────────────────────────────────────────────

    private ElementStatusController _elementStatus;

    // Server writes; all clients read.
    // Single struct variable so element + strength always arrive together — no partial-update risk.
    private readonly NetworkVariable<ElementSnapshot> _syncedState = new(
        new ElementSnapshot { Element = (int)ElementType.None, Strength = 0f },
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ── Read-only state ──────────────────────────────────────────────────────

    /// <summary>Current element synced from the server. Always up to date on all clients.</summary>
    public ElementType CurrentElement => (ElementType)_syncedState.Value.Element;

    /// <summary>Current element strength synced from the server.</summary>
    public float CurrentStrength => _syncedState.Value.Strength;

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired on all machines whenever the element state changes (including clears).
    /// Signature matches ElementStatusController.OnElementChanged so the same
    /// VFX/UI subscriber code works on both server and clients.
    /// Also fired once on network spawn with the initial state.
    /// </summary>
    public event Action<ElementType, float> OnElementChanged;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        _elementStatus = GetComponent<ElementStatusController>();
    }

    // ── NGO lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Called on all machines when the NetworkObject is spawned.
    /// Server initialises the synced value; all machines subscribe to future changes.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Initialise with whatever element is already active (handles mid-fight spawns)
            _syncedState.Value = new ElementSnapshot
            {
                Element  = (int)_elementStatus.CurrentElement,
                Strength = _elementStatus.CurrentStrength
            };

            // Track future element changes and replicate them
            _elementStatus.OnElementChanged += OnElementChangedServer;
        }

        // All machines react to replicated value changes
        _syncedState.OnValueChanged += OnSyncedStateChanged;

        // Fire once immediately so subscribers that registered before spawn get the current state
        OnElementChanged?.Invoke(CurrentElement, CurrentStrength);

        Debug.Log($"[ElementStateNetworkSync] '{name}' spawned — " +
                  $"element:{CurrentElement} strength:{CurrentStrength:F1}, IsServer:{IsServer}");
    }

    /// <summary>Called on all machines when the NetworkObject is despawned.</summary>
    public override void OnNetworkDespawn()
    {
        if (IsServer)
            _elementStatus.OnElementChanged -= OnElementChangedServer;

        _syncedState.OnValueChanged -= OnSyncedStateChanged;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called on the server when ElementStatusController's element changes.
    /// Writes the new snapshot to the NetworkVariable, triggering replication to all clients.
    /// </summary>
    private void OnElementChangedServer(ElementType element, float strength)
    {
        _syncedState.Value = new ElementSnapshot
        {
            Element  = (int)element,
            Strength = strength
        };

        Debug.Log($"[ElementStateNetworkSync] '{name}' element synced: {element} (strength:{strength:F1})");
    }

    /// <summary>
    /// Called on all machines when the NetworkVariable value changes.
    /// Fires the local C# event so subscribers (VFX, UI) update without polling.
    /// </summary>
    private void OnSyncedStateChanged(ElementSnapshot oldValue, ElementSnapshot newValue)
    {
        OnElementChanged?.Invoke((ElementType)newValue.Element, newValue.Strength);
    }
}
