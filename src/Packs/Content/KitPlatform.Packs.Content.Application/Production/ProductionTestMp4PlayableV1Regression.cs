using System.Buffers.Binary;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Structural proof that the fixture picture, sample location, and timeline agree.
/// ffmpeg is not a test dependency; media decode is checked outside this suite.
/// </summary>
public static class ProductionTestMp4PlayableV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_PLAYABLE_FIXTURE_MP4_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var once = ProductionTestMp4.Encode("same-seed", 5);
        var twice = ProductionTestMp4.Encode("same-seed", 5);
        Ok(once.AsSpan().SequenceEqual(twice), "A same seed and duration are the same bytes");
        Ok(KitVideoIntegrityRules.Sha256Hex(once) == KitVideoIntegrityRules.Sha256Hex(twice), "A same SHA");
        Ok(KitVideoIntegrityRules.Sha256Hex(once) != KitVideoIntegrityRules.Sha256Hex(ProductionTestMp4.Encode("other-seed", 5)), "A seed changes SHA");
        Ok(KitVideoIntegrityRules.Sha256Hex(once) != KitVideoIntegrityRules.Sha256Hex(ProductionTestMp4.Encode("same-seed", 1)), "A duration changes SHA");

        Ok(SampleLivesInMdat(once, out var sample) && sample.Length == 17, "B sample offset is inside mdat");
        Ok(sample.Length >= 4 && sample[0] == 0 && sample[1] == 0 && sample[2] == 1 && sample[3] == 0xB3, "C sample starts with a video object sequence");
        Ok(IndexOf(once, "esds"u8) >= 0 && IndexOf(once, "mp4v"u8) >= 0, "C sample entry carries mp4v and esds");
        Ok(ProductionTestMp4.IsDecodable(once, out _), "existing IsDecodable still accepts the fixture");

        foreach (var duration in new decimal[] { 1, 5, 2.5m, 5.042m, 10 })
        {
            var bytes = ProductionTestMp4.Encode("dur-" + duration, duration);
            Ok(ProductionTestMp4.TryReadDuration(bytes, out var read) && read == duration, "D mvhd duration " + duration);
            Ok(TimelineAgrees(bytes, duration), "D movie track media and sample timeline " + duration);
        }

        Ok(!ProductionTestMp4.IsDecodable(ProductionTestMp4.EncodeWithoutVideoStream("audio-only"), out _), "missing vide handler still fails IsDecodable");
        Ok(!ProductionTestMp4.TryReadDuration(ProductionTestMp4.EncodeUnreadableDuration("bad-dur"), out _), "unreadable mvhd still fails TryReadDuration");
        return fail;
    }

    static bool SampleLivesInMdat(byte[] bytes, out byte[] sample)
    {
        sample = [];
        if (!TryTopLevel(bytes, "mdat", out var mdatAt, out var mdatSize)) return false;
        if (!TryFourcc(bytes, "stco", out var stco) || stco + 16 > bytes.Length) return false;
        if (!TryFourcc(bytes, "stsz", out var stsz) || stsz + 16 > bytes.Length) return false;
        var offset = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(stco + 12, 4));
        var size = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(stsz + 8, 4));
        var payload = mdatAt + 8;
        var end = mdatAt + mdatSize;
        if (size <= 0 || offset < payload || offset + size > end) return false;
        sample = bytes.AsSpan(offset, size).ToArray();
        return true;
    }

    static bool TimelineAgrees(byte[] bytes, decimal duration)
    {
        if (!TryFourcc(bytes, "mvhd", out var mvhd)) return false;
        if (!TryFourcc(bytes, "tkhd", out var tkhd)) return false;
        if (!TryFourcc(bytes, "mdhd", out var mdhd)) return false;
        if (!TryFourcc(bytes, "stts", out var stts) || stts + 20 > bytes.Length) return false;
        var movieScale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mvhd + 16, 4));
        var movieDur = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mvhd + 20, 4));
        var trackDur = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(tkhd + 24, 4));
        var mediaScale = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mdhd + 16, 4));
        var mediaDur = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(mdhd + 20, 4));
        var sampleCount = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(stts + 12, 4));
        var sampleDelta = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(stts + 16, 4));
        if (movieScale == 0 || mediaScale == 0 || sampleCount != 1) return false;
        var movie = (decimal)movieDur / movieScale;
        var track = (decimal)trackDur / movieScale;
        var media = (decimal)mediaDur / mediaScale;
        var sample = (decimal)sampleDelta / mediaScale;
        return movie == duration && track == duration && media == duration && sample == duration;
    }

    static bool TryTopLevel(byte[] bytes, string type, out int at, out int size)
    {
        at = 0;
        size = 0;
        for (var i = 0; i + 8 <= bytes.Length;)
        {
            var box = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(i, 4));
            if (box < 8 || i + box > bytes.Length) return false;
            if (Encoding.ASCII.GetString(bytes, i + 4, 4) == type)
            {
                at = i;
                size = box;
                return true;
            }
            i += box;
        }
        return false;
    }

    static bool TryFourcc(byte[] bytes, string type, out int at)
    {
        var needle = Encoding.ASCII.GetBytes(type);
        for (var i = 0; i <= bytes.Length - 4; i++)
        {
            if (bytes[i] == needle[0] && bytes[i + 1] == needle[1] && bytes[i + 2] == needle[2] && bytes[i + 3] == needle[3])
            {
                at = i;
                return true;
            }
        }
        at = -1;
        return false;
    }

    static int IndexOf(byte[] hay, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i <= hay.Length - needle.Length; i++)
        {
            if (hay.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        }
        return -1;
    }
}
