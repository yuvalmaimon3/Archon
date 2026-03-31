using UnityEngine;

/// <summary>
/// Rotates this GameObject each frame to face the main camera.
/// Used by sprite-based VFX prefabs so they are always visible
/// regardless of camera angle (billboard effect).
/// </summary>
public class FaceCamera : MonoBehaviour
{
    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (_mainCamera == null) return;

        // Face toward the camera by copying its rotation.
        // LateUpdate ensures this runs after all movement/animation.
        transform.rotation = _mainCamera.transform.rotation;
    }
}
