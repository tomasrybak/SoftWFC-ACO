namespace SoftWfcAco;

/// <summary>
/// A concrete rotation of a TileDef with its rotated sockets. This is the unit WFC chooses between.
/// </summary>
public sealed class TileVariant
{
    public TileDef Def { get; }

    /// <summary>Clockwise quarter turns, 0-3.</summary>
    public int Rotation { get; }

    /// <summary>Indexes 0:N, 1:E, 2:S, 3:W.</summary>
    public string[] Sockets { get; }

    public TileVariant(TileDef def, int rotation)
    {
        if (def.Sockets.Length != 4)
            throw new ArgumentException($"Tile '{def.Name}' must have exactly 4 sockets (N, E, S, W).");

        Def = def;
        Rotation = rotation;
        Sockets = new string[4];
        for (int i = 0; i < 4; i++)
            Sockets[i] = def.Sockets[(i - rotation + 4) % 4];
    }

    /// <summary>All variants of a tile set: one per tile, four per rotatable tile.</summary>
    public static List<TileVariant> Expand(IEnumerable<TileDef> defs)
    {
        var variants = new List<TileVariant>();
        foreach (var def in defs)
        {
            int rotations = def.Rotatable ? 4 : 1;
            for (int r = 0; r < rotations; r++)
                variants.Add(new TileVariant(def, r));
        }
        return variants;
    }
}
