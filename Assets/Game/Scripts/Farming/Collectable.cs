using UnityEngine;

/// <summary>
/// A harvested item that pops out of a plot, bobs, and is picked up when the player
/// comes near. Self-contained so a plot can spawn one and forget about it.
/// </summary>
public class Collectable : MonoBehaviour
{
    [Header("Value")]
    public ItemDef item;
    public int amount = 1;

    [Header("Pop")]
    public float popHeight = 0.9f;
    public float popSeconds = 0.35f;

    [Header("Idle bob")]
    public float bobHeight = 0.12f;
    public float bobSpeed = 2.5f;
    public float spinSpeed = 60f;

    [Header("Collect")]
    [Tooltip("Player gets this close and it flies to them.")]
    public float collectRadius = 1.6f;
    public float flySpeed = 9f;

    Transform _tf;
    Transform _player;
    Vector3 _restPos;
    float _t;
    float _bobPhase;
    enum State { Popping, Waiting, Flying }
    State _state = State.Popping;
    Vector3 _popFrom, _popTo;

    void Awake()
    {
        _tf = transform;
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) _player = pc.transform;

        _popFrom = _tf.position;
        _popTo = _popFrom + Vector3.up * popHeight + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
        _restPos = _popTo;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        _tf.Rotate(0f, spinSpeed * dt, 0f, Space.World);

        switch (_state)
        {
            case State.Popping:
                _t += dt / Mathf.Max(popSeconds, 0.01f);
                if (_t >= 1f) { _t = 1f; _state = State.Waiting; }
                // ease out so it decelerates as it reaches the top
                float e = 1f - (1f - _t) * (1f - _t);
                _tf.position = Vector3.Lerp(_popFrom, _popTo, e);
                break;

            case State.Waiting:
                _bobPhase += dt * bobSpeed;
                _tf.position = _restPos + Vector3.up * (Mathf.Sin(_bobPhase) * bobHeight);
                if (_player != null)
                {
                    Vector3 d = _player.position - _tf.position;
                    if (d.x * d.x + d.z * d.z <= collectRadius * collectRadius) _state = State.Flying;
                }
                break;

            case State.Flying:
                if (_player == null) { Destroy(gameObject); return; }
                Vector3 target = _player.position + Vector3.up * 0.6f;
                _tf.position = Vector3.MoveTowards(_tf.position, target, flySpeed * dt);
                if ((target - _tf.position).sqrMagnitude < 0.04f) Collect();
                break;
        }
    }

    void Collect()
    {
        Inventory.AddProduce(item, amount);
        Destroy(gameObject);
    }
}
