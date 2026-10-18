namespace SoftWfcAco.Tests
{
    public class MapValidatorTests
    {
        private static readonly List<TileVariant> Variants =
            TileVariant.Expand([TestTiles.Grass, TestTiles.Water, TestTiles.PathStraight]);

        private static TileVariant Grass => Variants[0];
        private static TileVariant Water => Variants[1];
        private static TileVariant PathNorthSouth => Variants[2];

        [Fact]
        public void MatchingNeighbours_AreValid()
        {
            Cell[,] grid = TestTiles.NewGrid(2, 2, Variants);
            TestTiles.Set(grid, 0, 0, Grass);
            TestTiles.Set(grid, 1, 0, Grass);
            TestTiles.Set(grid, 0, 1, Grass);
            TestTiles.Set(grid, 1, 1, Grass);

            Assert.Empty(MapValidator.Validate(grid));
        }

        [Fact]
        public void EastMismatch_IsReportedOnceFromTheWestCell()
        {
            Cell[,] grid = TestTiles.NewGrid(2, 1, Variants);
            TestTiles.Set(grid, 0, 0, Grass);
            TestTiles.Set(grid, 1, 0, Water);

            MapViolation violation = Assert.Single(MapValidator.Validate(grid));

            Assert.Equal(new SocketMismatch(new GridPos(0, 0), Direction.East, "grass", "water"), violation);
        }

        [Fact]
        public void NorthMismatch_IsReportedFromTheSouthCell()
        {
            Cell[,] grid = TestTiles.NewGrid(1, 2, Variants);
            TestTiles.Set(grid, 0, 0, PathNorthSouth);
            TestTiles.Set(grid, 0, 1, Grass);

            MapViolation violation = Assert.Single(MapValidator.Validate(grid));

            Assert.Equal(new SocketMismatch(new GridPos(0, 0), Direction.North, "path", "grass"), violation);
        }

        [Fact]
        public void UncollapsedCell_IsReportedWithoutEdgeViolations()
        {
            Cell[,] grid = TestTiles.NewGrid(2, 1, Variants);
            TestTiles.Set(grid, 0, 0, Grass);

            MapViolation violation = Assert.Single(MapValidator.Validate(grid));

            Assert.Equal(new UncollapsedCell(new GridPos(1, 0)), violation);
        }
    }
}
