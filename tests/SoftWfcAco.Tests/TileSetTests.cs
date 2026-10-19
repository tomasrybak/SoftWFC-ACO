namespace SoftWfcAco.Tests
{
    public class TileSetTests
    {
        [Fact]
        public void Parse_ReadsRSlikeFormat_IgnoresRenderingFields()
        {
            // Comments, trailing commas and the game-only fields ground/prop, as in RSlike's tiles.json
            string json = """
                {
                  "tiles": [
                    // Land
                    { "name": "grass", "sockets": ["grass", "grass", "grass", "grass"], "weight": 10, "ground": "grass" },
                    { "name": "forest", "sockets": ["grass", "grass", "grass", "grass"], "weight": 2, "ground": "grass", "prop": "tree", "walkable": false },
                    { "name": "path_straight", "sockets": ["path", "grass", "path", "grass"], "rotatable": true, "ground": "path" },
                  ]
                }
                """;

            TileSet set = TileSet.Parse(json);

            Assert.Equal(3, set.Tiles.Count);
            Assert.Equal(6, set.Variants.Count);
            Assert.False(set.Tiles[1].Walkable);
            Assert.Equal(1f, set.Tiles[2].Weight);
        }

        [Fact]
        public void Parse_RejectsMissingRequiredField()
        {
            string json = """{ "tiles": [ { "name": "grass" } ] }""";

            InvalidDataException e = Assert.Throws<InvalidDataException>(() => TileSet.Parse(json, "test.json"));

            Assert.StartsWith("test.json:", e.Message);
        }

        [Fact]
        public void Parse_RejectsEmptySet()
        {
            Assert.Throws<InvalidDataException>(() => TileSet.Parse("""{ "tiles": [] }"""));
        }

        [Fact]
        public void Constructor_ListsAllFieldErrors()
        {
            TileDef[] tiles =
            [
                new TileDef { Name = "a", Sockets = ["x", "x", "x", "x"] },
                new TileDef { Name = "a", Sockets = ["x", "x", "x"], Weight = 0 },
            ];

            InvalidDataException e = Assert.Throws<InvalidDataException>(() => new TileSet(tiles));

            Assert.Contains("Duplicate tile name 'a'", e.Message);
            Assert.Contains("needs exactly 4 sockets", e.Message);
            Assert.Contains("weight must be positive", e.Message);
        }

        [Fact]
        public void Constructor_RejectsSocketWithoutPartner()
        {
            // Nothing has "path" on its south side, so the north socket can never be matched
            TileDef[] tiles =
            [
                TestTiles.Grass,
                new TileDef { Name = "stub", Sockets = ["path", "grass", "grass", "grass"] },
            ];

            InvalidDataException e = Assert.Throws<InvalidDataException>(() => new TileSet(tiles));

            Assert.Contains("socket 'path' on side N has no tile with 'path' on side S", e.Message);
        }

        [Fact]
        public void Constructor_AcceptsSmokeSet()
        {
            TileSet set = new TileSet(TestTiles.Smoke);

            Assert.Equal(TileVariant.Expand(TestTiles.Smoke).Count, set.Variants.Count);
        }
    }
}
