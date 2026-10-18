namespace SoftWfcAco
{
    /// <summary>
    /// Class for the WFC solver.
    /// It handles the main WFC algorithm and its constraints.
    /// Ported from the thesis 3D solver to a 2D grid (floors and Up/Down sockets removed).
    /// </summary>
    public class WFCSolver
    {
        // Map dimensions and grid
        private readonly Cell[,] grid;
        private readonly int mapWidth;
        private readonly int mapDepth;

        //History of removed variants for backtracking
        private readonly Stack<(Cell cell, TileVariant removedVariant)> removalHistory = new Stack<(Cell cell, TileVariant removedVariant)>();

        // Stack of snapshot markers for backtracking
        private readonly Stack<int> snapshotMarkers = new Stack<int>();

        private readonly CellMinHeap entropyHeap; // Min-heap for entropy-based priority queue

        // Flags for solver state
        private bool contradictionFound = false;

        public bool HasContradiction => contradictionFound;

        // Kept in sync by CollapseCell and RestoreSnapshot so IsFullyCollapsed is O(1)
        private int uncollapsedCount;

        private readonly Random rng;

        /// <summary>
        /// Constructor for the WFCSolver class.
        /// Initializes the solver with the grid, width and depth of the map.
        /// </summary>
        /// <param name="grid">The 2D grid of cells representing the map, indexed [x, z]</param>
        /// <param name="width">Width of the map</param>
        /// <param name="depth">Depth of the map</param>
        public WFCSolver(Cell[,] grid, int width, int depth, Random? rng = null)
        {
            this.grid = grid;
            this.mapWidth = width;
            this.mapDepth = depth;
            this.rng = rng ?? new Random();

            this.entropyHeap = new CellMinHeap();
            InitializeEntropyHeap();
        }

        /// <summary>
        /// Initializes the entropy heap with all cells and their entropy values.
        /// </summary>
        private void InitializeEntropyHeap()
        {
            entropyHeap.Clear();
            uncollapsedCount = 0;
            foreach (var cell in grid)
            {
                if (cell.IsCollapsed)
                {
                    continue;
                }

                uncollapsedCount++;
                entropyHeap.Enqueue(cell, cell.Entropy);
            }
        }

        /// <summary>
        /// Applies boundary constraints to the map by removing any tile variants whose
        /// outward-facing socket on the map edge is not the boundary socket.
        /// </summary>
        /// <param name="boundarySocket">Socket every edge-facing side must have (the thesis used "wall").</param>
        public void ApplyBoundaryConstraints(string boundarySocket)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int z = 0; z < mapDepth; z++)
                {
                    Cell cell = grid[x, z];
                    List<TileVariant> toRemove = new List<TileVariant>();

                    foreach (var variant in cell.AvailableVariants)
                    {
                        bool isValid = true;

                        if (z == mapDepth - 1 && variant.Sockets[0] != boundarySocket) isValid = false;
                        if (x == mapWidth - 1 && variant.Sockets[1] != boundarySocket) isValid = false;
                        if (z == 0 && variant.Sockets[2] != boundarySocket) isValid = false;
                        if (x == 0 && variant.Sockets[3] != boundarySocket) isValid = false;

                        if (!isValid) toRemove.Add(variant);
                    }

                    foreach (var variant in toRemove)
                    {
                        cell.AvailableVariants.Remove(variant);
                    }
                }
            }
        }

        /// <summary>
        /// Force the solver to collapse a cell to a specific variant.
        /// Removes all variants that are not in the allowedVariants list.
        /// Used for setting start and finish tiles.
        /// </summary>
        public void ForceCollapse(Cell cell, List<TileVariant> allowedVariants)
        {
            List<TileVariant> toRemove = new List<TileVariant>();
            foreach (var variant in cell.AvailableVariants)
            {
                if (!allowedVariants.Contains(variant))
                {
                    toRemove.Add(variant);
                }
            }

            foreach (var variant in toRemove)
            {
                RemoveVariantFromCell(cell, variant);
            }

            CollapseCell(cell);
            Propagate(cell);
        }

        /// <summary>
        /// Helper method for setting the initial cell to a specific variant.
        /// Used for setting start and finish tiles.
        /// </summary>
        /// <returns>False if none of the initial variants is available.</returns>
        public bool SetInitialCell(Cell cell, List<TileVariant> initialVariants)
        {
            // Remove all variants that are not in the initialVariants list
            List<TileVariant> oldVariants = new List<TileVariant>(cell.AvailableVariants);
            foreach (var variant in oldVariants)
            {
                RemoveVariantFromCell(cell, variant);
            }

            // Add the allowed initial variants back to the cell's available variants
            cell.AvailableVariants.AddRange(initialVariants);

            if (cell.AvailableVariants.Count == 0)
            {
                return false;
            }

            // Collapse and propagate the cell to set the initial cells
            CollapseCell(cell);
            Propagate(cell);
            return true;
        }

        /// <summary>
        /// Main WFC algorithm.
        /// It repeatedly collapses the cell with the lowest entropy and propagates the constraints
        /// until the map is fully collapsed or a failure occurs (no valid variants for a cell).
        /// </summary>
        /// <returns>True if the map was successfully generated, false if a failure occurred.</returns>
        public bool RunWFC()
        {
            contradictionFound = false;

            while (!IsFullyCollapsed())
            {
                Cell? nextCell = GetCellWithLowestEntropyHeap();

                if (nextCell == null || nextCell.Entropy == 0)
                {
                    return false; // Failure - no cell to collapse or a cell with no valid variants
                }

                CollapseCell(nextCell);
                Propagate(nextCell);
            }

            return true;
        }

        /// <summary>
        /// Checks if the map is fully collapsed.
        /// </summary>
        /// <returns>True if the map is fully collapsed, false otherwise</returns>
        public bool IsFullyCollapsed()
        {
            return uncollapsedCount == 0;
        }

        /// <summary>
        /// Retrieves the cell with the lowest entropy from the min-heap.
        /// Actively discards stale and ghost references.
        /// </summary>
        /// <returns>The optimal uncollapsed Cell, or null if generation is complete.</returns>
        private Cell? GetCellWithLowestEntropyHeap()
        {
            while (entropyHeap.Count > 0)
            {
                var node = entropyHeap.Dequeue();
                Cell cell = node.cell;
                int storedPriority = node.priority;

                // Check for collapsed cells or ghost entries
                if (cell.IsCollapsed || cell.Entropy != storedPriority)
                {
                    continue;
                }

                // Valid uncollapsed cell with available variants found
                if (cell.Entropy > 0)
                {
                    return cell;
                }
            }

            return null;
        }

        /// <summary>
        /// Re-enqueues a cell into the heap when its entropy is reduced during propagation.
        /// </summary>
        private void UpdateCellEntropyInHeap(Cell cell)
        {
            if (!cell.IsCollapsed && cell.Entropy > 0)
            {
                entropyHeap.Enqueue(cell, cell.Entropy);
            }
        }

        /// <summary>
        /// Collapses the next lowest-entropy cell and propagates constraints.
        /// </summary>
        /// <param name="collapsedCell">Outputs the collapsed cell.</param>
        /// <returns>False if a contradiction occurred or no valid cell exists.</returns>
        public bool TryCollapseNextCell(out Cell? collapsedCell)
        {
            collapsedCell = GetCellWithLowestEntropyHeap();
            if (collapsedCell == null || collapsedCell.Entropy == 0)
            {
                return false;
            }

            CollapseCell(collapsedCell);
            Propagate(collapsedCell);
            return true;
        }

        /// <summary>
        /// Collapses a cell by choosing a weighted random variant from its available variants.
        /// </summary>
        /// <param name="cell">Cell to be collapsed</param>
        private void CollapseCell(Cell cell)
        {
            if (cell.AvailableVariants.Count == 0)
            {
                return;
            }

            cell.CollapsedVariant = PickWeighted(cell.AvailableVariants);

            List<TileVariant> toRemove = new List<TileVariant>();

            foreach (var variant in cell.AvailableVariants)
            {
                if (variant != cell.CollapsedVariant)
                {
                    toRemove.Add(variant);
                }
            }

            foreach (var variant in toRemove)
            {
                RemoveVariantFromCell(cell, variant);
            }

            if (!cell.IsCollapsed)
            {
                uncollapsedCount--;
            }
            cell.IsCollapsed = true;
        }

        /// <summary>
        /// Roulette-wheel selection: each variant's chance is proportional to its weight.
        /// </summary>
        private TileVariant PickWeighted(List<TileVariant> variants)
        {
            double total = 0;
            foreach (var v in variants)
            {
                total += v.Weight;
            }

            double roll = rng.NextDouble() * total;
            double cumulative = 0;
            foreach (var v in variants)
            {
                cumulative += v.Weight;
                if (roll < cumulative)
                {
                    return v;
                }
            }

            // Floating point rounding can leave roll == total
            return variants[^1];
        }

        /// <summary>
        /// Helper for hybrid solvers that need deterministic collapse and propagation sequencing.
        /// </summary>
        public void CollapseAndPropagate(Cell cell)
        {
            CollapseCell(cell);
            Propagate(cell);
        }

        /// <summary>
        /// Propagates the collapsed cell's variant to its neighboring cells.
        /// </summary>
        /// <param name="collapsedCell">Collapsed cell</param>
        private void Propagate(Cell collapsedCell)
        {
            Stack<Cell> stack = new Stack<Cell>();
            stack.Push(collapsedCell);

            while (stack.Count > 0 && !contradictionFound)
            {
                Cell current = stack.Pop();

                // Check all 4 directions, index matches the socket index (0:N, 1:E, 2:S, 3:W)
                for (int i = 0; i < 4; i++)
                {
                    GridPos nPos = current.Position.Step((Direction)i);

                    // Check if the neighbor is within the map bounds
                    if (nPos.X >= 0 && nPos.X < mapWidth &&
                        nPos.Z >= 0 && nPos.Z < mapDepth)
                    {
                        Cell neighbor = grid[nPos.X, nPos.Z];

                        if (neighbor.IsCollapsed)
                        {
                            continue;
                        }

                        // Check if the neighbor's variant is compatible with the current cell's variant
                        bool changed = ConstrainNeighbor(current, neighbor, i);

                        if (changed)
                        {
                            stack.Push(neighbor);

                            // Re-enqueue the neighbor in the heap to update its priority based on the new entropy
                            if (!neighbor.IsCollapsed)
                            {
                                UpdateCellEntropyInHeap(neighbor);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reduces the possible tile variants for the neighbor Cell by enforcing socket compatibility
        /// </summary>
        /// <param name="current">The current Cell that is influencing its neighbor.</param>
        /// <param name="neighbor">The neighboring Cell whose available variants are being constrained.</param>
        /// <param name="directionIndex">The direction from the current Cell to the neighbor Cell</param>
        /// <returns>
        /// Returns true if the neighbor's list of possible variants has changed, otherwise false.
        /// </returns>
        private bool ConstrainNeighbor(Cell current, Cell neighbor, int directionIndex)
        {
            bool changed = false;
            int neighborSideIndex = GetOppositeSide(directionIndex);

            List<TileVariant> toRemove = new List<TileVariant>();

            foreach (var neighborVariant in neighbor.AvailableVariants)
            {
                bool possible = false;
                foreach (var currentVariant in current.AvailableVariants)
                {
                    // Check for socket compatibility
                    if (currentVariant.Sockets[directionIndex] == neighborVariant.Sockets[neighborSideIndex])
                    {
                        possible = true;
                        break;
                    }
                }

                if (!possible)
                {
                    toRemove.Add(neighborVariant);
                    changed = true;
                }
            }

            foreach (var variant in toRemove)
            {
                RemoveVariantFromCell(neighbor, variant);
            }

            return changed;
        }

        /// <summary>
        /// Returns the opposite side of a direction index (North -> South, East -> West...).
        /// </summary>
        public int GetOppositeSide(int directionIndex)
        {
            return (int)((Direction)directionIndex).Opposite();
        }

        /// <summary>
        /// Saves the current state of the solver - the number of removed variants.
        /// </summary>
        public void SaveSnapshot()
        {
            snapshotMarkers.Push(removalHistory.Count);
        }

        /// <summary>
        /// Restores the removed variant from the stack to the last saved snapshot.
        /// </summary>
        public void RestoreSnapshot()
        {
            if (snapshotMarkers.Count == 0)
            {
                return;
            }

            // Reset the contradiction flag
            contradictionFound = false;

            int targetHistorySize = snapshotMarkers.Pop();

            while (removalHistory.Count > targetHistorySize)
            {
                var record = removalHistory.Pop();

                // Add the removed variant back to the cell's available variants
                record.cell.AvailableVariants.Add(record.removedVariant);

                // Reset the cell's collapsed variant to null
                if (record.cell.IsCollapsed)
                {
                    uncollapsedCount++;
                }
                record.cell.IsCollapsed = false;
                record.cell.CollapsedVariant = null;
            }
        }

        /// <summary>
        /// Removes a variant from a cell's available variants.
        /// </summary>
        /// <param name="cell">The cell from which the variant is being removed.</param>
        /// <param name="variant">The tile variant that is being removed from the cell's available variants.</param>
        private void RemoveVariantFromCell(Cell cell, TileVariant variant)
        {
            cell.AvailableVariants.Remove(variant);
            removalHistory.Push((cell, variant));

            if (cell.AvailableVariants.Count == 0)
            {
                contradictionFound = true;
            }
        }
    }
}
