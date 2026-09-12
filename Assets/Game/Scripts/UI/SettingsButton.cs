using UnityEngine;
using UnityEngine.UI;

namespace Farm.UI
{
    /// <summary>
    /// The cog in the corner of the screen that opens the settings dialog.
    ///
    /// Separate from SettingsMenu rather than part of it, because the two have opposite
    /// lifetimes: BaseMenu switches its own GameObject off when the panel closes, so a cog
    /// living inside the panel would disappear the moment it was used and never come back.
    ///
    /// It holds a reference to the panel rather than asking UIManager for it, so a scene can
    /// carry the cog without the settings panel, or two of them, without either knowing about
    /// the other.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SettingsButton : MonoBehaviour
    {
        [Tooltip("The dialog this opens.")]
        [SerializeField] SettingsMenu settings;

        Button _button;

        void OnEnable()
        {
            if (_button == null) _button = GetComponent<Button>();

            // Rebound on every enable rather than once at startup: a domain reload leaves the
            // object alive but wipes the binding, which is the same reason BaseMenu rebinds
            // its own close button.
            _button.onClick.RemoveListener(OpenSettings);
            _button.onClick.AddListener(OpenSettings);
        }

        void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(OpenSettings);
        }

        void OpenSettings()
        {
            if (settings == null) return;

            // Already up, so the cog means close - the same thing pressing Escape would do.
            if (settings.IsOpen) { settings.Close(); return; }

            settings.Open();
        }
    }
}
