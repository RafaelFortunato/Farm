using UnityEngine;

/// <summary>
/// What a kind of farm animal makes, how fast, and how much it banks.
///
/// Species data rather than per-animal data: both chickens read the same asset, so retuning egg
/// timing is one edit instead of hunting down every bird in the scene. The Animal component
/// keeps only what is genuinely per-instance - its badge, its body, its current store.
/// </summary>
[CreateAssetMenu(fileName = "Animal_", menuName = "Farm/Animal Definition")]
public class AnimalDef : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Animal";

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
