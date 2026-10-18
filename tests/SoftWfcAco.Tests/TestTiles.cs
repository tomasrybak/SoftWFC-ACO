namespace SoftWfcAco.Tests
{
    /// <summary>
    /// Fixtures owned by the tests, so changes to the bench or sample tile sets can't silently
    /// change what the tests check.
    /// </summary>
    internal static class TestTiles
    {
        public static readonly TileDef Grass = new TileDef { Name = "grass", Sockets = ["grass", "grass", "grass", "grass"], Weight = 10 };
        public static readonly TileDef Water = new TileDef { Name = "water", Sockets = ["water", "water", "water", "water"], Weight = 4 };
        public static readonly TileDef PathStraight = new TileDef { Name = "path_straight", Sockets = ["path", "grass", "path", "grass"], Weight = 1, Rotatable = true };

        /// <summary>
        /// Land/road/water set; mirrors the bench smoke set at the time of writing.
        /// </summary>
        public static readonly TileDef[] Smoke =
        [
            Grass,
            PathStraight,
            new TileDef { Name = "path_corner", Sockets = ["path", "path", "grass", "grass"], Weight = 1, Rotatable = true },
            new TileDef { Name = "path_end", Sockets = ["path", "grass", "grass", "grass"], Weight = 0.2f, Rotatable = true },
            Water,
            new TileDef { Name = "shore", Sockets = ["water", "grass", "grass", "grass"], Weight = 0.5f, Rotatable = true },
        ];

        /// <summary>
        /// Creates an uncollapsed grid where every cell allows all given variants.
        /// </summary>
        public static Cell[,] NewGrid(int width, int depth, List<TileVariant> variants)
        {
            Cell[,] grid = new Cell[width, depth];

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    grid[x, z] = new Cell(new GridPos(x, z), variants);
                }
            }

            return grid;
        }

        /// <summary>
        /// Marks a cell as collapsed without a solver, for hand-built validator inputs.
        /// </summary>
        public static void Set(Cell[,] grid, int x, int z, TileVariant variant)
        {
            grid[x, z].CollapsedVariant = variant;
            grid[x, z].IsCollapsed = true;
        }

        /// <summary>
        /// One string per map, e.g. "grass:0 path_straight:1 ...", for comparing runs.
        /// </summary>
        public static string Fingerprint(Cell[,] grid)
        {
            List<string> parts = new List<string>();

            foreach (Cell cell in grid)
            {
                TileVariant? v = cell.CollapsedVariant;
                parts.Add(v != null ? $"{v.Def.Name}:{v.Rotation}" : "?");
            }

            return string.Join(' ', parts);
        }
    }
}
