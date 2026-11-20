namespace KitPlatform.Packs.Content.Production;

public static class ProductionVoiceSelectionCanon
{
    public const string VoiceRefKey = "approvedVoiceRef=";

    public static string ReasonOf(string voiceId, string lineId, string speakerCharacterId, string sourceField) =>
        string.Join(';',
            VoiceRefKey + (voiceId ?? "").Trim(),
            "lineId=" + (lineId ?? "").Trim(),
            "speakerCharacterId=" + (speakerCharacterId ?? "").Trim(),
            "sourceField=" + (sourceField ?? "").Trim());

    public static string VoiceIdOf(string? reason)
    {
        var raw = (reason ?? "").Trim();
        const string key = VoiceRefKey;
        var start = raw.IndexOf(key, StringComparison.Ordinal);
        if (start < 0) return "";
        var value = raw[(start + key.Length)..];
        var end = value.IndexOf(';');
        return (end < 0 ? value : value[..end]).Trim();
    }

    public static string VoiceIdOf(ProductionAiSelection? selection) =>
        VoiceIdOf(selection?.Reason);
}
