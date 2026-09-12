using UnityEngine;
using Farm.Farming;

namespace Farm.Interaction
{
    /// <summary>
    /// The shop counter. The player walks up and sells what the farm has made.
    ///
    /// It sells nothing back: seed is free and unlimited, so the only thing crossing this counter
    /// is produce going out. It is the dependable half of the economy - always open, no queue, and
    /// it will take any quantity of anything. The trucks pay several times better but only turn up
    /// now and then, so the choice on every harvest is whether to bank it here or hold it for one
    /// that might come.
    ///
    /// Put the focusAnchor at the counter rather than the building's pivot, so approaching
    /// from behind does not count as being at the shop.
    /// </summary>
    public class Store : Interactable
    {
        [Tooltip("Everything the counter will buy, in the order it should be listed. Worth keeping " +
                 "in the same order the economy is tuned to - cheapest per minute first.")]
        public ItemDef[] sellable;

        public override string Prompt => "Sell";

        /// <summary>Opens the seed shop. The menu owns the transaction; the counter only unlocks it.</summary>
        public override void Interact(PlayerInteractor interactor)
        {
            UIManager.Shop.Open(this, interactor);
        }
    }
}
