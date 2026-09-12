using System.Text;
using UnityEngine;
using Farm.Farming;

namespace Farm.Cooking
{
    /// <summary>
    /// One dish the stove can make: what it eats, what it gives back, and how long it takes.
    ///
    /// Inputs are ItemStacks - the same pair a farmhouse upgrade is priced in - so both systems
    /// describe "some number of this item" the same way.
    /// </summary>
    [CreateAssetMenu(fileName = "Recipe_", menuName = "Farm/Recipe")]
    public class RecipeDef : ScriptableObject
    {
        [Header("Recipe")]
        [Tooltip("What one batch costs out of the produce crate.")]
        public ItemStack[] inputs;
        public ItemDef output;
        public int outputCount = 1;

        [Header("Timing")]
        [Tooltip("Seconds on the heat. The stove is the throughput bottleneck of the whole economy, " +
                 "so this is the number that decides how much a dish is really worth.")]
        public float cookSeconds = 25f;

        [Tooltip("Farmhouse level this recipe needs. The stove itself arrives at level 2, so 2 is " +
                 "the lowest value that can ever be cooked.")]
        public int requiredLevel = 2;

        /// <summary>What to call this dish in the UI - the output item's name, or the asset's own.</summary>
        public string DisplayName => output != null ? output.displayName : name;

        /// <summary>"2 Corn  2 Egg  1 Milk". Built once - a menu row reads it on every refresh.</summary>
        public string IngredientSummary => _summary ??= BuildSummary();
        string _summary;

        void OnEnable() => _summary = null;
        void OnValidate() => _summary = null;

        /// <summary>True when the crate holds every input.</summary>
        public bool HasIngredients
        {
            get
            {
                if (inputs == null || inputs.Length == 0) return false;
                foreach (var i in inputs)
                    if (!i.InStock) return false;
                return true;
            }
        }

        /// <summary>
        /// Takes the whole recipe out of the crate, or nothing at all. The check runs over every
        /// input before a single one is spent, so a recipe that is short on its last ingredient
        /// cannot swallow the earlier ones.
        /// </summary>
        public bool TryConsume()
        {
            if (!HasIngredients) return false;
            foreach (var i in inputs) Inventory.TrySpendProduce(i.item, i.count);
            return true;
        }

        string BuildSummary()
        {
            if (inputs == null || inputs.Length == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach (var i in inputs)
            {
                if (i.item == null) continue;
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(i.count).Append(' ').Append(i.item.displayName);
            }
            return sb.ToString();
        }
    }
}
