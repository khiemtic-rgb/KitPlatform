namespace KitPlatform.Packs.Content.Production;

/// <summary>Best-effort MPEG audio duration from frame headers. No vendor decode.</summary>
public static class ProductionMpegAudio
{
    static readonly int[] Mpeg1L3 =
        [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0];
    static readonly int[] Mpeg2L3 =
        [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, 0];
    static readonly int[] Mpeg1Rate = [44100, 48000, 32000];
    static readonly int[] Mpeg2Rate = [22050, 24000, 16000];

    public static bool TryReadDuration(byte[] bytes, out decimal durationSec)
    {
        durationSec = 0;
        if (bytes is null || bytes.Length < 4) return false;
        var offset = 0;
        if (bytes.Length >= 3 && bytes[0] == 0x49 && bytes[1] == 0x44 && bytes[2] == 0x33)
        {
            if (bytes.Length < 10) return false;
            offset = 10 + ((bytes[6] & 0x7F) << 21 | (bytes[7] & 0x7F) << 14 | (bytes[8] & 0x7F) << 7 | (bytes[9] & 0x7F));
        }

        double seconds = 0;
        var frames = 0;
        while (offset + 4 <= bytes.Length && frames < 20000)
        {
            if (bytes[offset] != 0xFF || (bytes[offset + 1] & 0xE0) != 0xE0)
            {
                offset++;
                continue;
            }
            var versionBits = (bytes[offset + 1] >> 3) & 0x03;
            var layerBits = (bytes[offset + 1] >> 1) & 0x03;
            if (layerBits != 1) { offset++; continue; }
            var bitrateIndex = bytes[offset + 2] >> 4;
            var rateIndex = (bytes[offset + 2] >> 2) & 0x03;
            var padding = (bytes[offset + 2] >> 1) & 0x01;
            var mpeg1 = versionBits == 3;
            var mpeg2 = versionBits is 2 or 0;
            if ((!mpeg1 && !mpeg2) || rateIndex > 2) { offset++; continue; }
            var bitrate = (mpeg1 ? Mpeg1L3 : Mpeg2L3)[bitrateIndex] * 1000;
            var sampleRate = (mpeg1 ? Mpeg1Rate : Mpeg2Rate)[rateIndex];
            if (bitrate <= 0 || sampleRate <= 0) { offset++; continue; }
            var samples = mpeg1 ? 1152 : 576;
            var frameSize = (samples / 8 * bitrate / sampleRate) + padding;
            if (frameSize < 4) { offset++; continue; }
            seconds += (double)samples / sampleRate;
            offset += frameSize;
            frames++;
        }

        if (frames == 0 || seconds <= 0) return false;
        durationSec = Math.Round((decimal)seconds, 3);
        return durationSec > 0;
    }
}
