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
public class WinPanel : MonoBehaviour
{
    /// <summary>True while the end screen is up, so gameplay input can ignore Interact.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Prefab wiring")]
    [SerializeField] GameObject panelRoot;
    [SerializeField] TextMeshProUGUI headline;
    [SerializeField] TextMeshProUGUI stats;
    [SerializeField] Button closeButton;

    [Header("Camera")]
    [Tooltip("How far the camera pulls back to show off the finished farm.")]
    public float cameraZoom = -6f;
    public float cameraPitch = 4f;
    public float cameraBlend = 0.8f;

    CameraFollow _cameraRig;

    public void Show(float seconds, int coinsEarned, int dishesBaked, int truckOrders)
    {
        gameObject.SetActive(true);              // this object is the toggle
        if (panelRoot != null) panelRoot.SetActive(true);

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

        // pull back rather than push in - the other menus lean toward one thing, this one
        // wants the player to see the whole island they built
        if (_cameraRig == null) _cameraRig = GameManager.CameraRig;
        _cameraRig.SetActionFraming(cameraZoom, cameraPitch, cameraBlend);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Close);
        }

        IsOpen = true;
        Menus.RebuildLayout(stats != null ? stats.rectTransform : null,
                            panelRoot != null ? panelRoot.transform as RectTransform : null);
    }

    public void Close()
    {
        // The other panels announce their own close; this one is the last thing the player
        // touches in a run, so it gets the same courtesy rather than shutting in silence.
        AudioManager.PlayClose();

        IsOpen = false;
        if (_cameraRig != null) _cameraRig.ClearActionFraming();
        gameObject.SetActive(false);
    }

    void OnDestroy() => IsOpen = false;

    static string Clock(float seconds)
    {
        int total = Mathf.Max(Mathf.RoundToInt(seconds), 0);
        return (total / 60) + ":" + (total % 60).ToString("00");
    }
}
