using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Seed shop, opened by walking up to the Store and pressing interact.
///
/// The panel is authored as a prefab under Prefabs/UI and lives beneath MainCanvas.
/// This script only shows/hides it and fills in data - all styling is in the prefab.
/// </summary>
public class StoreMenu : MonoBehaviour
{
    /// <summary>True while the shop is up, so gameplay input can ignore Interact.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Prefab wiring")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Transform rowContainer;
    [SerializeField] StoreRow rowPrefab;
    [SerializeField] Button closeButton;

    [Header("Camera")]
    [Tooltip("How far the camera pushes in while the shop is open, in world units.")]
    public float cameraZoom = 5f;
    [Tooltip("Extra tilt while the shop is open, in degrees. Negative drops the camera lower.")]
    public float cameraPitch = -8f;
    [Tooltip("Seconds to ease the camera in, and back out on close.")]
    public float cameraBlend = 0.35f;

    CameraFollow _cameraRig;

    readonly System.Collections.Generic.List<StoreRow> _rows = new System.Collections.Generic.List<StoreRow>();
    CropDef[] _stock;
    PlayerInteractor _interactor;

    void OnDestroy()
    {
        IsOpen = false;
    }

    public void Open(CropDef[] stock, PlayerInteractor interactor = null)
    {
        _stock = stock;
        _interactor = interactor;

        gameObject.SetActive(true);              // this object is the toggle
        if (panelRoot != null) panelRoot.SetActive(true);

        // same push-in the planting beat uses, so talking to the shopkeeper gets the
        // same emphasis. Resolved here rather than in OnEnable: this object starts disabled.
        if (_cameraRig == null) _cameraRig = GameManager.CameraRig;
        _cameraRig.SetActionFraming(cameraZoom, cameraPitch, cameraBlend);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }
        BuildRows();

        IsOpen = true;
        Refresh();

        // rows were just spawned into a panel enabled this frame - settle it now
        Menus.RebuildLayout(rowContainer as RectTransform,
                            rowContainer != null ? rowContainer.parent as RectTransform : null);
    }

    public void Close()
    {
        _interactor = null;
        IsOpen = false;

        if (_cameraRig != null) _cameraRig.ClearActionFraming();

        gameObject.SetActive(false);
    }

    /// <summary>
    /// One row per stocked crop. Clears any existing rows first so this is safe to call
    /// again - the store's stock is passed in, so it can differ between visits.
    /// </summary>
    void BuildRows()
    {
        if (rowPrefab == null || rowContainer == null || _stock == null) return;

        _rows.Clear();
        for (int i = rowContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(rowContainer.GetChild(i).gameObject);

        foreach (var crop in _stock)
        {
            if (crop == null) continue;
            var row = Instantiate(rowPrefab, rowContainer);
            row.name = "StoreRow_" + crop.displayName;
            row.Bind(crop, Buy);
            _rows.Add(row);
        }
    }

    void Refresh()
    {
        for (int i = 0; i < _rows.Count && i < _stock.Length; i++)
            if (_rows[i] != null && _stock[i] != null)
                _rows[i].SetAffordable(Inventory.Coins >= _stock[i].seedCost);
    }

    void Buy(CropDef crop)
    {
        // TrySpend covers the free seed too - spending 0 always succeeds
        if (!Inventory.TrySpend(crop.seedCost)) { Refresh(); return; }

        Inventory.AddSeeds(crop, 1);
        Refresh();
    }
}
