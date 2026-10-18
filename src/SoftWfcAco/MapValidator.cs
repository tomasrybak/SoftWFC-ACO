namespace SoftWfcAco
{
    /// <summary>
    /// A reason a generated map is not a valid tiling.
    /// </summary>
    public abstract record MapViolation(GridPos Position);

    /// <summary>
    /// The cell has no collapsed variant.
    /// </summary>
    public sealed record UncollapsedCell(GridPos Position) : MapViolation(Position);

    /// <summary>
    /// The cell's socket on Side differs from the neighbour's facing socket.
    /// </summary>
    public sealed record SocketMismatch(GridPos Position, Direction Side, string Socket, string NeighborSocket)
        : MapViolation(Position);

    /// <summary>
    /// Checks a map independently of the solver that produced it,
    /// so a solver bug cannot report an invalid map as a success.
    /// </summary>
    public static class MapValidator
    {
        /// <summary>
        /// Finds all violations in the grid.
        /// </summary>
        /// <param name="grid">The map to check, indexed [x, z]</param>
        /// <returns>All violations; an empty list means the map is valid.</returns>
        public static List<MapViolation> Validate(Cell[,] grid)
        {
            int width = grid.GetLength(0);
            int depth = grid.GetLength(1);
            List<MapViolation> violations = new List<MapViolation>();

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    GridPos pos = new GridPos(x, z);
                    TileVariant? variant = grid[x, z].CollapsedVariant;

                    if (variant == null)
                    {
                        violations.Add(new UncollapsedCell(pos));
                        continue;
                    }

                    // Each shared edge is checked once, from its west or south cell
                    if (x + 1 < width)
                    {
                        CheckEdge(variant, grid[x + 1, z].CollapsedVariant, pos, Direction.East, violations);
                    }

                    if (z + 1 < depth)
                    {
                        CheckEdge(variant, grid[x, z + 1].CollapsedVariant, pos, Direction.North, violations);
                    }
                }
            }

            return violations;
        }

        /// <summary>
        /// Adds a SocketMismatch if the two variants don't fit on the given side.
        /// </summary>
        private static void CheckEdge(TileVariant variant, TileVariant? neighbor, GridPos pos, Direction side,
            List<MapViolation> violations)
        {
            // An uncollapsed neighbour is reported when it is visited itself
            if (neighbor == null)
            {
                return;
            }

            string socket = variant.Sockets[(int)side];
            string neighborSocket = neighbor.Sockets[(int)side.Opposite()];

            if (socket != neighborSocket)
            {
                violations.Add(new SocketMismatch(pos, side, socket, neighborSocket));
            }
        }
    }
}
