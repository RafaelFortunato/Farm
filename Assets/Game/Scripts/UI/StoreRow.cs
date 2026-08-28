using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One line in the store: crop name, seed price, and how many the player already owns.
/// Authored as a prefab (Prefabs/UI/StoreRow) - this script only fills in the data.
/// </summary>
public class StoreRow : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI priceLabel;
    [SerializeField] TextMeshProUGUI ownedLabel;
    [SerializeField] GameObject coinIcon;

    CropDef _crop;

    public void Bind(CropDef crop, Action<CropDef> onBuy)
    {
        _crop = crop;
        if (crop == null) return;

        if (nameLabel != null) nameLabel.text = crop.displayName.ToUpperInvariant();

        // a free seed shows the word, not a zero next to a coin
        bool free = crop.seedCost <= 0;
        if (priceLabel != null) priceLabel.text = free ? "FREE" : crop.seedCost.ToString();
        if (coinIcon != null) coinIcon.SetActive(!free);

        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onBuy?.Invoke(crop));
    }

    /// <summary>Refreshes the owned count and greys the row when it cannot be afforded.</summary>
    public void SetAffordable(bool affordable)
    {
        if (ownedLabel != null && _crop != null) ownedLabel.text = "x" + Inventory.SeedCount(_crop);
        if (button != null) button.interactable = affordable;
    }
}
