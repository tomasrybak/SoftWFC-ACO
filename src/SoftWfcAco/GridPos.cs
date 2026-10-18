namespace SoftWfcAco;

/// <summary>
/// Tile coordinate on the generator grid. X grows east, Z grows north.
/// </summary>
public readonly record struct GridPos(int X, int Z)
{
    public static GridPos operator +(GridPos a, GridPos b) => new(a.X + b.X, a.Z + b.Z);

    public int ManhattanDistance(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Z - other.Z);

    public GridPos Step(Direction dir) => this + dir.Offset();
}

/// <summary>
/// Cardinal directions. Order N, E, S, W matches the WFC socket indices.
/// </summary>
public enum Direction
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

public static class DirectionExtensions
{
    public static GridPos Offset(this Direction dir) => dir switch
    {
        Direction.North => new GridPos(0, 1),
        Direction.East => new GridPos(1, 0),
        Direction.South => new GridPos(0, -1),
        Direction.West => new GridPos(-1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(dir)),
    };

    public static Direction Opposite(this Direction dir) => (Direction)(((int)dir + 2) % 4);
}
