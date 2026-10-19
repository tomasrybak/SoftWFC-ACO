namespace SoftWfcAco.Tests
{
    public class WFCSolverTests
    {
        private static readonly List<TileVariant> Variants = TileVariant.Expand(TestTiles.Smoke);

        /// <summary>
        /// Runs pure WFC on a fresh grid with the given seed.
        /// </summary>
        private static (bool ok, Cell[,] grid) Solve(int seed, int width = 20, int depth = 20)
        {
            Cell[,] grid = TestTiles.NewGrid(width, depth, Variants);
            WFCSolver solver = new WFCSolver(grid, width, depth, new Random(seed));

            return (solver.RunWFC(), grid);
        }

        [Fact]
        public void SeedSweep_EverySuccessIsAValidMap()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                (bool ok, Cell[,] grid) = Solve(seed);

                // Contradictions are allowed; reporting an invalid map as success is not
                if (!ok)
                {
                    continue;
                }

                List<MapViolation> violations = MapValidator.Validate(grid);
                Assert.True(violations.Count == 0,
                    $"seed {seed}: {violations.Count} violations, first: {string.Join(", ", violations.Take(5))}");
            }
        }

        [Fact]
        public void ForceCollapse_PropagatesToNeighbours()
        {
            Cell[,] grid = TestTiles.NewGrid(3, 3, Variants);
            WFCSolver solver = new WFCSolver(grid, 3, 3, new Random(0));
            TileVariant water = Variants.First(v => v.Def.Name == "water");

            solver.ForceCollapse(grid[1, 1], [water]);

            Assert.False(solver.HasContradiction);
            Assert.DoesNotContain(grid[1, 2].AvailableVariants, v => v.Def.Name == "grass");
        }

        /// <summary>
        /// Characterization test of a thesis bug kept in the reference (ADR 0001): emptying the cell
        /// before re-adding the allowed variants raises the contradiction flag, so Propagate does nothing.
        /// </summary>
        [Fact]
        public void SetInitialCell_FlagsContradictionAndDoesNotPropagate()
        {
            Cell[,] grid = TestTiles.NewGrid(3, 3, Variants);
            WFCSolver solver = new WFCSolver(grid, 3, 3, new Random(0));
            TileVariant water = Variants.First(v => v.Def.Name == "water");

            Assert.True(solver.SetInitialCell(grid[1, 1], [water]));

            Assert.True(solver.HasContradiction);
            Assert.Contains(grid[1, 2].AvailableVariants, v => v.Def.Name == "grass");
        }

        [Fact]
        public void SameSeed_GivesTheSameMap()
        {
            (_, Cell[,] first) = Solve(seed: 42);
            (_, Cell[,] second) = Solve(seed: 42);

            Assert.Equal(TestTiles.Fingerprint(first), TestTiles.Fingerprint(second));
        }

        [Fact]
        public void DifferentSeeds_GiveDifferentMaps()
        {
            (_, Cell[,] first) = Solve(seed: 1);
            (_, Cell[,] second) = Solve(seed: 2);

            Assert.NotEqual(TestTiles.Fingerprint(first), TestTiles.Fingerprint(second));
        }
    }
}
