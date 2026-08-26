using UnityEngine;
using UnityEngine.InputSystem;

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
    float _hopPhase;
    Vector3 _visualBaseLocalPos;
    Vector3 _visualBaseScale;
    Transform _cam;

    void Awake()
    {
        if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
        if (visual != null)
        {
            _visualBaseLocalPos = visual.localPosition;
            _visualBaseScale = visual.localScale;
        }

    }

    // Bindings live in Assets/Game/InputSystem_Actions (Player/Move), which already covers
    // WASD, arrows, gamepad stick, joystick and XR. This is its generated wrapper.
    // Created here rather than in Awake: a domain reload during play calls OnEnable without
    // re-running Awake, which would leave this null.
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
        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

        Vector2 raw = _input.Player.Move.ReadValue<Vector2>();
        if (_externalInput.sqrMagnitude > raw.sqrMagnitude) raw = _externalInput;
        if (raw.sqrMagnitude > 1f) raw.Normalize();

        // camera-relative on the ground plane
        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (_cam != null)
        {
            fwd = Vector3.ProjectOnPlane(_cam.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();
            // Cross(up, forward) already yields the camera's right in Unity's axes.
            right = Vector3.Cross(Vector3.up, fwd);
        }

        Vector3 desired = (right * raw.x + fwd * raw.y) * moveSpeed;
        _velocity = Vector3.MoveTowards(_velocity, desired, acceleration * Time.deltaTime);

        Vector3 pos = transform.position + _velocity * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, boundsMin.x, boundsMax.x);
        pos.z = Mathf.Clamp(pos.z, boundsMin.y, boundsMax.y);
        pos.y = 0f;
        transform.position = pos;

        CurrentSpeed = _velocity.magnitude;

        if (_velocity.sqrMagnitude > 0.01f)
        {
            var look = Quaternion.LookRotation(_velocity.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        ApplyProceduralMotion();
    }

    void ApplyProceduralMotion()
    {
        if (visual == null) return;

        float t = moveSpeed > 0.001f ? Mathf.Clamp01(CurrentSpeed / moveSpeed) : 0f;

        if (t > 0.01f) _hopPhase += Time.deltaTime * hopFrequency * t;
        else _hopPhase = Mathf.MoveTowards(_hopPhase % (Mathf.PI * 2f), 0f, Time.deltaTime * 8f);

        // abs(sin) gives a bouncing arc rather than a floaty sine
        float hop = Mathf.Abs(Mathf.Sin(_hopPhase)) * hopHeight * t;
        visual.localPosition = _visualBaseLocalPos + Vector3.up * hop;

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
