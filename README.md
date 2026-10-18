# SoftWFC-ACO

Procedural map generation that combines **Wave Function Collapse (WFC)** with **Ant Colony Optimization (ACO)**, extended with **soft constraints**. It's a .NET library with no engine or rendering dependencies.

This project continues my bachelor's thesis, [WFC-ACO-Hybrid](https://github.com/tomasrybak/WFC-ACO-Hybrid) (Unity, C#; thesis text in Czech in that repo). It picks up the main open question the thesis left behind.

## Background

WFC fills a grid by repeatedly collapsing the cell with the fewest remaining tile options and propagating adjacency rules to its neighbours. On long routes it often runs into contradictions. In the thesis, an ant colony first finds a path through the uncollapsed grid. That path is then committed to WFC as a **hard constraint**: the road tiles are forced, and the rest of the map collapses around them.

The thesis assignment originally asked for **soft constraints**, where pheromones *bias* the WFC choice instead of forcing it. I implemented hard constraints, partly because of time. I also argued that soft ones are risky: a biased choice can conflict with already-collapsed neighbours and *raise* the contradiction rate. The thesis never tested that hypothesis. Its conclusion names soft constraints as the main direction for further work.

This repository is that further work.

## Scope

The thesis generated 3D voxel environments. This library targets **2D tile grids**, because I'm using it to generate the overworld in a top-down game. Tile sets are plain data (sockets and weights) instead of hand-modelled Unity prefabs, so larger and more varied tile sets are cheap to try.

## Approach

Each cell gets its own weight for each tile variant:

```
w(cell, v) = baseWeight(v) × prior(cell, v)
```

- **Prior:** a per-cell bias from an external source. The first prior is a heightmap (low ground → water is likely). The second is the ACO pheromone field (strong trail → road is likely). Both plug into the same mechanism, so they can be compared directly.
- **Softness:** a single parameter sweeps from "the prior decides" (close to hard constraints) to "the prior is ignored" (pure WFC). That makes the thesis hypothesis measurable as a curve, not a yes/no answer.
- **Weighted entropy:** the next cell to collapse is chosen by Shannon entropy over the weights instead of by the number of options, so cells the prior is confident about collapse first.

### Questions to answer

1. Does the contradiction rate rise with soft constraints compared with hard ones, and at what softness?
2. When a run fails, is the cause the ACO path search or a WFC contradiction? The thesis didn't separate the two.
3. How do time and map quality scale above the ~5000 cells the thesis tested?
4. Does the hard vs soft result hold across tile sets, from a complete set (a tile for every socket combination) to a sparse one?

All benchmarks use fixed seed lists, so every number can be reproduced. The thesis benchmarks were unseeded.

## Status

| Step | State |
|---|---|
| Port of the thesis WFC + ACO solvers to engine-agnostic .NET (2D grid) | done |
| Weighted collapse (tile weights were stored but unused in the thesis) | done |
| Seeded benchmark runner (pure WFC) | done |
| Per-cell priors + weighted entropy | next |
| Heightmap prior, softness sweep | planned |
| Pheromone prior: soft vs hard comparison | planned |
| Failure attribution and scaling benchmarks | planned |

Results will be added here as they're measured.

## Layout

```
src/SoftWfcAco/          the library: WFCSolver, ACOHybridSolver, Ant, tiles and grid types
bench/SoftWfcAco.Bench/  seeded benchmark runner
tests/SoftWfcAco.Tests/  unit tests and seed-sweep validity tests (xUnit)
```

## Build and run

Requires the .NET 10 SDK.

```sh
dotnet build -c Release
dotnet test
dotnet run -c Release --project bench/SoftWfcAco.Bench -- 30 30 100 0   # width depth runs firstSeed
```

## License

MIT. See [LICENSE](LICENSE).
