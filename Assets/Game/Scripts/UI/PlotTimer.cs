using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Radial countdown floating over whatever the player is waiting on - a growing crop, a
/// cooking stove.
///
/// Lives on a world-space canvas so it sits in the scene rather than the HUD, which keeps it
/// readable when several things are running at once. The canvas is switched off unless its
/// source is actually counting, so idle plots cost nothing per frame.
///
/// The source is found on a parent rather than wired by hand: the ring is always a child of
/// the thing it counts, and that is what let the stove reuse this instead of getting a
/// second copy of it.
/// </summary>
public class PlotTimer : MonoBehaviour
{
    [Tooltip("What this ring counts down. Must implement ITimedProgress. Found on a parent if left empty.")]
    [SerializeField] MonoBehaviour source;

    [Tooltip("Canvas object toggled on only while the clock is running.")]
    public GameObject canvasRoot;

    [Tooltip("Radial-filled ring showing progress.")]
    public Image fillImage;

    [Header("Colours")]
    public Color growingColor = new Color(0.55f, 0.85f, 0.35f);
    public Color nearlyDoneColor = new Color(1f, 0.85f, 0.3f);

    Transform _tf;
    Transform _cam;
    ITimedProgress _source;

    void OnEnable()
    {
        _tf = transform;
        _source = source as ITimedProgress ?? GetComponentInParent<ITimedProgress>();
        _cam = GameManager.CameraTransform;
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    void LateUpdate()
    {
        bool running = _source != null && _source.InProgress;

        if (canvasRoot != null && canvasRoot.activeSelf != running)
            canvasRoot.SetActive(running);

        if (!running) return;

        float p = _source.Progress;
        if (fillImage != null)
        {
            fillImage.fillAmount = p;
            fillImage.color = p > 0.75f ? nearlyDoneColor : growingColor;
        }

        // face the camera - cheap, and only runs for rings that are actually counting
        _tf.rotation = _cam.rotation;
    }
}
