namespace SoftWfcAco.Viz
{
    /// <summary>
    /// Drives a WFCSolver one collapse at a time through its public API, so the collapse can be
    /// recorded without changing the reference solver. Consumes the RNG exactly like RunWFC,
    /// so a successful run produces the same map.
    /// </summary>
    public static class StepwiseRun
    {
        /// <summary>
        /// Collapses until the map is done or the first contradiction.
        /// Unlike RunWFC it stops at the contradiction, so the map shows where it happened.
        /// </summary>
        /// <param name="wfc">Solver on a fresh grid</param>
        /// <param name="every">Call onFrame after every this many collapses</param>
        /// <param name="onFrame">Receives the step number; also called once for the final state</param>
        /// <returns>True if the map was fully collapsed.</returns>
        public static bool Run(WFCSolver wfc, int every = 0, Action<int>? onFrame = null)
        {
            int step = 0;

            while (!wfc.IsFullyCollapsed())
            {
                if (!wfc.TryCollapseNextCell(out _) || wfc.HasContradiction)
                {
                    onFrame?.Invoke(step);
                    return false;
                }

                step++;

                if (every > 0 && step % every == 0)
                {
                    onFrame?.Invoke(step);
                }
            }

            if (every <= 0 || step % every != 0)
            {
                onFrame?.Invoke(step);
            }

            return true;
        }
    }
}
