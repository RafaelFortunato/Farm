using System;
using UnityEngine;

/// <summary>
/// A mushroom that has come up somewhere on the island, waiting to be picked.
///
/// Foraging is the one thing on the farm that is not tended: it costs nothing, grows on its own,
/// and only pays the player who is looking around. So it is deliberately small money - the point
/// is that walking somewhere is occasionally worth it, not that it competes with a plot.
///
/// Picked the same way as everything else on the farm - walk up, press interact - so there is one
/// verb to learn. What comes out is a Collectable flying to the player, exactly as a harvested
/// crop or a collected egg does, rather than a silent number going up.
///
/// Pooled by MushroomPatch, so this has to survive being switched off and placed again. Nothing
/// is cached from the world in Awake for that reason, and Place() is the single entry point that
/// resets every piece of per-appearance state.
/// </summary>
public class Mushroom : Interactable
{
    [Header("Value")]
    [Tooltip("What picking this puts in the crate.")]
    public ItemDef item;
    [Min(1)] public int amount = 1;

    [Header("Player action")]
    [Tooltip("Beat the player performs when picking. Leave empty to pick instantly.")]
    public CharacterAction pickAction;

    [Header("Look")]
    [Tooltip("Model root, turned and sized a little differently each time one comes up so a " +
             "patch of them does not read as the same prop stamped out repeatedly.")]
    public Transform visual;

    [Tooltip("How much bigger or smaller than authored one may come up.")]
    public Vector2 scaleRange = new Vector2(0.85f, 1.15f);

    [Header("Twinkle")]
    [Tooltip("Star burst played on the cap. The same particle the apple tree uses, so anything " +
             "worth walking over to pick sparkles the same way.")]
    public ParticleSystem sparkle;

    [Tooltip("Shortest wait between twinkles, in seconds.")]
    [Min(0.1f)] public float sparkleMinSeconds = 0.9f;

    [Tooltip("Longest wait between twinkles, in seconds. Rolled fresh between the two every " +
             "time - irregular on purpose, since a steady pulse reads as machinery rather than " +
             "as something growing.")]
    [Min(0.1f)] public float sparkleMaxSeconds = 1.8f;

    /// <summary>A fresh gap between twinkles. Tolerates a max typed below the min.</summary>
    float RollSparkle()
    {
        float lo = Mathf.Max(sparkleMinSeconds, 0.1f);
        return UnityEngine.Random.Range(lo, Mathf.Max(sparkleMaxSeconds, lo));
    }

    /// <summary>The spot this came up on. The patch reads it back to free the spot.</summary>
    public MushroomSpawnPoint Point { get; private set; }

    // A mushroom mid-pick is already spoken for: the beat takes real time, and until it ends the
    // object is still standing there in the registry.
    public override bool CanInteract => !_picked && item != null;

    public override int StateKey => _picked ? 1 : 0;

    public override string Prompt => _prompt;

    Action<Mushroom> _onPicked;
    Action _pickComplete;
    string _prompt = string.Empty;
    bool _picked;
    float _nextSparkle;

    /// <summary>
    /// Stand this mushroom up on a spot. Called by the patch on a fresh or recycled instance,
    /// so everything that could have been left over from the last appearance is set here.
    /// </summary>
    public void Place(MushroomSpawnPoint point, Action<Mushroom> onPicked)
    {
        Point = point;
        _onPicked = onPicked;
        _picked = false;
        _prompt = item != null ? "Pick " + item.displayName : "Pick";

        var tf = transform;
        tf.position = point.transform.position;

        if (visual != null)
        {
            visual.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            visual.localScale = Vector3.one * UnityEngine.Random.Range(scaleRange.x, scaleRange.y);
        }

        _nextSparkle = Time.time + RollSparkle();
    }

    void Update()
    {
        if (_picked || sparkle == null || Time.time < _nextSparkle) return;

        sparkle.Play();
        _nextSparkle = Time.time + RollSparkle();
    }

    [Tooltip("Soft pluck as the mushroom comes up. Optional.")]
    [SerializeField] SoundEvent pickSound;

    public override void Interact(PlayerInteractor interactor)
    {
        if (_picked) return;
        _picked = true;                       // claimed before the beat, not after it

        // cached delegate rather than a lambda, the way the sell counter and the plots do it -
        // this fires on every mushroom for the whole session
        _pickComplete ??= Picked;
        AudioManager.PlayAt(pickSound, transform.position);
        interactor.Controller.BeginAction(pickAction, transform, _pickComplete);
    }

    void Picked()
    {
        if (item == null) return;

        // Fly it to the player rather than banking it silently, so picking reads the same as
        // harvesting a plot. Falls back to paying straight into the crate if the item has no
        // model, the way Producer does.
        var prefab = item.DisplayPrefab;
        if (prefab != null)
        {
            var go = Instantiate(prefab, transform.position + Vector3.up * 0.35f, Quaternion.identity);
            var col = go.GetComponent<Collectable>();
            if (col == null) col = go.AddComponent<Collectable>();
            col.item = item;
            col.amount = amount;
        }
        else Inventory.AddProduce(item, amount);

        _onPicked?.Invoke(this);
    }
}
