using System.Buffers.Binary;
using System.IO.Compression;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Deterministic uncompressed-style PNG writer. No AI, no network.
/// </summary>
public static class ProductionTestKeyframePng
{
    public const int DefaultWidth = 160;
    public const int DefaultHeight = 90;

    static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] EncodeSolidRgb(int width, int height, byte r, byte g, byte b)
    {
        if (width < 16 || height < 16)
            throw new InvalidOperationException(ProductionFailureReasons.FixtureDimensionsInvalid);
        var raw = new byte[height * (1 + width * 3)];
        var i = 0;
        for (var y = 0; y < height; y++)
        {
            raw[i++] = 0;
            for (var x = 0; x < width; x++)
            {
                var border = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                raw[i++] = border ? (byte)8 : r;
                raw[i++] = border ? (byte)24 : g;
                raw[i++] = border ? (byte)40 : b;
            }
        }

        using var png = new MemoryStream();
        png.Write(Signature);
        WriteChunk(png, "IHDR", Ihdr(width, height));
        WriteChunk(png, "IDAT", Zlib(raw));
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    public static bool TryReadPngSize(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 33) return false;
        for (var i = 0; i < Signature.Length; i++)
            if (bytes[i] != Signature[i]) return false;
        if (bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
            return false;
        width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        return width > 0 && height > 0;
    }

    public static bool HasPngSignature(byte[] bytes)
    {
        if (bytes.Length < Signature.Length) return false;
        for (var i = 0; i < Signature.Length; i++)
            if (bytes[i] != Signature[i]) return false;
        return true;
    }

    static byte[] Ihdr(int width, int height)
    {
        var buf = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(4, 4), height);
        buf[8] = 8;
        buf[9] = 2;
        return buf;
    }

    static byte[] Zlib(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var zs = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            zs.Write(raw);
        return ms.ToArray();
    }

    static void WriteChunk(Stream dest, string type, byte[] data)
    {
        var name = System.Text.Encoding.ASCII.GetBytes(type);
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        dest.Write(len);
        dest.Write(name);
        dest.Write(data);
        var crcSrc = new byte[name.Length + data.Length];
        name.CopyTo(crcSrc, 0);
        data.CopyTo(crcSrc, name.Length);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(crcSrc));
        dest.Write(crc);
    }

    static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc ^ 0xFFFFFFFF;
    }
}
