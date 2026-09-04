using UnityEngine;

/// <summary>
/// The strip of item chips beside the coin readout.
///
/// A chip exists for every tracked item but is only shown once the player actually holds one.
/// Seven chips sitting at zero from the first minute would spoil what the farmhouse levels
/// unlock and leave the player reading an inventory that is mostly nothing.
///
/// Driven by Inventory.Changed rather than polled, like CoinHud, and each chip guards its own
/// count - a harvest that changes one item does not rewrite the other six labels.
/// </summary>
public class InventoryHud : MonoBehaviour
{
    [Header("Prefab wiring")]
    [SerializeField] InventoryChip chipPrefab;

    [Tooltip("Layout group the chips are parented to.")]
    [SerializeField] RectTransform chipRow;

    [Header("Contents")]
    [Tooltip("Every item that can appear, in the order they should read left to right.")]
    [SerializeField] ItemDef[] tracked;

    InventoryChip[] _chips;
    bool _stoodAside;

    // Subscribed in OnEnable rather than Awake: a domain reload during play re-runs OnEnable
    // but NOT Awake, and it wipes the static event we are listening to.
    void OnEnable()
    {
        Build();
        Inventory.Changed -= Refresh;   // never double-subscribe
        Inventory.Changed += Refresh;
        Refresh();
    }

    void OnDisable() => Inventory.Changed -= Refresh;

    /// <summary>
    /// Step aside while a menu is up. The panels open over this corner of the screen and the
    /// strip is drawn after them, so without this it shows through the shop rather than behind
    /// them. Nothing is lost by going: the shop's sell tab lists the same counts, item for item.
    /// </summary>
    void LateUpdate()
    {
        bool aside = Menus.AnyOpen;
        if (aside == _stoodAside) return;

        _stoodAside = aside;
        if (chipRow != null) chipRow.gameObject.SetActive(!aside);
    }

    /// <summary>
    /// Clones one chip per tracked item. Re-enabling reuses what is already there, and the two
    /// halves of the state - this array and the chips themselves - are reconciled rather than
    /// trusted: a domain reload clears the array while the chips survive, and a chip destroyed
    /// from outside leaves the array pointing at nothing. Either way the strip rebuilds instead
    /// of quietly rendering an empty row for the rest of the session.
    /// </summary>
    void Build()
    {
        if (chipPrefab == null || chipRow == null || tracked == null) return;
        if (Intact()) return;

        // Adopt whatever is already under the row instead of stacking a second set on top of it.
        var existing = chipRow.GetComponentsInChildren<InventoryChip>(true);

        _chips = new InventoryChip[tracked.Length];
        for (int i = 0; i < tracked.Length; i++)
        {
            if (tracked[i] == null) continue;

            var chip = i < existing.Length ? existing[i] : Instantiate(chipPrefab, chipRow);
            chip.Bind(tracked[i]);
            chip.gameObject.SetActive(false);   // shown by the first Refresh that finds a count
            _chips[i] = chip;
        }
    }

    /// <summary>True when the strip is already built and every chip is still alive.</summary>
    bool Intact()
    {
        if (_chips == null || _chips.Length != tracked.Length) return false;

        for (int i = 0; i < _chips.Length; i++)
            if (tracked[i] != null && _chips[i] == null) return false;

        return true;
    }

    void Refresh()
    {
        if (_chips == null) return;

        for (int i = 0; i < _chips.Length; i++)
        {
            var chip = _chips[i];
            if (chip == null) continue;

            int count = Inventory.ProduceCount(tracked[i]);
            bool holding = count > 0;

            if (chip.gameObject.activeSelf != holding) chip.gameObject.SetActive(holding);
            if (holding) chip.SetCount(count);
        }
    }
}
