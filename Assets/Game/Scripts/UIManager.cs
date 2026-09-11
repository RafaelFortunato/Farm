using UnityEngine;
using UnityEngine.UI;

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

    /// <summary>
    /// The panel currently up, or null. One at a time: opening a second dismisses the first,
    /// so a single reference is the whole truth about what the UI is showing.
    ///
    /// Held here rather than as a flag on each panel because this is where the panels already
    /// live, and because "what is open" is a question about the UI as a whole.
    /// </summary>
    public static BaseMenu OpenMenu { get; private set; }

    /// <summary>
    /// Does a panel currently own input? Gameplay asks this instead of naming panels, so
    /// adding a fifth needs no edit here - it only has to derive from BaseMenu.
    /// </summary>
    public static bool AnyMenuOpen => OpenMenu != null;

    /// <summary>Claim the slot. Called by BaseMenu when a panel shows itself.</summary>
    public static void SetOpenMenu(BaseMenu menu) => OpenMenu = menu;

    /// <summary>
    /// Release the slot, but only if this panel still holds it - a panel torn down after
    /// another has already opened must not clear the newer one.
    /// </summary>
    public static void ClearOpenMenu(BaseMenu menu)
    {
        if (OpenMenu == menu) OpenMenu = null;
    }

    /// <summary>
    /// Dismiss whatever is up, and report whether anything was. This counts as the player
    /// dismissing it, so it sounds like a close - Escape is exactly "I changed my mind".
    /// </summary>
    public static bool CloseOpenMenu()
    {
        var menu = OpenMenu;
        if (menu == null) return false;

        menu.Close();
        return true;
    }

    /// <summary>
    /// Settle a panel's layout immediately, innermost first.
    ///
    /// Layout groups and ContentSizeFitters normally resolve at the end of the frame. A panel
    /// that is activated AND populated in the same frame has not had that pass yet, so its
    /// first appearance shows rows sitting at default positions on top of each other. Nested
    /// fitters compound it: the outer one cannot measure until the inner one has solved, so
    /// the rebuild has to run inside-out.
    /// </summary>
    public static void RebuildLayout(RectTransform inner, RectTransform outer)
    {
        Canvas.ForceUpdateCanvases();
        if (inner != null) LayoutRebuilder.ForceRebuildLayoutImmediate(inner);
        if (outer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(outer);
    }

    // Escape lives here rather than on each panel: the panels do not know about each other,
    // and the panel reference this closes already lives here.
    //
    // The UI map's Cancel action rather than a raw key check, so it comes through the Input
    // System like everything else and picks up gamepad B for free - the binding is */{Cancel},
    // a usage, not a hardcoded Escape.
    InputSystem_Actions _input;

    // OVERRIDE, not a new method. Singleton declares these virtual and binds Instance in them;
    // a plain OnEnable here would hide the base one, Unity would call only this, and Instance
    // would stay null - which is exactly what happened.
    protected override void OnEnable()
    {
        base.OnEnable();

        _input ??= new InputSystem_Actions();
        _input.UI.Enable();
    }

    void OnDisable() => _input?.UI.Disable();

    protected override void OnDestroy()
    {
        _input?.Dispose();
        base.OnDestroy();
    }

    void Update()
    {
        // Polled next to the rest of the UI wiring, the way PlayerInteractor polls Interact:
        // no subscribe/unsubscribe lifecycle to get wrong across a domain reload.
        if (_input.UI.Cancel.WasPressedThisFrame()) CloseOpenMenu();
    }

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
