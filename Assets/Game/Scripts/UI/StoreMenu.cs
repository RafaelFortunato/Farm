using System.Collections.Generic;
using UnityEngine;
using Farm.Audio;
using Farm.Farming;
using Farm.Interaction;

namespace Farm.UI
{
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
    public class StoreMenu : BaseMenu
    {
        [Header("Prefab wiring")]
        [SerializeField] Transform rowContainer;
        [SerializeField] SellRow sellRowPrefab;

        [Tooltip("Shown centred in the panel instead of any rows, when there is currently nothing " +
                 "to sell.")]
        [SerializeField] GameObject emptyMessage;

        [Tooltip("Cash register, played on each item sold. Kept quiet on purpose - this fires once " +
                 "per click and a player emptying a crate will click it a lot.")]
        [SerializeField] SoundEvent sellSound;

        readonly List<SellRow> _rows = new List<SellRow>();

        // What the rows were built from. Held separately from the store's own array because that
        // can contain nulls and the roster is filtered - without this, row i and entry i drift
        // apart and Refresh starts writing one item's numbers onto another's row.
        readonly List<ItemDef> _listed = new List<ItemDef>();

        Store _store;

        // The panel's own GameObject is the toggle, so these fire exactly on open and close: the
        // subscription is scoped to the panel being visible and costs nothing while it is shut.
        void OnEnable()
        {
            Inventory.Changed -= Refresh;    // never double-subscribe
            Inventory.Changed += Refresh;
        }

        void OnDisable() => Inventory.Changed -= Refresh;

        protected override void OnDestroy()
        {
            Inventory.Changed -= Refresh;
            base.OnDestroy();
        }

        /// <summary>Shows the shop stock. The interactor is released when the panel closes.</summary>
        public void Open(Store store, PlayerInteractor interactor = null)
        {
            if (store == null) return;

            _store = store;
            Present(interactor);

            BuildRows();
            Refresh();

            // rows were just spawned into a panel enabled this frame - settle it now, innermost
            // first, or they appear stacked on top of one another
            UIManager.RebuildLayout(rowContainer as RectTransform,
                                rowContainer != null ? rowContainer.parent as RectTransform : null);
        }

        /// <summary>Drops the store reference so a stale one cannot be bought from next time.</summary>
        protected override void OnDismissed() => _store = null;

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
            if (!Inventory.TrySpendProduce(item, 1)) { AudioManager.PlayDenied(); Refresh(); return; }

            Inventory.AddCoins(item.sellValue);

            // After the spend succeeds, so a refused click stays silent - a sound on a sale that
            // did not happen reads as if it did. Flat rather than positional: this is a panel in
            // front of the player, not a thing across the farm.
            //
            // Selling is one click per item, so a player emptying a crate hammers this. The event
            // asset carries the guards for that: a low level, a wide pitch spread so repeats do not
            // stack into one tone, and a short clip that frees its voice quickly.
            AudioManager.PlayUI(sellSound);
        }
    }
}
