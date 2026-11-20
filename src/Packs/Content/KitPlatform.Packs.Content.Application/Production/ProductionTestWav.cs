using System.Buffers.Binary;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Deterministic PCM WAV for FixtureVoiceAdapter. No vendor TTS.</summary>
public static class ProductionTestWav
{
    public const string Mime = "audio/wav";
    public const int SampleRate = 8000;

    public static byte[] Encode(string seed, decimal durationSec)
    {
        var samples = Math.Max(SampleRate / 10, (int)Math.Round((double)durationSec * SampleRate));
        var data = new byte[samples * 2];
        var token = Encoding.UTF8.GetBytes(("FAMIXA-TEST-WAV|" + (seed ?? "")).PadRight(32, '0')[..32]);
        for (var i = 0; i < data.Length; i++)
            data[i] = token[i % token.Length];

        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("RIFF"));
        Span<byte> u32 = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)(36 + data.Length));
        ms.Write(u32);
        ms.Write(Encoding.ASCII.GetBytes("WAVE"));
        ms.Write(Encoding.ASCII.GetBytes("fmt "));
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 16);
        ms.Write(u32);
        Span<byte> u16 = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 1);
        ms.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 1);
        ms.Write(u16);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, SampleRate);
        ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, SampleRate * 2);
        ms.Write(u32);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 2);
        ms.Write(u16);
        BinaryPrimitives.WriteUInt16LittleEndian(u16, 16);
        ms.Write(u16);
        ms.Write(Encoding.ASCII.GetBytes("data"));
        BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)data.Length);
        ms.Write(u32);
        ms.Write(data);
        return ms.ToArray();
    }

    public static bool IsDecodable(byte[] bytes, out string reason)
    {
        reason = "";
        if (bytes is null || bytes.Length < 44)
        {
            reason = "too-small";
            return false;
        }
        var ascii = Encoding.ASCII.GetString(bytes, 0, 12);
        if (!ascii.StartsWith("RIFF", StringComparison.Ordinal) || !ascii.Contains("WAVE", StringComparison.Ordinal))
        {
            reason = "not-wav";
            return false;
        }
        return true;
    }

    public static bool TryReadDuration(byte[] bytes, out decimal durationSec)
    {
        durationSec = 0;
        if (bytes is null || bytes.Length < 44) return false;
        var dataBytes = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40, 4));
        if (dataBytes == 0) return false;
        durationSec = (decimal)dataBytes / (SampleRate * 2);
        return durationSec > 0;
    }
}
