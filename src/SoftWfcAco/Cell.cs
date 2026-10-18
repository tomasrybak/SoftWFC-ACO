namespace SoftWfcAco;

/// <summary>
/// One grid cell in the WFC solver: the set of variants still possible here.
/// </summary>
public sealed class Cell
{
    public GridPos Position { get; }
    public bool IsCollapsed { get; internal set; }
    public List<TileVariant> AvailableVariants { get; }
    public TileVariant? CollapsedVariant { get; internal set; }

    public Cell(GridPos position, List<TileVariant> allVariants)
    {
        Position = position;
        AvailableVariants = new List<TileVariant>(allVariants);
    }

    /// <summary>Number of variants still possible.</summary>
    public int Entropy => AvailableVariants.Count;
}
