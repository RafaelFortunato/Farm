using UnityEngine;
using UnityEngine.UI;
using Farm.Audio;
using Farm.Interaction;

namespace Farm.UI
{
    /// <summary>
    /// What every panel in the game does the same way.
    ///
    /// The four panels were each carrying their own copy of: toggle the GameObject, switch the
    /// panel root on, wire the close button, push the camera in, flip a static IsOpen flag, and
    /// play the open and close sounds. Six things, four times over, and the only place that knew
    /// the full set was a hand-written list in Menus that had to be edited whenever a panel was
    /// added. That list is gone: opening registers the panel with UIManager, and "is a menu up"
    /// is now simply whether that reference is null.
    ///
    /// One panel is up at a time, by construction - opening a second dismisses the first - which
    /// is what makes a single reference sufficient.
    ///
    /// Subclasses supply their own Open(...) taking whatever they need, call Present, then fill
    /// in their rows.
    /// </summary>
    public abstract class BaseMenu : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("The visual root inside this object. Switched on with the panel.")]
        [SerializeField] protected GameObject panelRoot;

        [Tooltip("Cancel or close. Wired automatically, so every panel is dismissed the same way.")]
        [SerializeField] protected Button closeButton;

        [Header("Camera")]
        [Tooltip("How far the camera pushes in while this panel is open, in world units. Negative " +
                 "pulls back. Leave zoom and pitch at 0 for a panel that should not move the camera.")]
        public float cameraZoom;
        [Tooltip("Extra tilt while open, in degrees. Negative drops the camera lower.")]
        public float cameraPitch;
        [Tooltip("Seconds to ease the camera in, and back out on close.")]
        public float cameraBlend = 0.35f;

        CameraFollow _cameraRig;

        /// <summary>Who opened it, for panels that hand the interactor on to an action.</summary>
        protected PlayerInteractor Interactor { get; private set; }

        /// <summary>
        /// True while THIS panel is the one that is up. Asked of the UIManager rather than kept as
        /// a flag here, so there is a single answer and it cannot drift from reality.
        /// </summary>
        public bool IsOpen => UIManager.OpenMenu == this;

        /// <summary>
        /// Show the panel. Subclasses call this first from their own Open, then populate.
        ///
        /// Anything already up is dismissed silently: the player is not closing that panel, they
        /// are opening this one, and two sounds for one action reads as a mistake.
        /// </summary>
        protected void Present(PlayerInteractor interactor = null)
        {
            var previous = UIManager.OpenMenu;
            if (previous != null && previous != this) previous.Dismiss();

            Interactor = interactor;

            gameObject.SetActive(true);                  // this object is the toggle
            if (panelRoot != null) panelRoot.SetActive(true);

            if (closeButton != null)
            {
                // rebound on every open rather than once at startup: a domain reload leaves the
                // object alive but wipes the binding
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            // Resolved here rather than in OnEnable: these objects start disabled, so OnEnable has
            // not run when the reference is first needed.
            if (MovesCamera)
            {
                if (_cameraRig == null) _cameraRig = GameManager.CameraRig;
                if (_cameraRig != null) _cameraRig.SetActionFraming(cameraZoom, cameraPitch, cameraBlend);
            }

            UIManager.SetOpenMenu(this);
            AudioManager.PlayOpen();
        }

        /// <summary>Dismisses the panel. Wired to the close button, and called by Escape.</summary>
        public void Close() => Close(true);

        /// <summary>
        /// <paramref name="playerDismissed"/> separates "I changed my mind" from "I chose something
        /// and the panel got out of the way". Only the first is a close as far as the player is
        /// concerned; the second is a button press whose result is the thing they picked, so it
        /// takes the click and lets the chosen action make its own noise.
        /// </summary>
        public void Close(bool playerDismissed)
        {
            if (!IsOpen) return;                         // already gone; say nothing twice

            if (playerDismissed) AudioManager.PlayClose();
            else AudioManager.PlayClick();

            Dismiss();
        }

        /// <summary>
        /// Tear-down with no sound - the shared half of every close, and what a panel gets when it
        /// is pushed aside by another one opening.
        /// </summary>
        void Dismiss()
        {
            UIManager.ClearOpenMenu(this);
            OnDismissed();

            if (_cameraRig != null) _cameraRig.ClearActionFraming();
            gameObject.SetActive(false);
        }

        /// <summary>Drop whatever the panel was pointing at. Runs on every close.</summary>
        protected virtual void OnDismissed() { }

        /// <summary>A panel with no zoom and no tilt leaves the camera alone.</summary>
        bool MovesCamera => !Mathf.Approximately(cameraZoom, 0f) || !Mathf.Approximately(cameraPitch, 0f);

        // Destroying the panel while it is up would otherwise leave UIManager holding a dead
        // reference, and every AnyOpen check would say a menu is open forever.
        protected virtual void OnDestroy() => UIManager.ClearOpenMenu(this);
    }
}
