using UnityEngine;

namespace Farm.Audio
{
    /// <summary>
    /// One authored sound - a spade in soil, a coin pickup - stored as an asset
    /// rather than a bare AudioClip reference on some prefab.
    ///
    /// The indirection buys three things a raw clip can't. Variation: several takes
    /// picked at random with a pitch wobble, so ten harvests in a row don't read as
    /// one sample stuttering. Tuning: a sound's level lives with the sound, instead
    /// of being re-typed at every call site that plays it. And rate limiting, which
    /// is the difference between a busy farm sounding busy and sounding like
    /// static - see minRetriggerInterval.
    /// </summary>
    [CreateAssetMenu(menuName = "Farm/Sound Event", fileName = "SFX_New")]
    public class SoundEvent : ScriptableObject
    {
        [Tooltip("Takes for this sound. One is picked per play; with several authored, " +
                 "the same clip never plays twice in a row.")]
        [SerializeField] AudioClip[] clips;

        [Header("Level")]
        [SerializeField, Range(0f, 1f)] float volume = 0.8f;

        [Tooltip("Random pitch window. A little spread keeps repeated actions from sounding " +
                 "mechanical; too much and the same sound reads as a different object.")]
        [SerializeField] Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Header("Rate limit")]
        [Tooltip("Shortest gap between two plays of this event, in seconds. Several plots " +
                 "ripening together should read as one flourish, not the same clip stacked " +
                 "on itself. 0 plays every request.")]
        [SerializeField] float minRetriggerInterval = 0.06f;

        [Header("Timing")]
        [Tooltip("Seconds to wait before this is heard. Most sounds want 0 - they fire the instant " +
                 "the thing happens. It exists for sounds that play UNDER an animation, where the " +
                 "moment they belong to is a little way into the clip: the planting pat lands when " +
                 "the cat's hand reaches the soil, not when the button was pressed. Tuning it here " +
                 "rather than at the call site keeps the delay with the sound it belongs to, so a " +
                 "swapped clip brings its own timing.")]
        [SerializeField, Min(0f)] float startDelay;

        [Header("Space")]
        [Tooltip("How much this sound is positioned in the world. 0 is flat stereo (use " +
                 "for UI), 1 is fully positional. Leave negative to take the manager's " +
                 "default, which is what world sounds normally want.")]
        [SerializeField, Range(-1f, 1f)] float spatialBlend = -1f;

        // How far this particular sound carries. The AudioManager holds a farm-wide default that
        // suits most things, and most sounds should leave these at -1 and take it. They exist
        // because reach is a property of the SOURCE, not of the farm: a stove burbling to itself
        // should be gone within a few paces, while a truck pulling up is meant to be heard from
        // across the fields. One shared pair of numbers cannot be right for both, and the
        // alternative - re-typing distances at every call site - is how the two loop and pool
        // paths drifted apart in the first place.
        [Tooltip("How close counts as being right there, in world units, measured from the " +
                 "player. Inside this the sound is at full level. Leave negative to take the " +
                 "manager's default.")]
        [SerializeField] float fullVolumeDistance = -1f;

        [Tooltip("How far away this sound fades out completely, in world units from the player. " +
                 "Lower it to make something local and private; raise it for something the whole " +
                 "farm should notice. Leave negative to take the manager's default.")]
        [SerializeField] float silenceDistance = -1f;

        // Deliberately not serialized: this is play-time bookkeeping, not authored data,
        // and writing it into the asset would dirty it on every play.
        [System.NonSerialized] int _lastClipIndex = -1;

        /// <summary>Authored loudness for this event, before the master and category sliders.</summary>
        public float Volume => volume;
        /// <summary>
        /// Shortest gap between two plays of this event. Guards against the same clip being fired
        /// several times in one frame and stacking into something twice as loud.
        /// </summary>
        public float MinRetriggerInterval => minRetriggerInterval;

        /// <summary>Seconds between the request and the sound being audible. 0 for almost everything.</summary>
        public float StartDelay => startDelay;

        /// <summary>Blend authored on this sound, or a negative value to take the manager's default.</summary>
        public float SpatialBlendOverride => spatialBlend;

        /// <summary>Full-volume radius authored here, or negative to take the manager's default.</summary>
        public float FullVolumeDistanceOverride => fullVolumeDistance;

        /// <summary>Silence radius authored here, or negative to take the manager's default.</summary>
        public float SilenceDistanceOverride => silenceDistance;

        public bool HasClips => clips != null && clips.Length > 0;

        /// <summary>A fresh pitch for one play, inside the authored window.</summary>
        public float RandomPitch => Random.Range(pitchRange.x, pitchRange.y);

        /// <summary>
        /// Picks a clip for one play. With more than one take authored it never returns
        /// the same clip twice running - back-to-back repeats are exactly what makes a
        /// randomised sound stop sounding randomised.
        /// </summary>
        public AudioClip PickClip()
        {
            if (!HasClips) return null;
            if (clips.Length == 1) return clips[0];

            int index = Random.Range(0, clips.Length);
            if (index == _lastClipIndex) index = (index + 1) % clips.Length;

            _lastClipIndex = index;
            return clips[index];
        }

        /// <summary>
        /// Pushes an edited distance onto anything already looping this sound.
        ///
        /// Without it these fields would only be read when a voice starts, so tuning the reach of
        /// the stove while listening to the stove would do nothing until the next cook - which is
        /// exactly the moment you want to hear the change. One-shots are too short to be worth
        /// chasing; they pick the new numbers up on their next play regardless.
        ///
        /// Editor-only. OnValidate does not run in a build, and AudioManager is null-safe here
        /// anyway, so this is silent during asset import and in scenes with no audio rig.
        /// </summary>
        void OnValidate()
        {
            // A silence radius inside the full-volume radius means Unity clamps and the sound
            // simply stops fading, which looks precisely like the bug this feature fixes.
            if (fullVolumeDistance >= 0f && silenceDistance >= 0f && silenceDistance <= fullVolumeDistance)
                silenceDistance = fullVolumeDistance + 1f;

            AudioManager.RefreshRolloff(this);
        }
    }
}
