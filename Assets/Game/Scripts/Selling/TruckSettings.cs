using UnityEngine;
using Farm.Farming;

namespace Farm.Selling
{
    /// <summary>
    /// Everything that tunes the trucks: what they ask for, what they pay, how often they come and
    /// how long they wait.
    ///
    /// Held as an asset rather than on TruckQueue so the order table can be rebalanced next to the
    /// items, recipes and farmhouse ladder it has to stay in step with, without opening the scene.
    /// The queue keeps only what belongs to the scene - the road's slots and end points, the truck
    /// bodies, the sounds - and the farm level, which is live state rather than tuning.
    /// </summary>
    [CreateAssetMenu(fileName = "TruckSettings", menuName = "Farm/Truck Settings")]
    public class TruckSettings : ScriptableObject
    {
        /// <summary>
        /// One thing trucks might ask for.
        ///
        /// The two weights are the same entry's pull at the start of the game and at the last
        /// farm level; everything in between is interpolated. That is what turns the queue from
        /// buying carrots into buying cakes without anyone writing a schedule - raw crops fade,
        /// cooked goods take over, and the pipeline the player built starts paying for itself.
        /// </summary>
        [System.Serializable]
        public struct Demand
        {
            public ItemDef item;

            [Tooltip("Relative chance at farm level 1.")]
            public int weightAtStart;
            [Tooltip("Relative chance at the top farm level.")]
            public int weightAtTop;

            [Tooltip("Trucks do not ask for this until the farm reaches this level.")]
            public int minLevel;

            /// <summary>Fewest of the item a truck will ask for.</summary>
            public int minAmount;
            /// <summary>Most of the item a truck will ask for. Rolled fresh per order.</summary>
            public int maxAmount;
        }

        [Header("Orders")]
        [Tooltip("What trucks ask for, and how often. Weights shift with the farm level.")]
        public Demand[] demands;

        [Tooltip("The level at which the end-of-table weights apply. Weights interpolate up to it.")]
        public int topLevel = 5;

        [Tooltip("What a truck pays, as a multiple of the shop price, rolled fresh for each order. " +
                 "The shop is the floor and this is the payday. The figure lands in the sell " +
                 "counter's prompt before the player commits, so it is a decision and not a bet.")]
        public Vector2 truckPayoff = new Vector2(3f, 5f);

        [Header("Arrivals")]
        [Tooltip("How many trucks are already parked when the road opens. One, so the player meets " +
                 "the mechanic straight away without the road looking permanently busy.")]
        public int startingTrucks = 1;

        [Tooltip("How many trucks may be at the gate at once.")]
        public int maxParked = 1;

        [Tooltip("Shortest quiet stretch before the next truck rolls in, in seconds.")]
        [Min(0.1f)] public float arriveMinSeconds = 45f;

        [Tooltip("Longest quiet stretch before the next truck rolls in, in seconds. Rolled fresh " +
                 "each time, and counted from the moment the last one pulled away rather than off a " +
                 "free-running clock, so serving an order quickly is never punished with a shorter " +
                 "wait for the next.")]
        [Min(0.1f)] public float arriveMaxSeconds = 90f;

        [Tooltip("How long a truck waits at the counter before giving up and driving off. The clock " +
                 "starts when it parks, not when it spawns, so the drive in does not eat the window.")]
        public float patience = 30f;

        [Tooltip("Pause before the very first truck appears, in seconds. The road switches on the " +
                 "instant the farmhouse reaches its level, which is the same beat as the upgrade " +
                 "fanfare - so without this the horn lands on top of it and neither is heard " +
                 "properly. Only the opening truck waits; every one after it is on the normal gap.")]
        [Min(0f)] public float firstArrivalDelay = 2f;

        [Header("Movement")]
        [Tooltip("How fast a truck drives between the road's points, in units per second.")]
        public float driveSpeed = 6f;

        /// <summary>A fresh gap between trucks, in seconds. Tolerates a max typed below the min.</summary>
        public float RollGap()
        {
            float lo = Mathf.Max(arriveMinSeconds, 0.1f);
            return Random.Range(lo, Mathf.Max(arriveMaxSeconds, lo));
        }

        /// <summary>A fresh payout multiple for one order.</summary>
        public float RollPayoff() => Random.Range(truckPayoff.x, truckPayoff.y);

        /// <summary>A fresh amount for one order of this entry, never below one.</summary>
        public static int RollAmount(in Demand d) =>
            Random.Range(Mathf.Max(d.minAmount, 1), Mathf.Max(d.maxAmount, Mathf.Max(d.minAmount, 1)) + 1);

        /// <summary>
        /// An entry's pull at a given farm level: zero until it unlocks, then interpolated from its
        /// start weight to its top weight as the farm climbs towards <see cref="topLevel"/>.
        /// </summary>
        /// <param name="d">The entry to weigh.</param>
        /// <param name="level">The farm's current level.</param>
        public int WeightAt(in Demand d, int level)
        {
            if (d.item == null || level < d.minLevel) return 0;
            float t = topLevel > 1 ? Mathf.Clamp01((level - 1f) / (topLevel - 1f)) : 1f;
            return Mathf.Max(Mathf.RoundToInt(Mathf.Lerp(d.weightAtStart, d.weightAtTop, t)), 0);
        }
    }
}
