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
    }

    void Fill()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var t = Rent();
            var d = PickDemand();
            t.Configure(d.item, AmountFor(d));
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
        fresh.Configure(demand.item, AmountFor(demand));
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

        int total = 0;
        for (int i = 0; i < demands.Length; i++) total += CurrentWeight(demands[i]);

        // Nothing eligible would mean an empty badge and a stuck queue, so fall back to the
        // first entry that has an item at all.
        if (total <= 0)
        {
            for (int i = 0; i < demands.Length; i++)
                if (demands[i].item != null) return demands[i];
            return default;
        }

        int roll = Random.Range(0, total);
        for (int i = 0; i < demands.Length; i++)
        {
            roll -= CurrentWeight(demands[i]);
            if (roll < 0) return demands[i];
        }
        return demands[demands.Length - 1];
    }

    static int AmountFor(in Demand d) =>
        Random.Range(Mathf.Max(d.minAmount, 1), Mathf.Max(d.maxAmount, Mathf.Max(d.minAmount, 1)) + 1);

    /// <summary>How many truck objects exist. Should settle at slots + 1 and stop growing.</summary>
    public int PoolSize => _pool.Count;
}
