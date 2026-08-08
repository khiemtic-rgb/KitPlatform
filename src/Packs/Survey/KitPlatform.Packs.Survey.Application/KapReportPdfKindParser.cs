namespace KitPlatform.Packs.Survey;

public static class KapReportPdfKindParser
{
    public static KapReportPdfKind Parse(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            "executive" or "exec" => KapReportPdfKind.Executive,
            "appendix" or "technical" => KapReportPdfKind.Appendix,
            "owner" or "owner_v1" or "chu" or "short" => KapReportPdfKind.Owner,
            "consulting" or "full" or "" or null => KapReportPdfKind.Consulting,
            _ => KapReportPdfKind.Consulting,
        };
}
