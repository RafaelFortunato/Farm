using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Points at the truck waiting at the gate while it is off the edge of the screen.
///
/// A truck is only there for half a minute and the camera rarely happens to be looking at the
/// road, so the whole feature can pass a player by: the order badge floats over the truck, which
/// is exactly where you cannot see it. This puts the same information - what it wants - on the
/// border of the screen, with an arrow saying which way to run.
///
/// It hides itself the moment the truck comes into view, because at that point the badge over
/// the truck is saying the same thing in the right place, and two of them is clutter.
/// </summary>
public class TruckMarker : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] TruckQueue queue;

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
    bool _captured;
    bool _visible = true;      // impossible starting state, so the first frame always applies
    int _seconds = int.MinValue;

    void OnEnable()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) _canvas = canvas.rootCanvas.transform as RectTransform;
        Show(false);
    }

    // LateUpdate so the camera has already moved this frame - marking off a moving target from
    // its position at the start of the frame leaves the arrow lagging a frame behind.
    void LateUpdate()
    {
        var front = queue != null ? queue.Front : null;
        if (front == null || front.Wanted == null || Menus.AnyOpen) { Show(false); return; }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _canvas == null) { Show(false); return; }

        var viewport = _cam.WorldToViewportPoint(front.transform.position + Vector3.up * aimHeight);

        // Behind the camera the projection comes back mirrored, which would send the arrow the
        // wrong way round. Flip it back before anything is measured off it.
        bool behind = viewport.z < 0f;
        if (behind) { viewport.x = 1f - viewport.x; viewport.y = 1f - viewport.y; }

        var size = _canvas.rect.size;
        var pos = new Vector2((viewport.x - 0.5f) * size.x, (viewport.y - 0.5f) * size.y);
        var half = size * 0.5f - Vector2.one * edgePadding;

        if (!behind && Mathf.Abs(pos.x) <= half.x && Mathf.Abs(pos.y) <= half.y)
        {
            Show(false);            // the truck is on screen; its own badge has this covered
            return;
        }

        var dir = pos.sqrMagnitude > 0.0001f ? pos.normalized : Vector2.down;

        // Walk out along the direction of the truck until the first edge of the safe rectangle
        // is hit. Clamping the axes independently would slide the marker along the border away
        // from the line the player should actually run down.
        float reach = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(dir.x), 1e-4f),
                                half.y / Mathf.Max(Mathf.Abs(dir.y), 1e-4f));

        marker.anchoredPosition = dir * reach;
        if (arrowPivot != null)
            arrowPivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Bind(front.Wanted);
        Tick(front);
        Show(true);
    }

    /// <summary>
    /// The clock. Only rewritten when the whole second changes - this runs every frame on every
    /// frame a truck is out there, and building the same string sixty times a second to say the
    /// same thing is pure garbage.
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

    void Show(bool on)
    {
        if (on == _visible) return;
        _visible = on;

        if (group != null)
        {
            group.alpha = on ? 1f : 0f;
            group.blocksRaycasts = false;       // never in the way of a click
        }
    }
}
