using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One button's four sprite states, matching the Game/Sprites/Buttons sets.
/// Grouped so a skin can be assigned as a unit instead of four loose fields.
/// </summary>
[System.Serializable]
public class ButtonSkin
{
    public Sprite normal;
    public Sprite hover;
    public Sprite pressed;
    public Sprite disabled;

    public bool IsValid => normal != null;

    /// <summary>Applies this skin to a Button using sprite swapping.</summary>
    public void ApplyTo(Button button, Image image)
    {
        if (image == null || normal == null) return;

        image.sprite = normal;
        image.type = Image.Type.Sliced;

        if (button == null) return;
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;

        var state = button.spriteState;
        state.highlightedSprite = hover != null ? hover : normal;
        state.pressedSprite = pressed != null ? pressed : normal;
        state.selectedSprite = normal;
        state.disabledSprite = disabled != null ? disabled : normal;
        button.spriteState = state;
    }
}
