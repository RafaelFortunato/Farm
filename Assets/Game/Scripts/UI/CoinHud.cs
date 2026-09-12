using TMPro;
using UnityEngine;
using Farm.Farming;

namespace Farm.UI
{
    /// <summary>
    /// Top-right coin readout.
    ///
    /// Authored as a prefab under Prefabs/UI and dropped beneath MainCanvas - this script
    /// only fills in the number, all styling lives in the prefab.
    ///
    /// Driven by Inventory.Changed rather than polled in Update, so it costs nothing on the
    /// frames where nothing was bought or sold.
    /// </summary>
    public class CoinHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI label;

        int _shown = int.MinValue;   // impossible value, so the first refresh always writes

        // Subscribed in OnEnable rather than Awake: a domain reload during play re-runs
        // OnEnable but NOT Awake, and it wipes the static event we are listening to.
        void OnEnable()
        {
            Inventory.Changed -= Refresh;   // never double-subscribe
            Inventory.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => Inventory.Changed -= Refresh;

        void Refresh()
        {
            // Changed also fires for item pickups, so only touch the label when coins moved
            int coins = Inventory.Coins;
            if (coins == _shown) return;

            _shown = coins;
            label.text = coins.ToString();
        }
    }
}
