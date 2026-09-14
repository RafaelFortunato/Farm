using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Farm.Audio;
using Farm.UI;

namespace Farm.Selling
{
    /// <summary>
    /// The trucks that call at the farm gate.
    ///
    /// A truck is an event, not a fixture. One rolls up, waits a short while for its order to be
    /// filled, and pulls away again whether it was served or not - and then the road is empty for
    /// most of a minute before the next. That gap is the point: a truck pays several times what the
    /// shop does, so it has to be something the player looks up for rather than a counter that is
    /// always sitting there.
    ///
    /// Bounded in instances as well as in time: only as many trucks as there are visible slots
    /// (plus the ones currently driving off) can ever exist, so they are pooled and recycled rather
    /// than spawned and destroyed. After the first lap the pool stops growing and the feature
    /// allocates nothing per sale.
    ///
    /// All the tuning - the order table, the payout, the arrival gap, the patience - lives in a
    /// <see cref="TruckSettings"/> asset. This component holds only what belongs to the scene.
    /// </summary>
    public class TruckQueue : MonoBehaviour
    {
        [Header("Tuning")]
        [Tooltip("What trucks ask for, what they pay, how often they come and how long they wait.")]
        [SerializeField] TruckSettings settings;

        [Header("Sound")]
        [Tooltip("A truck rolling up to the counter. Optional.")]
        [SerializeField] SoundEvent arriveSound;
        [Tooltip("A truck pulling away, served or not. Optional.")]
        [SerializeField] SoundEvent departSound;

        [Tooltip("The horn, the moment the truck parks and its clock starts. Separate from " +
                 "arriveSound, which covers the drive in: this one is the driver announcing they " +
                 "are here and waiting, so it marks the start of the window rather than the " +
                 "approach. Optional.")]
        [SerializeField] SoundEvent hornSound;

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

        [Header("State")]
        [Tooltip("How far the farm has come. Drives which goods trucks ask for, and how often. Set " +
                 "by the farmhouse as it levels up, so this is live state rather than tuning.")]
        public int level = 1;

        /// <summary>The tuning this queue runs on.</summary>
        public TruckSettings Settings => settings;

        /// <summary>The truck at the counter, or null while the road is empty.</summary>
        public TruckOrder Front => _queue.Count > 0 ? _queue[0] : null;

        /// <summary>Bumped every time the front order changes, so UI can tell it went stale.</summary>
        public int OrderVersion { get; private set; }

        /// <summary>Seconds until the next truck is due, for anything that wants to say so.</summary>
        public float NextArrivalIn => Mathf.Max(0f, _nextArrival - Time.time);

        readonly List<TruckOrder> _pool = new List<TruckOrder>();
        readonly List<TruckOrder> _queue = new List<TruckOrder>();
        readonly List<TruckOrder> _leaving = new List<TruckOrder>();

        float _nextArrival;

        // The border arrow. Held rather than looked up per frame, and read in OnEnable the way
        // PlayerInteractor reads the interact prompt - it is saved disabled, so only a wired
        // reference can reach it.
        OffscreenMarker _marker;

        // Hard ceiling on trucks mid-departure. Without it a player selling faster than a truck
        // can drive off would keep renting new bodies and the pool would grow without limit,
        // which defeats the point of pooling. Worst case the oldest one is recycled early -
        // it is already off past the counter by then.
        const int MaxLeaving = 2;

        void OnEnable()
        {
            // Every tick reads the settings, so a road with none wired would throw once a frame.
            // Better to say so once and stand down.
            if (settings == null)
            {
                Debug.LogError("TruckQueue has no TruckSettings assigned - trucks are disabled.", this);
                enabled = false;
                return;
            }

            // rebuilt here rather than Awake so a domain reload during play restores the line
            if (_queue.Count == 0) StartCoroutine(FillAfterDelay());
            _nextArrival = Time.time + settings.RollGap();
            _marker = UIManager.TruckPointer;
        }

        /// <summary>
        /// Puts the opening truck on the road, a beat late.
        ///
        /// A coroutine rather than a countdown ticked in Update: this happens once in a run, and a
        /// comparison every frame forever to catch a single moment two seconds in is work the game
        /// pays for the whole session. It also stops itself - disabling the road cancels the
        /// coroutine, so a truck cannot appear on a road that has been switched off again.
        ///
        /// The queue is re-checked after the wait, not just before it. Two seconds is long enough
        /// for the regular arrival clock to have produced a truck on its own in some future tuning,
        /// and two trucks for one slot would be a worse bug than a missing one.
        /// </summary>
        IEnumerator FillAfterDelay()
        {
            if (settings.firstArrivalDelay > 0f) yield return new WaitForSeconds(settings.firstArrivalDelay);
            if (_queue.Count == 0) Fill(settings.startingTrucks);
        }

        /// <summary>
        /// The road can be switched off - it does not exist until the farm reaches level 3 - and a
        /// marker pointing at a truck that is no longer there would outlive it.
        /// </summary>
        void OnDisable()
        {
            if (_marker != null) _marker.Show(null, null);
        }

        void Update()
        {
            RecycleArrived();
            TickPatience();
            TickArrivals();
        }

        // The marker is aimed in LateUpdate so the camera has already moved this frame; pointing at
        // a truck from where the camera was at the start of the frame leaves the arrow a frame behind.
        void LateUpdate()
        {
            if (_marker == null) return;

            var front = Front;
            if (front == null || front.Wanted == null) { _marker.Show(null, null); return; }

            // A truck still rolling in has not started its patience, so there is no honest number to
            // put on the clock yet - the marker blanks it rather than counting down from nothing.
            _marker.Show(front.transform, front.Wanted, front.Waiting ? front.SecondsLeft : -1f);
        }

        // ---- the clock ----

        /// <summary>
        /// Start the front truck's clock once it has actually parked, and send it on its way when it
        /// runs out. Only the front runs a clock: anything behind it is queueing, not being ignored.
        /// </summary>
        void TickPatience()
        {
            var front = Front;
            if (front == null) return;

            if (!front.Waiting)
            {
                if (front.Arrived)
                {
                    front.BeginWait(settings.patience);

                    // Paired with BeginWait deliberately, not with Arrived. Arrived turns true the
                    // frame the truck reaches its slot and stays true for the rest of its visit, so
                    // sounding the horn on it would mean re-sounding it every frame. This branch
                    // runs exactly once per truck - the next frame it is Waiting and takes the other
                    // path - which makes one horn per arrival structural rather than remembered.
                    AudioManager.PlayAt(hornSound, front.transform.position);
                }
                return;
            }

            if (front.OutOfPatience) Depart();
        }

        /// <summary>Roll a truck in when one is due and there is room at the gate.</summary>
        void TickArrivals()
        {
            if (Time.time < _nextArrival) return;
            if (_queue.Count >= Mathf.Min(settings.maxParked, slots.Length)) return;

            SpawnAtBack();
            _nextArrival = Time.time + settings.RollGap();
        }

        // ---- the line ----

        /// <summary>The trucks already parked when the road opens. These snap in, no convoy.</summary>
        void Fill(int count)
        {
            count = Mathf.Clamp(count, 0, Mathf.Min(settings.maxParked, slots.Length));

            for (int i = 0; i < count; i++)
            {
                var t = Rent();
                var d = PickDemand();
                t.Configure(d.item, TruckSettings.RollAmount(d), settings.RollPayoff());
                t.MoveTo(slots[i].position, settings.driveSpeed, true);
                t.ShowBadge(true);
                _queue.Add(t);
            }
            OrderVersion++;
        }

        /// <summary>A fresh order rolling in from off-screen to the back of the line.</summary>
        void SpawnAtBack()
        {
            if (_queue.Count >= slots.Length) return;

            var t = Rent();
            var d = PickDemand();
            t.Configure(d.item, TruckSettings.RollAmount(d), settings.RollPayoff());
            t.transform.position = spawnPoint.position;

            // At the SLOT rather than the spawn point: the truck starts off-screen and the engine
            // should read as approaching the counter the player is standing at, not as a noise
            // from somewhere out of frame.
            AudioManager.PlayAt(arriveSound, slots[_queue.Count].position);

            t.MoveTo(slots[_queue.Count].position, settings.driveSpeed);
            t.ShowBadge(true);
            _queue.Add(t);
            OrderVersion++;
        }

        /// <summary>
        /// The front truck has been paid: it pulls away and the next quiet stretch begins.
        /// </summary>
        public void Advance() => Depart();

        /// <summary>
        /// The front truck leaves, served or not. Both endings run through here so the pool trim
        /// and the arrival clock cannot be remembered in one path and forgotten in the other.
        /// </summary>
        void Depart()
        {
            if (_queue.Count == 0) return;

            RecycleArrived();

            AudioManager.PlayAt(departSound, _queue[0].transform.position);

            var going = _queue[0];
            _queue.RemoveAt(0);
            going.EndWait();
            going.ShowBadge(false);
            going.MoveTo(exitPoint.position, settings.driveSpeed);
            _leaving.Add(going);

            for (int i = 0; i < _queue.Count; i++)
                _queue[i].MoveTo(slots[i].position, settings.driveSpeed);

            while (_leaving.Count > MaxLeaving)
            {
                Return(_leaving[0]);
                _leaving.RemoveAt(0);
            }

            // the gap is measured from the departure, not from a clock running underneath it
            _nextArrival = Time.time + settings.RollGap();
            OrderVersion++;
        }

        /// <summary>Returns trucks that have finished driving off to the pool.</summary>
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

        /// <summary>A truck body from the pool, or a new one when every existing body is in use.</summary>
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

        /// <summary>Clears a truck's order and parks it back in the pool.</summary>
        void Return(TruckOrder t)
        {
            t.EndWait();
            t.ShowBadge(false);
            t.gameObject.SetActive(false);
        }

        /// <summary>
        /// This entry's pull right now, at the farm's current level. Public so the balance can be
        /// inspected with the same function the queue actually rolls against, rather than a copy.
        /// </summary>
        public int CurrentWeight(in TruckSettings.Demand d) => settings.WeightAt(d, level);

        /// <summary>
        /// Rolls one order. Weighted rather than uniform, and entries the farm has not unlocked
        /// weigh nothing, so a level-1 player is never asked for a cake they cannot bake.
        ///
        /// Nothing checks whether the player can currently fill it. The queue used to, because a
        /// truck was the only buyer and an order out of reach ended the run - but planting is free
        /// now, the shop buys at any hour, and a truck nobody serves drives off by itself. An order
        /// being more than they have on hand is the point: missing it costs them the next quiet
        /// stretch, and that is what makes catching one worth the walk.
        /// </summary>
        TruckSettings.Demand PickDemand()
        {
            var demands = settings.demands;
            if (demands == null || demands.Length == 0) return default;

            int total = 0;
            for (int i = 0; i < demands.Length; i++) total += CurrentWeight(demands[i]);

            // Nothing eligible would mean an empty badge, so fall back to any entry at all.
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

        /// <summary>How many truck objects exist. Should settle and stop growing.</summary>
        public int PoolSize => _pool.Count;
    }
}
