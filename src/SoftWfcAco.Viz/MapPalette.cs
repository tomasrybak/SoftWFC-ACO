namespace SoftWfcAco.Viz
{
    /// <summary>
    /// Colours and glyphs for sockets. Tiles are drawn from their sockets, so any tile set
    /// renders without per-tile art; unknown sockets get a stable generated colour.
    /// </summary>
    public sealed class MapPalette
    {
        public string PathSocket { get; init; } = "path";

        public Dictionary<string, (byte r, byte g, byte b)> Colors { get; init; } = new Dictionary<string, (byte r, byte g, byte b)>
        {
            ["grass"] = (96, 160, 72),
            ["path"] = (176, 136, 88),
            ["water"] = (56, 112, 196),
        };

        public Dictionary<string, char> Glyphs { get; init; } = new Dictionary<string, char>
        {
            ["grass"] = '.',
            ["water"] = '~',
        };

        public (byte r, byte g, byte b) Uncollapsed { get; init; } = (150, 150, 150);
        public (byte r, byte g, byte b) Error { get; init; } = (230, 30, 30);

        /// <summary>
        /// Colour of a socket; unknown sockets get a colour derived from their name.
        /// </summary>
        public (byte r, byte g, byte b) ColorOf(string socket)
        {
            if (Colors.TryGetValue(socket, out (byte r, byte g, byte b) color))
            {
                return color;
            }

            // string.GetHashCode is randomised per process, so images would change between runs
            uint hash = 2166136261;
            foreach (char c in socket)
            {
                hash = (hash ^ c) * 16777619;
            }

            return ((byte)(64 + hash % 160), (byte)(64 + (hash >> 8) % 160), (byte)(64 + (hash >> 16) % 160));
        }

        /// <summary>
        /// Glyph of a socket; unknown sockets use their first letter.
        /// </summary>
        public char GlyphOf(string socket)
        {
            if (Glyphs.TryGetValue(socket, out char glyph))
            {
                return glyph;
            }

            return socket.Length > 0 ? socket[0] : '#';
        }

        /// <summary>
        /// The most frequent non-path socket of a tile, used as its background.
        /// Ties go to the first socket in N, E, S, W order.
        /// </summary>
        public string BackgroundSocket(TileVariant variant)
        {
            string best = variant.Sockets[0];
            int bestCount = -1;

            foreach (string socket in variant.Sockets)
            {
                if (socket == PathSocket)
                {
                    continue;
                }

                int count = variant.Sockets.Count(s => s == socket);
                if (count > bestCount)
                {
                    best = socket;
                    bestCount = count;
                }
            }

            return best;
        }

        /// <summary>
        /// Bit mask of the sides with a path socket: N = 1, E = 2, S = 4, W = 8.
        /// </summary>
        public int PathMask(TileVariant variant)
        {
            int mask = 0;

            for (int i = 0; i < 4; i++)
            {
                if (variant.Sockets[i] == PathSocket)
                {
                    mask |= 1 << i;
                }
            }

            return mask;
        }
    }
}
