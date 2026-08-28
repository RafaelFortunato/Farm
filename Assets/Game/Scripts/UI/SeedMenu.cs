using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Seed picker shown when the player uses an empty plot.
///
/// The panel is authored as a prefab under Prefabs/UI and lives beneath MainCanvas.
/// This script only shows/hides it and fills in data - all styling is in the prefab.
/// </summary>
public class SeedMenu : MonoBehaviour
{
    /// <summary>
    /// Found lazily, INCLUDING while disabled. The panel is saved deactivated so it does
    /// not clutter the editor view, which means OnEnable has not run and cannot have
    /// registered anything - so the lookup has to tolerate an inactive object.
    /// </summary>
    public static SeedMenu Instance
    {
        get
        {
            if (_instance == null) _instance = FindAnyObjectByType<SeedMenu>(FindObjectsInactive.Include);
            return _instance;
        }
    }
    static SeedMenu _instance;

    /// <summary>True while the menu is up, so gameplay input can ignore Interact.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Content")]
    public CropDef[] crops;

    [Header("Prefab wiring")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Transform cropButtonContainer;
    [SerializeField] CropButton cropButtonPrefab;
    [SerializeField] Button cancelButton;

    readonly System.Collections.Generic.List<CropButton> _buttons = new System.Collections.Generic.List<CropButton>();
    SoilPlot _target;
    PlayerInteractor _interactor;

    void OnEnable() => _instance = this;

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        IsOpen = false;
    }

    /// <summary>
    /// One row per crop. Clears any existing rows first so this is safe to call again
    /// after a domain reload, when the old rows survive but the bindings do not.
    /// </summary>
    void BuildCropButtons()
    {
        if (cropButtonPrefab == null || cropButtonContainer == null || crops == null) return;

        _buttons.Clear();
        for (int i = cropButtonContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(cropButtonContainer.GetChild(i).gameObject);

        foreach (var crop in crops)
        {
            if (crop == null) continue;
            var row = Instantiate(cropButtonPrefab, cropButtonContainer);
            row.name = "CropButton_" + crop.displayName;
            row.Bind(crop, Choose);
            _buttons.Add(row);
        }
    }

    public void Open(SoilPlot plot, PlayerInteractor interactor = null)
    {
        _target = plot;
        _interactor = interactor;

        gameObject.SetActive(true);              // this object is the toggle
        if (panelRoot != null) panelRoot.SetActive(true);

        // rebuilt on every open rather than once at startup: three rows is nothing, and it
        // cannot go stale after a domain reload wipes the bindings
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Close);
        }
        BuildCropButtons();

        IsOpen = true;
        Refresh();

        // rows were just spawned into a panel enabled this frame - settle it now
        Menus.RebuildLayout(cropButtonContainer as RectTransform,
                            cropButtonContainer != null ? cropButtonContainer.parent as RectTransform : null);
    }

    public void Close()
    {
        _target = null;
        _interactor = null;
        IsOpen = false;
        gameObject.SetActive(false);
    }

    void Refresh()
    {
        // a crop is plantable only while there is a seed for it in the bag
        for (int i = 0; i < _buttons.Count && i < crops.Length; i++)
            if (_buttons[i] != null && crops[i] != null)
                _buttons[i].SetStock(Inventory.SeedCount(crops[i]));
    }

    void Choose(CropDef crop)
    {
        if (_target == null) { Close(); return; }

        // The plot owns which beat plays - the menu only forwards who is doing the planting.
        if (!_target.TryPlant(crop, _interactor)) { Refresh(); return; }

        Close();
    }
}
