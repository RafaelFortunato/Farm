using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lets a list panel grow with its contents, up to a point, and scroll after that.
///
/// The menus are built out of vertical layout groups with content size fitters, so a panel is
/// exactly as tall as the rows inside it. That reads beautifully at three rows and is a
/// soft-lock at ten: the panel is centre-pivoted, so it grows off both ends of the screen and
/// takes the close button with it, and nothing in the project closes a menu with Escape.
///
/// Rather than cap the roster - which would mean deciding the player may not hold more than
/// eight kinds of thing - this caps the *viewport*. Below the cap the panel still hugs its
/// contents exactly as before; above it the height stops and the list scrolls.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(ScrollRect))]
public class ClampedScrollView : MonoBehaviour
{
    [Tooltip("The rows themselves - the thing whose height this is tracking.")]
    [SerializeField] RectTransform content;

    [Tooltip("How tall the list may get before it starts scrolling instead of growing. Keep the " +
             "whole panel under the 1080-unit canvas: the rest of the panel costs about 280.")]
    [SerializeField] float maxHeight = 620f;

    RectTransform _rect;
    float _applied = -1f;

    void OnEnable()
    {
        _rect = (RectTransform)transform;
        _applied = -1f;      // re-measure on every open, the roster may have changed
        Apply();
    }

    // The row set changes from inside a click handler and the fitters settle a frame later, so
    // this tracks rather than being told. It is a float compare on one rect while a menu is open.
    void LateUpdate() => Apply();

    void Apply()
    {
        if (content == null) return;

        float wanted = Mathf.Min(content.rect.height, maxHeight);
        if (Mathf.Abs(wanted - _applied) < 0.5f) return;

        _applied = wanted;
        _rect.sizeDelta = new Vector2(_rect.sizeDelta.x, wanted);

        // The panel above sizes itself from this rect, so it has to be told to look again.
        var parent = _rect.parent as RectTransform;
        if (parent != null) LayoutRebuilder.MarkLayoutForRebuild(parent);
    }
}
