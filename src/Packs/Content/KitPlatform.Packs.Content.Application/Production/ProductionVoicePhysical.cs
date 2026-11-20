using System.Buffers.Binary;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Physical VOICE bytes: fixture WAV or ElevenLabs MPEG. Duration from the bytes when possible.</summary>
public static class ProductionVoicePhysical
{
    public const string MpegMime = "audio/mpeg";

    public static bool IsMpegMime(string? mime) =>
        string.Equals((mime ?? "").Trim(), MpegMime, StringComparison.OrdinalIgnoreCase)
        || string.Equals((mime ?? "").Trim(), "audio/mp3", StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeMpeg(byte[] bytes) =>
        bytes is { Length: >= 32 }
        && (bytes[0] == 0x49 && bytes[1] == 0x44 && bytes[2] == 0x33
            || bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0);

    public static bool TryVerify(byte[]? bytes, string? mime, decimal claimedDurationSec, out decimal durationSec)
    {
        durationSec = 0;
        if (bytes is null || bytes.Length < 32) return false;
        if (string.Equals(mime, ProductionTestWav.Mime, StringComparison.OrdinalIgnoreCase)
            && ProductionTestWav.IsDecodable(bytes, out _)
            && ProductionTestWav.TryReadDuration(bytes, out durationSec)
            && durationSec > 0)
            return true;
        if ((IsMpegMime(mime) || LooksLikeMpeg(bytes)) && LooksLikeMpeg(bytes))
        {
            if (ProductionMpegAudio.TryReadDuration(bytes, out durationSec) && durationSec > 0)
                return true;
            if (claimedDurationSec > 0)
            {
                durationSec = claimedDurationSec;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Duration read from the audio header. A claimed duration is not accepted.
    /// </summary>
    public static bool TryMeasureDuration(byte[]? bytes, string? mime, out decimal durationSec)
    {
        durationSec = 0;
        if (bytes is null || bytes.Length < 32) return false;
        var declared = (mime ?? "").Trim();
        if (IsWav(bytes))
        {
            if (declared.Length > 0 && !IsWavMime(declared)) return false;
            return TryWavDuration(bytes, out durationSec);
        }
        if (LooksLikeMpeg(bytes))
        {
            if (declared.Length > 0 && !IsMpegMime(declared)) return false;
            return ProductionMpegAudio.TryReadDuration(bytes, out durationSec) && durationSec > 0;
        }
        return false;
    }

    public static bool IsWavMime(string? mime)
    {
        var text = (mime ?? "").Trim();
        return text.Equals("audio/wav", StringComparison.OrdinalIgnoreCase)
            || text.Equals("audio/x-wav", StringComparison.OrdinalIgnoreCase)
            || text.Equals("audio/wave", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsWav(byte[] bytes) =>
        bytes.Length >= 12
        && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF"
        && Encoding.ASCII.GetString(bytes, 8, 4) == "WAVE";

    static bool TryWavDuration(byte[] bytes, out decimal durationSec)
    {
        durationSec = 0;
        var sampleRate = 0;
        var channels = 0;
        var bits = 0;
        var dataBytes = 0;
        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var id = Encoding.ASCII.GetString(bytes, offset, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset + 4, 4));
            if (size < 0) return false;
            var start = offset + 8;
            if (id == "fmt " && size >= 16 && start + 16 <= bytes.Length)
            {
                var format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start, 2));
                if (format != 1) return false;
                channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(start + 4, 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start + 14, 2));
            }
            else if (id == "data")
            {
                dataBytes = size;
                break;
            }
            var step = start + size;
            if ((size & 1) == 1) step++;
            if (step <= offset) return false;
            offset = step;
        }
        if (sampleRate <= 0 || channels <= 0 || bits < 8 || bits % 8 != 0 || dataBytes <= 0) return false;
        var bytesPerSecond = sampleRate * channels * (bits / 8);
        if (bytesPerSecond <= 0) return false;
        durationSec = Math.Round((decimal)dataBytes / bytesPerSecond, 3, MidpointRounding.AwayFromZero);
        return durationSec > 0;
    }
}
