namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Provider-boundary duration only. Does not mutate ProductionTaskInputSnapshot.DurationSec.
/// Evidence: ContentVeoClient.CreateImageToVideoAsync maps >=8→8, >=6→6, else→4.
/// </summary>
public static class VeoMotionDurationMapping
{
    public static int ToProviderDurationSec(decimal osDurationSec)
    {
        if (osDurationSec >= 8) return 8;
        if (osDurationSec >= 6) return 6;
        return 4;
    }
}

public static class VeoMotionPrompt
{
    public static string FromFrozen(ProductionTaskInputSnapshot snapshot) =>
        string.Join(" ", new[] { snapshot.Story, snapshot.ActingBeat, snapshot.VisualContract }
            .Where(v => !string.IsNullOrWhiteSpace(v)));
}

public static class VeoMotionImageDataUri
{
    public static string FromBytes(byte[] bytes, string mime)
    {
        if (bytes is null || bytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);
        var normalized = NormalizeMime(mime);
        if (normalized is null)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return "data:" + normalized + ";base64," + Convert.ToBase64String(bytes);
    }

    public static string ToProviderAspect(string? frozenAspect)
    {
        var r = (frozenAspect ?? "").Trim().ToLowerInvariant();
        if (r is "9:16" or "9x16" or "720:1280") return "9:16";
        return "16:9";
    }

    static string? NormalizeMime(string? mime)
    {
        var raw = (mime ?? "").Trim().ToLowerInvariant();
        if (raw is "image/png" or "image/jpeg" or "image/jpg" or "image/webp")
            return raw == "image/jpg" ? "image/jpeg" : raw;
        return null;
    }
}
