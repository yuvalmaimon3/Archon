using System.Collections;
using UnityEngine;

// Physical gate that blocks the room exit.
// Listens to RoomManager.OnRoomComplete and slides open when fired.
//
// Animation: simple lerp downward (gate sinks into the floor).
// Replace with an Animator trigger once art assets are in place.
public class RoomGate : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Tooltip("The RoomManager this gate belongs to. Listens for OnRoomComplete.")]
    [SerializeField] private RoomManager _roomManager;

    [Header("Open Animation")]
    [Tooltip("World-space distance the gate travels downward when opening.")]
    [SerializeField] private float _openDistance = 4f;

    [Tooltip("Units per second the gate moves.")]
    [SerializeField] private float _openSpeed    = 3f;

    // ── Private state ─────────────────────────────────────────────────────────

    private Vector3   _closedPosition;
    private Vector3   _openPosition;
    private bool      _isOpen;
    private bool      _isAnimating;
    private Coroutine _animCoroutine;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _closedPosition = transform.position;
        _openPosition   = _closedPosition + Vector3.down * _openDistance;
    }

    private void OnEnable()
    {
        if (_roomManager != null)
            _roomManager.OnRoomComplete += Open;
    }

    private void OnDisable()
    {
        if (_roomManager != null)
            _roomManager.OnRoomComplete -= Open;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Opens the gate (slides it down into the floor).
    // Called automatically via OnRoomComplete; can also be called manually.
    public void Open()
    {
        if (_isOpen)
        {
            Debug.LogWarning("[RoomGate] Already open.");
            return;
        }

        Debug.Log("[RoomGate] Opening.");
        _isOpen = true;
        StartAnimation(_openPosition);
    }

    // Resets the gate to its closed position (call when loading the next room).
    public void Close()
    {
        if (!_isOpen) return;

        Debug.Log("[RoomGate] Closing.");
        _isOpen = false;
        StartAnimation(_closedPosition);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // Cancels any in-progress animation and starts a new one toward the target.
    private void StartAnimation(Vector3 target)
    {
        if (_animCoroutine != null)
            StopCoroutine(_animCoroutine);

        _animCoroutine = StartCoroutine(SlideTo(target));
    }

    // Lerps the gate toward target at _openSpeed units/second.
    private IEnumerator SlideTo(Vector3 target)
    {
        _isAnimating = true;

        while (Vector3.Distance(transform.position, target) > 0.005f)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target, _openSpeed * Time.deltaTime);
            yield return null;
        }

        transform.position = target;
        _isAnimating       = false;
    }
}
