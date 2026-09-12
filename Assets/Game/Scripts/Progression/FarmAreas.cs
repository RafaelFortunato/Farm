using UnityEngine;

namespace Farm.Progression
{
    /// <summary>
    /// Where the island is cut into the pieces the farmhouse hands out, one level at a time.
    ///
    /// The shapes are authored here rather than baked into the scene, because deciding how much
    /// island a level is worth is a judgement call that gets made over and over. Move a circle,
    /// press Rebuild in Farm > Farm Levels, and every tile, tree and fence re-sorts itself into the
    /// level it now falls in - instead of dragging two hundred objects between groups by hand.
    ///
    /// The pieces nest: level 1 is the inner circle, level 2 is the ring out to the outer circle,
    /// level 3 is whatever is left of the starting island, and level 4 is the top-left corner plus
    /// the ranch land off the west edge. Nothing here runs in the game - FarmExpansion switches the
    /// groups this produced, and it does not care how they were sorted.
    /// </summary>
    public class FarmAreas : MonoBehaviour
    {
        [Header("Level 1 - crops and the store")]
        [Tooltip("The island the game opens on. Everything inside this circle is visible from the start.")]
        public Vector2 startCentre = new Vector2(3f, 3f);
        public float startRadius = 10.5f;

        [Header("Level 2 - cooking")]
        [Tooltip("The stove's ground. Everything inside this circle but outside the first one.")]
        public Vector2 secondCentre = new Vector2(-1f, 0f);
        [Tooltip("Radius of that second circle, in world units.")]
        public float secondRadius = 12f;

        [Header("Level 4 - the top-left corner")]
        [Tooltip("Held back until the ranch arrives, so the last upgrade opens land on both sides. " +
                 "Everything else left over on the starting island belongs to level 3, with the road.")]
        public Vector2 cornerMin = new Vector2(-40f, 3f);
        [Tooltip("Opposite corner of that rectangle.")]
        public Vector2 cornerMax = new Vector2(-6.5f, 40f);

        /// <summary>
        /// Which level a point on the starting island belongs to. The corner is tested first: it
        /// overlaps both circles, and being held back for the ranch is the whole point of it.
        /// </summary>
        public int LevelAt(Vector3 world)
        {
            var p = new Vector2(world.x, world.z);

            if (p.x >= cornerMin.x && p.x <= cornerMax.x && p.y >= cornerMin.y && p.y <= cornerMax.y) return 4;
            if ((p - startCentre).magnitude <= startRadius) return 1;
            if ((p - secondCentre).magnitude <= secondRadius) return 2;
            return 3;
        }

        static readonly Color[] LevelTint =
        {
            new Color(0.40f, 0.85f, 0.45f),   // Lv1
            new Color(0.35f, 0.70f, 0.95f),   // Lv2
            new Color(0.98f, 0.78f, 0.30f),   // Lv3
            new Color(0.95f, 0.45f, 0.35f),   // Lv4
        };

        /// <summary>Public so the level window can tint its buttons to match the gizmos.</summary>
        public static Color TintFor(int level) => LevelTint[Mathf.Clamp(level - 1, 0, LevelTint.Length - 1)];

        void OnDrawGizmos()
        {
            DrawCircle(startCentre, startRadius, TintFor(1));
            DrawCircle(secondCentre, secondRadius, TintFor(2));

            Gizmos.color = TintFor(4);
            var min = new Vector3(cornerMin.x, 0.1f, cornerMin.y);
            var max = new Vector3(cornerMax.x, 0.1f, cornerMax.y);
            Gizmos.DrawLine(min, new Vector3(max.x, 0.1f, min.z));
            Gizmos.DrawLine(new Vector3(max.x, 0.1f, min.z), max);
            Gizmos.DrawLine(max, new Vector3(min.x, 0.1f, max.z));
            Gizmos.DrawLine(new Vector3(min.x, 0.1f, max.z), min);
        }

        static void DrawCircle(Vector2 centre, float radius, Color tint)
        {
            Gizmos.color = tint;
            const int steps = 64;
            var prev = new Vector3(centre.x + radius, 0.1f, centre.y);
            for (int i = 1; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                var next = new Vector3(centre.x + Mathf.Cos(a) * radius, 0.1f, centre.y + Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
