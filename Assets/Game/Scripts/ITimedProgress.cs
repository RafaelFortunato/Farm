namespace Farm
{
    /// <summary>
    /// Something the player set running and is now waiting on: a growing crop, a cooking stove.
    ///
    /// Exists so the countdown ring can hang over any of them without knowing what it is counting.
    /// PlotTimer finds this on a parent instead of being wired to one concrete type, which is what
    /// let the stove reuse the ring rather than grow a second copy of it.
    /// </summary>
    public interface ITimedProgress
    {
        /// <summary>True while the clock is running, so the ring knows whether to show itself.</summary>
        bool InProgress { get; }

        /// <summary>0..1 through that clock.</summary>
        float Progress { get; }
    }
}
