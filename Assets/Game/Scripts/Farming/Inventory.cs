using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minimal item and coin store. Static because there is exactly one player and a
/// 20-minute session; it will grow into the GameState described in the concept doc.
/// </summary>
public static class Inventory
{
    static readonly Dictionary<CropDef, int> Items = new Dictionary<CropDef, int>();

    public static int Coins { get; private set; } = 30;

    /// <summary>Raised whenever coins or any item count changes, so UI can refresh.</summary>
    public static event Action Changed;

    public static int CountOf(CropDef crop)
    {
        if (crop == null) return 0;
        return Items.TryGetValue(crop, out int n) ? n : 0;
    }

    public static void Add(CropDef crop, int amount)
    {
        if (crop == null || amount == 0) return;
        Items.TryGetValue(crop, out int n);
        Items[crop] = n + amount;
        Changed?.Invoke();
    }

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

    /// <summary>Clears everything. Called on a fresh run so play-mode state never leaks.</summary>
    public static void Reset(int startingCoins = 30)
    {
        Items.Clear();
        Coins = startingCoins;
        Changed?.Invoke();
    }

    // Static state survives play-mode exit when domain reload is disabled, so start clean.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Items.Clear();
        Coins = 30;
        Changed = null;
    }
}
