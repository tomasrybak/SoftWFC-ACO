# 2. Use a road set without dead ends as the contradiction-prone baseline

Date: 2026-10-19 · Status: accepted

## Context

Comparing hard and soft constraints by contradiction rate needs a tile set on which the
baseline actually fails; on a set that never fails every comparison reads 0 % vs 0 %.

The first candidate was RSlike's overworld set without the two diagonal shore tiles
(`shore_diag_*`). Without them a checkerboard of water and land corners has no fitting tile,
so the expectation was that WFC would get stuck. It doesn't: pure WFC succeeded in 2000 of
2000 runs at 30×30 and 300 of 300 at 60×60, also with all weights set to 1.

The reason is that the missing combination is a local constraint. It involves one cell and
its direct neighbours, and propagation (arc consistency) removes the impossible options from
the cell as soon as one neighbour fixes two of its corners, before the other neighbour
collapses. Nothing is committed that later turns out impossible.

Removing `path_end` gives a long-range constraint instead: a road can't stop, so it has to
reach the map border or close a loop. No single cell or neighbour pair can see that, and pure
WFC fails in 17.1 % of runs (30×30, seeds 0-999).

## Decision

The contradiction-prone baseline set is `data/tilesets/rslike-no-dead-ends.json`: RSlike's
overworld set without `path_end`. All sets are snapshots in `data/tilesets/`, so numbers don't
change when the game's tiles do:

| File | Purpose |
|---|---|
| `rslike-overworld.json` | Complete set, the game's tiles as of RSlike commit 2c4c03c |
| `rslike-no-dead-ends.json` | Contradiction-prone baseline |
| `rslike-rare-ends.json` | `path_end` at weight 0.02 instead of 0.2: dead ends possible but rare |
| `rslike-no-diagonals.json` | Kept as the negative result above |
| `smoke.json` | Small set used by the bench by default |

## Consequences

- The sets fail because of roads, which is the part ACO is responsible for, so hard and soft
  road constraints are compared on the kind of failure they claim to address.
- Weights don't change which tiles are possible, only which are chosen. `rslike-rare-ends`
  therefore behaves like the complete set in success rate; it differs in how many dead ends
  the maps contain.
- Missing local combinations are not a useful way to make WFC fail. Long-range rules are.
