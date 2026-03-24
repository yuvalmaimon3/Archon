using UnityEngine;

/// <summary>
/// Follows the player with a fixed offset, matching Archero's angled top-down perspective.
/// The camera position and angle are set in the scene — this script only drives the follow.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow")]
    [SerializeField] private float smoothSpeed = 10f;

    // Computed once at start from the camera's initial placement in the scene
    private Vector3 _offset;

    private void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (target != null)
            _offset = transform.position - target.position;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + _offset;
        transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}
