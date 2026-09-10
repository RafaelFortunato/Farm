using UnityEngine;

/// <summary>
/// A harvested item that pops out of whatever produced it, then flies to the player.
///
/// It always flies, whatever the distance. It used to sit and wait for the player to come
/// within collectRadius, but the player can harvest from PlayerInteractor.range (2.5 units)
/// while the item only homed from 1.6 - so anything picked at arm's length was simply left
/// lying in the field. Distance was never a meaningful lever here: by the time this spawns
/// the item is already earned, and the flight is the payoff animation, not a second hurdle.
///
/// The player comes from GameManager, read once and held. Several of these can be in flight
/// at once and they run every frame, so this is the last place that should be hunting through
/// the scene for anything.
/// </summary>
public class Collectable : MonoBehaviour
{
    [Header("Value")]
    public ItemDef item;
    public int amount = 1;

    [Header("Pop")]
    public float popHeight = 0.9f;
    public float popSeconds = 0.35f;

    [Header("Flight")]
    [Tooltip("Comfortably faster than the player, so it closes even on someone running away.")]
    public float flySpeed = 12f;
    [Tooltip("How close to the player it has to get to count as caught.")]
    public float catchDistance = 0.2f;
    public float spinSpeed = 60f;

    Transform _tf;
    Transform _player;
    Vector3 _popFrom, _popTo;
    float _t;

    enum State { Popping, Flying }
    State _state = State.Popping;

    void Awake()
    {
        _tf = transform;
        _popFrom = _tf.position;
        _popTo = _popFrom + Vector3.up * popHeight
               + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
    }

    // GameManager only guarantees its references from OnEnable onward, so the player is read
    // here rather than in Awake.
    void OnEnable() => _player = GameManager.PlayerTransform;

    void Update()
    {
        float dt = Time.deltaTime;
        _tf.Rotate(0f, spinSpeed * dt, 0f, Space.World);

        switch (_state)
        {
            case State.Popping:
                _t += dt / Mathf.Max(popSeconds, 0.01f);
                if (_t >= 1f) { _t = 1f; _state = State.Flying; }
                // ease out, so it decelerates as it reaches the top of the arc
                float e = 1f - (1f - _t) * (1f - _t);
                _tf.position = Vector3.Lerp(_popFrom, _popTo, e);
                break;

            case State.Flying:
                Vector3 target = _player.position + Vector3.up * 0.6f;
                _tf.position = Vector3.MoveTowards(_tf.position, target, flySpeed * dt);
                if ((target - _tf.position).sqrMagnitude < catchDistance * catchDistance) Collect();
                break;
        }
    }

    void Collect()
    {
        Inventory.AddProduce(item, amount);

        // Shared rather than a field here: one harvest spawns a flier per item, and every crop
        // prefab in the game carries this component. A per-prefab reference would be the same
        // asset a dozen times over, with a dozen chances to leave one empty.
        AudioManager.PlayPickup(_tf != null ? _tf.position : transform.position);

        // guarded the way SoilPlot and TruckOrder do it, so editor tooling can drive a pickup
        // without Unity refusing the destroy and the item collecting itself every frame
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}
