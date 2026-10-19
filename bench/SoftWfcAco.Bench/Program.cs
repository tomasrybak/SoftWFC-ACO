using System.Diagnostics;
using System.Globalization;
using SoftWfcAco;
using SoftWfcAco.Viz;

// Usage: SoftWfcAco.Bench [width] [depth] [runs] [firstSeed] [options]
//   --tiles <file>         tile set JSON (default: smoke.json copied next to the executable)
//   --mode wfc|hybrid      pure WFC (default) or the thesis hard-constraint ACO + WFC hybrid
//   --endpoints <kind>     hybrid road endpoints: corners (default, opposite corners) or random
//   --endpoint-tile <name> tile the endpoints are collapsed to (default path_end)
//   --thesis-endpoints     set endpoints with SetInitialCell like the thesis (doesn't propagate them)
//   --csv <file>           one row per run
//   --png <dir>            final map of every run, mismatched edges in red
//   --frames <dir>         collapse frames of the first run (wfc mode)
//   --every <n>            collapses between frames (default 10)
//   --ascii                print the first run's map
// Run i uses seed firstSeed + i, so every result can be reproduced individually.
List<string> positional = new List<string>();
Dictionary<string, string> options = new Dictionary<string, string>();

for (int i = 0; i < args.Length; i++)
{
    if (!args[i].StartsWith("--"))
    {
        positional.Add(args[i]);
    }
    else if (args[i] == "--ascii" || args[i] == "--thesis-endpoints")
    {
        options[args[i][2..]] = "";
    }
    else if (i + 1 < args.Length)
    {
        options[args[i][2..]] = args[++i];
    }
    else
    {
        Console.Error.WriteLine($"Missing value for {args[i]}");
        return 1;
    }
}

int width = positional.Count > 0 ? int.Parse(positional[0]) : 30;
int depth = positional.Count > 1 ? int.Parse(positional[1]) : 30;
int runs = positional.Count > 2 ? int.Parse(positional[2]) : 100;
int firstSeed = positional.Count > 3 ? int.Parse(positional[3]) : 0;
int every = options.TryGetValue("every", out string? everyArg) ? int.Parse(everyArg) : 10;
string mode = options.GetValueOrDefault("mode", "wfc");
string endpointKind = options.GetValueOrDefault("endpoints", "corners");
string endpointTile = options.GetValueOrDefault("endpoint-tile", "path_end");
bool hybrid = mode == "hybrid";
bool thesisEndpoints = options.ContainsKey("thesis-endpoints");

if (mode != "wfc" && !hybrid)
{
    Console.Error.WriteLine($"Unknown mode '{mode}'");
    return 1;
}

if (endpointKind != "corners" && endpointKind != "random")
{
    Console.Error.WriteLine($"Unknown endpoints '{endpointKind}'");
    return 1;
}

string tilesPath = options.TryGetValue("tiles", out string? tilesArg)
    ? tilesArg
    : Path.Combine(AppContext.BaseDirectory, "tilesets", "smoke.json");
string tilesName = Path.GetFileNameWithoutExtension(tilesPath);
IReadOnlyList<TileVariant> variants = TileSet.Load(tilesPath).Variants;

// Must match ACOHybridSolver's default path socket
const string PathSocket = "path";

// Ants and endpoint placement get their own random streams, so WFC's stream starts the same as in
// a pure WFC run with the same seed (ACO's dry runs still draw from it during FindPath)
const int AcoStream = 0x2C1B3C6D;
const int EndpointStream = 0x297A2D39;

List<RunResult> results = new List<RunResult>(runs);

StreamWriter? csv = null;
if (options.TryGetValue("csv", out string? csvPath))
{
    string? csvDir = Path.GetDirectoryName(csvPath);
    if (!string.IsNullOrEmpty(csvDir))
    {
        Directory.CreateDirectory(csvDir);
    }

    csv = new StreamWriter(csvPath);
    csv.WriteLine("tiles,mode,endpoints,seed,width,depth,outcome,time_ms,aco_ms,wfc_ms,path_len,manhattan,dead_ends");
}

for (int i = 0; i < runs; i++)
{
    int seed = firstSeed + i;
    Cell[,] grid = NewGrid();

    RunResult result = hybrid ? RunHybrid(grid, seed) : RunWfc(grid, seed);
    results.Add(result);

    // Invariant culture: a Czech locale would write decimal commas into the CSV
    csv?.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{tilesName},{mode},{(hybrid ? endpointKind : "-")},{seed},{width},{depth},{result.Outcome}," +
        $"{result.TotalMs:F3},{result.AcoMs:F3},{result.WfcMs:F3},{result.PathLength},{result.Manhattan},{result.DeadEnds}"));

    if (options.TryGetValue("png", out string? pngDir))
    {
        // RunWFC keeps collapsing after a contradiction without propagating, so a failed pure WFC map
        // doesn't show where generation broke; an untimed stepwise rerun of the same seed does.
        // Hybrid maps are drawn as they are: the stepwise runner doesn't drive the hybrid.
        Cell[,] imageGrid = grid;
        if (!hybrid)
        {
            imageGrid = NewGrid();
            StepwiseRun.Run(new WFCSolver(imageGrid, width, depth, new Random(seed)));
        }

        RgbImage image = MapImage.Render(imageGrid, MapValidator.Validate(imageGrid));
        PngWriter.Save(image, Path.Combine(pngDir, $"seed{seed:D4}.png"));
    }

    if (i == 0 && !hybrid && options.TryGetValue("frames", out string? framesDir))
    {
        Cell[,] frameGrid = NewGrid();
        StepwiseRun.Run(new WFCSolver(frameGrid, width, depth, new Random(seed)), every,
            step => PngWriter.Save(MapImage.Render(frameGrid), Path.Combine(framesDir, $"step{step:D5}.png")));
    }

    if (i == 0 && options.ContainsKey("ascii"))
    {
        Console.WriteLine(AsciiMap.Render(grid));
    }
}

csv?.Dispose();
PrintSummary();
return 0;

// Pure WFC run of the reference solver
RunResult RunWfc(Cell[,] grid, int seed)
{
    Stopwatch sw = Stopwatch.StartNew();
    WFCSolver wfc = new WFCSolver(grid, width, depth, new Random(seed));
    bool ok = wfc.RunWFC();
    sw.Stop();

    double ms = sw.Elapsed.TotalMilliseconds;
    return new RunResult(Classify(grid, ok), ms, 0, ms, 0, 0, CountDeadEnds(grid, null));
}

// Thesis hybrid with failures attributed: FindPath and ApplyPath + RunWFC are called separately
// instead of RunACOHybrid, which returns null for both kinds of failure
RunResult RunHybrid(Cell[,] grid, int seed)
{
    Stopwatch total = Stopwatch.StartNew();
    WFCSolver wfc = new WFCSolver(grid, width, depth, new Random(seed));
    (GridPos start, GridPos end) = PickEndpoints(new Random(seed ^ EndpointStream));
    int manhattan = start.ManhattanDistance(end);
    HashSet<GridPos> endpoints = new HashSet<GridPos> { start, end };

    if (thesisEndpoints)
    {
        // SetInitialCell empties the cell before re-adding the allowed variants, which raises the
        // contradiction flag, so the endpoints never propagate. Kept to measure the thesis as it was
        if (!wfc.SetInitialCell(grid[start.X, start.Z], EndpointVariants(start))
            || !wfc.SetInitialCell(grid[end.X, end.Z], EndpointVariants(end)))
        {
            return new RunResult("setup_fail", total.Elapsed.TotalMilliseconds, 0, 0, 0, manhattan, 0);
        }
    }
    else
    {
        wfc.ForceCollapse(grid[start.X, start.Z], EndpointVariants(start));
        wfc.ForceCollapse(grid[end.X, end.Z], EndpointVariants(end));

        if (wfc.HasContradiction)
        {
            return new RunResult("setup_fail", total.Elapsed.TotalMilliseconds, 0, 0, 0, manhattan, 0);
        }
    }

    ACOHybridSolver aco = new ACOHybridSolver(grid, width, depth, wfc, PathSocket, rng: new Random(seed ^ AcoStream));

    Stopwatch acoSw = Stopwatch.StartNew();
    List<GridPos>? path = aco.FindPath(start, end);
    acoSw.Stop();

    if (path == null)
    {
        return new RunResult("aco_fail", total.Elapsed.TotalMilliseconds, acoSw.Elapsed.TotalMilliseconds, 0, 0, manhattan, 0);
    }

    Stopwatch wfcSw = Stopwatch.StartNew();
    aco.ApplyPath(path);
    bool ok = wfc.RunWFC();
    wfcSw.Stop();
    total.Stop();

    // The cleaned path includes both endpoints, so its step count is Count - 1
    return new RunResult(Classify(grid, ok), total.Elapsed.TotalMilliseconds, acoSw.Elapsed.TotalMilliseconds,
        wfcSw.Elapsed.TotalMilliseconds, path.Count - 1, manhattan, CountDeadEnds(grid, endpoints));
}

// A success counts only if the map passes the independent validator
string Classify(Cell[,] grid, bool solverOk)
{
    if (!solverOk)
    {
        return "wfc_fail";
    }

    return MapValidator.Validate(grid).Count == 0 ? "ok" : "invalid";
}

// Opposite corners give every run the same Manhattan distance (thesis setup);
// random pairs at least half the map apart are closer to how the game places roads
(GridPos, GridPos) PickEndpoints(Random rng)
{
    if (endpointKind == "corners")
    {
        return (new GridPos(0, 0), new GridPos(width - 1, depth - 1));
    }

    int minDistance = (width + depth) / 2;
    while (true)
    {
        GridPos a = new GridPos(rng.Next(width), rng.Next(depth));
        GridPos b = new GridPos(rng.Next(width), rng.Next(depth));

        if (a.ManhattanDistance(b) >= minDistance)
        {
            return (a, b);
        }
    }
}

// Variants of the endpoint tile with a road socket facing into the map, so ants can leave the cell.
// Sets without the endpoint tile fall back to any road tile with such a socket.
List<TileVariant> EndpointVariants(GridPos pos)
{
    List<TileVariant> inward = new List<TileVariant>();

    foreach (TileVariant v in variants)
    {
        for (int side = 0; side < 4; side++)
        {
            GridPos n = pos.Step((Direction)side);
            bool inBounds = n.X >= 0 && n.X < width && n.Z >= 0 && n.Z < depth;

            if (inBounds && v.Sockets[side] == PathSocket)
            {
                inward.Add(v);
                break;
            }
        }
    }

    List<TileVariant> named = inward.Where(v => v.Def.Name == endpointTile).ToList();
    return named.Count > 0 ? named : inward;
}

// Cells whose tile has exactly one road socket: roads leading nowhere. Endpoints are excluded
int CountDeadEnds(Cell[,] grid, HashSet<GridPos>? endpoints)
{
    int count = 0;

    foreach (Cell cell in grid)
    {
        TileVariant? v = cell.CollapsedVariant;
        if (v == null || (endpoints != null && endpoints.Contains(cell.Position)))
        {
            continue;
        }

        if (v.Sockets.Count(s => s == PathSocket) == 1)
        {
            count++;
        }
    }

    return count;
}

void PrintSummary()
{
    string header = hybrid ? $"Hybrid ({endpointKind} endpoints{(thesisEndpoints ? ", thesis setup" : "")})" : "Pure WFC";
    Console.WriteLine($"{header}, {tilesName}, {width}x{depth}, {runs} runs, seeds {firstSeed}..{firstSeed + runs - 1}");

    foreach (string outcome in new[] { "ok", "aco_fail", "wfc_fail", "invalid", "setup_fail" })
    {
        int n = results.Count(r => r.Outcome == outcome);
        if (n > 0 || outcome == "ok")
        {
            Console.WriteLine($"  {outcome + ":",-12} {100.0 * n / runs,5:F1} %");
        }
    }

    List<RunResult> ok = results.Where(r => r.Outcome == "ok").ToList();
    if (ok.Count > 0)
    {
        Console.WriteLine($"  dead ends:   mean {ok.Average(r => r.DeadEnds):F2} per successful map");
        if (hybrid)
        {
            Console.WriteLine($"  path/Manhattan: mean {ok.Average(r => (double)r.PathLength / r.Manhattan):F2}");
        }
    }

    List<double> times = results.Select(r => r.TotalMs).Order().ToList();
    Console.WriteLine($"  time (ms):   median {times[runs / 2]:F2}, mean {times.Average():F2}, max {times[^1]:F2}");
    if (hybrid)
    {
        Console.WriteLine($"  of which:   ACO mean {results.Average(r => r.AcoMs):F2}, WFC mean {results.Average(r => r.WfcMs):F2}");
    }
}

// Fresh uncollapsed grid where every cell allows all variants
Cell[,] NewGrid()
{
    Cell[,] grid = new Cell[width, depth];

    for (int x = 0; x < width; x++)
    {
        for (int z = 0; z < depth; z++)
        {
            grid[x, z] = new Cell(new GridPos(x, z), variants);
        }
    }

    return grid;
}

/// <summary>
/// One benchmark run. Outcome is ok, invalid, wfc_fail, aco_fail or setup_fail.
/// PathLength is in steps; DeadEnds counts cells with exactly one road socket, endpoints excluded.
/// </summary>
sealed record RunResult(string Outcome, double TotalMs, double AcoMs, double WfcMs, int PathLength, int Manhattan, int DeadEnds);
