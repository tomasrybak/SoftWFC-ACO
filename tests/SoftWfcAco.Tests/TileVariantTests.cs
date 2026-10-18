namespace SoftWfcAco.Tests
{
    public class TileVariantTests
    {
        [Fact]
        public void Rotation_TurnsSocketsClockwise()
        {
            TileDef def = new TileDef { Name = "t", Sockets = ["n", "e", "s", "w"], Rotatable = true };

            List<TileVariant> variants = TileVariant.Expand([def]);

            // After one clockwise turn the old west side faces north
            Assert.Equal(["n", "e", "s", "w"], variants[0].Sockets);
            Assert.Equal(["w", "n", "e", "s"], variants[1].Sockets);
            Assert.Equal(["s", "w", "n", "e"], variants[2].Sockets);
            Assert.Equal(["e", "s", "w", "n"], variants[3].Sockets);
        }

        [Fact]
        public void Expand_SplitsWeightAcrossRotations()
        {
            List<TileVariant> variants = TileVariant.Expand([TestTiles.Grass, TestTiles.PathStraight]);

            Assert.Equal(5, variants.Count);
            Assert.Equal(10f, variants[0].Weight);
            Assert.All(variants.Skip(1), v => Assert.Equal(0.25f, v.Weight));
        }

        [Fact]
        public void Constructor_RejectsWrongSocketCount()
        {
            TileDef def = new TileDef { Name = "bad", Sockets = ["a", "b", "c"] };

            Assert.Throws<ArgumentException>(() => TileVariant.Expand([def]));
        }
    }
}
