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

    [Tooltip("Price colour for a crop the farm has not unlocked yet.")]
    public Color lockedColor = new Color(0.69f, 0.23f, 0.18f);

    CropDef _crop;
    Color _priceColor;
    bool _priceColorCached;

    public void Bind(CropDef crop, Action<CropDef> onBuy)
    {
        _crop = crop;
        if (crop == null) return;

        // remember the authored price colour once, so the lock state can put it back
        if (!_priceColorCached && priceLabel != null)
        {
            _priceColor = priceLabel.color;
            _priceColorCached = true;
        }

        if (nameLabel != null) nameLabel.text = crop.displayName;

        // a free seed shows the word, not a zero next to a coin
        bool free = crop.seedCost <= 0;
        if (priceLabel != null) priceLabel.text = free ? "FREE" : crop.seedCost.ToString();
        if (coinIcon != null) coinIcon.SetActive(!free);

        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onBuy?.Invoke(crop));
    }

    /// <summary>
    /// Two separate reasons a row can be dead, and they should not look the same: a crop the
    /// farm has not reached yet shows the level it needs, which is a reason to go and upgrade;
    /// one that is merely unaffordable keeps its price and just greys out.
    /// </summary>
    public void SetAvailable(bool unlocked, bool affordable)
    {
        if (_crop == null) return;

        if (ownedLabel != null) ownedLabel.text = "x" + Inventory.SeedCount(_crop);

        if (priceLabel != null)
        {
            bool free = _crop.seedCost <= 0;
            priceLabel.text = unlocked ? (free ? "FREE" : _crop.seedCost.ToString())
                                       : "Lv " + _crop.requiredLevel;
            priceLabel.color = unlocked ? _priceColor : lockedColor;
            if (coinIcon != null) coinIcon.SetActive(unlocked && !free);
        }

        if (button != null) button.interactable = unlocked && affordable;
    }
}
