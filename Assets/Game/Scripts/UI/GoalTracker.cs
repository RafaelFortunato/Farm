using System.Text;
using TMPro;
using UnityEngine;
using Farm.Farming;
using Farm.Progression;

namespace Farm.UI
{
    /// <summary>
    /// The one line on the HUD that says what winning means.
    ///
    /// A player who has been dropped into a farm with no menu screen has no way to know the game
    /// ends at farmhouse Lv5, or what the next upgrade will cost. This is cheaper than a tutorial
    /// and answers both at a glance.
    ///
    /// Costs are shown as have/need rather than a flat price, so the line doubles as progress: the
    /// player can see the coins climbing toward the next rung without opening anything.
    /// </summary>
    public class GoalTracker : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The farmhouse this tracks. Wired straight across from the scene.")]
        [SerializeField] MainBuilding farmhouse;

        [SerializeField] TextMeshProUGUI label;

        static readonly StringBuilder Line = new StringBuilder(96);

        string _shown;

        // Both events are static, and a domain reload during play wipes them while re-running
        // OnEnable but not Awake - so subscribing here is what keeps this alive across one.
        void OnEnable()
        {
            Inventory.Changed -= Refresh;
            Inventory.Changed += Refresh;
            MainBuilding.Changed -= OnLevelChanged;
            MainBuilding.Changed += OnLevelChanged;
            Refresh();
        }

        void OnDisable()
        {
            Inventory.Changed -= Refresh;
            MainBuilding.Changed -= OnLevelChanged;
        }

        void OnLevelChanged(int level) => Refresh();

        void Refresh()
        {
            if (label == null || farmhouse == null) return;

            string text = Build();
            if (text == _shown) return;      // most inventory changes do not move this line

            _shown = text;
            label.text = text;
        }

        string Build()
        {
            if (farmhouse.IsMaxLevel) return "Farm complete";

            Line.Length = 0;
            Line.Append("Farmhouse Lv").Append(farmhouse.Level)
                .Append(" of ").Append(farmhouse.MaxLevel);

            var next = farmhouse.Next;
            if (next != null)
            {
                Line.Append("\nNext: ").Append(Mathf.Min(Inventory.Coins, next.coinCost))
                    .Append('/').Append(next.coinCost).Append(" coins");

                if (next.alsoNeeds != null)
                    foreach (var need in next.alsoNeeds)
                    {
                        if (need.item == null) continue;

                        Line.Append(", ").Append(Mathf.Min(Inventory.ProduceCount(need.item), need.count))
                            .Append('/').Append(need.count).Append(' ').Append(need.item.displayName);
                    }
            }

            return Line.ToString();
        }
    }
}
