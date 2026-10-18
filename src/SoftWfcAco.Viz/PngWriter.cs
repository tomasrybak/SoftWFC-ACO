using System.Buffers.Binary;
using System.IO.Compression;

namespace SoftWfcAco.Viz
{
    /// <summary>
    /// Minimal PNG encoder (truecolour, 8 bits per channel, no filtering).
    /// Written by hand so the repo needs no image library.
    /// </summary>
    public static class PngWriter
    {
        private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
        private static readonly uint[] CrcTable = BuildCrcTable();

        /// <summary>
        /// Writes the image to a PNG file, creating the directory if needed.
        /// </summary>
        public static void Save(RgbImage image, string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(path, Encode(image));
        }

        /// <summary>
        /// Encodes the image as PNG bytes.
        /// </summary>
        public static byte[] Encode(RgbImage image)
        {
            using MemoryStream output = new MemoryStream();
            output.Write(Signature);

            // IHDR: width, height, bit depth 8, colour type 2 (RGB), default compression/filter/interlace
            byte[] header = new byte[13];
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0), image.Width);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
            header[8] = 8;
            header[9] = 2;
            WriteChunk(output, "IHDR", header);

            // IDAT: zlib stream of rows, each prefixed with filter type 0 (none)
            using MemoryStream raw = new MemoryStream();
            using (ZLibStream zlib = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            {
                int stride = image.Width * 3;
                for (int y = 0; y < image.Height; y++)
                {
                    zlib.WriteByte(0);
                    zlib.Write(image.Pixels, y * stride, stride);
                }
            }

            WriteChunk(output, "IDAT", raw.ToArray());
            WriteChunk(output, "IEND", []);

            return output.ToArray();
        }

        /// <summary>
        /// Writes one chunk: length, type, data, CRC32 of type + data.
        /// </summary>
        private static void WriteChunk(Stream output, string type, byte[] data)
        {
            byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
            Span<byte> buffer = stackalloc byte[4];

            BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
            output.Write(buffer);
            output.Write(typeBytes);
            output.Write(data);

            uint crc = Crc32(typeBytes, 0xFFFFFFFFu);
            crc = Crc32(data, crc) ^ 0xFFFFFFFFu;
            BinaryPrimitives.WriteUInt32BigEndian(buffer, crc);
            output.Write(buffer);
        }

        /// <summary>
        /// Continues a CRC-32 (the zlib/PNG polynomial) over the given bytes.
        /// </summary>
        private static uint Crc32(byte[] bytes, uint crc)
        {
            foreach (byte b in bytes)
            {
                crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }

            return crc;
        }

        /// <summary>
        /// Lookup table for the reflected polynomial 0xEDB88320.
        /// </summary>
        private static uint[] BuildCrcTable()
        {
            uint[] table = new uint[256];

            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }

                table[n] = c;
            }

            return table;
        }
    }
}
