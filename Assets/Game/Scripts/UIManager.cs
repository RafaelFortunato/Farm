using UnityEngine;

/// <summary>
/// Where the game's one-of-a-kind UI lives.
///
/// Split from GameManager so the two do not become one drawer for everything: this one only
/// ever holds screens and world-space widgets, and gameplay code that wants a panel asks here.
///
/// Wiring matters more here than anywhere else. Every panel is saved DEACTIVATED so it does not
/// clutter the editor view, and a disabled object never runs Awake or OnEnable - so nothing it
/// could do would let it announce itself. A reference wired in the inspector points at it
/// regardless.
///
/// Read these from OnEnable or later, never from Awake - see Singleton for why.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class UIManager : Singleton<UIManager>
{
    [Header("World-space")]
    [SerializeField] InteractPrompt interactPrompt;

    [Header("Panels (saved disabled, so these must be wired)")]
    [SerializeField] SeedMenu seedMenu;
    [SerializeField] StoreMenu storeMenu;
    [SerializeField] RecipeMenu recipeMenu;

    /// <summary>The floating "E - Plant" badge over whatever the player is standing at.</summary>
    public static InteractPrompt Prompt => Instance.interactPrompt;

    public static SeedMenu Seeds => Instance.seedMenu;
    public static StoreMenu Shop => Instance.storeMenu;
    public static RecipeMenu Kitchen => Instance.recipeMenu;

    void OnValidate()
    {
        var missing = string.Empty;
        if (interactPrompt == null) missing += " interactPrompt";
        if (seedMenu == null) missing += " seedMenu";
        if (storeMenu == null) missing += " storeMenu";
        if (recipeMenu == null) missing += " recipeMenu";

        if (missing.Length > 0) Debug.LogWarning("UIManager is missing references:" + missing, this);
    }
}
