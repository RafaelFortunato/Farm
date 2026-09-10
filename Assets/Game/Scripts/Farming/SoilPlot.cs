using UnityEngine;

/// <summary>
/// One tile of fertile soil.
///
/// Empty -> (player picks a seed) -> Growing -> Ready -> (player harvests) -> Empty.
/// Growth runs on elapsed real time so a throttled browser tab catches up on focus
/// rather than losing progress.
/// </summary>
public class SoilPlot : Interactable, ITimedProgress
{
    [Header("Wiring")]
    [Tooltip("Where the crop model is parented. Defaults to a child named CropAnchor.")]
    public Transform cropAnchor;

    [Header("Player actions")]
    [Tooltip("Beat the player performs when sowing a seed. Leave empty to plant instantly.")]
    public CharacterAction plantAction;
    [Tooltip("Beat the player performs when pulling a crop. Leave empty to harvest instantly.")]
    public CharacterAction harvestAction;

    [Header("Sound")]
    [Tooltip("Seed going into the soil, at the end of the planting beat.")]
    [SerializeField] SoundEvent plantSound;
    [Tooltip("Crop coming out of the soil.")]
    [SerializeField] SoundEvent harvestSound;
    [Tooltip("Quiet chime when this plot ripens. Every plot in a field can ripen within " +
             "a few seconds of each other, so the event asset rate-limits itself.")]
    [SerializeField] SoundEvent readySound;

    [Header("Runtime (read-only)")]
    [SerializeField] PlotState _state = PlotState.Empty;
    [SerializeField] CropDef _crop;
    [SerializeField] float _plantedAt;

    GameObject _stageInstance;
    int _stageShown = -1;
    string _prompt = "Plant";

    public PlotState Current => _state;
    public CropDef Crop => _crop;

    /// <summary>True while the crop is growing, so the countdown ring shows itself.</summary>
    public bool InProgress => _state == PlotState.Growing;

    /// <summary>0..1 through the growth timer.</summary>
    public float Progress
    {
        get
        {
            if (_state == PlotState.Ready) return 1f;
            if (_state != PlotState.Growing || _crop == null) return 0f;
            return Mathf.Clamp01((Time.time - _plantedAt) / Mathf.Max(_crop.growSeconds, 0.01f));
        }
    }

    public override bool CanInteract => _state != PlotState.Growing;

    // The plot's own state enum drives the key, so it changes exactly when the plot does.
    public override int StateKey => (int)_state;

    // Cached rather than built on access: "Harvest X" would otherwise allocate a string
    // every time the prompt is read.
    public override string Prompt => _prompt;

    /// <summary>Single place where state changes, so the prompt can never drift from it.</summary>
    void SetState(PlotState next)
    {
        // Only on the CHANGE into Ready. SetState is also called on enable to rebuild the
        // prompt from serialised state, and announcing every plot in the field on load would
        // greet the player with a chord of chimes.
        bool ripened = next == PlotState.Ready && _state != PlotState.Ready;

        _state = next;
        if (ripened) AudioManager.PlayAt(readySound, transform.position);
        switch (_state)
        {
            case PlotState.Empty: _prompt = "Plant"; break;
            case PlotState.Ready: _prompt = _crop != null ? "Harvest " + _crop.displayName : "Harvest"; break;
            default: _prompt = string.Empty; break;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (cropAnchor == null)
        {
            var t = transform.Find("CropAnchor");
            cropAnchor = t != null ? t : transform;
        }
        SetState(_state);   // rebuild the cached prompt from whatever state was serialised
    }

    void Update()
    {
        if (_state != PlotState.Growing) return;

        float p = Progress;
        int stage = StageForProgress(p);
        if (stage != _stageShown) ShowStage(stage);

        if (p >= 1f)
        {
            SetState(PlotState.Ready);
            ShowStage(_crop.StageCount - 1);
        }
    }

    int StageForProgress(float p)
    {
        int count = _crop != null ? _crop.StageCount : 0;
        if (count <= 0) return 0;
        // last stage is reserved for "ready", so grow through the earlier ones
        int growStages = Mathf.Max(count - 1, 1);
        return Mathf.Clamp(Mathf.FloorToInt(p * growStages), 0, count - 1);
    }

    void ShowStage(int stage)
    {
        _stageShown = stage;
        ClearStageInstance();

        var prefab = _crop != null ? _crop.StagePrefab(stage) : null;
        if (prefab == null) return;

        _stageInstance = Instantiate(prefab, cropAnchor.position, cropAnchor.rotation, cropAnchor);
        _stageInstance.transform.localScale = Vector3.one * _crop.StageScale(stage);
    }

    void ClearStageInstance()
    {
        if (_stageInstance == null) return;
        if (Application.isPlaying) Destroy(_stageInstance);
        else DestroyImmediate(_stageInstance);
        _stageInstance = null;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        switch (_state)
        {
            case PlotState.Empty:
                UIManager.Seeds.Open(this, interactor);
                break;
            case PlotState.Ready:
                // The crop pops out at the end of the beat, so the dip reads as its cause.
                // With no action wired the callback runs immediately, same as before.
        // Under the animation, not after it. These are the sound of the player DOING
        // the thing; the callback they used to sit in runs a beat later, which reads
        // as lag. The payoff sounds - the till, the upgrade flourish - stay on the
        // callback, because those really do happen at the end.
                AudioManager.PlayAt(harvestSound, transform.position);
                interactor.Controller.BeginAction(harvestAction, transform, Harvest);
                break;
        }
    }

    /// <summary>
    /// Plants a crop. Free and unlimited - the only thing that can refuse is a plot that is
    /// already in use. Pass the interactor to play the planting beat; leave it null to plant
    /// silently.
    /// </summary>
    public bool TryPlant(CropDef crop, PlayerInteractor interactor = null)
    {
        if (_state != PlotState.Empty || crop == null) return false;

        _crop = crop;
        _stageShown = -1;

        // At the START of the beat, not on the callback at the end of it. The sound IS the
        // player working the soil, so it has to run under the animation rather than announce
        // that the animation finished - BeginGrowing fires a beat later, which read as lag.
        AudioManager.PlayAt(plantSound, transform.position);

        // The crop only goes in when the beat ends, so the growth clock and the ring above
        // the plot both start with the animation rather than with the button press.
        if (interactor != null) interactor.Controller.BeginAction(plantAction, transform, BeginGrowing);
        else BeginGrowing();
        return true;
    }

    /// <summary>Starts the growth clock. Deferred to the end of the planting beat.</summary>
    void BeginGrowing()
    {
        _plantedAt = Time.time;
        SetState(PlotState.Growing);
        ShowStage(0);
    }

    void Harvest()
    {
        if (_state != PlotState.Ready || _crop == null) return;

        // DisplayPrefab is the single item, not the ripe stage - the ripe stage is a cluster of
        // however many this crop yields, and a pickup showing four carrots flying to the player
        // four times over would multiply the harvest on screen.
        var prefab = _crop.DisplayPrefab;
        int yield = Mathf.Max(_crop.yieldPerHarvest, 1);

        if (prefab != null)
        {
            // One flier per item, spread around the plot rather than stacked, so the count the
            // plant was showing is visibly the count that arrives.
            for (int i = 0; i < yield; i++)
            {
                var offset = Vector3.zero;
                if (yield > 1)
                {
                    float angle = i / (float)yield * Mathf.PI * 2f;
                    offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.22f;
                }

                var go = Instantiate(prefab, cropAnchor.position + Vector3.up * 0.3f + offset,
                                     Quaternion.identity);
                var col = go.GetComponent<Collectable>();
                if (col == null) col = go.AddComponent<Collectable>();
                col.item = _crop;
                col.amount = 1;
            }
        }

        ClearStageInstance();
        _crop = null;
        _stageShown = -1;
        SetState(PlotState.Empty);
    }
}
