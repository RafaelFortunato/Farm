using TMPro;
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
    public static SeedMenu Instance { get; private set; }

    /// <summary>True while the menu is up, so gameplay input can ignore Interact.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Content")]
    public CropDef[] crops;

    [Header("Prefab wiring")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Transform cropButtonContainer;
    [SerializeField] CropButton cropButtonPrefab;
    [SerializeField] Button cancelButton;
    [SerializeField] TextMeshProUGUI coinLabel;

    readonly System.Collections.Generic.List<CropButton> _buttons = new System.Collections.Generic.List<CropButton>();
    SoilPlot _target;
    PlayerInteractor _interactor;

    // Initialised in OnEnable rather than Awake: a domain reload during play wipes the
    // static Instance and re-runs OnEnable but NOT Awake, which would leave Instance null
    // while the spawned rows still exist.
    void OnEnable()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildCropButtons();

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(Close);
        }

        Close();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
        if (panelRoot != null) panelRoot.SetActive(true);
        IsOpen = true;
        Refresh();
    }

    public void Close()
    {
        _target = null;
        _interactor = null;
        if (panelRoot != null) panelRoot.SetActive(false);
        IsOpen = false;
    }

    void Refresh()
    {
        if (coinLabel != null) coinLabel.text = Inventory.Coins + " COINS";

        // grey out anything the player cannot afford right now
        for (int i = 0; i < _buttons.Count && i < crops.Length; i++)
            if (_buttons[i] != null && crops[i] != null)
                _buttons[i].SetAffordable(Inventory.Coins >= crops[i].seedCost);
    }

    void Choose(CropDef crop)
    {
        if (_target == null) { Close(); return; }

        // The plot owns which beat plays - the menu only forwards who is doing the planting.
        if (!_target.TryPlant(crop, _interactor)) { Refresh(); return; }

        Close();
    }
}
