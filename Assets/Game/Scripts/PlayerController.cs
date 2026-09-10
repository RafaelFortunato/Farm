using UnityEngine;

/// <summary>
/// Top-down character movement for the farm cat.
///
/// Movement is camera-relative: "up" means away from the camera, which is what a
/// player expects on a fixed-angle top-down view.
///
/// Locomotion visuals come from retargeted Mixamo clips (HappyIdle / HappyWalk) blended
/// on the "Speed" parameter. Both clips are In Place, so this script keeps sole ownership
/// of position and the Animator only poses the rig.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int ActionSpeedHash = Animator.StringToHash("ActionSpeed");

    [Header("Movement")]
    [Tooltip("Units per second at full tilt. A ground tile is 2 units wide.")]
    public float moveSpeed = 6f;
    [Tooltip("How quickly the cat reaches full speed / stops.")]
    public float acceleration = 24f;
    [Tooltip("Degrees per second the cat turns to face travel direction.")]
    public float turnSpeed = 720f;



    [Header("Animation")]
    [Tooltip("Child transform holding the mesh and the body Animator.")]
    public Transform visual;
    [Tooltip("Smooths the idle<->walk blend so tapping a key doesn't pop the pose.")]
    public float speedDamp = 0.08f;

    /// <summary>Current planar speed, 0..moveSpeed. Useful for future footstep SFX / dust.</summary>
    public float CurrentSpeed { get; private set; }

    /// <summary>
    /// True while a scripted beat is playing. Movement is frozen and interaction refused
    /// for the duration - see PlayerInteractor.CanInteractNow.
    /// </summary>
    public bool IsBusy => _action != null;

    /// <summary>The beat currently playing, or null. Lets callers react per action.</summary>
    public CharacterAction CurrentAction => _action;

    InputSystem_Actions _input;
    Vector2 _externalInput;
    Vector3 _velocity;

    // cached in OnEnable - Update does no lookups and no null checks
    Transform _tf;
    Transform _cam;
    CameraFollow _cameraRig;
    Animator _animator;
    float _invMoveSpeed;
    CharacterController _controller;
    int _groundMask;
    Vector3 _visualBaseLocalPos;
    Quaternion _visualBaseLocalRot;

    // camera basis, recomputed only when the camera actually rotates.
    Quaternion _lastCamRot = new Quaternion(2f, 0f, 0f, 0f); // impossible value forces first compute
    Vector3 _camFwd;
    Vector3 _camRight;

    CharacterAction _action;
    float _actionTimer;
    float _actionDuration;
    System.Action _onActionComplete;

    void OnValidate() => CacheInverseSpeed();

    void CacheInverseSpeed() => _invMoveSpeed = moveSpeed > 0.0001f ? 1f / moveSpeed : 0f;

    // Everything is cached here rather than in Awake. OnEnable always runs before the first
    // Update - including after a domain reload during play, which re-runs OnEnable but NOT
    // Awake. That keeps Update free of null checks.
    void OnEnable()
    {
        _tf = transform;
        _cam = GameManager.CameraTransform;
        _cameraRig = GameManager.CameraRig;

        if (visual == null && _tf.childCount > 0) visual = _tf.GetChild(0);
        _animator = visual.GetComponent<Animator>();
        _visualBaseLocalPos = visual.localPosition;
        _visualBaseLocalRot = visual.localRotation;

        CacheInverseSpeed();

        _controller = GetComponent<CharacterController>();
        _groundMask = 1 << LayerMask.NameToLayer("Ground");

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

    /// <summary>
    /// Locks input and plays a scripted beat. The cat stops, optionally turns to face what
    /// it is using, and holds until the beat ends.
    ///
    /// onComplete fires once, at the end - that is where the result of the beat belongs
    /// (the crop popping out, the cake appearing), so the animation reads as its cause.
    /// A null action means nothing is authored yet: the result still fires, immediately.
    /// </summary>
    public void BeginAction(CharacterAction action, Transform faceTarget = null, System.Action onComplete = null)
    {
        if (action == null) { onComplete?.Invoke(); return; }

        _action = action;
        _actionDuration = Mathf.Max(action.duration, 0.0001f);
        _actionTimer = _actionDuration;
        _onActionComplete = onComplete;

        _velocity = Vector3.zero;
        CurrentSpeed = 0f;

        if (action.faceTarget && faceTarget != null)
        {
            Vector3 to = faceTarget.position - _tf.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f) _tf.rotation = Quaternion.LookRotation(to.normalized);
        }

        // the beat can ask the camera to lean in on it
        _cameraRig.SetActionFraming(action.cameraZoom, action.cameraPitch, action.cameraBlend);

        _animator.SetFloat(SpeedHash, 0f);
        if (!action.UsesProceduralPose)
        {
            // stretch the clip onto the beat's duration before firing it
            _animator.SetFloat(ActionSpeedHash, action.PlaybackSpeed);
            _animator.SetTrigger(action.TriggerHash);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (IsBusy)
        {
            TickAction(dt);
            return;
        }

        // A menu owns input while it is up. Stop dead rather than letting the cat wander off
        // behind the panel - the same gate PlayerInteractor uses to refuse the Interact button.
        if (Menus.AnyOpen)
        {
            _velocity = Vector3.zero;
            CurrentSpeed = 0f;
            _animator.SetFloat(SpeedHash, 0f, speedDamp, dt);
            return;
        }

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
            // One axis at a time. The CharacterController does the hard part - sliding along
            // walls, angled ones included - and the per-axis split is only there so the LEDGE
            // veto below can refuse one direction without killing the other, which is what
            // lets the cat walk along a shoreline instead of stopping dead against it. The
            // tile grid means every shoreline is axis-aligned, so splitting on X/Z is exact.
            StepAxis(new Vector3(_velocity.x * dt, 0f, 0f));
            StepAxis(new Vector3(0f, 0f, _velocity.z * dt));

            if (speedSq > 0.01f)
            {
                // reuse the square root already taken for CurrentSpeed
                Vector3 dir = _velocity / CurrentSpeed;
                _tf.rotation = Quaternion.RotateTowards(_tf.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
            }
        }

        _animator.SetFloat(SpeedHash, CurrentSpeed * _invMoveSpeed, speedDamp, dt);
    }

    /// <summary>
    /// Runs the clock on the current beat. When the action names a clip the Animator owns
    /// the pose and this only holds the lock; otherwise the procedural dip stands in.
    /// </summary>
    void TickAction(float dt)
    {
        _actionTimer -= dt;

        if (_action.UsesProceduralPose)
        {
            float t = 1f - Mathf.Clamp01(_actionTimer / _actionDuration);
            float arc = Mathf.Sin(t * Mathf.PI);   // 0 -> 1 -> 0, so it dips and returns

            visual.localPosition = _visualBaseLocalPos - new Vector3(0f, _action.crouch * arc, 0f);
            visual.localRotation = _visualBaseLocalRot * Quaternion.Euler(_action.lean * arc, 0f, 0f);

            // hold the idle pose underneath the dip
            _animator.SetFloat(SpeedHash, 0f);
        }

        if (_actionTimer <= 0f) EndAction();
    }

    void EndAction()
    {
        _cameraRig.ClearActionFraming();

        if (_action.UsesProceduralPose)
        {
            visual.localPosition = _visualBaseLocalPos;
            visual.localRotation = _visualBaseLocalRot;
        }

        // cleared before the callback runs, so the callback is free to start the next beat
        _action = null;
        var done = _onActionComplete;
        _onActionComplete = null;
        done?.Invoke();
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

    /// <summary>
    /// Move along one axis, and take it back if it stepped off the island.
    ///
    /// Collision with anything solid is the CharacterController's job - it slides along walls
    /// at any angle and will not tunnel, which is exactly the part not worth hand-writing.
    ///
    /// What it will NOT do is stop at a ledge: a shoreline is an absence of floor, and there is
    /// nothing there to collide with. Rather than ring the island in invisible walls that would
    /// need rebuilding every time it grows, the tiles answer the question themselves - if there
    /// is no ground under where we landed, the step is undone.
    /// </summary>
    void StepAxis(Vector3 step)
    {
        if (step.sqrMagnitude < 1e-10f) return;

        Vector3 before = _tf.position;
        _controller.Move(step);

        Vector3 after = _tf.position;
        after.y = 0f;                       // flat farm: never let the controller drift off the plane
        _tf.position = after;

        if (!HasGround(after)) _tf.position = before;
    }

    /// <summary>
    /// Is there floor here? Cast down from head height - starting the ray inside the very tile
    /// it is meant to find would let it miss.
    /// </summary>
    bool HasGround(Vector3 p) =>
        Physics.Raycast(p + Vector3.up, Vector3.down, 2f, _groundMask, QueryTriggerInteraction.Ignore);

    void OnDrawGizmosSelected()
    {
        // where the ledge test looks
        Gizmos.color = new Color(0.3f, 0.9f, 0.4f, 0.7f);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.down);
    }
}
