using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The sell counter, opened by walking up to the Store and pressing interact.
///
/// One list, one direction: everything the farm has ever made, and what the counter pays for
/// one of it. There is nothing to buy - seed is free and unlimited - so this panel has no tabs
/// and no prices going the other way.
///
/// The panel is authored as a prefab under Prefabs/UI and lives beneath MainCanvas.
/// This script only shows/hides it and fills in data - all styling is in the prefab.
/// </summary>
public class StoreMenu : MonoBehaviour
{
    /// <summary>True while the panel is up, so gameplay input can ignore Interact.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Prefab wiring")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] Transform rowContainer;
    [SerializeField] SellRow sellRowPrefab;
    [SerializeField] Button closeButton;

    [Tooltip("Shown centred in the panel instead of any rows, when there is currently nothing " +
             "to sell.")]
    [SerializeField] GameObject emptyMessage;

    [Header("Camera")]
    [Tooltip("How far the camera pushes in while the panel is open, in world units.")]
    public float cameraZoom = 5f;
    [Tooltip("Extra tilt while the panel is open, in degrees. Negative drops the camera lower.")]
    public float cameraPitch = -8f;
    [Tooltip("Seconds to ease the camera in, and back out on close.")]
    public float cameraBlend = 0.35f;

    CameraFollow _cameraRig;

    readonly List<SellRow> _rows = new List<SellRow>();

    // What the rows were built from. Held separately from the store's own array because that
    // can contain nulls and the roster is filtered - without this, row i and entry i drift
    // apart and Refresh starts writing one item's numbers onto another's row.
    readonly List<ItemDef> _listed = new List<ItemDef>();

    Store _store;
    PlayerInteractor _interactor;

    // The panel's own GameObject is the toggle, so these fire exactly on open and close: the
    // subscription is scoped to the panel being visible and costs nothing while it is shut.
    void OnEnable()
    {
        Inventory.Changed -= Refresh;    // never double-subscribe
        Inventory.Changed += Refresh;
    }

    void OnDisable() => Inventory.Changed -= Refresh;

    void OnDestroy()
    {
        Inventory.Changed -= Refresh;
        IsOpen = false;
    }

    public void Open(Store store, PlayerInteractor interactor = null)
    {
        if (store == null) return;

        _store = store;
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

        IsOpen = true;
        BuildRows();
        Refresh();

        // rows were just spawned into a panel enabled this frame - settle it now, innermost
        // first, or they appear stacked on top of one another
        Menus.RebuildLayout(rowContainer as RectTransform,
                            rowContainer != null ? rowContainer.parent as RectTransform : null);
    }

    public void Close()
    {
        _store = null;
        _interactor = null;
        IsOpen = false;

        if (_cameraRig != null) _cameraRig.ClearActionFraming();

        gameObject.SetActive(false);
    }

    /// <summary>
    /// One row per thing the farm currently holds, in the order the store lists them.
    ///
    /// The roster is decided here, when the panel opens, and then left alone until the next
    /// open. Refresh runs off Inventory.Changed and selling raises that from inside a row's own
    /// click handler, so a Refresh that rebuilt the list would destroy the button currently
    /// dispatching the click - instead a row sold down to zero simply keeps showing "x0" for the
    /// rest of this visit, and only drops out of the list the next time the panel is opened.
    /// A farm holding nothing shows the empty message instead of a bare panel.
    /// </summary>
    void BuildRows()
    {
        if (rowContainer == null || sellRowPrefab == null || _store == null) return;

        _rows.Clear();
        _listed.Clear();

        for (int i = rowContainer.childCount - 1; i >= 0; i--)
        {
            var child = rowContainer.GetChild(i);
            if (emptyMessage != null && child == emptyMessage.transform) continue;
            DestroyImmediate(child.gameObject);
        }

        if (_store.sellable != null)
            foreach (var item in _store.sellable)
            {
                if (item == null || Inventory.ProduceCount(item) <= 0) continue;

                var row = Instantiate(sellRowPrefab, rowContainer);
                row.name = "SellRow_" + item.displayName;
                row.Bind(item, Sell);
                _rows.Add(row);
                _listed.Add(item);
            }

        if (emptyMessage != null) emptyMessage.SetActive(_listed.Count == 0);
    }

    /// <summary>Repaint what the rows say. Contents only, never the set of rows.</summary>
    void Refresh()
    {
        if (_store == null) return;

        for (int i = 0; i < _rows.Count && i < _listed.Count; i++)
            if (_rows[i] != null)
                _rows[i].SetAvailable(Inventory.ProduceCount(_listed[i]), _listed[i].sellValue);
    }

    /// <summary>
    /// One piece at a time. This is the second place in the codebase that hands out coins -
    /// the sell counter on the road is the other.
    /// </summary>
    void Sell(ItemDef item)
    {
        if (_store == null || item == null) return;
        if (!Inventory.TrySpendProduce(item, 1)) { Refresh(); return; }

        Inventory.AddCoins(item.sellValue);
    }
}
