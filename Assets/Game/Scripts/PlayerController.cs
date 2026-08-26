using UnityEngine;

/// <summary>
/// Top-down character movement for the farm cat.
///
/// Movement is camera-relative: "up" means away from the camera, which is what a
/// player expects on a fixed-angle top-down view.
///
/// The Neko Cat asset ships rigged but with no locomotion clip (only a tail wriggle),
/// so the sense of walking is faked procedurally: a hop, a lean into the direction of
/// travel, and a small squash on landing. Cheap, and it reads better than a slide.
/// </summary>
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour
{
    const float TAU = 6.2831853f;

    [Header("Movement")]
    [Tooltip("Units per second at full tilt. A ground tile is 2 units wide.")]
    public float moveSpeed = 6f;
    [Tooltip("How quickly the cat reaches full speed / stops.")]
    public float acceleration = 24f;
    [Tooltip("Degrees per second the cat turns to face travel direction.")]
    public float turnSpeed = 720f;

    [Header("Island bounds (world XZ)")]
    [Tooltip("The 13x13 tile island spans -12..12; inset slightly so the cat stays on solid ground.")]
    public Vector2 boundsMin = new Vector2(-11f, -11f);
    public Vector2 boundsMax = new Vector2(11f, 11f);

    [Header("Procedural motion")]
    [Tooltip("Child transform holding the mesh. Bobbed/leaned without affecting the logical position.")]
    public Transform visual;
    public float hopHeight = 0.18f;
    public float hopFrequency = 9f;
    public float leanAngle = 9f;
    public float squashAmount = 0.08f;

    /// <summary>Current planar speed, 0..moveSpeed. Useful for future footstep SFX / dust.</summary>
    public float CurrentSpeed { get; private set; }

    InputSystem_Actions _input;
    Vector2 _externalInput;
    Vector3 _velocity;

    // cached in Awake - Update does no lookups and no null checks
    Transform _tf;
    Transform _cam;
    float _invMoveSpeed;

    // camera basis, recomputed only when the camera actually rotates.
    // CameraFollow holds a fixed yaw, so in practice this resolves once.
    Quaternion _lastCamRot = new Quaternion(2f, 0f, 0f, 0f); // impossible value forces first compute
    Vector3 _camFwd;
    Vector3 _camRight;

    float _hopPhase;
    bool _visualAtRest;
    Vector3 _visualBaseLocalPos;
    Vector3 _visualBaseScale;

    void Awake()
    {
        _tf = transform;
        _cam = Camera.main.transform;

        if (visual == null && _tf.childCount > 0) visual = _tf.GetChild(0);
        if (visual != null)
        {
            _visualBaseLocalPos = visual.localPosition;
            _visualBaseScale = visual.localScale;
        }

        CacheInverseSpeed();
    }

    void OnValidate() => CacheInverseSpeed();

    void CacheInverseSpeed() => _invMoveSpeed = moveSpeed > 0.0001f ? 1f / moveSpeed : 0f;

    // Bindings live in Assets/Game/InputSystem_Actions (Player/Move), which already covers
    // WASD, arrows, gamepad stick, joystick and XR. This is its generated wrapper.
    void OnEnable()
    {
        _input ??= new InputSystem_Actions();
        _input.Player.Enable();
    }

    void OnDisable() => _input?.Player.Disable();
    void OnDestroy() => _input?.Dispose();

    /// <summary>
    /// Feed movement from something other than the keyboard - an on-screen joystick
    /// for the mobile build. Set to Vector2.zero to hand control back.
    /// </summary>
    public void SetExternalInput(Vector2 input) => _externalInput = input;

    void Update()
    {
        float dt = Time.deltaTime;

        Vector2 raw = _input.Player.Move.ReadValue<Vector2>();
        if (_externalInput.sqrMagnitude > raw.sqrMagnitude) raw = _externalInput;

        float inputSq = raw.sqrMagnitude;
        if (inputSq > 1f) raw /= Mathf.Sqrt(inputSq);

        RefreshCameraBasis();

        // desired velocity on the ground plane, built component-wise to avoid Vector3 operator churn
        float dx = (_camRight.x * raw.x + _camFwd.x * raw.y) * moveSpeed;
        float dz = (_camRight.z * raw.x + _camFwd.z * raw.y) * moveSpeed;

        _velocity = Vector3.MoveTowards(_velocity, new Vector3(dx, 0f, dz), acceleration * dt);

        float speedSq = _velocity.sqrMagnitude;
        CurrentSpeed = speedSq > 1e-8f ? Mathf.Sqrt(speedSq) : 0f;

        if (CurrentSpeed > 0f)
        {
            Vector3 p = _tf.position;
            p.x = Mathf.Clamp(p.x + _velocity.x * dt, boundsMin.x, boundsMax.x);
            p.z = Mathf.Clamp(p.z + _velocity.z * dt, boundsMin.y, boundsMax.y);
            p.y = 0f;
            _tf.position = p;

            if (speedSq > 0.01f)
            {
                // reuse the square root already taken for CurrentSpeed
                Vector3 dir = _velocity / CurrentSpeed;
                _tf.rotation = Quaternion.RotateTowards(_tf.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
            }
        }

        ApplyProceduralMotion(dt);
    }

    /// <summary>Flatten the camera's forward onto the ground plane, only when it has moved.</summary>
    void RefreshCameraBasis()
    {
        Quaternion rot = _cam.rotation;
        if (rot == _lastCamRot) return;
        _lastCamRot = rot;

        Vector3 fwd = _cam.forward;
        fwd.y = 0f;
        _camFwd = fwd.normalized;
        _camRight = new Vector3(_camFwd.z, 0f, -_camFwd.x); // == Cross(Vector3.up, _camFwd)
    }

    void ApplyProceduralMotion(float dt)
    {
        if (visual == null) return;

        float t = CurrentSpeed * _invMoveSpeed;
        if (t > 1f) t = 1f;

        if (t > 0.01f)
        {
            _hopPhase += dt * hopFrequency * t;
            if (_hopPhase > TAU) _hopPhase -= TAU;
            _visualAtRest = false;
        }
        else if (!_visualAtRest)
        {
            _hopPhase = Mathf.MoveTowards(_hopPhase, 0f, dt * 8f);
            if (_hopPhase <= 0f)
            {
                // settle exactly on the rest pose once, then stop touching the transform
                _hopPhase = 0f;
                visual.localPosition = _visualBaseLocalPos;
                visual.localScale = _visualBaseScale;
                visual.localRotation = Quaternion.identity;
                _visualAtRest = true;
                return;
            }
        }
        else
        {
            return; // idle and already settled - no transform writes at all
        }

        // abs(sin) gives a bouncing arc rather than a floaty sine
        float hop = Mathf.Abs(Mathf.Sin(_hopPhase)) * hopHeight * t;
        visual.localPosition = new Vector3(
            _visualBaseLocalPos.x,
            _visualBaseLocalPos.y + hop,
            _visualBaseLocalPos.z);

        // squash at the bottom of the arc, stretch at the top
        float squash = Mathf.Cos(_hopPhase * 2f) * squashAmount * t;
        visual.localScale = new Vector3(
            _visualBaseScale.x * (1f + squash),
            _visualBaseScale.y * (1f - squash),
            _visualBaseScale.z * (1f + squash));

        // lean forward into the run
        visual.localRotation = Quaternion.Euler(leanAngle * t, 0f, 0f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.6f);
        var c = new Vector3((boundsMin.x + boundsMax.x) * 0.5f, 0.05f, (boundsMin.y + boundsMax.y) * 0.5f);
        var s = new Vector3(boundsMax.x - boundsMin.x, 0.1f, boundsMax.y - boundsMin.y);
        Gizmos.DrawWireCube(c, s);
    }
}
