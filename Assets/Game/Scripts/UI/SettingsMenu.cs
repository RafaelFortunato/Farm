using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Farm.Audio;

namespace Farm.UI
{
    /// <summary>
    /// The settings dialog: two volume sliders and a credit line, opened by the cog in the
    /// corner of the screen.
    ///
    /// Ported from the tower defence project, but rebuilt on BaseMenu rather than carried
    /// over whole. That version owned everything itself - its own Escape handling in Update,
    /// its own open flag, its own backdrop click, its own show and hide. The farm already has
    /// all of that: BaseMenu shows and hides the panel and wires the close button, UIManager
    /// keeps the one-panel-at-a-time rule and closes whatever is open when Escape is pressed,
    /// and the open and close sounds come for free. What is genuinely new here is the two
    /// sliders and the pause, so that is all this file contains.
    ///
    /// The sliders drive AudioManager live, so the player hears what they are dragging.
    /// Saving is deliberately NOT done per tick: PlayerPrefs writes to disk, and a drag would
    /// issue one write per pixel of travel. It happens once, when the dialog closes. Loading
    /// is not done here at all - AudioManager reads the prefs when it binds, so audio is
    /// already at the saved level before this panel exists and this only has to show what it
    /// finds.
    /// </summary>
    public class SettingsMenu : BaseMenu
    {
        [Header("Sliders")]
        [SerializeField] Slider sfxSlider;
        [SerializeField] Slider ambienceSlider;

        [Tooltip("Optional readouts showing each slider's value as a percentage.")]
        [SerializeField] TextMeshProUGUI sfxValue;
        [SerializeField] TextMeshProUGUI ambienceValue;

        [Header("Credit")]
        [Tooltip("Filled in at runtime from the build's own version, so it cannot fall out of " +
                 "step with Player Settings the way a typed-in string would.")]
        [SerializeField] TextMeshProUGUI versionLabel;
        [SerializeField] string author = "Created by Rafael Paz";

        [Header("Pause")]
        [Tooltip("Freeze the farm while the dialog is up. Crops, animals and the truck's " +
                 "patience all run on Time.time, so they hold where they are; the dialog and " +
                 "its sounds run on unscaled time and keep working.")]
        [SerializeField] bool pauseWhileOpen = true;

        // Set while the sliders are being filled in from AudioManager, so the resulting
        // onValueChanged callbacks do not write those same values straight back.
        bool _applyingValues;

        // Whatever the clock was doing before this dialog paused it. Restored rather than
        // hard-set to 1, so opening settings on the win screen - which freezes the clock
        // itself - does not un-freeze the farm on the way out.
        float _timeScaleBeforeOpen = 1f;

        /// <summary>
        /// Shows the dialog at the levels the AudioManager is actually playing at.
        ///
        /// Read fresh on every open rather than trusting where the slider was left: this is
        /// the only place the levels are shown, but the manager is the authority on what is
        /// being played.
        /// </summary>
        public void Open()
        {
            Present();

            _applyingValues = true;
            if (sfxSlider != null) sfxSlider.value = AudioManager.SfxVolume;
            if (ambienceSlider != null) ambienceSlider.value = AudioManager.AmbienceVolume;
            _applyingValues = false;

            RefreshReadouts();

            if (versionLabel != null) versionLabel.text = "v" + Application.version + "\n" + author;

            if (pauseWhileOpen)
            {
                _timeScaleBeforeOpen = Time.timeScale;
                Time.timeScale = 0f;
            }
        }

        /// <summary>
        /// Runs on every close, whichever way it happened - the close button, Escape, or
        /// another panel opening over the top. Lifting the pause here rather than in a Close
        /// override is what makes it impossible to leave the farm frozen with nothing on
        /// screen to explain why.
        /// </summary>
        protected override void OnDismissed()
        {
            if (pauseWhileOpen) Time.timeScale = _timeScaleBeforeOpen;
            AudioManager.SaveVolumePrefs();
        }

        // Bound here rather than in Present, because the sliders are the one thing this panel
        // adds and they only ever need hooking once per enable.
        void OnEnable()
        {
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            if (ambienceSlider != null) ambienceSlider.onValueChanged.AddListener(HandleAmbienceChanged);
        }

        void OnDisable()
        {
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
            if (ambienceSlider != null) ambienceSlider.onValueChanged.RemoveListener(HandleAmbienceChanged);
        }

        void HandleSfxChanged(float value)
        {
            if (_applyingValues) return;
            AudioManager.SetSfxVolume(value);
            RefreshReadouts();

            // A level is easier to judge against a sound than against a number, and the click
            // is short enough to drag over. Rate limiting on the event keeps a fast drag from
            // turning into a buzz.
            AudioManager.PlayClick();
        }

        void HandleAmbienceChanged(float value)
        {
            if (_applyingValues) return;
            AudioManager.SetAmbienceVolume(value);
            RefreshReadouts();
        }

        void RefreshReadouts()
        {
            if (sfxValue != null)
                sfxValue.text = Mathf.RoundToInt(AudioManager.SfxVolume * 100f) + "%";
            if (ambienceValue != null)
                ambienceValue.text = Mathf.RoundToInt(AudioManager.AmbienceVolume * 100f) + "%";
        }
    }
}
