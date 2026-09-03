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

    [Tooltip("Seconds to make one.")]
    [Min(0.1f)] public float regrowSeconds = 30f;

    [Tooltip("How many it banks before it stops making more, so a player who wanders off " +
             "loses the surplus rather than coming back to an unbounded pile.")]
    [Min(1)] public int capacity = 3;

    [Header("Player action")]
    [Tooltip("Beat the player performs when collecting. Leave empty to collect instantly.")]
    public CharacterAction collectAction;
}
