using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives a Button a click sound without its own script having to know about audio.
///
/// Buttons that already say something specific when pressed - a recipe row starting
/// a cook, the upgrade button reporting success or refusal - voice themselves and
/// should not carry this as well, or the player hears two sounds for one press.
/// This is for the rest: the plain buttons whose only feedback is that they were
/// pressed at all.
///
/// Riding Button.onClick rather than IPointerClickHandler means a press that the
/// button itself ignores - because it is not interactable - stays silent too.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIClickSound : MonoBehaviour
{
    [Tooltip("Played on press. Leave empty to silence this button without removing " +
             "the component.")]
    [SerializeField] SoundEvent clickSound;

    Button _button;

    /// <summary>Caches the Button this rides on.</summary>
    void Awake() => _button = GetComponent<Button>();

    // Subscribed here rather than in Awake because these buttons live on panels that
    // get switched off and back on - the shop and kitchen menus being the obvious
    // ones - and a listener added once in Awake would be re-added on every re-enable.
    void OnEnable()
    {
        if (_button != null) _button.onClick.AddListener(Play);
    }

    /// <summary>
    /// Drops the listener so a panel that is switched off and on again doesn't
    /// end up playing the click twice.
    /// </summary>
    void OnDisable()
    {
        if (_button != null) _button.onClick.RemoveListener(Play);
    }

    /// <summary>Plays the click. Routed through AudioManager so it obeys the SFX volume.</summary>
    void Play()
    {
        // An empty slot means "the normal click", not "silent" - that is what almost every
        // button wants, and it keeps the shared sound in one place. A button that needs its
        // own voice still overrides it here.
        if (clickSound != null) AudioManager.PlayUI(clickSound);
        else AudioManager.PlayClick();
    }
}
