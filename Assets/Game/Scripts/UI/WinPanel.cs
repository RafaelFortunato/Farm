using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The end screen, shown once the farmhouse reaches its top level.
///
/// Same shape as the other panels: authored as a prefab under Prefabs/UI, saved deactivated,
/// found through UIManager. It counts as an open menu, so the player is not still walking
/// around underneath it.
///
/// It does not stop the game. The farm keeps ticking behind it and the player can dismiss it
/// and carry on - this is a twenty-minute portfolio piece, and taking the toy away at the
/// moment of success would be a strange reward.
/// </summary>
public class WinPanel : BaseMenu
{
    [Header("Prefab wiring")]
    [SerializeField] TextMeshProUGUI headline;
    [SerializeField] TextMeshProUGUI stats;

    public void Show(float seconds, int coinsEarned, int dishesBaked, int truckOrders)
    {
        // pulls back rather than pushing in - the other menus lean toward one thing, this one
        // wants the player to see the whole island they built. That is the cameraZoom on this
        // prefab being negative; the behaviour itself is the same as every other panel.
        Present();

        if (headline != null) headline.text = "FARM COMPLETE";

        // <pos> puts the values on a shared column. A proportional font will not line them up
        // on spaces alone, and a ragged stack of numbers reads as an accident, not a result.
        // Earned rather than held: the balance punishes the player who spent everything on the
        // last upgrade, which is exactly what they had to do to get here.
        if (stats != null)
            stats.text = "Time<pos=58%>" + Clock(seconds)
                       + "\nCoins earned<pos=58%>" + coinsEarned
                       + "\nRecipes baked<pos=58%>" + dishesBaked
                       + "\nTruck orders<pos=58%>" + truckOrders;

        UIManager.RebuildLayout(stats != null ? stats.rectTransform : null,
                            panelRoot != null ? panelRoot.transform as RectTransform : null);
    }

    static string Clock(float seconds)
    {
        int total = Mathf.Max(Mathf.RoundToInt(seconds), 0);
        return (total / 60) + ":" + (total % 60).ToString("00");
    }
}
