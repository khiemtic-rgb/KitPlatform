using System.Globalization;

namespace KitPlatform.Packs.Content.Production;

/// <summary>CREATE_LIPSYNC freeze: line|speaker|videoId|voiceId|x|y|w|h|frame[|x1|y1|x2|y2]. No schema change.</summary>
public static class ProductionLipsyncFreeze
{
    public static string Token(
        string lineId,
        string speakerCharacterId,
        Guid videoArtifactId,
        Guid voiceArtifactId,
        ProductionLipsyncSpeakerPin pin,
        int[]? boxXyxy = null)
    {
        var core = string.Join('|',
            (lineId ?? "").Trim(),
            (speakerCharacterId ?? "").Trim(),
            videoArtifactId.ToString("N"),
            voiceArtifactId.ToString("N"),
            pin.X01.ToString("0.##", CultureInfo.InvariantCulture),
            pin.Y01.ToString("0.##", CultureInfo.InvariantCulture),
            pin.Width.ToString(CultureInfo.InvariantCulture),
            pin.Height.ToString(CultureInfo.InvariantCulture),
            pin.FrameNumber.ToString(CultureInfo.InvariantCulture));
        if (boxXyxy is not { Length: 4 }) return core;
        return core + "|" + string.Join('|', boxXyxy[0], boxXyxy[1], boxXyxy[2], boxXyxy[3]);
    }

    public static bool TryParseBox(string? raw, out int[] box)
    {
        box = [];
        var parts = (raw ?? "").Split('|');
        if (parts.Length < 13) return false;
        if (!int.TryParse(parts[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x1)) return false;
        if (!int.TryParse(parts[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y1)) return false;
        if (!int.TryParse(parts[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x2)) return false;
        if (!int.TryParse(parts[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y2)) return false;
        if (x2 <= x1 || y2 <= y1) return false;
        box = [x1, y1, x2, y2];
        return true;
    }

    public static bool TryParse(string? raw, out string lineId, out string speaker, out Guid videoId, out Guid voiceId)
    {
        lineId = "";
        speaker = "";
        videoId = Guid.Empty;
        voiceId = Guid.Empty;
        var parts = (raw ?? "").Split('|');
        if (parts.Length < 4) return false;
        lineId = parts[0].Trim();
        speaker = parts[1].Trim();
        return Guid.TryParseExact(parts[2].Trim(), "N", out videoId)
            && Guid.TryParseExact(parts[3].Trim(), "N", out voiceId)
            && speaker.Length > 0;
    }

    public static bool TryParsePin(string? raw, out ProductionLipsyncSpeakerPin pin)
    {
        pin = default;
        if (!TryParse(raw, out _, out var speaker, out _, out _))
            return false;
        var parts = (raw ?? "").Split('|');
        if (parts.Length < 9) return false;
        if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return false;
        if (!double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return false;
        if (!int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)) return false;
        if (!int.TryParse(parts[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)) return false;
        if (!int.TryParse(parts[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame)) return false;
        pin = new ProductionLipsyncSpeakerPin(speaker, x, y, w, h, frame);
        return FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection(x, y, w, h, frame, out _, out _);
    }
}
