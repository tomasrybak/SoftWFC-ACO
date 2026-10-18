namespace SoftWfcAco;

/// <summary>
/// One tile type. Replaces the thesis TileData ScriptableObject; how a tile is rendered
/// is up to the consumer, which can look it up by Name.
/// </summary>
public sealed class TileDef
{
    public required string Name { get; init; }

    /// <summary>Socket names in order N, E, S, W. Neighbours fit when facing sockets are equal.</summary>
    public required string[] Sockets { get; init; }

    public float Weight { get; init; } = 1f;
    public bool Walkable { get; init; } = true;

    /// <summary>When true, the tile set contains all four 90° clockwise rotations of this tile.</summary>
    public bool Rotatable { get; init; }
}
