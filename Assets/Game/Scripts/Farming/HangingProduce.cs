using UnityEngine;

/// <summary>
/// Shows a producer's stock as real objects hanging on it, instead of a card floating overhead.
///
/// An apple tree holding two apples has two apples in its branches - the player reads the state
/// off the world rather than off a HUD element, and the tree still looks like a tree from across
/// the farm.
///
/// Where each piece hangs is worked out at load rather than authored into the prefab, because a
/// fixed spot has to be wrong on some tree: trees are planted at whatever angle looks good, and
/// an authored offset turns with them, round to the back where the player never sees it. So the
/// fruit are scattered over the front of the canopy instead - front meaning whichever way the
/// camera actually faces, asked of it rather than written down - and no two trees fruit alike.
///
/// The twinkle is what makes them read as *collectable* rather than as decoration. It fires on
/// one ripe piece at a time, at an irregular interval, because a steady pulse on every fruit at
/// once looks mechanical.
/// </summary>
[RequireComponent(typeof(Producer))]
public class HangingProduce : MonoBehaviour
{
    [Tooltip("One per item the producer can bank. Any spare entries past the producer's " +
             "capacity simply never switch on.")]
    public GameObject[] fruit;

    [Tooltip("Parent of the fruit, squared up to the world at load so a turned tree cannot carry " +
             "its fruit round the back. Required - the scatter needs a frame to work in.")]
    public Transform fruitAnchor;

    [Header("Canopy")]
    [Tooltip("Height range the profile below covers, measured up from the anchor in its own " +
             "units: the bottom of the foliage to the tip of the crown.")]
    public Vector2 canopyRange = new Vector2(1f, 2.35f);

    [Tooltip("How wide the foliage is across that range, bottom to top. Ray-cast off the tree " +
             "mesh rather than eyeballed - re-measure if the model or its scale changes.")]
    public float[] canopyRadius = { 0.78f, 0.86f, 0.94f, 0.99f, 0.98f, 0.91f, 0.87f, 0.86f, 0.78f, 0.55f };

    [Tooltip("How far round the front a piece may hang, in degrees either side of the camera.")]
    [Range(10f, 80f)] public float frontSpread = 52f;

    [Tooltip("How far above and below the middle of the canopy a piece may hang, in degrees. " +
             "This is what spreads them up the tree instead of leaving them in a row round the " +
             "waist of it; keep the low end off the underside, which the camera looks over.")]
    public Vector2 riseRange = new Vector2(-8f, 34f);

    [Tooltip("How far a piece stands proud of the leaves, toward the camera, in the same units. " +
             "Resting exactly on the surface is not enough: the camera meets the flanks of the " +
             "canopy at a glance, and half of a piece sitting there is behind foliage.")]
    public float standProud = 0.22f;

    [Header("Twinkle")]
    [Tooltip("Star burst moved onto a ripe piece and played. Optional.")]
    public ParticleSystem sparkle;

    [Tooltip("Shortest wait between twinkles, in seconds.")]
    [Min(0.1f)] public float sparkleMinSeconds = 2.5f;

    [Tooltip("Longest wait between twinkles, in seconds. Rolled fresh between the two every " +
             "time. Set this equal to the minimum for a steady pulse.")]
    [Min(0.1f)] public float sparkleMaxSeconds = 5f;

    /// <summary>A fresh gap between twinkles. Tolerates a max typed below the min.</summary>
    float RollSparkle()
    {
        float lo = Mathf.Max(sparkleMinSeconds, 0.1f);
        return Random.Range(lo, Mathf.Max(sparkleMaxSeconds, lo));
    }

    Producer _producer;
    int _shown = -1;              // impossible count, so the first frame always applies
    float _nextSparkle;

    void Awake() => _producer = GetComponent<Producer>();

    void OnEnable()
    {
        if (_producer == null) _producer = GetComponent<Producer>();

        Scatter();
        _shown = -1;
        _nextSparkle = Time.time + RollSparkle();
    }

    /// <summary>
    /// Hang the fruit somewhere fresh on the front of the canopy. Also on the component menu, so
    /// an arrangement can be re-rolled in the editor and looked at.
    /// </summary>
    [ContextMenu("Scatter Fruit")]
    public void Scatter()
    {
        if (fruitAnchor == null || fruit == null || fruit.Length == 0) return;
        if (canopyRadius == null || canopyRadius.Length < 2) return;

        // Square the anchor to the world before anything else. Everything below is worked out in
        // world directions, and this is what stops the tree's own angle reaching the fruit.
        fruitAnchor.localPosition = Vector3.zero;
        fruitAnchor.rotation = Quaternion.identity;

        var cam = Camera.main;
        var toCam = cam != null ? -cam.transform.forward : Vector3.back;
        var flat = new Vector3(toCam.x, 0f, toCam.z);
        float facing = flat.sqrMagnitude > 1e-4f
                     ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg
                     : 180f;

        var heart = new Vector3(0f, Mathf.Lerp(canopyRange.x, canopyRange.y, 0.5f), 0f);
        int n = fruit.Length;
        var wedge = Shuffled(n);
        var tier = Shuffled(n);

        for (int i = 0; i < n; i++)
        {
            if (fruit[i] == null) continue;

            // A wedge of the front and a tier of the height each, drawn from two independent
            // shuffles and jittered within: three pieces never crowd, and a part-picked tree is
            // not always left holding the same corner.
            float az = facing - frontSpread + (wedge[i] + Random.Range(0.2f, 0.8f)) * (frontSpread * 2f / n);
            float rise = Mathf.Lerp(riseRange.x, riseRange.y, (tier[i] + Random.Range(0.15f, 0.85f)) / n);

            float a = az * Mathf.Deg2Rad, e = rise * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));

            // Walk out of the middle of the canopy until the leaves run out, and hang it there.
            // Seating by height and width alone only works round the waist of the tree - it
            // strands a piece in mid-air off the crown and buries one under the skirt.
            float reach = 0f;
            for (int k = 1; k <= 48; k++)
            {
                float d = k * 0.03f;
                if (!InLeaves(heart + dir * d)) break;
                reach = d;
            }

            var t = fruit[i].transform;
            t.localPosition = heart + dir * reach + toCam * standProud;
            t.localRotation = Quaternion.Euler(Random.Range(-15f, 15f),
                                               Random.Range(0f, 360f),
                                               Random.Range(-15f, 15f));
        }
    }

    /// <summary>0..n-1 in a random order, so the pieces take their slots unpredictably.</summary>
    static int[] Shuffled(int n)
    {
        var slots = new int[n];
        for (int i = 0; i < n; i++) slots[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }
        return slots;
    }

    /// <summary>True while a point is still inside the foliage.</summary>
    bool InLeaves(Vector3 p)
    {
        if (p.y < canopyRange.x || p.y > canopyRange.y) return false;

        float r = RadiusAt(Mathf.InverseLerp(canopyRange.x, canopyRange.y, p.y));
        return p.x * p.x + p.z * p.z < r * r;
    }

    /// <summary>How wide the foliage is at a height up the canopy, 0 at the bottom and 1 at the top.</summary>
    float RadiusAt(float up)
    {
        if (canopyRadius.Length == 1) return canopyRadius[0];

        float f = Mathf.Clamp01(up) * (canopyRadius.Length - 1);
        int i = Mathf.Min((int)f, canopyRadius.Length - 2);
        return Mathf.Lerp(canopyRadius[i], canopyRadius[i + 1], f - i);
    }

    void Update()
    {
        int stored = _producer.Stored;

        // only touch the fruit when the count actually moves
        if (stored != _shown)
        {
            _shown = stored;
            for (int i = 0; i < fruit.Length; i++)
            {
                if (fruit[i] == null) continue;
                bool want = i < stored;
                if (fruit[i].activeSelf != want) fruit[i].SetActive(want);
            }
        }

        if (sparkle == null || stored <= 0 || Time.time < _nextSparkle) return;

        int ripe = Mathf.Min(stored, fruit.Length);
        var pick = fruit[Random.Range(0, ripe)];
        if (pick != null)
        {
            sparkle.transform.position = pick.transform.position;
            sparkle.Play();
        }

        _nextSparkle = Time.time + RollSparkle();
    }
}
