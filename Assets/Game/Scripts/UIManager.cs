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

    [Tooltip("Border arrow pointing at a waiting truck. Saved disabled like everything else here, " +
             "and driven by TruckQueue.")]
    [SerializeField] OffscreenMarker truckMarker;


    [Header("Panels (saved disabled, so these must be wired)")]
    [SerializeField] SeedMenu seedMenu;
    [SerializeField] StoreMenu storeMenu;
    [SerializeField] RecipeMenu recipeMenu;
    [SerializeField] WinPanel winPanel;

    /// <summary>The floating "E - Plant" badge over whatever the player is standing at.</summary>
    public static InteractPrompt Prompt => Instance.interactPrompt;

    /// <summary>The arrow that pins a waiting truck's order to the edge of the screen.</summary>
    public static OffscreenMarker TruckPointer => Instance.truckMarker;

    public static SeedMenu Seeds => Instance.seedMenu;
    public static StoreMenu Shop => Instance.storeMenu;
    public static RecipeMenu Kitchen => Instance.recipeMenu;

    /// <summary>The end screen. Raised by the farmhouse when it reaches its top level.</summary>
    public static WinPanel Win => Instance.winPanel;

    void OnValidate()
    {
        var missing = string.Empty;
        if (interactPrompt == null) missing += " interactPrompt";
        if (truckMarker == null) missing += " truckMarker";
        if (seedMenu == null) missing += " seedMenu";
        if (storeMenu == null) missing += " storeMenu";
        if (recipeMenu == null) missing += " recipeMenu";
        if (winPanel == null) missing += " winPanel";

        if (missing.Length > 0) Debug.LogWarning("UIManager is missing references:" + missing, this);
    }
}
