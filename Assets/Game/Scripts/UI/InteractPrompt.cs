using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating "[E] Plant" badge that follows whatever the player is currently near.
///
/// One instance for the whole scene rather than one per interactable: only a single
/// target is ever focused, so a shared badge that moves is cheaper and keeps the
/// prompt styling in one place.
/// </summary>
public class InteractPrompt : MonoBehaviour
{
    [Tooltip("Found automatically if left empty.")]
    public PlayerInteractor interactor;

    [Tooltip("Canvas object toggled on only when something is in range.")]
    public GameObject canvasRoot;

    public Image keyIcon;
    public TextMeshProUGUI label;

    [Tooltip("Offset above the target's focus point.")]
    public Vector3 worldOffset = new Vector3(0f, 2.1f, 0f);

    Transform _tf;
    Transform _cam;
    Interactable _shown;
    int _shownStateKey = -1;

    void OnEnable()
    {
        _tf = transform;
        _cam = Camera.main.transform;
        if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
        if (canvasRoot != null) canvasRoot.SetActive(false);
        _shown = null;
        _shownStateKey = -1;
    }

    void LateUpdate()
    {
        // Hidden whenever the player cannot actually press the button - a menu is up, or a
        // scripted beat is playing - so the badge never offers an interaction that is refused.
        Interactable target = (interactor != null && interactor.CanInteractNow) ? interactor.Current : null;

        bool show = target != null && target.CanInteract && !string.IsNullOrEmpty(target.Prompt);

        if (canvasRoot != null && canvasRoot.activeSelf != show)
            canvasRoot.SetActive(show);

        if (!show) { _shown = null; _shownStateKey = -1; return; }

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
