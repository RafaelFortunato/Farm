using UnityEngine;

/// <summary>
/// The seed shop. The player walks up to the storefront and opens the buy menu.
///
/// Put the focusAnchor at the counter rather than the building's pivot, so approaching
/// from behind does not count as being at the shop.
/// </summary>
public class Store : Interactable
{
    [Tooltip("What the store stocks, cheapest first. The first entry should be free so " +
             "the player can never be stranded with no seeds and no coins.")]
    public CropDef[] stock;

    public override string Prompt => "Shop";

    public override void Interact(PlayerInteractor interactor)
    {
        if (StoreMenu.Instance == null) return;
        StoreMenu.Instance.Open(stock, interactor);
    }
}
