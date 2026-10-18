namespace SoftWfcAco
{
    /// <summary>
    /// Class representing the ACO-WFC hybrid path generation algorithm.
    /// Ported from the thesis 3D solver to a 2D grid. RunACOHybrid is split into FindPath and
    /// ApplyPath so several roads can be constrained before a single WFC run.
    /// </summary>
    public class ACOHybridSolver
    {
        private readonly Cell[,] grid; // Grid representation of the map
        private readonly WFCSolver wfc; // WFC solver instance

        // Map dimensions
        private readonly int width;
        private readonly int depth;

        private readonly string pathSocket; // Socket name that counts as a walkable path

        // ACO parameters
        private const float Alpha = 1.0f; // Pheromone influence
        private readonly float beta; // Heuristic weight
        private const float InitialPheromone = 0.1f; // Initial pheromone value
        private const float Rho = 0.15f; // Pheromone evaporation rate
        private const float Q = 100.0f; // Pheromone award constant
        private readonly int colonySize; // Number of agents in the colony
        private readonly int maxIterations; // Maximum number of colony iterations

        private float[,] pheromones;
        private readonly Random rng;

        /// <summary>
        /// Constructor for the ACOHybridSolver class.
        /// Initializes the solver with the grid, map dimensions, WFC solver, and ACO parameters.
        /// </summary>
        public ACOHybridSolver(Cell[,] grid, int mapWidth, int mapDepth, WFCSolver wfc,
            string pathSocket = "path", float beta = -1, int colonySize = -1, int iterations = -1, Random? rng = null)
        {
            this.grid = grid;
            this.width = mapWidth;
            this.depth = mapDepth;
            this.wfc = wfc;
            this.pathSocket = pathSocket;
            this.rng = rng ?? new Random();
            this.beta = beta > 0 ? beta : 7.9f;

            if (colonySize > 0 && iterations > 0)
            {
                this.colonySize = colonySize;
                this.maxIterations = iterations;
            }
            else
            {
                // Calculate dynamic parameters based on the number of cells in the map
                int cells = mapWidth * mapDepth;
                float sqrt = MathF.Sqrt(cells);
                this.colonySize = Math.Clamp((int)MathF.Round(sqrt * 0.5f), 10, 300);
                this.maxIterations = Math.Clamp((int)MathF.Round(sqrt * 0.2f), 8, 100);
            }

            pheromones = new float[width, depth];
            InitializePheromones();
        }

        /// <summary>
        /// Initializes the pheromone matrix with the initial value.
        /// </summary>
        private void InitializePheromones()
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    pheromones[x, z] = InitialPheromone;
                }
            }
        }

        /// <summary>
        /// Original thesis entry point: finds a path, applies it to WFC and runs WFC to completion.
        /// </summary>
        /// <returns>The built path, or null if no valid path was found or WFC failed.</returns>
        public List<GridPos>? RunACOHybrid(GridPos startPos, GridPos endPos)
        {
            List<GridPos>? bestValidPath = FindPath(startPos, endPos);

            // If a valid path was found, run the WFC solver with the best path and finish the generation
            if (bestValidPath != null)
            {
                ApplyPath(bestValidPath);
                if (wfc.RunWFC())
                {
                    return bestValidPath;
                }
            }

            return null; // No valid path found
        }

        /// <summary>
        /// Path generation algorithm using an ant colony optimization approach.
        /// The algorithm explores the map using multiple ants, each with its own pheromone influence.
        /// The ants explore the map until they reach the target position or find a dead end.
        /// After each iteration, the pheromone levels are updated based on the successful paths found by the ants.
        /// The resulting path is simulated in the WFC solver to check if it's valid.
        /// If the path is valid, the pheromones are awarded; if not, the pheromones are penalized.
        /// Leaves the WFC state unchanged.
        /// </summary>
        /// <param name="startPos">Starting position of the ants</param>
        /// <param name="endPos">Target position the ants are trying to reach</param>
        /// <returns>The shortest buildable path found, or null.</returns>
        public List<GridPos>? FindPath(GridPos startPos, GridPos endPos)
        {
            // Initialization
            List<GridPos>? bestValidPath = null;
            int bestLength = int.MaxValue;
            int maxAntSteps = width * depth;

            // Main loop of the algorithm
            for (int iter = 0; iter < maxIterations; iter++)
            {
                List<Ant> successfulAnts = new List<Ant>();

                // Run ColonySize ants to explore the map and find paths to the target position
                for (int i = 0; i < colonySize; i++)
                {
                    Ant ant = new Ant(grid, pheromones, endPos, pathSocket, width, depth, Alpha, beta, rng);

                    ant.Explore(startPos, maxAntSteps);

                    if (ant.ReachedTarget)
                    {
                        successfulAnts.Add(ant);
                    }
                }

                // If any ants reached the target, update the pheromones based on the successful paths
                if (successfulAnts.Count > 0)
                {
                    // Find the best path from the successful ants
                    List<List<GridPos>> cleanedPaths = successfulAnts.Select(a => RemoveLoops(a.Path)).ToList();

                    List<GridPos> bestIterAntPath = cleanedPaths.OrderBy(p => p.Count).First();

                    wfc.SaveSnapshot(); // Saves the current state of the WFC solver before simulating the path

                    // Dry run the path in WFC to check if it's valid
                    bool isBuildable = SimulatePathInWFC(bestIterAntPath);

                    wfc.RestoreSnapshot(); // Restores the WFC state after simulation

                    // If the path is valid, rewarded with pheromones
                    if (isBuildable)
                    {
                        // Update path if the new path is shorter than the best found so far
                        if (bestIterAntPath.Count < bestLength)
                        {
                            bestLength = bestIterAntPath.Count;
                            bestValidPath = new List<GridPos>(bestIterAntPath);
                        }

                        // Deposit pheromones on the path based on the length of the path
                        DepositPheromones(bestIterAntPath, Q / bestIterAntPath.Count);
                    }
                    else
                    {
                        // Penalize the path if it's not valid
                        PenalizePath(bestIterAntPath);
                    }
                }
                // Evaporate pheromones after each iteration
                EvaporatePheromones();
            }

            return bestValidPath;
        }

        /// <summary>
        /// Simulates the path in the WFC solver.
        /// Goes through each cell in the path and forces the collapse of the cell to a variant that has a path socket.
        /// If any cell in the path cannot be collapsed to a path variant, the simulation fails and returns false.
        /// </summary>
        /// <param name="path">List of grid positions representing the path to be simulated in the WFC solver</param>
        private bool SimulatePathInWFC(List<GridPos> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Cell cell = grid[path[i].X, path[i].Z];

                // Skip collapsed cells (start and finish)
                if (cell.IsCollapsed)
                {
                    continue;
                }

                List<TileVariant> pathVariants = GetPathVariants(cell, path, i);

                if (pathVariants.Count == 0) return false;

                wfc.ForceCollapse(cell, pathVariants);
            }

            return !wfc.HasContradiction;
        }

        /// <summary>
        /// Applies constraints to the WFC solver based on the path found by the ACO algorithm.
        /// The constraints ensure that the cells in the path collapse to variants with path sockets
        /// towards the previous and next path cell.
        /// </summary>
        public void ApplyPath(List<GridPos> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Cell cell = grid[path[i].X, path[i].Z];

                // Skip collapsed start and finish
                if (cell.IsCollapsed)
                {
                    continue;
                }

                // No empty-list check: the path was validated by SimulatePathInWFC in FindPath
                wfc.ForceCollapse(cell, GetPathVariants(cell, path, i));
            }
        }

        /// <summary>
        /// Variants of the cell at path[i] that have path sockets towards both path neighbours.
        /// </summary>
        private List<TileVariant> GetPathVariants(Cell cell, List<GridPos> path, int i)
        {
            List<int> requiredDirections = new List<int>();
            if (i > 0) requiredDirections.Add(GetDirectionIndex(path[i], path[i - 1]));
            if (i < path.Count - 1) requiredDirections.Add(GetDirectionIndex(path[i], path[i + 1]));

            return cell.AvailableVariants.Where(v =>
            {
                // Check for path sockets in all required directions
                foreach (int dirIndex in requiredDirections)
                {
                    if (v.Sockets[dirIndex] != pathSocket) return false;
                }
                return true;
            }).ToList();
        }

        /// <summary>
        /// Removes loops from the path.
        /// </summary>
        /// <param name="rawPath">List of grid positions representing the raw path found by the ant.
        /// The path may contain loops where the ant crosses its own trail.</param>
        /// <returns>List of grid positions representing the cleaned path with loops removed.</returns>
        public List<GridPos> RemoveLoops(List<GridPos> rawPath)
        {
            List<GridPos> cleanPath = new List<GridPos>();

            foreach (GridPos pos in rawPath)
            {
                int index = cleanPath.IndexOf(pos);

                if (index != -1)
                {
                    // Loop detected
                    cleanPath.RemoveRange(index + 1, cleanPath.Count - index - 1);
                }
                else
                {
                    // Add position to the cleaned path if it's not already present
                    cleanPath.Add(pos);
                }
            }

            return cleanPath;
        }

        /// <summary>
        /// Deposits pheromones on the path based on the length of the path.
        /// </summary>
        /// <param name="path">List of grid positions representing the path on which to deposit pheromones</param>
        /// <param name="amount">The amount of pheromones to be deposited on each cell in the path</param>
        private void DepositPheromones(List<GridPos> path, float amount)
        {
            foreach (var p in path)
            {
                pheromones[p.X, p.Z] += amount;
            }
        }

        /// <summary>
        /// Penalizes the path by reducing the pheromone levels on each cell in the path.
        /// Used when no valid path is found during the simulation in the WFC solver
        /// to discourage ants from exploring similar paths in future iterations.
        /// </summary>
        private void PenalizePath(List<GridPos> path)
        {
            foreach (var p in path)
            {
                pheromones[p.X, p.Z] *= 0.1f;
            }
        }

        /// <summary>
        /// Evaporates the pheromones in the map by the rate Rho each iteration.
        /// </summary>
        private void EvaporatePheromones()
        {
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    pheromones[x, z] *= (1 - Rho);
                }
            }
        }

        /// <summary>
        /// Gets the index of the direction that leads from one position to another.
        /// </summary>
        private static int GetDirectionIndex(GridPos from, GridPos to)
        {
            for (int i = 0; i < 4; i++)
            {
                if (from.Step((Direction)i) == to) return i;
            }
            return -1; // Invalid direction
        }

        /// <summary>
        /// Returns the current state of the pheromone matrix.
        /// </summary>
        public float[,] GetPheromones()
        {
            return pheromones;
        }
    }
}
