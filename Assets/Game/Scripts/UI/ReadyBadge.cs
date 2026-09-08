using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small icon on a world-space canvas, floating over the thing it belongs to.
///
/// The stove finishes a dish and then says nothing: the cook ring hides itself the moment it
/// reaches full, so a stove with food waiting looks exactly like an idle one until the player
/// walks into interact range and the prompt appears. This is the bit in between - the dish's own
/// icon, sitting above the stove, readable from anywhere it is on screen.
///
/// Deliberately not the border marker the trucks use. A truck parks out of sight for thirty
/// seconds and then leaves, so it has to be findable from off-camera; a finished dish is in no
/// hurry and sits where the player already knows the stove is. Because the camera follows the
/// player, a badge in world space is visible exactly when the stove is nearby and simply out of
/// frame when it is not, which is the whole behaviour wanted here and needs no code.
///
/// Lives as a child of its owner, so it switches off with the level group it belongs to and there
/// is nothing to clean up - unlike a shared screen-space widget, which has to be told to let go.
/// </summary>
public class ReadyBadge : MonoBehaviour
{
    [Tooltip("The canvas, switched off whenever there is nothing to announce. Toggled rather " +
             "than faded so an idle badge costs nothing to draw.")]
    [SerializeField] GameObject canvasRoot;

    [Tooltip("Shows what is waiting - the dish's own icon, the same one the cooking menu uses.")]
    [SerializeField] Image icon;

    ItemDef _shown;
    Transform _tf, _cam;

    void Awake()
    {
        _tf = transform;

        // The canvas is saved switched off, like every other transient thing here, so it does not
        // clutter the editor view. This is only a safety net for one saved the other way round.
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    void OnEnable() => _cam = GameManager.CameraTransform;

    /// <summary>
    /// Turn to face the camera, exactly as PlotTimer does for the ring that shares this spot.
    /// Without it the badge sits flat against world +Z and the camera, which looks down at the
    /// farm from fifty degrees, sees it almost edge-on as a sliver.
    ///
    /// Only while something is actually being announced - an idle badge costs nothing.
    /// </summary>
    void LateUpdate()
    {
        if (_cam == null || canvasRoot == null || !canvasRoot.activeSelf) return;
        _tf.rotation = _cam.rotation;
    }

    /// <summary>
    /// Put something on the badge, or pass null to clear it. Safe to call every frame - the
    /// sprite is only touched when the thing being announced actually changes.
    /// </summary>
    public void Show(ItemDef item)
    {
        if (_shown != item)
        {
            _shown = item;
            if (icon != null && item != null)
            {
                icon.sprite = item.icon;
                icon.enabled = item.icon != null;   // a missing icon would draw as a white box
            }
        }

        bool wanted = item != null;
        if (canvasRoot != null && canvasRoot.activeSelf != wanted) canvasRoot.SetActive(wanted);
    }
}
