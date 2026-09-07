using System;
using UnityEngine;

/// <summary>
/// The farmhouse, and the spine of the game's progression.
///
/// Each upgrade costs coins and, from the middle of the ladder on, actual produce: eggs, then
/// bread, then cakes. That is deliberate - paying in cooked goods forces the player to run the
/// pipeline they built rather than grind carrots, which is the arc the whole game is about.
/// Reaching the top level wins.
///
/// Every price is payable with what the level below it can already make. An earlier ladder
/// asked for cakes at Lv4 when the cake recipe only arrived AT Lv4, which made the game
/// unwinnable; anything added here has to be checked against the level that must pay for it.
///
/// Its level is also the one number the rest of the farm reads: the stove's recipe tier and
/// what the trucks ask for both follow it, so the world opens up in step instead of each
/// system keeping its own idea of how far along the player is.
/// </summary>
public class MainBuilding : Interactable
{
    [Header("Ladder")]
    [Tooltip("What each step up costs. An asset rather than a list on this component, so the " +
             "ladder sits beside the crops and recipes it has to stay in balance with.")]
    public FarmhouseLadder ladder;

    [Header("Wiring")]
    public FarmExpansion expansion;

    [Tooltip("Recipe tier follows the farmhouse.")]
    public Stove stove;

    [Tooltip("What the trucks ask for follows the farmhouse.")]
    public TruckQueue truckQueue;

    [Tooltip("Which crops can be planted follows the farmhouse.")]
    public SeedMenu cropMenu;

    [Tooltip("Which foraging spots are in play follows the farmhouse, so growing the island " +
             "opens the new ground for mushrooms as well.")]
    public MushroomPatch mushroomPatch;

    [Header("Player action")]
    [Tooltip("Beat the player performs when upgrading. Leave empty to upgrade instantly.")]
    public CharacterAction upgradeAction;

    [Header("Runtime (read-only)")]
    [SerializeField] int _level = 1;

    /// <summary>Raised with the new level whenever the farmhouse goes up.</summary>
    public static event Action<int> Changed;

    public int Level => _level;

    /// <summary>The level that wins the game.</summary>
    public int MaxLevel => ladder != null ? ladder.Count + 1 : 1;

    public bool IsMaxLevel => _level >= MaxLevel;

    /// <summary>The next step up, or null once the farmhouse is finished.</summary>
    public FarmhouseLadder.Step Next => IsMaxLevel ? null : ladder.steps[_level - 1];

    public override bool CanInteract => !IsMaxLevel;

    // Recomputed only when the level or affordability changes, so the prompt never allocates
    // per frame and the world badge refreshes at exactly the right moments.
    public override int StateKey => _stateKey;

    public override string Prompt => _prompt;

    string _prompt = string.Empty;
    int _stateKey;
    Action _onUpgraded;

    protected override void OnEnable()
    {
        base.OnEnable();
        Apply();            // the world must match the serialised level from the first frame

        // The prompt depends on the level and the inventory and nothing else, so it is rebuilt
        // when the inventory says so rather than polled every frame. An earlier version cached
        // on "can afford" alone and went stale the moment the reason changed - coins arriving
        // while the eggs were still missing left it saying "Need 450 coins".
        Inventory.Changed += Refresh;
        Refresh();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Inventory.Changed -= Refresh;
    }

    /// <summary>True when the player can pay for the next step right now.</summary>
    public bool CanAfford()
    {
        var next = Next;
        if (next == null) return false;
        if (Inventory.Coins < next.coinCost) return false;

        if (next.alsoNeeds != null)
            foreach (var need in next.alsoNeeds)
                if (!need.InStock) return false;

        return true;
    }

    /// <summary>
    /// Rebuild the prompt. The state key moves only when the wording actually changes, so the
    /// world badge refreshes when the player would see something different and not otherwise.
    /// </summary>
    void Refresh()
    {
        string next = BuildPrompt(CanAfford());
        if (next == _prompt) return;

        _prompt = next;
        _stateKey++;
    }

    /// <summary>Names the first thing standing in the way, so the prompt reads as an instruction.</summary>
    string BuildPrompt(bool affordable)
    {
        var next = Next;
        if (next == null) return string.Empty;
        if (affordable) return "Upgrade to Lv" + (_level + 1);
        if (Inventory.Coins < next.coinCost) return "Need " + next.coinCost + " coins";

        if (next.alsoNeeds != null)
            foreach (var need in next.alsoNeeds)
                if (!need.InStock)
                    return "Need " + need.count + " " + (need.item != null ? need.item.displayName : "?");

        return "Upgrade";
    }

    public override void Interact(PlayerInteractor interactor)
    {
        if (!CanAfford()) return;

        // cached delegate rather than a lambda, the way the sell counter does it
        _onUpgraded ??= Upgraded;
        interactor.Controller.BeginAction(upgradeAction, transform, _onUpgraded);
    }

    void Upgraded()
    {
        var next = Next;

        // re-checked rather than trusted: the beat takes real time, and a truck could have
        // been paid or a recipe started in the middle of it
        if (next == null || !CanAfford()) return;

        Inventory.TrySpend(next.coinCost);
        if (next.alsoNeeds != null)
            foreach (var need in next.alsoNeeds)
                Inventory.TrySpendProduce(need.item, need.count);

        _level++;
        Apply();
        Refresh();
        Changed?.Invoke(_level);

        if (IsMaxLevel) Win();
    }

    /// <summary>
    /// Push the current level out to everything that follows it. Called on enable as well as on
    /// upgrade, so the world is never out of step with the number.
    /// </summary>
    void Apply()
    {
        if (expansion != null) expansion.ApplyUpTo(_level);
        if (stove != null) stove.level = _level;
        if (truckQueue != null) truckQueue.level = _level;
        if (cropMenu != null) cropMenu.level = _level;
        if (mushroomPatch != null) mushroomPatch.level = _level;
    }

    void Win()
    {
        var panel = UIManager.Win;
        if (panel == null) return;

        panel.Show(Time.timeSinceLevelLoad, Inventory.Coins, CakesBaked());
    }

    /// <summary>
    /// The headline number for the end screen. Read off the last upgrade's own price, so this
    /// does not need a second hard reference to the cake asset just to count them.
    /// </summary>
    int CakesBaked()
    {
        if (ladder == null || ladder.Count == 0) return 0;

        var last = ladder.steps[ladder.Count - 1];
        if (last.alsoNeeds == null) return 0;

        int most = 0;
        foreach (var need in last.alsoNeeds)
            if (need.item != null) most = Mathf.Max(most, Inventory.LifetimeProduced(need.item));
        return most;
    }
}
