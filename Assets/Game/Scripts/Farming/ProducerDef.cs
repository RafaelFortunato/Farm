using UnityEngine;

/// <summary>
/// What a kind of producer makes, how fast, and how much it banks.
///
/// Shared data rather than per-instance data: both chickens read one asset and every apple tree
/// reads another, so retuning egg or apple timing is a single edit instead of hunting down each
/// bird and trunk in the scene. The Producer component keeps only what is genuinely
/// per-instance - its badge, its body, its current store.
/// </summary>
[CreateAssetMenu(fileName = "Producer_", menuName = "Farm/Producer Definition")]
public class ProducerDef : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Producer";

    [Header("Produce")]
    public ItemDef produces;

    [Tooltip("Shortest wait to make one, in seconds.")]
    [Min(0.1f)] public float regrowMinSeconds = 25f;

    [Tooltip("Longest wait to make one, in seconds. The wait is rolled fresh between the two " +
             "every time, so a stand of trees stops ripening in lockstep. Set this equal to the " +
             "minimum for a fixed tick.")]
    [Min(0.1f)] public float regrowMaxSeconds = 35f;

    /// <summary>A fresh wait. Rolled per item, so no two are ever due on the same beat.</summary>
    public float RollRegrow()
    {
        // Tolerating a max below the min rather than trusting the inspector: the two are typed
        // independently, and a half-finished edit should not freeze a producer or spam it.
        float lo = Mathf.Max(regrowMinSeconds, 0.1f);
        return Random.Range(lo, Mathf.Max(regrowMaxSeconds, lo));
    }

    /// <summary>Middle of the range, for anything measuring the economy rather than running it.</summary>
    public float AverageRegrow => Mathf.Max((regrowMinSeconds + regrowMaxSeconds) * 0.5f, 0.1f);

    [Tooltip("How many it banks before it stops making more, so a player who wanders off " +
             "loses the surplus rather than coming back to an unbounded pile.")]
    [Min(1)] public int capacity = 3;

    [Header("Player action")]
    [Tooltip("Beat the player performs when collecting. Leave empty to collect instantly.")]
    public CharacterAction collectAction;
}
