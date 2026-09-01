using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's money, seed bag and produce crate.
///
/// Seeds and produce are tracked in separate bags because one CropDef names both ends of
/// the loop: a corn seed is what you plant, a corn is what you harvest. Keeping them apart
/// means buying seeds can never be confused with having grown something.
///
/// Static because there is exactly one player and a 20-minute session; it will grow into
/// the GameState described in the concept doc.
/// </summary>
public static class Inventory
{
    public const int StartingCoins = 30;

    static readonly Dictionary<CropDef, int> SeedBag = new Dictionary<CropDef, int>();
    // Produce is keyed by ItemDef, not CropDef: eggs, milk and cake are held the same way a
    // harvested carrot is. Seeds stay CropDef - only something you plant can have a seed.
    static readonly Dictionary<ItemDef, int> ProduceCrate = new Dictionary<ItemDef, int>();
    static readonly Dictionary<ItemDef, int> LifetimeCrate = new Dictionary<ItemDef, int>();

    public static int Coins { get; private set; } = StartingCoins;

    /// <summary>Raised whenever coins, seeds or produce change, so UI can refresh.</summary>
    public static event Action Changed;

    // --- seeds: bought at the store, spent by planting ---

    public static int SeedCount(CropDef crop) => Count(SeedBag, crop);

    public static void AddSeeds(CropDef crop, int amount)
    {
        if (crop == null || amount <= 0) return;
        SeedBag.TryGetValue(crop, out int n);
        SeedBag[crop] = n + amount;
        Changed?.Invoke();
    }

    /// <summary>Spends one seed to plant it. False when the bag is empty.</summary>
    public static bool TryUseSeed(CropDef crop)
    {
        if (crop == null) return false;
        SeedBag.TryGetValue(crop, out int n);
        if (n <= 0) return false;

        SeedBag[crop] = n - 1;
        Changed?.Invoke();
        return true;
    }

    // --- produce: what a finished plot yields ---

    public static int ProduceCount(ItemDef item) => Count(ProduceCrate, item);

    public static void AddProduce(ItemDef item, int amount)
    {
        if (item == null || amount == 0) return;
        ProduceCrate.TryGetValue(item, out int n);
        ProduceCrate[item] = n + amount;

        // Tallied separately from the crate, which goes down again when things are spent.
        // The end screen wants "how many cakes did you bake", not "how many are left over".
        if (amount > 0)
        {
            LifetimeCrate.TryGetValue(item, out int total);
            LifetimeCrate[item] = total + amount;
        }

        Changed?.Invoke();
    }

    /// <summary>Everything of this kind the farm has ever produced, spent or not.</summary>
    public static int LifetimeProduced(ItemDef item) => Count(LifetimeCrate, item);

    /// <summary>Hands produce over to fill an order. False when the crate is short.</summary>
    public static bool TrySpendProduce(ItemDef item, int amount)
    {
        if (item == null || amount <= 0) return false;

        ProduceCrate.TryGetValue(item, out int n);
        if (n < amount) return false;

        ProduceCrate[item] = n - amount;
        Changed?.Invoke();
        return true;
    }

    // --- coins ---

    public static bool TrySpend(int coins)
    {
        if (coins > Coins) return false;
        Coins -= coins;
        Changed?.Invoke();
        return true;
    }

    public static void AddCoins(int coins)
    {
        Coins += coins;
        Changed?.Invoke();
    }

    static int Count<T>(Dictionary<T, int> bag, T key) where T : ScriptableObject
    {
        if (key == null) return 0;
        return bag.TryGetValue(key, out int n) ? n : 0;
    }

    /// <summary>Clears everything. Called on a fresh run so play-mode state never leaks.</summary>
    public static void Reset(int startingCoins = StartingCoins)
    {
        SeedBag.Clear();
        ProduceCrate.Clear();
        LifetimeCrate.Clear();
        Coins = startingCoins;
        Changed?.Invoke();
    }

    // Static state survives play-mode exit when domain reload is disabled, so start clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        SeedBag.Clear();
        ProduceCrate.Clear();
        LifetimeCrate.Clear();
        Coins = StartingCoins;
        Changed = null;
    }
}
