/// <summary>
/// Lifecycle of a single soil plot.
///
/// Top-level rather than nested in SoilPlot so UI, save data and future systems can
/// reference it without reaching through the component.
/// </summary>
public enum PlotState
{
    Empty,
    Growing,
    Ready
}
