namespace SoftWfcAco.Viz
{
    /// <summary>
    /// 8-bit RGB image, rows top to bottom, 3 bytes per pixel.
    /// </summary>
    public sealed class RgbImage
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public RgbImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new byte[width * height * 3];
        }

        /// <summary>
        /// Fills a rectangle; pixels outside the image are ignored.
        /// </summary>
        public void FillRect(int left, int top, int w, int h, (byte r, byte g, byte b) color)
        {
            for (int y = Math.Max(top, 0); y < Math.Min(top + h, Height); y++)
            {
                for (int x = Math.Max(left, 0); x < Math.Min(left + w, Width); x++)
                {
                    int i = (y * Width + x) * 3;
                    Pixels[i] = color.r;
                    Pixels[i + 1] = color.g;
                    Pixels[i + 2] = color.b;
                }
            }
        }
    }
}
