namespace SoftWfcAco.Viz
{
    /// <summary>
    /// Renders a map as text, one character per cell, north at the top.
    /// Paths are box-drawing lines, other tiles show their background socket's glyph,
    /// '?' is an uncollapsed cell and '!' a cell with no variants left.
    /// </summary>
    public static class AsciiMap
    {
        // Indexed by MapPalette.PathMask (N = 1, E = 2, S = 4, W = 8)
        private const string PathGlyphs = " ╵╶└╷│┌├╴┘─┴┐┤┬┼";

        /// <summary>
        /// Renders the grid (indexed [x, z]) as lines separated by '\n'.
        /// </summary>
        public static string Render(Cell[,] grid, MapPalette? palette = null)
        {
            palette ??= new MapPalette();
            int width = grid.GetLength(0);
            int depth = grid.GetLength(1);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            for (int z = depth - 1; z >= 0; z--)
            {
                for (int x = 0; x < width; x++)
                {
                    sb.Append(GlyphOf(grid[x, z], palette));
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Character for one cell.
        /// </summary>
        private static char GlyphOf(Cell cell, MapPalette palette)
        {
            TileVariant? variant = cell.CollapsedVariant;

            if (variant == null)
            {
                return cell.Entropy == 0 ? '!' : '?';
            }

            int mask = palette.PathMask(variant);
            if (mask != 0)
            {
                return PathGlyphs[mask];
            }

            return palette.GlyphOf(palette.BackgroundSocket(variant));
        }
    }
}
