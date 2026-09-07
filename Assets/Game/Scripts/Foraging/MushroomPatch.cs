using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The mushrooms that come up around the island.
///
/// The root every spawn point hangs off, and the only thing that decides when one appears. Spots
/// are authored as child MushroomSpawnPoints; this picks a free one at random every so often and
/// stands a mushroom on it, up to a few at a time. So the island always has something worth a
/// detour, but never a field of them.
///
/// Which spots are in play follows the farmhouse, the same way the stove's recipes and the
/// trucks' orders do: a point marked for a later level simply is not drawn until the island has
/// grown out that far. Expanding the farm therefore opens new foraging ground for free, with no
/// second list to keep in step with the expansion groups.
///
/// Mushrooms are pooled rather than created and destroyed. They come and go for the whole
/// session, and after the first few the patch stops allocating entirely.
/// </summary>
public class MushroomPatch : MonoBehaviour
{
    [Header("Wiring")]
    [Tooltip("What comes up. One prefab - the variety comes from the turn and size it is placed at.")]
    [SerializeField] Mushroom mushroomPrefab;

    [Header("Progression")]
    [Tooltip("How far the farm has come. Set by the farmhouse; decides which spots are in play.")]
    public int level = 1;

    [Header("Appearance")]
    [Tooltip("How many may stand on the island at once, across every spot.")]
    [Min(1)] [SerializeField] int maxAtOnce = 3;

    [Tooltip("How many are already up when the game starts, so the player meets foraging " +
             "without waiting out a first timer.")]
    [SerializeField] int startingMushrooms = 1;

    [Tooltip("The wait before the next one comes up, rolled fresh each time. Counted from the " +
             "moment the last one was picked rather than off a free-running clock, so clearing " +
             "the island quickly is never punished with a shorter wait.")]
    [SerializeField] Vector2 appearEvery = new Vector2(25f, 50f);

    /// <summary>How many are standing right now.</summary>
    public int Live => _live;

    /// <summary>Every authored spot, whatever level it belongs to.</summary>
    public int PointCount => _points != null ? _points.Length : 0;

    readonly List<Mushroom> _pool = new List<Mushroom>();
    MushroomSpawnPoint[] _points;
    Action<Mushroom> _onPicked;
    int _live;
    float _nextAt;

    // Rebuilt in OnEnable rather than Awake: a domain reload during play re-runs OnEnable but not
    // Awake, and it wipes the fields tying a standing mushroom to its spot. Rather than trust
    // half-remembered state, the patch takes everything down and stands its opening set back up.
    void OnEnable()
    {
        _points = GetComponentsInChildren<MushroomSpawnPoint>(true);
        foreach (var p in _points) p.Occupant = null;

        // Adopt whatever is already parented here instead of leaving a second set on top of it.
        _pool.Clear();
        _pool.AddRange(GetComponentsInChildren<Mushroom>(true));
        foreach (var m in _pool)
            if (m != null && m.gameObject.activeSelf) m.gameObject.SetActive(false);

        _live = 0;
        _onPicked ??= Picked;

        for (int i = 0; i < startingMushrooms; i++)
            if (!Spawn()) break;

        _nextAt = Time.time + Roll();
    }

    void Update()
    {
        // Hold the clock while the island is carrying as many as it may. Without this the patch
        // banks time it cannot spend and then drops several the instant one is picked.
        if (_live >= maxAtOnce) { _nextAt = Time.time + Roll(); return; }

        if (Time.time < _nextAt) return;

        // A failed spawn means every unlocked spot is taken; look again shortly rather than
        // burning a whole fresh interval on a state that can change at any moment.
        _nextAt = Time.time + (Spawn() ? Roll() : 1f);
    }

    float Roll() => UnityEngine.Random.Range(appearEvery.x, appearEvery.y);

    /// <summary>Stand one on a free unlocked spot. False when there is nowhere to put it.</summary>
    bool Spawn()
    {
        if (mushroomPrefab == null || _points == null) return false;

        var point = PickOpenPoint();
        if (point == null) return false;

        var m = Rent();
        m.Place(point, _onPicked);
        point.Occupant = m;
        _live++;
        return true;
    }

    /// <summary>
    /// A uniformly random open spot, chosen in one pass. Reservoir sampling rather than building
    /// a shortlist, so picking a spot allocates nothing however many points are authored.
    /// </summary>
    MushroomSpawnPoint PickOpenPoint()
    {
        MushroomSpawnPoint pick = null;
        int seen = 0;

        for (int i = 0; i < _points.Length; i++)
        {
            var p = _points[i];
            if (p == null || !p.IsOpenAt(level)) continue;

            seen++;
            if (UnityEngine.Random.Range(0, seen) == 0) pick = p;
        }
        return pick;
    }

    void Picked(Mushroom m)
    {
        if (m.Point != null && m.Point.Occupant == m) m.Point.Occupant = null;

        m.gameObject.SetActive(false);
        _live = Mathf.Max(0, _live - 1);
        _nextAt = Time.time + Roll();
    }

    Mushroom Rent()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i] != null && !_pool[i].gameObject.activeSelf)
            {
                _pool[i].gameObject.SetActive(true);
                return _pool[i];
            }

        var fresh = Instantiate(mushroomPrefab, transform);
        fresh.name = "Mushroom_" + _pool.Count;
        _pool.Add(fresh);
        return fresh;
    }
}
