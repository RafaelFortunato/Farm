using UnityEngine;

/// <summary>
/// Smooth follow camera locked to a fixed orbit angle.
///
/// Deliberately does NOT rotate with the target: the farm reads on a fixed 3/4 view,
/// and a fixed yaw keeps mobile gestures unambiguous (pinch = zoom, one finger = pan)
/// and lets every future world-space icon be a fixed-facing billboard.
///
/// Pitch 51 degrees matches the framing the scene was laid out against.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    [Tooltip("Raise the look-at point off the floor so the cat sits lower in frame than dead centre.")]
    public Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Orbit (fixed)")]
    public float pitch = 51f;
    public float yaw = 0f;
    [Tooltip("Distance along the view axis. ~28 frames the cat plus a good chunk of farm.")]
    public float distance = 28f;

    [Header("Damping")]
    [Tooltip("Seconds to catch up. Higher = lazier, more cinematic.")]
    public float smoothTime = 0.28f;
    [Tooltip("Lead the camera slightly in the direction of travel.")]
    public float lookAhead = 0.9f;

    [Header("Bounds")]
    [Tooltip("Clamp the look-at point so the camera never shows the void past the island edge.")]
    public bool clampToBounds = true;
    public Vector2 boundsMin = new Vector2(-8f, -8f);
    public Vector2 boundsMax = new Vector2(8f, 8f);

    Vector3 _vel;
    Vector3 _smoothedLookAt;
    PlayerController _player;

    void Start()
    {
        if (target != null)
        {
            _player = target.GetComponent<PlayerController>();
            _smoothedLookAt = LookAtPoint();
            transform.position = DesiredPosition(_smoothedLookAt);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    Vector3 LookAtPoint()
    {
        Vector3 p = target.position + targetOffset;

        if (lookAhead > 0f && _player != null && _player.CurrentSpeed > 0.05f)
            p += target.forward * (lookAhead * Mathf.Clamp01(_player.CurrentSpeed / Mathf.Max(_player.moveSpeed, 0.001f)));

        if (clampToBounds)
        {
            p.x = Mathf.Clamp(p.x, boundsMin.x, boundsMax.x);
            p.z = Mathf.Clamp(p.z, boundsMin.y, boundsMax.y);
        }
        return p;
    }

    Vector3 DesiredPosition(Vector3 lookAt)
    {
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        return lookAt - (rot * Vector3.forward) * distance;
    }

    void LateUpdate()
    {
        if (target == null) return;

        _smoothedLookAt = LookAtPoint();
        Vector3 desired = DesiredPosition(_smoothedLookAt);

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>Snap instantly to the target - use after teleports or on scene load.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        _smoothedLookAt = LookAtPoint();
        transform.position = DesiredPosition(_smoothedLookAt);
        _vel = Vector3.zero;
    }
}
