using UnityEngine;
using Farm.Audio;
using Farm.Farming;
using Farm.Interaction;
using Farm.Progression;

namespace Farm.Selling
{
    /// <summary>
    /// The counter the player sells across. Only the truck at the head of the queue can be
    /// served - the fence keeps the player off the road, so this is the single point of contact.
    /// </summary>
    public class SellCounter : Interactable
    {
        [SerializeField] TruckQueue queue;

        [Tooltip("Beat the player performs when handing produce over. Optional.")]
        public CharacterAction sellAction;

        [Tooltip("Cash register, played once the sale actually goes through. Optional.")]
        [SerializeField] SoundEvent sellSound;

        /// <summary>"Sell 1 Bread" when the goods are in hand, "Need 1 Bread" when they are not.</summary>
        public override string Prompt => _prompt;

        // Rebuilt only when the order or the player's stock changes, so the prompt never
        // allocates per frame and the badge above it refreshes at the right moments.
        /// <summary>Folds the order and whether the player can fill it, so the prompt tracks both.</summary>
        public override int StateKey => _stateKey;

        string _prompt = "";
        int _pendingReward;
        System.Action _onSold;
        int _stateKey;
        int _lastVersion = -1;
        bool _lastAffordable;

        void Update()
        {
            var front = queue != null ? queue.Front : null;
            bool affordable = front != null && front.Wanted != null
                              && Inventory.ProduceCount(front.Wanted) >= front.Amount;

            int version = queue != null ? queue.OrderVersion : 0;
            if (version == _lastVersion && affordable == _lastAffordable) return;

            _lastVersion = version;
            _lastAffordable = affordable;
            _stateKey++;

            if (front == null || front.Wanted == null) { _prompt = ""; return; }

            // The coins are on both branches on purpose. A truck pays several times the shop price
            // and is only here for a moment, so "Need 3 Corn" is half the question - what the player
            // is deciding is whether it is worth running to grow them, and that needs the number.
            _prompt = (affordable ? "Sell " : "Need ") + front.Amount + " " +
                      front.Wanted.displayName + "  " + front.Reward + "c";
        }

        // Always focusable so the player can read what the truck wants; the sale itself is what
        // gets refused when they are short.
        /// <summary>Focusable whenever a truck is present, so its order can always be read.</summary>
        public override bool CanInteract => queue != null && queue.Front != null;

        /// <summary>Hands the goods over, or plays the denied sound when the crate is short.</summary>
        public override void Interact(PlayerInteractor interactor)
        {
            var front = queue.Front;
            if (front == null || front.Wanted == null) return;
            if (!front.Arrived) return;                       // still rolling up

            // The prompt already reads "Need 1 Carrot Soup"; this is the audible half of that
            // answer, so walking up and pressing the button is never met with nothing at all.
            // Same refusal the farmhouse gives when the coins are short.
            if (!Inventory.TrySpendProduce(front.Wanted, front.Amount))
            {
                AudioManager.PlayDenied();
                return;
            }

            // cached delegate rather than a lambda: this fires on every sale for the whole
            // session, and the reward rides in a field instead of a captured closure
            _pendingReward = front.Reward;
            _onSold ??= CompleteSale;
            interactor.Controller.BeginAction(sellAction, transform, _onSold);
        }

        void CompleteSale()
        {
            Inventory.AddCoins(_pendingReward);
            RunStats.RecordTruckOrder();

            // Here rather than in Interact, so the register rings when the coins land at the end
            // of the hand-over beat - not when the button was pressed a second earlier. Positional,
            // because the counter is a place the player walks to and the sound should belong to it.
            AudioManager.PlayAt(sellSound, transform.position);

            queue.Advance();
        }
    }
}
