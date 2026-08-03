using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.FamilyOs;

/// <summary>P0.6/P0.7 — lightweight image probes without external vision APIs.</summary>
public static class FamilyEvidenceImageProbe
{
    public const int MinWidthHard = 80;
    public const int MinHeightHard = 80;
    public const int MinBytesHard = 1200;
    public const int DuplicateLookbackDays = 30;

    public const string TinyImageCode = "evidence_too_small";
    public const string DuplicateCode = "evidence_duplicate";
    public const string SuspiciousNotStudyCode = "evidence_suspicious_not_study";

    public sealed record ProbeResult(
        string Sha256Hex,
        int ByteSize,
        int? Width,
        int? Height,
        IReadOnlyList<string> HardBlockCodes,
        IReadOnlyList<string> SoftWarningCodes,
        bool LooksLikeStudy);

    public static ProbeResult Probe(ReadOnlySpan<byte> bytes)
    {
        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var size = bytes.Length;
        var (w, h) = TryReadDimensions(bytes);

        var hard = new List<string>();
        var soft = new List<string>();

        if (size < MinBytesHard)
            hard.Add(TinyImageCode);
        if (w is int ww && h is int hh)
        {
            if (ww < MinWidthHard || hh < MinHeightHard)
                hard.Add(TinyImageCode);
            var pixels = (long)ww * hh;
            if (pixels > 0 && size < Math.Max(MinBytesHard, pixels / 80))
                soft.Add(SuspiciousNotStudyCode);
            if (ww < 160 || hh < 160)
                soft.Add(SuspiciousNotStudyCode);
        }
        else if (size < 8 * 1024)
        {
            soft.Add(SuspiciousNotStudyCode);
        }

        if (LooksMostlyFlat(bytes))
            soft.Add(SuspiciousNotStudyCode);

        var looksLikeStudy = soft.Count == 0;
        return new ProbeResult(sha, size, w, h, hard.Distinct().ToList(), soft.Distinct().ToList(), looksLikeStudy);
    }

    public static string HardBlockMessageVi(string code) => code switch
    {
        TinyImageCode =>
            "\u1ea2nh qu\u00e1 nh\u1ecf ho\u1eb7c kh\u00f4ng r\u00f5 \u2014 h\u00e3y ch\u1ee5p to h\u01a1n (\u0074\u1ed1i thi\u1ec3u ~80x80) \u0111\u1ec3 b\u1ed1 m\u1eb9 xem \u0111\u01b0\u1ee3c.",
        DuplicateCode =>
            "\u1ea2nh n\u00e0y gi\u1ed1ng \u1ea3nh \u0111\u00e3 n\u1ed9p g\u1ea7n \u0111\u00e2y. H\u00e3y ch\u1ee5p l\u1ea1i b\u00e0i h\u1ecdc h\u00f4m nay.",
        _ => "\u1ea2nh b\u1eb1ng ch\u1ee9ng ch\u01b0a h\u1ee3p l\u1ec7.",
    };

    public static string SoftWarningMessageVi(IReadOnlyList<string> codes)
    {
        if (codes.Contains(SuspiciousNotStudyCode))
            return "\u1ea2nh tr\u00f4ng ch\u01b0a gi\u1ed1ng b\u00e0i h\u1ecdc (c\u00f3 th\u1ec3 qu\u00e1 nh\u1ecf / m\u1edd / m\u1ed9t m\u00e0u). V\u1eabn g\u1eedi \u0111\u01b0\u1ee3c \u2014 b\u1ed1 m\u1eb9 s\u1ebd kh\u00f3 x\u00e1c nh\u1eadn h\u01a1n.";
        return "\u1ea2nh c\u00f3 th\u1ec3 kh\u00f3 x\u00e1c nh\u1eadn l\u00e0 b\u00e0i h\u1ecdc h\u00f4m nay.";
    }

    public static (int? Width, int? Height) TryReadDimensions(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            var w = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
            var h = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
            if (w > 0 && h > 0) return (w, h);
        }

        if (bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            var i = 2;
            while (i + 9 < bytes.Length)
            {
                if (bytes[i] != 0xFF) { i++; continue; }
                var marker = bytes[i + 1];
                if (marker == 0xD9 || marker == 0xDA) break;
                if (i + 3 >= bytes.Length) break;
                var segLen = (bytes[i + 2] << 8) | bytes[i + 3];
                if (segLen < 2) break;
                if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
                {
                    if (i + 8 < bytes.Length)
                    {
                        var h = (bytes[i + 5] << 8) | bytes[i + 6];
                        var w = (bytes[i + 7] << 8) | bytes[i + 8];
                        if (w > 0 && h > 0) return (w, h);
                    }
                }
                i += 2 + segLen;
            }
        }

        if (bytes.Length > 30
            && Encoding.ASCII.GetString(bytes.Slice(0, 4)) == "RIFF"
            && Encoding.ASCII.GetString(bytes.Slice(8, 4)) == "WEBP")
        {
            if (Encoding.ASCII.GetString(bytes.Slice(12, 4)) == "VP8X" && bytes.Length >= 30)
            {
                var w = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
                var h = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
                if (w > 0 && h > 0) return (w, h);
            }
        }

        return (null, null);
    }

    public static bool LooksMostlyFlat(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 64) return true;
        var sample = Math.Min(bytes.Length, 4096);
        Span<int> hist = stackalloc int[256];
        hist.Clear();
        var step = Math.Max(1, sample / 512);
        var n = 0;
        for (var i = 0; i < sample; i += step)
        {
            hist[bytes[i]]++;
            n++;
        }
        if (n == 0) return true;
        var max = 0;
        foreach (var v in hist) if (v > max) max = v;
        return max >= n * 0.55;
    }
}
