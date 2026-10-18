using System.Diagnostics;
using SoftWfcAco;

// Usage: SoftWfcAco.Bench [width] [depth] [runs] [firstSeed]
// Run i uses seed firstSeed + i, so every result can be reproduced individually.
int width = args.Length > 0 ? int.Parse(args[0]) : 30;
int depth = args.Length > 1 ? int.Parse(args[1]) : 30;
int runs = args.Length > 2 ? int.Parse(args[2]) : 100;
int firstSeed = args.Length > 3 ? int.Parse(args[3]) : 0;

List<TileVariant> variants = TileVariant.Expand(BenchTiles.All);

int successes = 0;
int invalid = 0;
var times = new List<double>(runs);

for (int i = 0; i < runs; i++)
{
    var rng = new Random(firstSeed + i);
    var grid = new Cell[width, depth];
    for (int x = 0; x < width; x++)
        for (int z = 0; z < depth; z++)
            grid[x, z] = new Cell(new GridPos(x, z), variants);

    var sw = Stopwatch.StartNew();
    var wfc = new WFCSolver(grid, width, depth, rng);
    bool ok = wfc.RunWFC();
    sw.Stop();

    // A success is counted only if the map passes the independent validator
    if (ok && MapValidator.Validate(grid).Count == 0)
    {
        successes++;
    }
    else if (ok)
    {
        invalid++;
    }

    times.Add(sw.Elapsed.TotalMilliseconds);
}

times.Sort();
Console.WriteLine($"Pure WFC {width}x{depth}, {runs} runs, seeds {firstSeed}..{firstSeed + runs - 1}");
Console.WriteLine($"  success rate:  {100.0 * successes / runs:F1} %");
if (invalid > 0)
{
    Console.WriteLine($"  INVALID:       {invalid} runs reported success but failed validation");
}

Console.WriteLine($"  time (ms):     median {times[runs / 2]:F2}, mean {times.Average():F2}, max {times[^1]:F2}");

/// <summary>
/// Small land/road/water smoke-test tile set. Pure WFC always solves it; contradiction-prone
/// sets for the hard vs soft comparison are still to be added.
/// </summary>
static class BenchTiles
{
    public static readonly TileDef[] All =
    [
        new() { Name = "grass", Sockets = ["grass", "grass", "grass", "grass"], Weight = 10 },
        new() { Name = "path_straight", Sockets = ["path", "grass", "path", "grass"], Weight = 1, Rotatable = true },
        new() { Name = "path_corner", Sockets = ["path", "path", "grass", "grass"], Weight = 1, Rotatable = true },
        new() { Name = "path_end", Sockets = ["path", "grass", "grass", "grass"], Weight = 0.2f, Rotatable = true },
        new() { Name = "water", Sockets = ["water", "water", "water", "water"], Weight = 4, Walkable = false },
        new() { Name = "shore", Sockets = ["water", "grass", "grass", "grass"], Weight = 0.5f, Rotatable = true, Walkable = false },
    ];
}
