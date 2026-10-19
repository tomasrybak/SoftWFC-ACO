using System.Text.Json;

namespace SoftWfcAco
{
    /// <summary>
    /// A validated tile set. Validation runs at load time so a broken tile file fails with a
    /// readable message instead of as a WFC contradiction later.
    /// The JSON format is RSlike's data/tiles.json; fields the library doesn't use
    /// (e.g. ground, prop) are ignored, so consumers can keep rendering data in the same file.
    /// </summary>
    public sealed class TileSet
    {
        public IReadOnlyList<TileDef> Tiles { get; }
        public IReadOnlyList<TileVariant> Variants { get; }

        private sealed class FileFormat
        {
            public List<TileDef> Tiles { get; init; } = new List<TileDef>();
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>
        /// Validates the tiles and expands them into variants.
        /// </summary>
        /// <exception cref="InvalidDataException">The tile set breaks a rule; the message lists every error.</exception>
        public TileSet(IReadOnlyList<TileDef> tiles)
        {
            Validate(tiles);
            Tiles = tiles;
            Variants = TileVariant.Expand(tiles);
        }

        /// <summary>
        /// Loads a tile set from a JSON file (comments and trailing commas allowed).
        /// </summary>
        public static TileSet Load(string path)
        {
            return Parse(File.ReadAllText(path), path);
        }

        /// <summary>
        /// Parses a tile set from JSON text.
        /// </summary>
        /// <param name="json">File contents in the tiles.json format</param>
        /// <param name="source">Name used in error messages, usually the file path</param>
        public static TileSet Parse(string json, string source = "tile set")
        {
            FileFormat? file;
            try
            {
                file = JsonSerializer.Deserialize<FileFormat>(json, JsonOptions);
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{source}: invalid JSON or missing required field: {e.Message}", e);
            }

            if (file == null || file.Tiles.Count == 0)
            {
                throw new InvalidDataException($"{source}: no tiles defined.");
            }

            return new TileSet(file.Tiles);
        }

        /// <summary>
        /// Checks per-tile fields, then that every socket can be matched from the opposite side
        /// by some variant. Without a partner a tile can never have a neighbour on that side,
        /// so WFC could only fail or never pick it.
        /// </summary>
        private static void Validate(IReadOnlyList<TileDef> tiles)
        {
            List<string> errors = new List<string>();
            HashSet<string> names = new HashSet<string>();

            foreach (TileDef tile in tiles)
            {
                if (!names.Add(tile.Name))
                {
                    errors.Add($"Duplicate tile name '{tile.Name}'.");
                }

                if (tile.Sockets.Length != 4)
                {
                    errors.Add($"Tile '{tile.Name}': needs exactly 4 sockets [N, E, S, W], has {tile.Sockets.Length}.");
                }

                if (tile.Weight <= 0)
                {
                    errors.Add($"Tile '{tile.Name}': weight must be positive.");
                }
            }

            // The partner check needs well-formed tiles, TileVariant would throw on them
            ThrowIfAny(errors);

            List<TileVariant> variants = TileVariant.Expand(tiles);
            HashSet<string>[] socketsBySide = new HashSet<string>[4];

            for (int side = 0; side < 4; side++)
            {
                socketsBySide[side] = variants.Select(v => v.Sockets[side]).ToHashSet();
            }

            foreach (TileVariant v in variants)
            {
                for (int side = 0; side < 4; side++)
                {
                    int opposite = (side + 2) % 4;

                    if (!socketsBySide[opposite].Contains(v.Sockets[side]))
                    {
                        errors.Add($"Tile '{v.Def.Name}' (rotation {v.Rotation}): socket '{v.Sockets[side]}' on side {"NESW"[side]} " +
                                   $"has no tile with '{v.Sockets[side]}' on side {"NESW"[opposite]}.");
                    }
                }
            }

            ThrowIfAny(errors);
        }

        /// <summary>
        /// Throws one exception listing all collected errors, so a tile file can be fixed in one pass.
        /// </summary>
        private static void ThrowIfAny(List<string> errors)
        {
            if (errors.Count > 0)
            {
                throw new InvalidDataException("Tile set errors:\n  " + string.Join("\n  ", errors));
            }
        }
    }
}
