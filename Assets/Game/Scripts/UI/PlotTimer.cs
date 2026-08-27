using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Radial countdown floating over a plot while its crop grows.
///
/// Lives on a world-space canvas so it sits in the scene rather than the HUD, which keeps
/// it readable when several plots are growing at once. The canvas is switched off unless
/// the plot is actually growing, so idle plots cost nothing per frame.
/// </summary>
public class PlotTimer : MonoBehaviour
{
    [Tooltip("Plot this timer belongs to. Found on a parent if left empty.")]
    public SoilPlot plot;

    [Tooltip("Canvas object toggled on only while growing.")]
    public GameObject canvasRoot;

    [Tooltip("Radial-filled ring showing progress.")]
    public Image fillImage;

    [Header("Colours")]
    public Color growingColor = new Color(0.55f, 0.85f, 0.35f);
    public Color nearlyDoneColor = new Color(1f, 0.85f, 0.3f);

    Transform _tf;
    Transform _cam;

    void OnEnable()
    {
        _tf = transform;
        if (plot == null) plot = GetComponentInParent<SoilPlot>();
        _cam = Camera.main.transform;
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    void LateUpdate()
    {
        bool growing = plot != null && plot.Current == PlotState.Growing;

        if (canvasRoot != null && canvasRoot.activeSelf != growing)
            canvasRoot.SetActive(growing);

        if (!growing) return;

        float p = plot.Progress;
        if (fillImage != null)
        {
            fillImage.fillAmount = p;
            fillImage.color = p > 0.75f ? nearlyDoneColor : growingColor;
        }

        // face the camera - cheap, and only runs for plots that are actually growing
        _tf.rotation = _cam.rotation;
    }
}
