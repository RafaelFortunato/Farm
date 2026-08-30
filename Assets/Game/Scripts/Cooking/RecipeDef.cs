using System.Text;
using UnityEngine;

/// <summary>
/// One dish the stove can make: what it eats, what it gives back, and how long it takes.
///
/// Inputs are pairs rather than two parallel arrays, so an item can never end up holding
/// someone else's count when the list is edited in the inspector.
/// </summary>
[CreateAssetMenu(fileName = "Recipe_", menuName = "Farm/Recipe")]
public class RecipeDef : ScriptableObject
{
    [System.Serializable]
    public struct Ingredient
    {
        public ItemDef item;
        public int count;
    }

    [Header("Recipe")]
    [Tooltip("What one batch costs out of the produce crate.")]
    public Ingredient[] inputs;
    public ItemDef output;
    public int outputCount = 1;

    [Header("Timing")]
    public float cookSeconds = 25f;

    [Tooltip("Stove tier this needs. The stove's tier rises with the farmhouse.")]
    public int requiredLevel = 1;

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
                if (i.item == null || Inventory.ProduceCount(i.item) < i.count) return false;
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
