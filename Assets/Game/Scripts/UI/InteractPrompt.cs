using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating "[E] Plant" badge that follows whatever the player is currently near.
///
/// One instance for the whole scene rather than one per interactable: only a single
/// target is ever focused, so a shared badge that moves is cheaper and keeps the
/// prompt styling in one place.
///
/// This object IS the thing that gets shown and hidden, and it is driven by
/// PlayerInteractor rather than by its own Update. That means it can sit disabled in the
/// scene - which is how it should be saved, so it does not clutter the editor view -
/// without the prompt going dead at runtime.
/// </summary>
public class InteractPrompt : MonoBehaviour
{
    public Image keyIcon;
    public TextMeshProUGUI label;

    [Tooltip("Offset above the target's focus point.")]
    public Vector3 worldOffset = new Vector3(0f, 2.1f, 0f);

    Transform _tf;
    Transform _cam;
    Interactable _shown;
    int _shownStateKey = -1;
    bool _ready;

    // Lazy rather than OnEnable: this object starts disabled, so OnEnable has not run by
    // the time the first Show call arrives.
    void EnsureInit()
    {
        if (_ready) return;
        _tf = transform;
        _cam = Camera.main.transform;
        _ready = true;
    }

    /// <summary>
    /// Point the badge at a target, or pass null to hide it. Safe to call every frame,
    /// and safe to call while this object is disabled.
    /// </summary>
    public void Show(Interactable target)
    {
        bool show = target != null && target.CanInteract && !string.IsNullOrEmpty(target.Prompt);

        if (gameObject.activeSelf != show) gameObject.SetActive(show);
        if (!show) { _shown = null; _shownStateKey = -1; return; }

        EnsureInit();

        // Refresh when the target OR its state changes. Harvesting keeps the same plot
        // focused while its state flips Ready -> Empty, so target identity alone is not
        // enough. StateKey is an int off the plot's own enum - no string diffing.
        int stateKey = target.StateKey;
        if (target != _shown || stateKey != _shownStateKey)
        {
            _shown = target;
            _shownStateKey = stateKey;
            if (label != null) label.text = target.Prompt;
        }

        _tf.position = target.FocusPoint + worldOffset;
        _tf.rotation = _cam.rotation;
    }
}
