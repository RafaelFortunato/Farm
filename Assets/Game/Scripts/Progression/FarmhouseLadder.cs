using UnityEngine;

/// <summary>
/// What each step up the farmhouse costs.
///
/// Held as an asset rather than on the building so the whole economy can be retuned without
/// opening the scene, next to the crops and recipes it has to stay in balance with.
///
/// Coins only. Steps carry an alsoNeeds list and the farmhouse still honours it, but the
/// balance deliberately leaves it empty - see MainBuilding for why. If produce ever goes back
/// on a step, it has to be payable from the level BELOW it: an earlier ladder asked for cakes
/// at the step that unlocked the cake recipe, which made the game unwinnable.
/// </summary>
[CreateAssetMenu(fileName = "FarmhouseLadder", menuName = "Farm/Farmhouse Ladder")]
public class FarmhouseLadder : ScriptableObject
{
    [System.Serializable]
    public class Step
    {
        public int coinCost = 250;

        [Tooltip("Produce this step also consumes, on top of the coins.")]
        public ItemStack[] alsoNeeds;
    }

    [Tooltip("One per step up. Entry 0 is the price of going from level 1 to level 2, so the " +
             "farmhouse tops out one level above the number of entries.")]
    public Step[] steps;

    /// <summary>How many steps up there are. The top level is one more than this.</summary>
    public int Count => steps != null ? steps.Length : 0;
}
