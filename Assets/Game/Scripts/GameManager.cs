using UnityEngine;

namespace Farm
{
    /// <summary>
    /// Where the farm's one-of-a-kind world objects live. UI lives on UIManager.
    ///
    /// Everything here is wired in the inspector rather than hunted for at runtime: it is visible,
    /// it is greppable from the scene, and it reaches objects that are switched off.
    ///
    /// Read these from OnEnable or later, never from Awake - see Singleton for why, and for why
    /// the accessors below do not null-check.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public class GameManager : Singleton<GameManager>
    {
        [Header("World")]
        [SerializeField] PlayerController player;
        [SerializeField] CameraFollow cameraRig;

        /// <summary>The player. Everything that chases or measures against the cat starts here.</summary>
        public static PlayerController Player => Instance.player;

        /// <summary>The player's transform, cached so per-frame callers do not walk a chain.</summary>
        public static Transform PlayerTransform => Instance._playerTf;

        /// <summary>The camera rig, for anything that wants to push the camera in for a beat.</summary>
        public static CameraFollow CameraRig => Instance.cameraRig;

        /// <summary>The camera's transform - what every world-space billboard turns to face.</summary>
        public static Transform CameraTransform => Instance._cameraTf;

        Transform _playerTf;
        Transform _cameraTf;

        protected override void OnBind()
        {
            _playerTf = player != null ? player.transform : null;
            _cameraTf = cameraRig != null ? cameraRig.transform : null;
        }

        /// <summary>
        /// A slot left empty is silent until something null-references at runtime, which is a poor
        /// way to find out. Say so at author time instead.
        /// </summary>
        void OnValidate()
        {
            var missing = string.Empty;
            if (player == null) missing += " player";
            if (cameraRig == null) missing += " cameraRig";

            if (missing.Length > 0) Debug.LogWarning("GameManager is missing references:" + missing, this);
        }
    }
}
