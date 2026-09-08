using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Points at the truck waiting at the gate while it is off the edge of the screen.
///
/// A truck is only there for half a minute and the camera rarely happens to be looking at the
/// road, so the whole feature can pass a player by: the order badge floats over the truck, which
/// is exactly where you cannot see it. This puts the same information - what it wants and how
/// long is left - on the border of the screen, with an arrow saying which way to run.
///
/// It hides itself the moment the truck comes into view, because at that point the badge over
/// the truck is saying the same thing in the right place, and two of them is clutter.
///
/// Like InteractPrompt, this object IS the thing that gets shown and hidden, and it is driven by
/// TruckQueue rather than by its own Update. That is what lets it sit disabled in the scene -
/// which is how it should be saved, so it does not clutter the editor view - without going dead
/// at runtime. Anything transient added later should follow the same shape: a public Show that
/// is safe to call while switched off, and an owner that calls it.
/// </summary>
public class TruckMarker : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("The thing that moves to the border. Anchored to the middle of the canvas.")]
    [SerializeField] RectTransform marker;

    [Tooltip("Rotated to aim the arrow. The icon hangs off the marker instead, so it stays upright.")]
    [SerializeField] RectTransform arrowPivot;

    [SerializeField] Image icon;
    [SerializeField] CanvasGroup group;

    [Tooltip("Seconds left before the driver gives up. Blank while the truck is still rolling in.")]
    [SerializeField] TextMeshProUGUI countdown;

    [Header("Countdown")]
    [Tooltip("Colour of the clock while there is still time to spare.")]
    [SerializeField] Color calmColor = new Color(0.93f, 0.95f, 1f);

    [Tooltip("Colour once the order is about to be lost.")]
    [SerializeField] Color urgentColor = new Color(0.95f, 0.36f, 0.30f);

    [Tooltip("How many seconds left counts as running out. The truck waits 30 by default, so a " +
             "third of that is about as long as a sprint across the farm takes.")]
    [SerializeField] float urgentBelow = 10f;

    [Header("Placement")]
    [Tooltip("How far the marker keeps from the edge of the screen, in canvas units.")]
    [SerializeField] float edgePadding = 96f;

    [Tooltip("Aimed above the truck's pivot, so the arrow points at the truck and not at the road " +
             "under it.")]
    [SerializeField] float aimHeight = 1.4f;

    RectTransform _canvas;
    Camera _cam;
    ItemDef _shown;
    int _seconds = int.MinValue;
    bool _ready;

    /// <summary>
    /// Point the marker at the truck at the gate, or pass null to put it away. Safe to call every
    /// frame, and safe to call while this object is switched off - which is how it is saved.
    ///
    /// Everything is positioned before the object is switched on, so it never appears for a frame
    /// at wherever the last truck happened to be.
    /// </summary>
    public void Show(TruckOrder front)
    {
        bool wanted = Place(front);
        if (gameObject.activeSelf != wanted) gameObject.SetActive(wanted);
    }

    // Lazy rather than OnEnable: this object starts disabled, so OnEnable has not run by the time
    // the first Show call arrives.
    void EnsureInit()
    {
        if (_ready) return;

        // Searched with inactive included - the search starts at this object, which is switched off.
        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null) return;

        _canvas = canvas.rootCanvas.transform as RectTransform;
        if (group != null) group.blocksRaycasts = false;   // never in the way of a click
        _ready = _canvas != null;
    }

    /// <summary>Works out where the marker belongs. False when there is nothing to point at.</summary>
    bool Place(TruckOrder front)
    {
        if (front == null || front.Wanted == null || Menus.AnyOpen) return false;

        EnsureInit();
        if (_canvas == null) return false;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;

        var viewport = _cam.WorldToViewportPoint(front.transform.position + Vector3.up * aimHeight);

        // Behind the camera the projection comes back mirrored, which would send the arrow the
        // wrong way round. Flip it back before anything is measured off it.
        bool behind = viewport.z < 0f;
        if (behind) { viewport.x = 1f - viewport.x; viewport.y = 1f - viewport.y; }

        var size = _canvas.rect.size;
        var pos = new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);
        var half = size * 0.5f - Vector2.one * edgePadding;

        // The truck is on screen; its own badge has this covered.
        if (!behind && Mathf.Abs(pos.x) <= half.x && Mathf.Abs(pos.y) <= half.y) return false;

        var dir = pos.sqrMagnitude > 0.0001f ? pos.normalized : Vector2.down;

        // Walk out along the direction of the truck until the first edge of the safe rectangle
        // is hit. Clamping the axes independently would slide the marker along the border away
        // from the line the player should actually run down.
        float reach = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(dir.x), 1e-4f),
                                half.y / Mathf.Max(Mathf.Abs(dir.y), 1e-4f));

        if (marker != null) marker.anchoredPosition = dir * reach;
        if (arrowPivot != null)
            arrowPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Bind(front.Wanted);
        Tick(front);
        return true;
    }

    /// <summary>Only touches the sprites when the order actually changes.</summary>
    void Bind(ItemDef item)
    {
        if (_shown == item) return;
        _shown = item;

        if (icon != null)
        {
            icon.sprite = item.icon;
            icon.enabled = item.icon != null;   // a missing icon would draw as a white box
        }
    }

    /// <summary>
    /// The clock. Only rewritten when the whole second changes - this runs every frame a truck is
    /// out there, and building the same string sixty times a second to say the same thing is pure
    /// garbage.
    /// </summary>
    void Tick(TruckOrder front)
    {
        if (countdown == null) return;

        // Still rolling in: its patience has not started, so there is no honest number to show.
        if (!front.Waiting)
        {
            if (_seconds == -1) return;
            _seconds = -1;
            countdown.text = string.Empty;
            return;
        }

        int left = Mathf.Max(0, Mathf.CeilToInt(front.SecondsLeft));
        if (left == _seconds) return;

        _seconds = left;
        countdown.text = left + "s";
        countdown.color = left <= urgentBelow ? urgentColor : calmColor;
    }
}
