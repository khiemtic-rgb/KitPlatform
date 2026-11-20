using System.Buffers.Binary;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Deterministic ISO-BMFF MP4 used only by FixtureMotionAdapter. No vendor encode and no runtime ffmpeg.
/// The picture is one embedded 16x16 MPEG-4 frame, held for the requested duration.
/// </summary>
public static class ProductionTestMp4
{
    public const string Mime = "video/mp4";
    public const int Timescale = 1000;
    public const int Width = 16;
    public const int Height = 16;

    /// <summary>One black 16x16 MPEG-4 Visual sample entry, including esds. Authored once; not generated at runtime.</summary>
    static readonly byte[] SampleEntry = Convert.FromHexString(
        "000000da6d703476000000000000000100000000000000000000000000000000001000100048000000480000000000000001134c61766336322e32382e313032206d706567340000000000000000000000000018ffff000000606573647300000000038080804f0001000480808041201100000000030d4000000088058080802f000001b001000001b58913000001000000012000c48d88000d0084021463000001b24c61766336322e32382e3130320680808001020000001070617370000000010000000100000014627472740000000000030d4000000088");

    /// <summary>VOS + one VOP. The bytes at stco are this sample, not a text token.</summary>
    static readonly byte[] Frame = Convert.FromHexString("000001b3001007000001b616051823db7e");

    public static byte[] Encode(string seed, decimal durationSec)
        => Encode(seed, durationSec, includeVideoHandler: true, readableDuration: true);

    public static byte[] EncodeWithoutVideoStream(string seed, decimal durationSec = 5)
        => Encode(seed, durationSec, includeVideoHandler: false, readableDuration: true);

    public static byte[] EncodeUnreadableDuration(string seed)
        => Encode(seed, 5, includeVideoHandler: true, readableDuration: false);

    public const string FixtureMarker = "FAMIXA-TEST-MP4";

    public static byte[] EncodeSized(string seed, decimal durationSec, int width, int height) =>
        EncodeCore(seed, durationSec, includeVideoHandler: true, readableDuration: true, width, height, "FAMIXA-SIZED-MP4");

    public static byte[] Encode(string seed, decimal durationSec, bool includeVideoHandler, bool readableDuration) =>
        EncodeCore(seed, durationSec, includeVideoHandler, readableDuration, Width, Height, FixtureMarker);

    public static bool ContainsFixtureMarker(byte[] bytes) =>
        bytes is { Length: > 0 } && IndexOf(bytes, Encoding.UTF8.GetBytes(FixtureMarker)) >= 0;

    public static bool TryReadDisplaySize(byte[] bytes, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (bytes is null || bytes.Length < 16)
            return false;
        var i = IndexOf(bytes, Four("tkhd"));
        if (i < 4)
            return false;
        var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i - 4, 4));
        if (size < 16 || i - 4 + size > bytes.Length)
            return false;
        var end = i - 4 + (int)size;
        width = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(end - 8, 4)) >> 16);
        height = (int)(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(end - 4, 4)) >> 16);
        return width > 0 && height > 0;
    }

    static byte[] EncodeCore(
        string seed,
        decimal durationSec,
        bool includeVideoHandler,
        bool readableDuration,
        int width,
        int height,
        string marker)
    {
        var timescale = readableDuration ? (uint)Timescale : 0u;
        var ticks = readableDuration ? DurationTicks(durationSec) : 0u;
        var token = Encoding.UTF8.GetBytes((marker + "|" + (seed ?? "")).PadRight(48, '0')[..48]);
        var ftyp = Box("ftyp", Concat(Four("isom"), U32(0), Four("isom"), Four("iso2"), Four("mp41")));
        var free = Box("free", token);
        var moov = Movie(includeVideoHandler, timescale, ticks, chunkOffset: 0, width, height);
        var sampleAt = ftyp.Length + free.Length + moov.Length + 8;
        moov = Movie(includeVideoHandler, timescale, ticks, (uint)sampleAt, width, height);
        var mdat = Box("mdat", Frame);
        return Concat(ftyp, free, moov, mdat);
    }

    public static bool IsDecodable(byte[] bytes, out string reason)
    {
        reason = "";
        if (bytes is null || bytes.Length < 32)
        {
            reason = "too-small";
            return false;
        }
        if (!HasBox(bytes, "ftyp") || !HasBox(bytes, "moov") || !HasBox(bytes, "mdat"))
        {
            reason = "missing-box";
            return false;
        }
        if (IndexOf(bytes, Four("vide")) < 0)
        {
            reason = "no-vide-handler";
            return false;
        }
        return true;
    }

    public static bool TryReadDuration(byte[] bytes, out decimal durationSec)
    {
        durationSec = 0;
        if (bytes is null || bytes.Length < 32) return false;
        var needle = Four("mvhd");
        var i = IndexOf(bytes, needle);
        if (i < 0 || i + 24 > bytes.Length) return false;
        var version = bytes[i + 4];
        uint timescale;
        ulong duration;
        if (version == 1)
        {
            if (i + 36 > bytes.Length) return false;
            timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i + 24, 4));
            duration = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(i + 28, 8));
        }
        else
        {
            timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i + 16, 4));
            duration = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i + 20, 4));
        }
        if (timescale == 0 || duration == 0) return false;
        durationSec = (decimal)duration / timescale;
        return durationSec > 0;
    }

    static uint DurationTicks(decimal durationSec)
    {
        var raw = decimal.Round(durationSec * Timescale, 0, MidpointRounding.AwayFromZero);
        if (raw < 1) raw = 1;
        if (raw > uint.MaxValue) raw = uint.MaxValue;
        return (uint)raw;
    }

    static byte[] Movie(bool includeVideoHandler, uint timescale, uint ticks, uint chunkOffset, int width, int height)
    {
        var handler = includeVideoHandler ? "vide" : "soun";
        var hdlr = Box("hdlr", Concat(U32(0), U32(0), Four(handler), U32(0), U32(0), U32(0), Encoding.UTF8.GetBytes("FixtureVideo\0")));
        var stbl = Box("stbl", Concat(
            Box("stsd", Concat(U32(0), U32(1), SampleEntry)),
            Box("stts", Concat(U32(0), U32(1), U32(1), U32(ticks))),
            Box("stsc", Concat(U32(0), U32(1), U32(1), U32(1), U32(1))),
            Box("stsz", Concat(U32(0), U32((uint)Frame.Length), U32(1))),
            Box("stco", Concat(U32(0), U32(1), U32(chunkOffset)))));
        var minf = Box("minf", Concat(
            Box("vmhd", Concat(U32(1), U16(0), U16(0), U16(0), U16(0))),
            Box("dinf", Box("dref", Concat(U32(0), U32(1), Box("url ", U32(1))))),
            stbl));
        var mdhd = Box("mdhd", Concat(U32(0), U32(0), U32(0), U32(timescale), U32(ticks), U32(0x55C40000)));
        var trak = Box("trak", Concat(Tkhd(ticks, width, height), Box("mdia", Concat(mdhd, hdlr, minf))));
        return Box("moov", Concat(Mvhd(timescale, ticks), trak));
    }

    static byte[] Mvhd(uint timescale, uint ticks) =>
        Box("mvhd", Concat(
            U32(0), U32(0), U32(0), U32(timescale), U32(ticks), U32(0x00010000), U16(0x0100), U16(0),
            U32(0), U32(0), IdentityMatrix(), U32(0), U32(0), U32(0), U32(0), U32(0), U32(0), U32(2)));

    static byte[] Tkhd(uint ticks, int width, int height) =>
        Box("tkhd", Concat(
            U32(0x0000000F), U32(0), U32(0), U32(1), U32(0), U32(ticks), U32(0), U32(0),
            U16(0), U16(0), U16(0), U16(0), IdentityMatrix(), U32((uint)width << 16), U32((uint)height << 16)));

    static byte[] IdentityMatrix() =>
        Concat(U32(0x00010000), U32(0), U32(0), U32(0), U32(0x00010000), U32(0), U32(0), U32(0), U32(0x40000000));

    static byte[] Box(string type, byte[] payload)
    {
        var body = Concat(Four(type), payload);
        return Concat(U32((uint)(body.Length + 4)), body);
    }

    static bool HasBox(byte[] bytes, string type)
    {
        var needle = Four(type);
        for (var i = 0; i <= bytes.Length - 8;)
        {
            if (i + 8 > bytes.Length) break;
            var size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i, 4));
            if (size < 8 || i + size > bytes.Length) break;
            if (bytes[i + 4] == needle[0] && bytes[i + 5] == needle[1] && bytes[i + 6] == needle[2] && bytes[i + 7] == needle[3])
                return true;
            i += (int)size;
        }
        return IndexOf(bytes, needle) >= 0;
    }

    static int IndexOf(byte[] hay, byte[] needle)
    {
        for (var i = 0; i <= hay.Length - needle.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }

    static byte[] Four(string s) => Encoding.ASCII.GetBytes(s);
    static byte[] U16(int v) { var b = new byte[2]; BinaryPrimitives.WriteUInt16BigEndian(b, (ushort)v); return b; }
    static byte[] U32(uint v) { var b = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, v); return b; }
    static byte[] Concat(params byte[][] parts)
    {
        var n = parts.Sum(p => p.Length);
        var buf = new byte[n];
        var i = 0;
        foreach (var p in parts)
        {
            Buffer.BlockCopy(p, 0, buf, i, p.Length);
            i += p.Length;
        }
        return buf;
    }
}
