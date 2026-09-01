using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One line in the cooking menu: the dish, what it costs in produce, how long it takes, and
/// how many the player already has. Authored as a prefab (Prefabs/UI/RecipeRow) - this script
/// only fills in the data.
/// </summary>
public class RecipeRow : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI ingredientsLabel;
    [SerializeField] TextMeshProUGUI timeLabel;
    [SerializeField] TextMeshProUGUI ownedLabel;

    // The row sits on a near-white button sprite, so the secondary lines are muted DARK
    // rather than light - a pale slate here vanishes against the background.
    [Header("Colours")]
    [Tooltip("Ingredient line while the crate can pay for the recipe.")]
    public Color affordableColor = new Color(0.36f, 0.39f, 0.47f);
    [Tooltip("Ingredient line while something is missing.")]
    public Color shortColor = new Color(0.69f, 0.23f, 0.18f);

    RecipeDef _recipe;

    public void Bind(RecipeDef recipe, Action<RecipeDef> onCook)
    {
        _recipe = recipe;
        if (recipe == null) return;

        if (nameLabel != null) nameLabel.text = recipe.DisplayName;
        if (ingredientsLabel != null) ingredientsLabel.text = recipe.IngredientSummary;
        if (timeLabel != null) timeLabel.text = Mathf.RoundToInt(recipe.cookSeconds) + "s";

        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onCook?.Invoke(recipe));
    }

    /// <summary>
    /// Two separate reasons a row can be dead, and they should not look the same: a locked
    /// recipe says which tier it needs, a merely unaffordable one reddens the missing
    /// ingredients so the player knows what to go and get.
    /// </summary>
    public void SetAvailable(bool unlocked, bool affordable)
    {
        if (_recipe == null) return;

        if (ownedLabel != null)
            ownedLabel.text = _recipe.output != null ? "x" + Inventory.ProduceCount(_recipe.output) : string.Empty;

        if (ingredientsLabel != null)
        {
            ingredientsLabel.text = unlocked ? _recipe.IngredientSummary : "Needs farmhouse Lv" + _recipe.requiredLevel;
            ingredientsLabel.color = unlocked && !affordable ? shortColor : affordableColor;
        }

        if (button != null) button.interactable = unlocked && affordable;
    }
}
