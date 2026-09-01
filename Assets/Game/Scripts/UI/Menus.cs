using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One place to ask whether a menu currently owns input.
///
/// Gameplay checks this instead of naming individual panels, so adding a third menu
/// means editing this file rather than hunting for every "is the menu open" test.
/// </summary>
public static class Menus
{
    public static bool AnyOpen => SeedMenu.IsOpen || StoreMenu.IsOpen || RecipeMenu.IsOpen || WinPanel.IsOpen;

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
}
