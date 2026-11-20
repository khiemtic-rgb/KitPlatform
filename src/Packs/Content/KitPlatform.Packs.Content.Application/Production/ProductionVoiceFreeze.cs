namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// CREATE_VOICE freeze token. VoiceId is not a snapshot column; it lives in
/// VoiceHash / ShotContract as LineId|Speaker|VoiceId|Text. No schema change.
/// </summary>
public static class ProductionVoiceFreeze
{
    public static string Token(string lineId, string speakerCharacterId, string voiceId, string text) =>
        string.Join('|', (lineId ?? "").Trim(), (speakerCharacterId ?? "").Trim(), (voiceId ?? "").Trim(), (text ?? "").Trim());

    public static bool TryParse(string? raw, out string lineId, out string speaker, out string voiceId, out string text)
    {
        lineId = "";
        speaker = "";
        voiceId = "";
        text = "";
        var value = raw ?? "";
        var first = value.IndexOf('|');
        if (first <= 0) return false;
        var second = value.IndexOf('|', first + 1);
        if (second <= first + 1) return false;
        var third = value.IndexOf('|', second + 1);
        if (third <= second + 1) return false;
        lineId = value[..first].Trim();
        speaker = value[(first + 1)..second].Trim();
        voiceId = value[(second + 1)..third].Trim();
        text = value[(third + 1)..].Trim();
        return lineId.Length > 0 && speaker.Length > 0 && voiceId.Length > 0 && text.Length > 0;
    }

    public static string? VoiceIdFromSnapshot(ProductionTaskInputSnapshot? snap)
    {
        if (snap is null) return null;
        if (TryParse(snap.ShotContract, out _, out _, out var voiceId, out _))
            return voiceId;
        return null;
    }

    public static bool IsTestIdentity(string? value)
    {
        var raw = (value ?? "").Trim();
        return string.Equals(raw, "VOICE-TEST", StringComparison.Ordinal)
            || string.Equals(raw, "CHAR-TEST", StringComparison.Ordinal)
            || raw.StartsWith("line-test", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "LINE-TEST", StringComparison.Ordinal);
    }
}
