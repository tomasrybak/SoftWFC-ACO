# 1. Keep the thesis WFC solver unchanged as a reference

Date: 2026-10-18 · Status: accepted

## Context

`WFCSolver` is a port of the solver from my bachelor's thesis. The planned work (per-cell
priors, weighted entropy, a faster bitset solver) all changes how a cell is chosen and
collapsed. The research question is whether soft constraints change the contradiction rate
compared with the thesis approach, so the thesis behaviour has to stay measurable.

## Decision

`WFCSolver` and `ACOHybridSolver` stay behaviourally unchanged from here on. New behaviour goes into new code next
to it. Changes to the reference are limited to fixes that don't alter which map a seed
produces (for example the uncollapsed-cell counter and the internal setters on `Cell`).

The one deliberate deviation from the thesis happened before this decision: the collapse
picks variants by tile weight (commit 6ebb124). The thesis stored weights but picked
uniformly. The reference is therefore "thesis solver with weighted collapse".

Debug tools drive it only through its public API: `StepwiseRun` records collapse frames
by calling `TryCollapseNextCell` instead of adding hooks to the solver.

## Consequences

- Every new solver can be compared with the reference on the same seed list, and a uniform
  prior must reproduce the reference maps exactly. That is the regression test for the
  prior mechanism.
- Known quirks of the thesis solver stay in the reference on purpose and are fixed only in
  new code. Examples: entropy ties are broken by heap insertion order, so cells collapse in
  scan order; after a contradiction `RunWFC` keeps collapsing without propagating.
- `SetInitialCell` (thesis code, unchanged) empties the cell before re-adding the allowed
  variants. The empty moment raises the contradiction flag, so the endpoint never propagates
  and the first dry run in `FindPath` reports a false contradiction. Found 2026-10-19 while
  setting up the hybrid benchmark; with the thesis setup 38.4 % of hybrid maps on
  `rslike-overworld` are invalid (mismatch next to an endpoint), see
  `docs/results/m1-baseline.md`. The bench sets endpoints with `ForceCollapse` instead and keeps
  the thesis setup behind `--thesis-endpoints`. A characterization test pins the behaviour.
- Some code is duplicated between the reference and newer solvers. That is accepted for the
  sake of a stable baseline.
