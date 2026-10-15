using System.Buffers.Binary;
using System.IO.Compression;

namespace Game.Content;

/// <summary>Decodes the ordinary non-interlaced RGBA8 PNG files emitted by the art importers.</summary>
public static class PngRgba8
{
    public static byte[] Decode(byte[] png, int expectedWidth = 0, int expectedHeight = 0)
    {
        if (png.Length < 45 || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(8, 4)) != 13 || !png.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
            !png.AsSpan(24, 5).SequenceEqual(new byte[] { 8, 6, 0, 0, 0 }))
            throw new InvalidDataException("Expected a non-interlaced RGBA8 PNG.");
        int width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        int height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        if (width <= 0 || height <= 0 || (long)width * height > 16_777_216 ||
            (expectedWidth != 0 && width != expectedWidth) || (expectedHeight != 0 && height != expectedHeight))
            throw new InvalidDataException("PNG dimensions differ from the asset metadata.");
        using var compressed = new MemoryStream();
        bool ended = false;
        for (int offset = 33; offset < png.Length;)
        {
            if (png.Length - offset < 12) throw new InvalidDataException("Truncated PNG chunk.");
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
            if (length < 0 || length > png.Length - offset - 12) throw new InvalidDataException("Invalid PNG chunk length.");
            var kind = png.AsSpan(offset + 4, 4);
            if (kind.SequenceEqual("IDAT"u8)) compressed.Write(png, offset + 8, length);
            else if (kind.SequenceEqual("IEND"u8))
            {
                if (length != 0 || offset + 12 != png.Length) throw new InvalidDataException("Invalid PNG end chunk.");
                ended = true;
                break;
            }
            else if ((kind[0] & 32) == 0) throw new InvalidDataException("Unsupported critical PNG chunk.");
            offset += length + 12;
        }
        if (!ended || compressed.Length == 0) throw new InvalidDataException("PNG image data is missing.");
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        int rowBytes = checked(width * 4);
        var result = new byte[checked(rowBytes * height)];
        var row = new byte[rowBytes];
        for (int y = 0; y < height; y++)
        {
            int filter = zlib.ReadByte();
            if (filter is < 0 or > 4) throw new InvalidDataException("Unsupported PNG scanline filter.");
            zlib.ReadExactly(row);
            int start = y * rowBytes;
            for (int x = 0; x < rowBytes; x++)
            {
                int left = x >= 4 ? result[start + x - 4] : 0;
                int above = y > 0 ? result[start + x - rowBytes] : 0;
                int upperLeft = x >= 4 && y > 0 ? result[start + x - rowBytes - 4] : 0;
                int predictor = filter switch { 0 => 0, 1 => left, 2 => above, 3 => (left + above) / 2, 4 => Paeth(left, above, upperLeft), _ => 0 };
                result[start + x] = unchecked((byte)(row[x] + predictor));
            }
        }
        if (zlib.ReadByte() != -1) throw new InvalidDataException("PNG pixel stream exceeds its dimensions.");
        return result;
    }

    private static int Paeth(int left, int above, int upperLeft)
    {
        int estimate = left + above - upperLeft;
        int a = Math.Abs(estimate - left), b = Math.Abs(estimate - above), c = Math.Abs(estimate - upperLeft);
        return a <= b && a <= c ? left : b <= c ? above : upperLeft;
    }
}
