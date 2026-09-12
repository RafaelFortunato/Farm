using TMPro;
using UnityEngine;
using Farm.Farming;

namespace Farm.Selling
{
    /// <summary>
    /// One truck waiting in line with an order to fill.
    ///
    /// The wanted item is shown as its icon on the badge over the cab. It used to be the item's
    /// own 3D model, spinning, instantiated once per item and pooled - which meant every cooked
    /// dish needed a world model built for it that nothing else used. Trucks only ask for cooked
    /// goods now, so a sprite says the same thing, costs one field, and lets the dishes be 2D.
    ///
    /// A truck pays several times what the shop does, and it will not wait forever. The clock it
    /// runs while parked is exposed as ITimedProgress, so the same ring that counts a growing crop
    /// and a cooking stove counts this too - see PlotTimer.
    /// </summary>
    public class TruckOrder : MonoBehaviour, ITimedProgress
    {
        [Header("Driver")]
        [Tooltip("The cat in the seat. Its animator is told whether the truck is moving, so it " +
                 "drives on the way in and out and sits while parked at the counter. Optional - a " +
                 "truck with no driver wired just has nothing to tell.")]
        public Animator driver;

        [Header("Badge")]
        [Tooltip("Root of the floating order badge, billboarded to the camera.")]
        public Transform badge;
        [Tooltip("Shows the wanted item's icon. A sprite rather than the item's 3D model: trucks " +
                 "only ask for cooked goods, and a dish needs no world model of its own just to be " +
                 "pictured here.")]
        public UnityEngine.UI.Image icon;

        [Tooltip("How many of the item this truck wants, drawn beside the icon.")]
        public TextMeshProUGUI countLabel;

        /// <summary>What this truck is buying. Set once by Configure when the order is rented.</summary>
        public ItemDef Wanted { get; private set; }
        /// <summary>How many of it. Set alongside Wanted, and never changed while the truck waits.</summary>
        public int Amount { get; private set; }

        /// <summary>
        /// Coins paid when this order is filled: the shop price marked up by what this driver is
        /// willing to pay. Kept a pure function of the order so a truck coming back out of the pool
        /// cannot carry the last one's price - Configure is the only thing that sets any of it.
        /// </summary>
        public int Reward => Wanted != null ? Mathf.RoundToInt(Wanted.sellValue * _payoff) * Amount : 0;

        /// <summary>True once the truck has finished rolling to its slot.</summary>
        public bool Arrived => !_moving;

        /// <summary>True while this truck is counting down its patience at the counter.</summary>
        public bool Waiting => _waiting;

        /// <summary>True once it has waited long enough and should pull away unserved.</summary>
        public bool OutOfPatience => _waiting && Time.time >= _waitUntil;

        /// <summary>
        /// Seconds before the driver gives up. Zero while the truck is still rolling in, because the
        /// clock does not start until it parks - counting down during the drive would promise the
        /// player less time than they are actually going to get.
        /// </summary>
        public float SecondsLeft => _waiting ? Mathf.Max(0f, _waitUntil - Time.time) : 0f;

        // ITimedProgress - what PlotTimer reads to draw the ring over the cab
        public bool InProgress => _waiting;
        public float Progress => _waiting ? Mathf.InverseLerp(_waitFrom, _waitUntil, Time.time) : 0f;

        Transform _tf;
        Transform _cam;
        Vector3 _target;
        float _speed;
        bool _moving;
        float _payoff = 1f;
        float _waitFrom;
        float _waitUntil;
        bool _waiting;

        // OnEnable, not Awake: GameManager only guarantees its references from OnEnable onward,
        // and a pooled truck re-enables on every rent, so this is the natural home for it too.
        void OnEnable()
        {
            _tf = transform;
            _cam = GameManager.CameraTransform;
        }

        /// <summary>
        /// Give this truck an order. Safe to call repeatedly as it is recycled - and the one place
        /// every piece of per-order state is set, so nothing can leak between rents.
        /// </summary>
        public void Configure(ItemDef crop, int amount, float payoff)
        {
            Wanted = crop;
            Amount = amount;
            _payoff = payoff;
            _waiting = false;

            // A pooled truck is re-rented for a fresh order; without this it would come back still
            // playing whatever state the last trip left it in.
            SetDriverMoving(false);

            if (countLabel != null) countLabel.text = "x" + amount;

            if (icon != null)
            {
                icon.sprite = crop != null ? crop.icon : null;
                icon.enabled = icon.sprite != null;   // a missing icon would draw as a white box
            }
        }

        /// <summary>Roll to a spot on the road. The truck drives itself the rest of the way.</summary>
        public void MoveTo(Vector3 position, float speed, bool instant = false)
        {
            _target = position;
            _speed = speed;
            if (instant)
            {
                transform.position = position;
                _moving = false;
            }
            else _moving = true;

            SetDriverMoving(_moving);
        }

        /// <summary>
        /// Tells the cat whether it is driving or parked.
        ///
        /// Called at the two moments the answer changes - setting off, and arriving - rather than
        /// every frame from Update. The animator only cares about the edges, and a truck spends
        /// almost all of its visit stationary, so a per-frame write would be the same value
        /// thousands of times over for the sake of two transitions.
        ///
        /// The parameter is set by hash and guarded on the animator existing, so a truck body with
        /// no driver wired is simply quiet rather than throwing.
        /// </summary>
        void SetDriverMoving(bool moving)
        {
            if (driver != null) driver.SetBool(DrivingHash, moving);
        }

        static readonly int DrivingHash = Animator.StringToHash("Driving");

        public void ShowBadge(bool visible)
        {
            if (badge != null && badge.gameObject.activeSelf != visible) badge.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Start the patience clock. Called when the truck reaches the counter rather than when it
        /// spawns, so the drive in does not eat into the window the player actually gets.
        /// </summary>
        public void BeginWait(float seconds)
        {
            _waitFrom = Time.time;
            _waitUntil = Time.time + Mathf.Max(seconds, 0.1f);
            _waiting = true;
        }

        /// <summary>Stop the clock, whether the order was filled or the driver gave up.</summary>
        public void EndWait() => _waiting = false;

        void Update()
        {
            float dt = Time.deltaTime;

            if (_moving)
            {
                _tf.position = Vector3.MoveTowards(_tf.position, _target, _speed * dt);
                if ((_tf.position - _target).sqrMagnitude < 0.0004f)
                {
                    _tf.position = _target;
                    _moving = false;
                    SetDriverMoving(false);      // arrived; the cat settles back into the seat
                }
            }

            if (badge != null && badge.gameObject.activeSelf)
            {
                badge.rotation = _cam.rotation;
            }
        }
    }
}
