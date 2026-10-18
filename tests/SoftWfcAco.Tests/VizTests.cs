using System.Buffers.Binary;
using System.IO.Compression;
using SoftWfcAco.Viz;

namespace SoftWfcAco.Tests
{
    public class VizTests
    {
        private static readonly List<TileVariant> SmokeVariants = TileVariant.Expand(TestTiles.Smoke);

        [Fact]
        public void StepwiseRun_ReproducesRunWfc()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                Cell[,] reference = TestTiles.NewGrid(20, 20, SmokeVariants);
                bool referenceOk = new WFCSolver(reference, 20, 20, new Random(seed)).RunWFC();

                Cell[,] stepwise = TestTiles.NewGrid(20, 20, SmokeVariants);
                bool stepwiseOk = StepwiseRun.Run(new WFCSolver(stepwise, 20, 20, new Random(seed)));

                Assert.Equal(referenceOk, stepwiseOk);
                Assert.Equal(TestTiles.Fingerprint(reference), TestTiles.Fingerprint(stepwise));
            }
        }

        [Fact]
        public void StepwiseRun_CallsFrameEveryNStepsAndOnceAtTheEnd()
        {
            Cell[,] grid = TestTiles.NewGrid(5, 5, SmokeVariants);
            List<int> frames = new List<int>();

            StepwiseRun.Run(new WFCSolver(grid, 5, 5, new Random(0)), every: 10, onFrame: frames.Add);

            Assert.Equal([10, 20, 25], frames);
        }

        [Fact]
        public void AsciiMap_DrawsNorthAtTheTop()
        {
            List<TileVariant> variants = TileVariant.Expand([TestTiles.Grass, TestTiles.Water, TestTiles.PathStraight]);
            Cell[,] grid = TestTiles.NewGrid(2, 2, variants);
            TestTiles.Set(grid, 0, 0, variants[0]);
            TestTiles.Set(grid, 1, 0, variants[1]);
            TestTiles.Set(grid, 0, 1, variants[2]);

            // Row z = 1 first: north-south path, then an uncollapsed cell
            Assert.Equal("│?\n.~\n", AsciiMap.Render(grid));
        }

        [Fact]
        public void MapImage_DrawsMismatchesAndDeadCellsInTheErrorColour()
        {
            List<TileVariant> variants = TileVariant.Expand([TestTiles.Grass, TestTiles.Water]);
            Cell[,] grid = TestTiles.NewGrid(3, 1, variants);
            TestTiles.Set(grid, 0, 0, variants[0]);
            TestTiles.Set(grid, 1, 0, variants[1]);
            grid[2, 0].AvailableVariants.Clear();
            MapPalette palette = new MapPalette();

            RgbImage image = MapImage.Render(grid, MapValidator.Validate(grid), scale: 1, palette: palette);

            // scale 1: each cell is 3x3 pixels; the shared edge pixels are (2, 1) and (3, 1)
            Assert.Equal(palette.Error, PixelAt(image, 2, 1));
            Assert.Equal(palette.Error, PixelAt(image, 3, 1));
            Assert.Equal(palette.ColorOf("grass"), PixelAt(image, 1, 1));
            Assert.Equal(palette.Error, PixelAt(image, 7, 1));
        }

        [Fact]
        public void PngWriter_RoundTripsHeaderAndPixels()
        {
            RgbImage image = new RgbImage(3, 2);
            image.FillRect(1, 0, 2, 1, (10, 20, 30));

            byte[] png = PngWriter.Encode(image);

            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
            Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
            Assert.Equal(3, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
            Assert.Equal(2, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));

            // IDAT starts at byte 33: 8 signature + IHDR chunk (4 length + 4 type + 13 data + 4 CRC)
            int idatLength = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(33));
            Assert.Equal("IDAT", System.Text.Encoding.ASCII.GetString(png, 37, 4));

            using ZLibStream zlib = new ZLibStream(new MemoryStream(png, 41, idatLength), CompressionMode.Decompress);
            using MemoryStream raw = new MemoryStream();
            zlib.CopyTo(raw);

            // Each row: filter byte 0, then RGB pixels
            byte[] expected = [0, 0, 0, 0, 10, 20, 30, 10, 20, 30, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
            Assert.Equal(expected, raw.ToArray());
        }

        /// <summary>
        /// RGB of one pixel.
        /// </summary>
        private static (byte r, byte g, byte b) PixelAt(RgbImage image, int x, int y)
        {
            int i = (y * image.Width + x) * 3;
            return (image.Pixels[i], image.Pixels[i + 1], image.Pixels[i + 2]);
        }
    }
}
