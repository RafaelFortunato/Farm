using UnityEngine;

/// <summary>
/// What the player actually did this run, for the end screen to report.
///
/// Kept apart from Inventory because these are a record, never a resource: nothing in the game
/// reads them to decide anything, and spending must never move them. That distinction is the
/// whole point of coins earned - the end screen used to show the balance, which rewarded
/// hoarding and punished the player who spent everything on the last upgrade, when spending it
/// is exactly what winning looks like.
///
/// Each number is banked at the single choke point its event has to pass through, so nothing
/// can happen without being counted: coins in Inventory.AddCoins, dishes in Stove.Collect,
/// orders in SellCounter.CompleteSale.
///
/// Static for the same reason Inventory is - one player, one session - and reset the same way,
/// so play-mode state cannot leak into the next run when domain reload is switched off.
/// </summary>
public static class RunStats
{
    /// <summary>Every coin that ever arrived this run. Never goes down.</summary>
    public static int CoinsEarned { get; private set; }

    /// <summary>Batches taken off the stove, whatever the recipe was.</summary>
    public static int DishesBaked { get; private set; }

    /// <summary>Orders filled. A truck that gave up and drove off does not count.</summary>
    public static int TruckOrdersFilled { get; private set; }

    /// <summary>
    /// Banked from Inventory.AddCoins, the one place coins arrive. Anything not positive is
    /// ignored: spending is not earning, and a zero-value sale should not read as income.
    /// </summary>
    public static void RecordCoins(int amount)
    {
        if (amount > 0) CoinsEarned += amount;
    }

    public static void RecordDish() => DishesBaked++;

    public static void RecordTruckOrder() => TruckOrdersFilled++;

    public static void Reset()
    {
        CoinsEarned = 0;
        DishesBaked = 0;
        TruckOrdersFilled = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => Reset();
}
