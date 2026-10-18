namespace SoftWfcAco.Viz
{
    /// <summary>
    /// Renders a map as an image, north at the top. Each cell is a 3×3 block: edge pixels take
    /// their socket's colour, so roads and coastlines connect across cells for any tile set.
    /// The centre is the path colour on path tiles, otherwise the background socket's colour.
    /// </summary>
    public static class MapImage
    {
        /// <summary>
        /// Renders the grid (indexed [x, z]).
        /// </summary>
        /// <param name="grid">The map, collapsed or not</param>
        /// <param name="violations">Validator output; mismatched edges are drawn in the error colour</param>
        /// <param name="scale">Pixels per block subdivision; a cell is 3 * scale pixels wide</param>
        public static RgbImage Render(Cell[,] grid, IReadOnlyList<MapViolation>? violations = null,
            int scale = 4, MapPalette? palette = null)
        {
            palette ??= new MapPalette();
            int width = grid.GetLength(0);
            int depth = grid.GetLength(1);
            RgbImage image = new RgbImage(width * 3 * scale, depth * 3 * scale);

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    DrawCell(image, grid[x, z], x, depth - 1 - z, scale, palette);
                }
            }

            if (violations != null)
            {
                foreach (MapViolation violation in violations)
                {
                    if (violation is SocketMismatch mismatch)
                    {
                        DrawMismatch(image, mismatch, depth, scale, palette);
                    }
                }
            }

            return image;
        }

        /// <summary>
        /// Draws one cell at image cell coordinates (column, row).
        /// </summary>
        private static void DrawCell(RgbImage image, Cell cell, int column, int row, int scale, MapPalette palette)
        {
            int left = column * 3 * scale;
            int top = row * 3 * scale;
            TileVariant? variant = cell.CollapsedVariant;

            if (variant == null)
            {
                image.FillRect(left, top, 3 * scale, 3 * scale, cell.Entropy == 0 ? palette.Error : palette.Uncollapsed);
                return;
            }

            (byte r, byte g, byte b) background = palette.ColorOf(palette.BackgroundSocket(variant));
            image.FillRect(left, top, 3 * scale, 3 * scale, background);

            for (int i = 0; i < 4; i++)
            {
                (int dx, int dy) = SubCellOf((Direction)i);
                image.FillRect(left + dx * scale, top + dy * scale, scale, scale, palette.ColorOf(variant.Sockets[i]));
            }

            if (palette.PathMask(variant) != 0)
            {
                image.FillRect(left + scale, top + scale, scale, scale, palette.ColorOf(palette.PathSocket));
            }
        }

        /// <summary>
        /// Marks both halves of a mismatched edge.
        /// </summary>
        private static void DrawMismatch(RgbImage image, SocketMismatch mismatch, int depth, int scale, MapPalette palette)
        {
            GridPos neighbor = mismatch.Position.Step(mismatch.Side);
            MarkSide(image, mismatch.Position, mismatch.Side, depth, scale, palette);
            MarkSide(image, neighbor, mismatch.Side.Opposite(), depth, scale, palette);
        }

        /// <summary>
        /// Paints the edge pixel of one cell side in the error colour.
        /// </summary>
        private static void MarkSide(RgbImage image, GridPos pos, Direction side, int depth, int scale, MapPalette palette)
        {
            (int dx, int dy) = SubCellOf(side);
            int left = pos.X * 3 * scale + dx * scale;
            int top = (depth - 1 - pos.Z) * 3 * scale + dy * scale;
            image.FillRect(left, top, scale, scale, palette.Error);
        }

        /// <summary>
        /// Position of a side's edge pixel inside the 3×3 block (row 0 is north).
        /// </summary>
        private static (int dx, int dy) SubCellOf(Direction side)
        {
            return side switch
            {
                Direction.North => (1, 0),
                Direction.East => (2, 1),
                Direction.South => (1, 2),
                _ => (0, 1),
            };
        }
    }
}
