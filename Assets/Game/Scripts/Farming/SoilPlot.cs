using UnityEngine;

/// <summary>
/// One tile of fertile soil.
///
/// Empty -> (player picks a seed) -> Growing -> Ready -> (player harvests) -> Empty.
/// Growth runs on elapsed real time so a throttled browser tab catches up on focus
/// rather than losing progress.
/// </summary>
public class SoilPlot : Interactable
{
    [Header("Wiring")]
    [Tooltip("Where the crop model is parented. Defaults to a child named CropAnchor.")]
    public Transform cropAnchor;

    [Header("Runtime (read-only)")]
    [SerializeField] PlotState _state = PlotState.Empty;
    [SerializeField] CropDef _crop;
    [SerializeField] float _plantedAt;

    GameObject _stageInstance;
    int _stageShown = -1;
    string _prompt = "Plant";

    public PlotState Current => _state;
    public CropDef Crop => _crop;

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
        _state = next;
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
                SeedMenu.Instance?.Open(this);
                break;
            case PlotState.Ready:
                Harvest();
                break;
        }
    }

    /// <summary>Plants a crop if the player can afford the seed. Returns false if too poor.</summary>
    public bool TryPlant(CropDef crop)
    {
        if (_state != PlotState.Empty || crop == null) return false;
        if (!Inventory.TrySpend(crop.seedCost)) return false;

        _crop = crop;
        _plantedAt = Time.time;
        _stageShown = -1;
        SetState(PlotState.Growing);
        ShowStage(0);
        return true;
    }

    void Harvest()
    {
        if (_state != PlotState.Ready || _crop == null) return;

        var prefab = _crop.collectablePrefab != null
            ? _crop.collectablePrefab
            : _crop.StagePrefab(_crop.StageCount - 1);

        if (prefab != null)
        {
            var spawnAt = cropAnchor.position + Vector3.up * 0.3f;
            var go = Instantiate(prefab, spawnAt, Quaternion.identity);
            var col = go.GetComponent<Collectable>();
            if (col == null) col = go.AddComponent<Collectable>();
            col.crop = _crop;
            col.amount = 1;
        }

        ClearStageInstance();
        _crop = null;
        _stageShown = -1;
        SetState(PlotState.Empty);
    }
}
