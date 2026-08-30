using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// One truck waiting in line with an order to fill.
///
/// The wanted crop is shown as its own 3D model floating over the cab - the icon sprites
/// for the crops do not exist, and the models read instantly at this camera distance.
/// Every crop's model is instantiated once and then just toggled, so a truck coming back
/// out of the pool with a different order allocates nothing.
/// </summary>
public class TruckOrder : MonoBehaviour
{
    [Header("Badge")]
    [Tooltip("Root of the floating order badge, billboarded to the camera.")]
    public Transform badge;
    [Tooltip("Where the wanted crop's model is parented.")]
    public Transform modelAnchor;
    public TextMeshProUGUI countLabel;

    [Header("Model")]
    [Tooltip("Uniform scale applied to the crop model so every crop reads the same size.")]
    public float modelScale = 0.5f;
    public float spinSpeed = 45f;

    public ItemDef Wanted { get; private set; }
    public int Amount { get; private set; }

    /// <summary>Coins paid when this order is filled.</summary>
    public int Reward => Wanted != null ? Wanted.sellValue * Amount : 0;

    /// <summary>True once the truck has finished rolling to its slot.</summary>
    public bool Arrived => !_moving;

    readonly Dictionary<ItemDef, GameObject> _models = new Dictionary<ItemDef, GameObject>();
    Transform _tf;
    Transform _cam;
    Vector3 _target;
    float _speed;
    bool _moving;

    // OnEnable, not Awake: GameManager only guarantees its references from OnEnable onward,
    // and a pooled truck re-enables on every rent, so this is the natural home for it too.
    void OnEnable()
    {
        _tf = transform;
        _cam = GameManager.CameraTransform;
    }

    /// <summary>Give this truck an order. Safe to call repeatedly as it is recycled.</summary>
    public void Configure(ItemDef crop, int amount)
    {
        Wanted = crop;
        Amount = amount;

        if (countLabel != null) countLabel.text = "x" + amount;

        // hide whatever was shown before, then show this crop's model - built on first use
        foreach (var kv in _models)
            if (kv.Value != null) kv.Value.SetActive(kv.Key == crop);

        if (crop != null && !_models.ContainsKey(crop))
        {
            var prefab = crop.DisplayPrefab;

            if (prefab != null && modelAnchor != null)
            {
                var go = Instantiate(prefab, modelAnchor);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * modelScale;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Discard(c);
                // a model floating inside a badge should not lay a shadow across the truck
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
                // the harvest pickup script would fly it to the player
                var col = go.GetComponent<Collectable>();
                if (col != null) Discard(col);
                _models[crop] = go;
            }
            else _models[crop] = null;
        }
    }

    /// <summary>
    /// Destroy that also works outside play mode, the way SoilPlot already does it. Orders are
    /// only configured at runtime in the real game, but an editor tool driving the queue would
    /// otherwise leave the stripped colliders behind and log an error for each one.
    /// </summary>
    static void Discard(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    /// <summary>Roll to a spot on the road. The truck drives itself the rest of the way.</summary>
    public void MoveTo(Vector3 position, float speed, bool instant = false)
    {
        _target = position;
        _speed = speed;
        if (instant)
        {
            transform.position = position;
            _moving = false;
        }
        else _moving = true;
    }

    public void ShowBadge(bool visible)
    {
        if (badge != null && badge.gameObject.activeSelf != visible) badge.gameObject.SetActive(visible);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (_moving)
        {
            _tf.position = Vector3.MoveTowards(_tf.position, _target, _speed * dt);
            if ((_tf.position - _target).sqrMagnitude < 0.0004f) { _tf.position = _target; _moving = false; }
        }

        if (badge != null && badge.gameObject.activeSelf)
        {
            badge.rotation = _cam.rotation;
            if (modelAnchor != null) modelAnchor.Rotate(0f, spinSpeed * dt, 0f, Space.World);
        }
    }
}
