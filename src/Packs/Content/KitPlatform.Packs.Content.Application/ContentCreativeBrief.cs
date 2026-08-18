namespace KitPlatform.Packs.Content;

public sealed record ContentCreativeBriefDto(
    string? Objective = null,
    string? Emotion = null,
    string? Format = null,
    string? VisualDirection = null,
    int? DurationSec = null)
{
    public static string FormatForPrompt(ContentCreativeBriefDto? brief)
    {
        if (brief is null) return "";
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(brief.Objective))
            lines.Add("Objective: " + brief.Objective.Trim());
        if (!string.IsNullOrWhiteSpace(brief.Emotion))
            lines.Add("Emotion: " + brief.Emotion.Trim());
        if (!string.IsNullOrWhiteSpace(brief.Format))
            lines.Add("Format: " + brief.Format.Trim());
        if (!string.IsNullOrWhiteSpace(brief.VisualDirection))
            lines.Add("Visual: " + brief.VisualDirection.Trim());
        if (brief.DurationSec is > 0)
            lines.Add("DurationSec: " + brief.DurationSec);
        return string.Join('\n', lines);
    }
}

public sealed record ContentPerformanceDto(
    Guid Id,
    Guid PackageId,
    Guid TopicId,
    Guid BrandId,
    string BrandCode,
    string BrandName,
    string Channel,
    DateTime MetricDate,
    int? Impressions,
    int? Views,
    int? Clicks,
    int? Engagements,
    int? Comments,
    int? Shares,
    string? UtmCampaign,
    string? UtmSource,
    string? UtmMedium,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record IngestContentPerformanceRequest(
    string Channel,
    DateTime MetricDate,
    int? Impressions = null,
    int? Views = null,
    int? Clicks = null,
    int? Engagements = null,
    int? Comments = null,
    int? Shares = null,
    string? UtmCampaign = null,
    string? UtmSource = null,
    string? UtmMedium = null,
    string? Notes = null);
