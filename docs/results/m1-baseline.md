# M1 baseline: pure WFC and the hard-constraint hybrid

Measured 2026-10-19 on an AMD Ryzen 7 5800X, .NET SDK 10.0.112, Release build. Times depend
on the machine; success rates and counts depend only on the seeds. Every success is checked by
`MapValidator`. Tile sets are described in ADR 0002.

Outcomes: `ok` valid map; `wfc_fail` WFC contradiction; `aco_fail` ACO found no buildable
path; `invalid` the solver reported success but the map has a mismatched edge.
Dead ends are cells with exactly one road socket, not counting the hybrid's two endpoints.

## Pure WFC (reference solver), 30×30, seeds 0-999

| Tile set | ok | wfc_fail | Dead ends per ok map | Median time |
|---|---|---|---|---|
| rslike-overworld | 100.0 % | 0.0 % | 4.08 | 4.85 ms |
| rslike-no-diagonals | 100.0 % | 0.0 % | 6.17 | 4.96 ms |
| rslike-rare-ends | 100.0 % | 0.0 % | 0.77 | 5.12 ms |
| rslike-no-dead-ends | 82.9 % | 17.1 % | 0 | 4.29 ms |

## Hard-constraint hybrid (thesis ACO + WFC), 30×30, seeds 0-499

Endpoints are collapsed to `path_end` facing into the map (any road tile with an inward road
socket in `rslike-no-dead-ends`, which has no `path_end`) and propagated with `ForceCollapse`.

| Endpoints | Tile set | ok | aco_fail | wfc_fail | Dead ends | Path / Manhattan | Median time (ACO + WFC mean) |
|---|---|---|---|---|---|---|---|
| corners | rslike-overworld | 100.0 % | 0.0 % | 0.0 % | 15.37 | 1.10 | 20.50 ms (15.11 + 6.10) |
| corners | rslike-rare-ends | 100.0 % | 0.0 % | 0.0 % | 3.25 | 1.10 | 20.51 ms (15.15 + 6.08) |
| corners | rslike-no-dead-ends | 53.6 % | 0.0 % | 46.4 % | 0 | 1.10 | 17.66 ms (13.02 + 5.28) |
| random | rslike-overworld | 100.0 % | 0.0 % | 0.0 % | 12.73 | 1.11 | 15.75 ms (10.59 + 5.93) |
| random | rslike-rare-ends | 100.0 % | 0.0 % | 0.0 % | 2.81 | 1.11 | 16.27 ms (10.78 + 6.09) |
| random | rslike-no-dead-ends | 60.4 % | 0.0 % | 39.6 % | 0 | 1.06 | 12.75 ms (8.75 + 4.63) |

`corners` are (0,0) and (29,29); `random` endpoints are seeded and at least 30 steps apart.

## Thesis endpoint setup (`--thesis-endpoints`), corners, 30×30, seeds 0-499

`SetInitialCell` doesn't propagate the endpoints (ADR 0001), so a neighbour can contradict them.

| Tile set | ok | wfc_fail | invalid |
|---|---|---|---|
| rslike-overworld | 61.6 % | 0.0 % | 38.4 % |
| rslike-no-dead-ends | 20.2 % | 43.2 % | 36.6 % |

## Observations

- On the contradiction-prone set, forcing the ACO road raises WFC failures from 17.1 % (pure
  WFC) to 46.4 % (corners) and 39.6 % (random endpoints). ACO itself never failed at 30×30, so
  every hybrid failure here is a WFC contradiction around or away from the forced road.
- Forcing the road also multiplies dead ends (4.08 → 15.37 on the complete set). Likely cause,
  not yet verified: path cells accept any variant with road sockets towards the previous and next
  cell, including T-junctions and crossings, and the side branches they start have to end
  somewhere. Without `path_end` they can't, which would explain the failure increase above.
- Lowering the `path_end` weight tenfold cuts dead ends roughly 5× without changing the success
  rate: weights change choices, not which maps are valid.
- The thesis endpoint bug made about 37-38 % of hybrid maps invalid on both sets.

## Reproduce

```sh
dotnet build -c Release
B=bench/SoftWfcAco.Bench/bin/Release/net10.0/SoftWfcAco.Bench
T=data/tilesets
for t in rslike-overworld rslike-no-diagonals rslike-rare-ends rslike-no-dead-ends; do
  $B 30 30 1000 0 --tiles $T/$t.json
done
for e in corners random; do
  for t in rslike-overworld rslike-rare-ends rslike-no-dead-ends; do
    $B 30 30 500 0 --mode hybrid --endpoints $e --tiles $T/$t.json
  done
done
for t in rslike-overworld rslike-no-dead-ends; do
  $B 30 30 500 0 --mode hybrid --thesis-endpoints --tiles $T/$t.json
done
```
