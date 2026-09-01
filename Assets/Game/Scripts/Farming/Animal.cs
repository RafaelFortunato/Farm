using TMPro;
using UnityEngine;

/// <summary>
/// A farm animal that fills up with produce on a timer and hands over the whole store when
/// the player walks up.
///
/// Unlike a plot, an animal banks what it makes: it lays one every regrowSeconds up to its
/// capacity and then waits, so the player is free to be somewhere else instead of standing
/// over it. The clock runs on elapsed real time and catches up in one go, so a throttled
/// browser tab loses nothing.
///
/// The badge is authored into the prefab - an animal always makes the same thing, so there is
/// nothing to swap at runtime, only a count to write and a visibility to toggle. The idle sway
/// lives here rather than in an Animator because these are primitive kitbashes with no rig,
/// and a little motion is what makes them read as animals instead of as furniture.
/// </summary>
public class Animal : Interactable
{
    [Header("Produce")]
    [Tooltip("Species data: what it makes, how fast, and how much it banks. Both chickens share " +
             "one asset, so retuning egg timing is a single edit.")]
    public AnimalDef definition;

    public ItemDef Produces => definition.produces;
    public float RegrowSeconds => definition.regrowSeconds;
    public int Capacity => definition.capacity;
    public CharacterAction CollectAction => definition.collectAction;

    [Header("Badge")]
    [Tooltip("Root of the floating badge, shown only while there is something to collect.")]
    public Transform badge;
    [Tooltip("Holds the produce model inside the badge. Spun so it catches the eye.")]
    public Transform modelAnchor;
    public TextMeshProUGUI countLabel;
    public float spinSpeed = 45f;

    [Header("Idle")]
    [Tooltip("Body that bobs and sways. Defaults to a child named Body.")]
    public Transform visual;
    public float bobHeight = 0.05f;
    public float bobSpeed = 1.8f;
    [Tooltip("How far the body turns from side to side, in degrees.")]
    public float sway = 9f;

    [Header("Runtime (read-only)")]
    [SerializeField] int _stored;
    [SerializeField] float _nextAt;

    Transform _tf;
    Transform _cam;
    Vector3 _visualRest;
    Quaternion _visualRestRot;
    float _phase;
    string _prompt = string.Empty;

    /// <summary>How many are waiting to be collected.</summary>
    public int Stored => _stored;

    /// <summary>0..1 towards the next one. Sits at 1 once the animal is full.</summary>
    public float Progress =>
        _stored >= Capacity ? 1f
        : Mathf.Clamp01(1f - (_nextAt - Time.time) / Mathf.Max(RegrowSeconds, 0.01f));

    public override bool CanInteract => _stored > 0;

    // The stored count *is* the state: it decides both CanInteract and the prompt, so keying
    // off it refreshes the UI exactly when either could have changed.
    public override int StateKey => _stored;

    // Cached rather than built on access: the prompt is read every frame while focused.
    public override string Prompt => _prompt;

    protected override void OnEnable()
    {
        base.OnEnable();
        _tf = transform;
        _cam = GameManager.CameraTransform;

        if (visual == null) visual = _tf.Find("Body");
        if (visual != null)
        {
            _visualRest = visual.localPosition;
            _visualRestRot = visual.localRotation;
        }

        // offset per instance so a pen full of chickens does not move in lockstep
        _phase = Random.value * Mathf.PI * 2f;

        _nextAt = Time.time + RegrowSeconds;
        SetStored(_stored);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // Put the body back where the prefab has it, so re-enabling reads a rest pose rather
        // than wherever the bob happened to leave it and drifts a little further each time.
        if (visual != null)
        {
            visual.localPosition = _visualRest;
            visual.localRotation = _visualRestRot;
        }
    }

    /// <summary>Single place the count changes, so the prompt and badge cannot drift from it.</summary>
    void SetStored(int count)
    {
        _stored = Mathf.Clamp(count, 0, Mathf.Max(Capacity, 1));

        _prompt = _stored > 0 && Produces != null
            ? "Collect " + _stored + " " + Produces.displayName
            : string.Empty;

        if (countLabel != null) countLabel.text = "x" + _stored;
        bool ready = _stored > 0;
        if (badge != null && badge.gameObject.activeSelf != ready) badge.gameObject.SetActive(ready);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (_stored < Capacity)
        {
            // Loop rather than a single check: after a backgrounded tab several may be due at
            // once, and the player should get all of them.
            int gained = 0;
            while (Time.time >= _nextAt && _stored + gained < Capacity)
            {
                gained++;
                _nextAt += RegrowSeconds;
            }
            if (gained > 0) SetStored(_stored + gained);

            // full now: hold the clock so it does not bank time it can never spend
            if (_stored >= Capacity) _nextAt = Time.time + RegrowSeconds;
        }

        if (visual != null)
        {
            _phase += dt * bobSpeed;
            visual.localPosition = _visualRest + Vector3.up * (Mathf.Sin(_phase) * bobHeight);
            visual.localRotation = _visualRestRot * Quaternion.Euler(0f, Mathf.Sin(_phase * 0.5f) * sway, 0f);
        }

        if (badge != null && badge.gameObject.activeSelf)
        {
            if (_cam != null) badge.rotation = _cam.rotation;
            if (modelAnchor != null) modelAnchor.Rotate(0f, spinSpeed * dt, 0f, Space.World);
        }
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (_stored <= 0) return;

        // The produce pops out at the end of the beat, so the animation reads as its cause.
        // With no action wired the callback runs immediately, same as a plot's harvest.
        interactor.Controller.BeginAction(CollectAction, transform, Collect);
    }

    void Collect()
    {
        if (_stored <= 0 || Produces == null) return;

        int taken = _stored;
        var prefab = Produces.DisplayPrefab;

        if (prefab != null)
        {
            var spawnAt = (badge != null ? badge.position : _tf.position + Vector3.up) + Vector3.up * 0.1f;
            var go = Instantiate(prefab, spawnAt, Quaternion.identity);
            var col = go.GetComponent<Collectable>();
            if (col == null) col = go.AddComponent<Collectable>();
            col.item = Produces;
            col.amount = taken;
        }
        else Inventory.AddProduce(Produces, taken);   // no model to fly over; still pay out

        _nextAt = Time.time + RegrowSeconds;
        SetStored(0);
    }
}
