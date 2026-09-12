using UnityEngine;

namespace Farm.Progression
{
    /// <summary>
    /// The island growing as the farmhouse does.
    ///
    /// Every level's tiles, plots, fences and decor are authored in the scene up front and saved
    /// DISABLED. Levelling up switches a group on - instant, free on WebGL, and tweakable by hand
    /// in the editor, which spawning terrain at runtime would be none of. From the player's side
    /// the island still visibly grows.
    ///
    /// Applying is idempotent and cumulative: ApplyUpTo(3) puts the world in exactly the state it
    /// should be at level 3, whether it got there one upgrade at a time or in one jump. That is
    /// what lets a save, a test, or a designer scrubbing the level field all land somewhere sane.
    /// </summary>
    public class FarmExpansion : MonoBehaviour
    {
        [System.Serializable]
        public class Stage
        {
            [Tooltip("Switched on when the farm reaches this level.")]
            public GameObject[] enable;

            [Tooltip("Switched off - old art the new art replaces, or scenery the new land sits on.")]
            public GameObject[] disable;

            [Header("How far the camera may look")]
            [Tooltip("Minimum x and z the camera may look at once this stage is reached.")]
            public Vector2 cameraBoundsMin = new Vector2(-7f, -15f);
            [Tooltip("Maximum x and z the camera may look at once this stage is reached.")]
            public Vector2 cameraBoundsMax = new Vector2(7f, 7f);
        }

        [Tooltip("One per farm level, starting at level 1. Entry 0 is the world as it ships.")]
        public Stage[] stages;

        /// <summary>
        /// Put the world into the state it should be in at this level. Safe to call repeatedly and
        /// with any level, so there is only ever one code path into a given state.
        /// </summary>
        public void ApplyUpTo(int level)
        {
            if (stages == null || stages.Length == 0) return;

            int last = Mathf.Clamp(level, 1, stages.Length) - 1;

            // Unwind anything above the target first, newest first, so a stage that replaced an
            // earlier one hands the world back in the right order. Without this the method would
            // only ever apply forward, and dropping the level in the inspector - the obvious way
            // to tune a mid-game state - would leave the island stuck at its largest.
            for (int i = stages.Length - 1; i > last; i--)
            {
                var stage = stages[i];
                if (stage == null) continue;

                SetAll(stage.enable, false);
                SetAll(stage.disable, true);
            }

            for (int i = 0; i <= last; i++)
            {
                var stage = stages[i];
                if (stage == null) continue;

                SetAll(stage.enable, true);
                SetAll(stage.disable, false);
            }

            // Bounds are not cumulative - the newest stage simply states the whole island.
            var reached = stages[last];

            // Where the player may walk is not set here any more: the ground tiles decide it, and
            // PlayerController refuses a step with no floor under it. That means the walkable area
            // follows whatever shape the island actually is, and grows with it, instead of being a
            // rectangle re-tuned by hand at every level.
            //
            // GameManager only binds itself once play starts, so out of play mode the rig is looked
            // up directly. That is what lets the level-preview tool frame a level without entering
            // play mode.
            var rig = Application.isPlaying
                ? GameManager.CameraRig
                : FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);

            if (rig != null)
            {
                rig.boundsMin = reached.cameraBoundsMin;
                rig.boundsMax = reached.cameraBoundsMax;
            }
        }

        static void SetAll(GameObject[] objects, bool active)
        {
            if (objects == null) return;

            foreach (var go in objects)
                if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
