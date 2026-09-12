using UnityEngine;

namespace Farm.Foraging
{
    /// <summary>
    /// One spot on the island where a mushroom can come up.
    ///
    /// Deliberately just a marker: where mushrooms appear is a level-design decision, so it is
    /// authored by dragging these around in the scene rather than computed at runtime from the
    /// terrain. The patch above them does the picking and the timing.
    ///
    /// A point is tied to a farm level rather than parented into the expansion groups, because the
    /// patch has to see every point it may ever use in one place - a spawn point switched off with
    /// its stage would drop out of GetComponentsInChildren and could never be drawn again. Keeping
    /// them all under one always-on root and gating on a number means the whole set is visible and
    /// adjustable in the editor at any level, which is exactly what tuning this needs.
    /// </summary>
    public class MushroomSpawnPoint : MonoBehaviour
    {
        [Tooltip("Farm level from which this spot joins the draw. Points standing on land the " +
                 "farmhouse has not unlocked yet keep themselves out until the island reaches them.")]
        [Min(1)] public int minLevel = 1;

        /// <summary>The mushroom standing here, or null while the spot is free.</summary>
        public Mushroom Occupant { get; set; }

        /// <summary>True when this spot is unlocked at the given level and has nothing on it.</summary>
        public bool IsOpenAt(int level) => Occupant == null && level >= minLevel;

        // Drawn unselected, and for every point at once: placing these is a "where are the gaps"
        // job, which is impossible to eye one selected point at a time.
        void OnDrawGizmos()
        {
            // Later levels shade warmer, so which points belong to which expansion reads at a glance.
            float t = Mathf.InverseLerp(1f, 5f, minLevel);
            var tint = Color.Lerp(new Color(0.45f, 0.85f, 0.45f), new Color(0.95f, 0.55f, 0.25f), t);

            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.85f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.35f);

            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.25f);
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.2f, 0.35f);

            // A stalk to the ground - a sphere alone floating over a hill is hard to place.
            Gizmos.color = new Color(tint.r, tint.g, tint.b, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.2f);
        }
    }
}
