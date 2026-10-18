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
