namespace SoftWfcAco
{
    /// <summary>
    /// Class representing an autonomous agent (ant) for pathfinding.
    /// Independently searches the grid based on pheromone levels and heuristics.
    /// </summary>
    public class Ant
    {
        // Extern data for the ant
        private readonly Cell[,] grid;
        private readonly float[,] pheromones;
        private readonly GridPos endPos;
        private readonly string pathSocket;

        private readonly float alpha; // Pheromone weight
        private readonly float beta; // Heuristic weight
        private readonly Random rng;

        // Map borders
        private readonly int mapWidth;
        private readonly int mapDepth;

        public List<GridPos> Path { get; private set; } = new(); // Path found by the ant
        public HashSet<GridPos> TabuList { get; private set; } = new(); // List of cells visited by the ant
        public bool ReachedTarget { get; private set; } // Flag indicating if the ant has reached the target

        /// <summary>
        /// Constructor for the Ant class.
        /// </summary>
        /// <param name="grid">The 2D grid of cells representing the map</param>
        /// <param name="pheromones">The 2D array of pheromone levels at the grid</param>
        /// <param name="endPos">The target position the ant is trying to reach</param>
        /// <param name="pathSocket">Socket name the ant may walk through</param>
        /// <param name="mapWidth">Width of the map</param>
        /// <param name="mapDepth">Depth of the map</param>
        /// <param name="alpha">Weight of the pheromone influence on the ant's decision-making</param>
        /// <param name="beta">Weight of the heuristic influence on the ant's decision-making</param>
        public Ant(Cell[,] grid, float[,] pheromones, GridPos endPos, string pathSocket,
            int mapWidth, int mapDepth, float alpha, float beta, Random rng)
        {
            this.grid = grid;
            this.pheromones = pheromones;
            this.endPos = endPos;
            this.pathSocket = pathSocket;
            this.alpha = alpha;
            this.beta = beta;
            this.rng = rng;

            this.mapWidth = mapWidth;
            this.mapDepth = mapDepth;
        }

        /// <summary>
        /// Lets the ant explore the grid, following the pheromone influence and heuristic.
        /// The ant explores the grid until it reaches the target or traps itself in a dead end.
        /// </summary>
        /// <param name="startPos">Starting position of the ant</param>
        /// <param name="maxSteps">Maximum number of steps the ant can take before giving up</param>
        public void Explore(GridPos startPos, int maxSteps)
        {
            // Initialization
            Path = new List<GridPos> { startPos };
            TabuList = new HashSet<GridPos> { startPos };
            GridPos currentPos = startPos;
            List<TileVariant> currentVariants = grid[startPos.X, startPos.Z].AvailableVariants;
            ReachedTarget = false;

            int steps = 0;

            // Main loop of the ant
            while (currentPos != endPos && steps < maxSteps)
            {
                steps++;

                // Get valid neighbors for pathing
                var validNeighbors = GetValidNeighbors(currentPos, currentVariants);

                // Dead end
                if (validNeighbors.Count == 0)
                {
                    return;
                }

                // Select the next step based on pheromone influence and heuristic
                var nextStep = SelectNextStep(validNeighbors);

                // Move to the next step
                Path.Add(nextStep.pos);
                TabuList.Add(nextStep.pos);

                currentPos = nextStep.pos;
                currentVariants = nextStep.variants;
            }

            // Check if the ant reached the target
            if (currentPos == endPos)
            {
                ReachedTarget = true;
            }
        }

        /// <summary>
        /// Selects the next step for the ant based on a combination of pheromone levels and heuristic information.
        /// Uses Tau (pheromone level) and Eta (heuristic) to calculate the probability of each neighbor.
        /// </summary>
        private (GridPos pos, List<TileVariant> variants) SelectNextStep(List<(GridPos pos, List<TileVariant> variants)> neighbors)
        {
            // Initialization
            List<double> probabilities = new List<double>();
            double totalProbability = 0;

            // Calculate the probability for each neighbor based on pheromone levels and heuristic information
            foreach (var n in neighbors)
            {
                float tau = pheromones[n.pos.X, n.pos.Z];

                // Calculation of the heuristic (Eta) based on the distance to the target.
                // Adding 0.1f to avoid division by zero
                float distance = n.pos.ManhattanDistance(endPos);
                float eta = 1.0f / (distance + 0.1f);

                // Calculation of the weight for the neighbor: (Tau^Alpha) * (Eta^Beta)
                double weight = Math.Pow(tau, alpha) * Math.Pow(eta, beta);

                probabilities.Add(weight);
                totalProbability += weight;
            }

            // Select the next step based on the probability with randomness
            double randomValue = rng.NextDouble() * totalProbability;
            double cumulative = 0;

            for (int i = 0; i < neighbors.Count; i++)
            {
                cumulative += probabilities[i];
                if (cumulative >= randomValue)
                {
                    return neighbors[i];
                }
            }

            // Fallback
            return neighbors[^1];
        }

        /// <summary>
        /// Finds valid neighbors for pathing based on the current position and the map boundaries.
        /// Checks if the neighbor is within the map bounds, not in the Tabu list, and has a valid path socket.
        /// </summary>
        private List<(GridPos pos, List<TileVariant> variants)> GetValidNeighbors(GridPos pos, List<TileVariant> currentVariants)
        {
            var valid = new List<(GridPos pos, List<TileVariant> variants)>();

            // Index matches the socket index (0:N, 1:E, 2:S, 3:W)
            for (int i = 0; i < 4; i++)
            {
                GridPos n = pos.Step((Direction)i);

                if (IsInBounds(n) && !TabuList.Contains(n))
                {
                    Cell neighborCell = grid[n.X, n.Z];
                    int oppositeDir = (i + 2) % 4;

                    // Check that some current variant has a path exit in direction i
                    bool hasExit = false;
                    foreach (var v in currentVariants)
                    {
                        if (v.Sockets[i] == pathSocket)
                        {
                            hasExit = true;
                            break;
                        }
                    }

                    // If there are no exit sockets, skip this neighbor
                    if (!hasExit)
                    {
                        continue;
                    }

                    // Check if the neighbor has any variant that can connect to the current cell's exit socket
                    List<TileVariant> nextVariants = new List<TileVariant>();
                    foreach (var nv in neighborCell.AvailableVariants)
                    {
                        if (nv.Sockets[oppositeDir] == pathSocket)
                        {
                            nextVariants.Add(nv);
                        }
                    }

                    // If there are valid variants, add the neighbor to the list of valid neighbors
                    if (nextVariants.Count > 0)
                    {
                        valid.Add((n, nextVariants));
                    }
                }
            }

            return valid;
        }

        /// <summary>
        /// Helper method to check if a position is within the map bounds.
        /// </summary>
        private bool IsInBounds(GridPos p)
        {
            return p.X >= 0 && p.X < mapWidth &&
                   p.Z >= 0 && p.Z < mapDepth;
        }
    }
}
