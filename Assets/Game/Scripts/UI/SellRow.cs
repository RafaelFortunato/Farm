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

        if (heldLabel != null) heldLabel.text = "x" + held;
        if (priceLabel != null) priceLabel.text = unitPrice.ToString();
        if (coinIcon != null) coinIcon.SetActive(true);
        if (button != null) button.interactable = held > 0;
    }
}
