using System.Collections.Generic;
using UnityEngine;

namespace Farm.Interaction
{
    /// <summary>
    /// Base for anything the player can walk up to and use.
    ///
    /// Instances register themselves in a static list so PlayerInteractor can find the
    /// nearest one without a physics query or a scene search every frame.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        static readonly List<Interactable> Registry = new List<Interactable>();
        /// <summary>Every enabled interactable, for PlayerInteractor to search without a physics query.</summary>
        public static IReadOnlyList<Interactable> All => Registry;

        [Tooltip("Where the focus marker sits. Defaults to this transform.")]
        public Transform focusAnchor;

        /// <summary>False while the object has nothing useful to offer right now.</summary>
        public virtual bool CanInteract => true;

        /// <summary>
        /// Identifies the current state. UI compares this to know when Prompt / CanInteract
        /// may have changed, instead of diffing prompt strings. Subclasses back it with their
        /// own state enum, so it changes exactly when the object's state does.
        /// </summary>
        public virtual int StateKey => 0;

        /// <summary>Short line shown while this is the focused target.</summary>
        public abstract string Prompt { get; }

        /// <summary>Do the thing. Only ever called while <see cref="CanInteract"/> is true.</summary>
        public abstract void Interact(PlayerInteractor interactor);

        /// <summary>The player just looked at this. Override to highlight; the base does nothing.</summary>
        public virtual void OnFocusEnter() { }
        /// <summary>The player looked away. Override to undo whatever OnFocusEnter did.</summary>
        public virtual void OnFocusExit() { }

        /// <summary>Where the marker and prompt hang - the anchor if one is set, else this object.</summary>
        public Vector3 FocusPoint => focusAnchor != null ? focusAnchor.position : transform.position;

        protected virtual void OnEnable() => Registry.Add(this);
        protected virtual void OnDisable() => Registry.Remove(this);
    }
}
