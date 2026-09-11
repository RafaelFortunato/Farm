using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crop picker shown when the player uses an empty plot.
///
/// Planting costs nothing and never runs out, so the only question this asks is which crop -
/// and the only thing that can say no is the farmhouse level. Crops above it are shown locked
/// rather than hidden: a crop you cannot plant yet is a reason to go and upgrade.
///
/// The panel is authored as a prefab under Prefabs/UI and lives beneath MainCanvas.
/// This script only shows/hides it and fills in data - all styling is in the prefab.
/// </summary>
public class SeedMenu : BaseMenu
{
    [Header("Content")]
    public CropDef[] crops;

    [Tooltip("Which crops are unlocked. Pushed in by the farmhouse, the same way the stove's " +
             "recipe tier and the trucks' demand table are.")]
    public int level = 1;

    [Header("Prefab wiring")]
    [SerializeField] Transform cropButtonContainer;
    [SerializeField] CropButton cropButtonPrefab;

    readonly System.Collections.Generic.List<CropButton> _buttons = new System.Collections.Generic.List<CropButton>();
    SoilPlot _target;

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
        Present(interactor);

        // rebuilt on every open rather than once at startup: three rows is nothing, and it
        // cannot go stale after a domain reload wipes the bindings
        BuildCropButtons();
        Refresh();

        // rows were just spawned into a panel enabled this frame - settle it now
        UIManager.RebuildLayout(cropButtonContainer as RectTransform,
                            cropButtonContainer != null ? cropButtonContainer.parent as RectTransform : null);
    }

    protected override void OnDismissed() => _target = null;

    void Refresh()
    {
        for (int i = 0; i < _buttons.Count && i < crops.Length; i++)
            if (_buttons[i] != null && crops[i] != null)
                _buttons[i].SetAvailable(crops[i].requiredLevel <= level);
    }

    void Choose(CropDef crop)
    {
        if (_target == null) { Close(); return; }

        // The plot owns which beat plays - the menu only forwards who is doing the planting.
        if (!_target.TryPlant(crop, Interactor)) { Refresh(); return; }

        Close(false);          // they picked a crop; the planting beat speaks for it
    }
}
