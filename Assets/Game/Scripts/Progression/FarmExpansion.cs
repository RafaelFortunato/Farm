using UnityEngine;

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

        [Header("How far the player may walk")]
        public Vector2 playerBoundsMin = new Vector2(-11f, -14.3f);
        public Vector2 playerBoundsMax = new Vector2(11f, 11f);

        [Header("How far the camera may look")]
        public Vector2 cameraBoundsMin = new Vector2(-7f, -15f);
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

        // GameManager only binds itself once play starts, so out of play mode the two rigs are
        // looked up directly. That is what lets the level-preview tool show a level - island,
        // walkable area and camera framing together - without entering play mode.
        var player = Application.isPlaying
            ? GameManager.Player
            : FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player != null)
        {
            player.boundsMin = reached.playerBoundsMin;
            player.boundsMax = reached.playerBoundsMax;
        }

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
