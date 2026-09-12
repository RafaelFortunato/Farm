using UnityEngine;

namespace Farm
{
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
        [Tooltip("Downward tilt of the orbit, in degrees. Fixed rather than player-controlled: the " +
                 "farm is authored to be read from one angle.")]
        public float pitch = 51f;
        [Tooltip("Compass heading of the orbit, in degrees. Fixed: the farm is authored to be read " +
                 "from one angle, so the player never rotates the camera.")]
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
        [Tooltip("Minimum x and z the look-at point may reach. Only used when clampToBounds is on; " +
                 "FarmExpansion widens these as the farm grows.")]
        public Vector2 boundsMin = new Vector2(-8f, -8f);
        [Tooltip("Maximum x and z the look-at point may reach.")]
        public Vector2 boundsMax = new Vector2(8f, 8f);

        Vector3 _vel;
        Vector3 _smoothedLookAt;
        PlayerController _player;

        // Action framing: a temporary push-in and extra tilt layered over the fixed orbit,
        // eased in and out so the move reads as a camera decision rather than a cut.
        float _zoom, _zoomTarget, _zoomVel;
        float _tilt, _tiltTarget, _tiltVel;
        float _framingBlend = 0.35f;

        float CurrentPitch => pitch + _tilt;
        float CurrentDistance => Mathf.Max(distance - _zoom, 2f);

        /// <summary>
        /// Push in and tilt down for a scripted beat. Values are offsets from the resting
        /// orbit, so an action only states how much closer and steeper it wants to be.
        /// </summary>
        public void SetActionFraming(float zoomIn, float extraTilt, float blendSeconds)
        {
            _zoomTarget = zoomIn;
            _tiltTarget = extraTilt;
            _framingBlend = Mathf.Max(blendSeconds, 0.01f);
        }

        /// <summary>Ease back to the resting orbit. Keeps the blend the beat came in on.</summary>
        public void ClearActionFraming()
        {
            _zoomTarget = 0f;
            _tiltTarget = 0f;
        }


        void Start()
        {
            if (target != null)
            {
                _player = target.GetComponent<PlayerController>();
                _smoothedLookAt = LookAtPoint();
                transform.position = DesiredPosition(_smoothedLookAt);
            }
            transform.rotation = Quaternion.Euler(CurrentPitch, yaw, 0f);
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
            var rot = Quaternion.Euler(CurrentPitch, yaw, 0f);
            return lookAt - (rot * Vector3.forward) * CurrentDistance;
        }

        void LateUpdate()
        {
            if (target == null) return;

            float dt = Time.deltaTime;

            // ease the action offsets first so position and rotation agree this frame
            _zoom = Mathf.SmoothDamp(_zoom, _zoomTarget, ref _zoomVel, _framingBlend, Mathf.Infinity, dt);
            _tilt = Mathf.SmoothDamp(_tilt, _tiltTarget, ref _tiltVel, _framingBlend, Mathf.Infinity, dt);

            _smoothedLookAt = LookAtPoint();
            Vector3 desired = DesiredPosition(_smoothedLookAt);

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
            transform.rotation = Quaternion.Euler(CurrentPitch, yaw, 0f);
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
}
