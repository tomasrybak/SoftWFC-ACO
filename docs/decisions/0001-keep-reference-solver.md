# 1. Keep the thesis WFC solver unchanged as a reference

Date: 2026-10-18 · Status: accepted

## Context

`WFCSolver` is a port of the solver from my bachelor's thesis. The planned work (per-cell
priors, weighted entropy, a faster bitset solver) all changes how a cell is chosen and
collapsed. The research question is whether soft constraints change the contradiction rate
compared with the thesis approach, so the thesis behaviour has to stay measurable.

## Decision

`WFCSolver` stays behaviourally unchanged from here on. New behaviour goes into new code next
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
- Some code is duplicated between the reference and newer solvers. That is accepted for the
  sake of a stable baseline.
