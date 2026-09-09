using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's money and produce crate.
///
/// There is no seed bag: planting is free and unlimited, and which crops are available is a
/// question for the farmhouse level rather than for anything the player has to carry. What is
/// held here is only what the farm has made and not yet sold.
///
/// Static because there is exactly one player and a 20-minute session; it will grow into
/// the GameState described in the concept doc.
/// </summary>
public static class Inventory
{
    public const int StartingCoins = 30;

    // Keyed by ItemDef rather than CropDef: an egg, a bottle of milk and a cake are held the
    // same way a harvested carrot is, and nothing here cares whether it was grown.
    static readonly Dictionary<ItemDef, int> ProduceCrate = new Dictionary<ItemDef, int>();
    static readonly Dictionary<ItemDef, int> LifetimeCrate = new Dictionary<ItemDef, int>();

    public static int Coins { get; private set; } = StartingCoins;

    /// <summary>Raised whenever coins or produce change, so UI can refresh.</summary>
    public static event Action Changed;

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
        RunStats.RecordCoins(coins);   // every payout passes here, so the run total cannot miss one
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
        ProduceCrate.Clear();
        LifetimeCrate.Clear();
        Coins = startingCoins;
        RunStats.Reset();              // a fresh crate means a fresh run, so the tally goes too
        Changed?.Invoke();
    }

    // Static state survives play-mode exit when domain reload is disabled, so start clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        ProduceCrate.Clear();
        LifetimeCrate.Clear();
        Coins = StartingCoins;
        Changed = null;
    }
}
