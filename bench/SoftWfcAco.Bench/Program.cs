using System.Diagnostics;
using System.Globalization;
using SoftWfcAco;
using SoftWfcAco.Viz;

// Usage: SoftWfcAco.Bench [width] [depth] [runs] [firstSeed] [options]
//   --tiles <file>    tile set JSON (default: smoke.json copied next to the executable)
//   --csv <file>      one row per run
//   --png <dir>       final map of every run, mismatched edges in red
//   --frames <dir>    collapse frames of the first run
//   --every <n>       collapses between frames (default 10)
//   --ascii           print the first run's map
// Run i uses seed firstSeed + i, so every result can be reproduced individually.
List<string> positional = new List<string>();
Dictionary<string, string> options = new Dictionary<string, string>();

for (int i = 0; i < args.Length; i++)
{
    if (!args[i].StartsWith("--"))
    {
        positional.Add(args[i]);
    }
    else if (args[i] == "--ascii")
    {
        options["ascii"] = "";
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

string tilesPath = options.TryGetValue("tiles", out string? tilesArg)
    ? tilesArg
    : Path.Combine(AppContext.BaseDirectory, "tilesets", "smoke.json");
string tilesName = Path.GetFileNameWithoutExtension(tilesPath);
IReadOnlyList<TileVariant> variants = TileSet.Load(tilesPath).Variants;

int successes = 0;
int invalid = 0;
List<double> times = new List<double>(runs);

StreamWriter? csv = null;
if (options.TryGetValue("csv", out string? csvPath))
{
    string? csvDir = Path.GetDirectoryName(csvPath);
    if (!string.IsNullOrEmpty(csvDir))
    {
        Directory.CreateDirectory(csvDir);
    }

    csv = new StreamWriter(csvPath);
    csv.WriteLine("tiles,seed,width,depth,success,invalid,time_ms");
}

for (int i = 0; i < runs; i++)
{
    int seed = firstSeed + i;
    Cell[,] grid = NewGrid();

    Stopwatch sw = Stopwatch.StartNew();
    WFCSolver wfc = new WFCSolver(grid, width, depth, new Random(seed));
    bool ok = wfc.RunWFC();
    sw.Stop();

    // A success is counted only if the map passes the independent validator
    bool valid = ok && MapValidator.Validate(grid).Count == 0;
    if (valid)
    {
        successes++;
    }
    else if (ok)
    {
        invalid++;
    }

    times.Add(sw.Elapsed.TotalMilliseconds);

    // Invariant culture: a Czech locale would write decimal commas into the CSV
    csv?.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{tilesName},{seed},{width},{depth},{(valid ? 1 : 0)},{(ok && !valid ? 1 : 0)},{sw.Elapsed.TotalMilliseconds:F3}"));

    // Images come from a separate, untimed run. RunWFC keeps collapsing after a contradiction
    // without propagating, so its failed maps don't show where generation broke.
    if (options.TryGetValue("png", out string? pngDir))
    {
        Cell[,] imageGrid = NewGrid();
        StepwiseRun.Run(new WFCSolver(imageGrid, width, depth, new Random(seed)));
        RgbImage image = MapImage.Render(imageGrid, MapValidator.Validate(imageGrid));
        PngWriter.Save(image, Path.Combine(pngDir, $"seed{seed:D4}.png"));
    }

    if (i == 0 && options.TryGetValue("frames", out string? framesDir))
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

times.Sort();
Console.WriteLine($"Pure WFC, {tilesName}, {width}x{depth}, {runs} runs, seeds {firstSeed}..{firstSeed + runs - 1}");
Console.WriteLine($"  success rate:  {100.0 * successes / runs:F1} %");
if (invalid > 0)
{
    Console.WriteLine($"  INVALID:       {invalid} runs reported success but failed validation");
}

Console.WriteLine($"  time (ms):     median {times[runs / 2]:F2}, mean {times.Average():F2}, max {times[^1]:F2}");
return 0;

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
