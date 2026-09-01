using UnityEngine;

/// <summary>
/// The seed shop. The player walks up to the storefront and opens the buy menu.
///
/// Put the focusAnchor at the counter rather than the building's pivot, so approaching
/// from behind does not count as being at the shop.
/// </summary>
public class Store : Interactable
{
    [Tooltip("Everything the store will ever stock, cheapest first. The first entry should be " +
             "free so the player can never be stranded with no seeds and no coins. Entries above " +
             "the current farm level are shown locked rather than hidden - a seed you cannot buy " +
             "yet is a reason to upgrade.")]
    public CropDef[] stock;

    [Tooltip("Which crops are unlocked. Rises with the farmhouse.")]
    public int level = 1;

    public override string Prompt => "Shop";

    /// <summary>True when the farm has come far enough for this crop to be on sale.</summary>
    public bool IsUnlocked(CropDef crop) => crop != null && crop.requiredLevel <= level;

    public override void Interact(PlayerInteractor interactor)
    {
        UIManager.Shop.Open(this, interactor);
    }
}
