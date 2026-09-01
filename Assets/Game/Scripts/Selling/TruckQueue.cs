using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The line of trucks waiting to buy produce.
///
/// The queue is endless in time but bounded in instances: only as many trucks as there are
/// visible slots (plus the one currently driving off) can ever exist at once, so they are
/// pooled and recycled rather than spawned and destroyed. After the first lap the pool
/// stops growing and the feature allocates nothing per sale.
/// </summary>
public class TruckQueue : MonoBehaviour
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

        public int minAmount;
        public int maxAmount;
    }

    [Header("Wiring")]
    [SerializeField] TruckOrder truckPrefab;
    [Tooltip("Extra truck bodies to alternate between, purely for variety. Optional.")]
    [SerializeField] TruckOrder[] truckVariants;

    [Tooltip("Stopping points along the road, front of the queue first.")]
    [SerializeField] Transform[] slots;
    [Tooltip("Off-screen point new trucks roll in from.")]
    [SerializeField] Transform spawnPoint;
    [Tooltip("Off-screen point a served truck drives away to.")]
    [SerializeField] Transform exitPoint;

    [Header("Orders")]
    [Tooltip("What trucks ask for, and how often. Weights shift with the farm level.")]
    [SerializeField] Demand[] demands;

    [Tooltip("How far the farm has come. Drives which goods trucks ask for, and how often.")]
    public int level = 1;

    [Tooltip("The level at which the end-of-table weights apply. Weights interpolate up to it.")]
    [SerializeField] int topLevel = 5;

    [Header("Movement")]
    [SerializeField] float driveSpeed = 6f;

    /// <summary>The truck at the counter, or null while the line is shuffling up.</summary>
    public TruckOrder Front => _queue.Count > 0 ? _queue[0] : null;

    /// <summary>Bumped every time the front order changes, so UI can tell it went stale.</summary>
    public int OrderVersion { get; private set; }

    readonly List<TruckOrder> _pool = new List<TruckOrder>();
    readonly List<TruckOrder> _queue = new List<TruckOrder>();
    readonly List<TruckOrder> _leaving = new List<TruckOrder>();

    // Hard ceiling on trucks mid-departure. Without it a player selling faster than a truck
    // can drive off would keep renting new bodies and the pool would grow without limit,
    // which defeats the point of pooling. Worst case the oldest one is recycled early -
    // it is already off past the counter by then.
    const int MaxLeaving = 2;

    void OnEnable()
    {
        // rebuilt here rather than Awake so a domain reload during play restores the line
        if (_queue.Count == 0) Fill();

        Inventory.Changed -= EnsureFrontReachable;   // never double-subscribe
        Inventory.Changed += EnsureFrontReachable;
    }

    void OnDisable() => Inventory.Changed -= EnsureFrontReachable;

    void Fill()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var t = Rent();
            var d = PickDemand();
            t.Configure(d.item, ReachableAmount(d));
            t.MoveTo(slots[i].position, driveSpeed, true);   // first fill snaps, no convoy sliding in
            t.ShowBadge(true);
            _queue.Add(t);
        }
        OrderVersion++;
    }

    /// <summary>
    /// The front truck has been paid: it pulls away, everyone shuffles up one, and a fresh
    /// order rolls in at the back.
    /// </summary>
    public void Advance()
    {
        if (_queue.Count == 0) return;

        RecycleArrived();

        var served = _queue[0];
        _queue.RemoveAt(0);
        served.ShowBadge(false);
        served.MoveTo(exitPoint.position, driveSpeed);
        _leaving.Add(served);

        for (int i = 0; i < _queue.Count; i++)
            _queue[i].MoveTo(slots[i].position, driveSpeed);

        var fresh = Rent();
        var demand = PickDemand();
        fresh.Configure(demand.item, ReachableAmount(demand));
        fresh.transform.position = spawnPoint.position;
        fresh.MoveTo(slots[_queue.Count].position, driveSpeed);
        fresh.ShowBadge(true);
        _queue.Add(fresh);

        while (_leaving.Count > MaxLeaving)
        {
            Return(_leaving[0]);
            _leaving.RemoveAt(0);
        }

        OrderVersion++;
        EnsureFrontReachable();
    }

    void Update() => RecycleArrived();

    void RecycleArrived()
    {
        for (int i = _leaving.Count - 1; i >= 0; i--)
        {
            if (!_leaving[i].Arrived) continue;
            Return(_leaving[i]);
            _leaving.RemoveAt(i);
        }
    }

    // ---- pool ----

    TruckOrder Rent()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].gameObject.activeSelf)
            {
                _pool[i].gameObject.SetActive(true);
                return _pool[i];
            }

        var prefab = truckPrefab;
        if (truckVariants != null && truckVariants.Length > 0)
        {
            var pick = truckVariants[_pool.Count % truckVariants.Length];
            if (pick != null) prefab = pick;
        }

        var t = Instantiate(prefab, transform);
        t.name = "Truck_" + _pool.Count;
        _pool.Add(t);
        return t;
    }

    void Return(TruckOrder t)
    {
        t.ShowBadge(false);
        t.gameObject.SetActive(false);
    }

    /// <summary>
    /// This entry's pull right now. Public so the balance can be inspected with the same
    /// function the queue actually rolls against, rather than a copy of it.
    /// </summary>
    public int CurrentWeight(in Demand d)
    {
        if (d.item == null || level < d.minLevel) return 0;
        float t = topLevel > 1 ? Mathf.Clamp01((level - 1f) / (topLevel - 1f)) : 1f;
        return Mathf.Max(Mathf.RoundToInt(Mathf.Lerp(d.weightAtStart, d.weightAtTop, t)), 0);
    }

    public Demand[] Demands => demands;

    /// <summary>
    /// Rolls one order. Weighted rather than uniform, and entries the farm has not unlocked
    /// weigh nothing, so a level-1 player is never asked for a cake they cannot bake.
    /// </summary>
    Demand PickDemand()
    {
        if (demands == null || demands.Length == 0) return default;

        // Only entries the player could actually deliver are in the running. See Reachable:
        // the front truck blocks the queue and selling is the only income, so an order out of
        // their reach does not slow the run down, it ends it.
        int total = 0;
        for (int i = 0; i < demands.Length; i++)
            if (Reachable(demands[i].item, demands[i].minAmount)) total += CurrentWeight(demands[i]);

        // Nothing eligible would mean an empty badge and a stuck queue, so fall back to
        // whatever the player can always grow, and only then to any entry at all.
        if (total <= 0)
        {
            for (int i = 0; i < demands.Length; i++)
                if (Reachable(demands[i].item, demands[i].minAmount)) return demands[i];
            for (int i = 0; i < demands.Length; i++)
                if (demands[i].item != null) return demands[i];
            return default;
        }

        int roll = Random.Range(0, total);
        for (int i = 0; i < demands.Length; i++)
        {
            if (!Reachable(demands[i].item, demands[i].minAmount)) continue;
            roll -= CurrentWeight(demands[i]);
            if (roll < 0) return demands[i];
        }
        return demands[demands.Length - 1];
    }

    /// <summary>
    /// Could the player ever put this many on the counter? What they hold, plus the seed
    /// already in the bag, plus every seed their coins can still buy. Animals and the stove
    /// cost only time, and the free starter crop can always be replanted, so paid seed is the
    /// only thing that can be genuinely out of reach.
    ///
    /// This is not a nicety. The front truck blocks the queue, Advance() runs only after a
    /// completed sale, and selling is the only source of coins - so an order the player cannot
    /// fund is not a setback, it is the end of the run. An opening order of 4 Cauliflower
    /// against 30 starting coins did exactly that in about one game in seven.
    /// </summary>
    static bool Reachable(ItemDef item, int amount)
    {
        if (item == null) return false;

        int held = Inventory.ProduceCount(item);
        if (held >= amount) return true;

        var crop = item as CropDef;
        if (crop == null || crop.seedCost <= 0) return true;

        int missing = amount - held - Inventory.SeedCount(crop);
        return missing <= 0 || Inventory.Coins >= missing * crop.seedCost;
    }

    /// <summary>The rolled amount, trimmed to what the player can actually fund.</summary>
    static int ReachableAmount(in Demand d)
    {
        int amount = AmountFor(d);
        while (amount > d.minAmount && !Reachable(d.item, amount)) amount--;
        return amount;
    }

    /// <summary>
    /// Keeps the order at the counter inside the player's reach. It can fall outside it after
    /// the fact - they spend their coins on seed for something else, and nothing can be sold
    /// in the meantime to earn them back. Trimming what the truck asks for is the only move
    /// that does not end the run, and it never asks for more than it did a moment ago.
    /// </summary>
    void EnsureFrontReachable()
    {
        var front = Front;
        if (front == null || front.Wanted == null) return;
        if (Reachable(front.Wanted, front.Amount)) return;

        int amount = front.Amount;
        while (amount > 1 && !Reachable(front.Wanted, amount)) amount--;

        if (Reachable(front.Wanted, amount))
            front.Configure(front.Wanted, amount);
        else
        {
            var d = PickDemand();
            front.Configure(d.item, ReachableAmount(d));
        }

        OrderVersion++;
    }

    static int AmountFor(in Demand d) =>
        Random.Range(Mathf.Max(d.minAmount, 1), Mathf.Max(d.maxAmount, Mathf.Max(d.minAmount, 1)) + 1);

    /// <summary>How many truck objects exist. Should settle at slots + 1 and stop growing.</summary>
    public int PoolSize => _pool.Count;
}
