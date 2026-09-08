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

    [Tooltip("The dish this makes, so the menu reads at a glance the way the shop and the crop " +
             "picker do.")]
    [SerializeField] Image iconImage;

    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] TextMeshProUGUI ingredientsLabel;
    [SerializeField] TextMeshProUGUI timeLabel;

    [Tooltip("Stopwatch beside the cook time, the way the sell rows put a coin beside the price.")]
    [SerializeField] Image timeIcon;
    [SerializeField] TextMeshProUGUI ownedLabel;

    // The row sits on a near-white button sprite, so the secondary lines are muted DARK
    // rather than light - a pale slate here vanishes against the background.
    [Header("Colours")]
    [Tooltip("Ingredient line while the crate can pay for the recipe.")]
    public Color affordableColor = new Color(0.36f, 0.39f, 0.47f);
    [Tooltip("Ingredient line while something is missing. Light rather than deep red: this line " +
             "only ever appears on a disabled row, and a dark red on that mid-slate sprite reads " +
             "at 1.3:1 - the warning the player most needs was the least legible thing there.")]
    public Color shortColor = new Color(1f, 0.702f, 0.639f);

    RecipeDef _recipe;

    // The authored colours, captured before anything dims them. Without this a second refresh
    // would grey the already-greyed colour and the row would fade a little further every time.
    Color _nameInk, _timeInk, _ownedInk, _timeIconInk;
    bool _inkCaptured;

    public void Bind(RecipeDef recipe, Action<RecipeDef> onCook)
    {
        _recipe = recipe;
        if (recipe == null) return;

        if (nameLabel != null) nameLabel.text = recipe.DisplayName;
        if (ingredientsLabel != null) ingredientsLabel.text = recipe.IngredientSummary;
        if (timeLabel != null) timeLabel.text = Mathf.RoundToInt(recipe.cookSeconds) + "s";

        if (iconImage != null)
        {
            var made = recipe.output;
            iconImage.sprite = made != null ? made.icon : null;
            // A missing icon would otherwise draw as a white box, the way the other rows guard it.
            iconImage.enabled = iconImage.sprite != null;
        }

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

        bool usable = unlocked && affordable;
        CaptureInk();

        if (ownedLabel != null)
        {
            ownedLabel.text = _recipe.output != null ? "x" + Inventory.ProduceCount(_recipe.output) : string.Empty;
            ownedLabel.color = RowInk.For(_ownedInk, usable);
        }

        if (nameLabel != null) nameLabel.color = RowInk.For(_nameInk, usable);
        if (timeLabel != null) timeLabel.color = RowInk.For(_timeInk, usable);

        // The stopwatch dims with the number it belongs to, or the row reads as half-alive.
        if (timeIcon != null) timeIcon.color = RowInk.For(_timeIconInk, usable);

        if (ingredientsLabel != null)
        {
            ingredientsLabel.text = unlocked ? _recipe.IngredientSummary : "Needs farmhouse Lv" + _recipe.requiredLevel;

            // The one line that does not simply grey with the rest. A locked recipe is nothing
            // the player can act on, so it dims; one they merely cannot pay for keeps its red,
            // because the missing ingredients are exactly the thing to go and do something about.
            ingredientsLabel.color = unlocked
                ? (affordable ? affordableColor : shortColor)
                : RowInk.For(affordableColor, false);
        }

        if (button != null) button.interactable = usable;
    }

    /// <summary>Remembers the prefab's own colours the first time, so dimming is reversible.</summary>
    void CaptureInk()
    {
        if (_inkCaptured) return;
        _inkCaptured = true;

        if (nameLabel != null) _nameInk = nameLabel.color;
        if (timeLabel != null) _timeInk = timeLabel.color;
        if (ownedLabel != null) _ownedInk = ownedLabel.color;
        if (timeIcon != null) _timeIconInk = timeIcon.color;
    }
}
