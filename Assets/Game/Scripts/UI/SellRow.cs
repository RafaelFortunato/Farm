using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One line in the sell half of the shop: what the item is, what the counter pays for one, and
/// how many the player is carrying.
///
/// Its own type rather than something shared with the crop picker: this row binds an ItemDef
/// and shows a price and a held count, which nothing else in the UI does. RecipeRow sits beside
/// it on the same terms.
/// </summary>
public class SellRow : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI priceLabel;
    [SerializeField] TextMeshProUGUI heldLabel;
    [SerializeField] GameObject coinIcon;
    
    [SerializeField] Image background;

    ItemDef _item;
    Color _baseColor;
    bool _captured;

    // The authored label colours, captured before anything dims them - see RowInk.
    Color _nameInk, _priceInk, _heldInk;
    bool _inkCaptured;

    /// <summary>Sets everything that depends on which item this is. Called once per build.</summary>
    public void Bind(ItemDef item, Action<ItemDef> onSell)
    {
        _item = item;
        if (item == null) return;

        if (nameLabel != null) nameLabel.text = item.displayName;

        if (iconImage != null)
        {
            iconImage.sprite = item.icon;
            // A missing icon would otherwise draw as a white box - apples have none yet.
            iconImage.enabled = item.icon != null;
        }
        
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSell?.Invoke(item));
    }

    /// <summary>
    /// The numbers, which move on every sale. Both are passed in rather than read off Inventory:
    /// the row should not have to know where the price comes from, and a sell row has exactly one
    /// reason to be dead, so this takes counts rather than StoreRow's pair of bools.
    /// </summary>
    public void SetAvailable(int held, int unitPrice)
    {
        if (_item == null) return;

        bool usable = held > 0;
        CaptureInk();

        // A row sold down to zero stays on the list for the rest of the visit, so it has to say
        // so in the text - the button going dead is not visible on a near-white row.
        if (heldLabel != null)
        {
            heldLabel.text = "x" + held;
            heldLabel.color = RowInk.For(_heldInk, usable);
        }
        if (priceLabel != null)
        {
            priceLabel.text = unitPrice.ToString();
            priceLabel.color = RowInk.For(_priceInk, usable);
        }
        if (nameLabel != null) nameLabel.color = RowInk.For(_nameInk, usable);

        if (coinIcon != null) coinIcon.SetActive(true);
        if (button != null) button.interactable = usable;
    }

    /// <summary>Remembers the prefab's own colours the first time, so dimming is reversible.</summary>
    void CaptureInk()
    {
        if (_inkCaptured) return;
        _inkCaptured = true;

        if (nameLabel != null) _nameInk = nameLabel.color;
        if (priceLabel != null) _priceInk = priceLabel.color;
        if (heldLabel != null) _heldInk = heldLabel.color;
    }
}
