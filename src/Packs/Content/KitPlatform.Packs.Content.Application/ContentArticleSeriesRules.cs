using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>Pure rules for Article Series Engine V1. Video series_pilot is out of scope.</summary>
public static class ContentArticleSeriesRules
{
    public const string OutlineMarker = "SERIES ENGINE V1";
    public const string PromptVersion = "article-series-v1";

    public static readonly int[] AllowedLengths = [5, 10, 20, 30, 50, 100];

    public const string FactorySystemRules =
        "\nThis topic belongs to a Content Series. Honor SERIES ENGINE V1 in the outline:\n" +
        "1. Do not repeat previous episode theses or key points.\n" +
        "2. Stay inside the Core Idea — do not wander into a new thesis.\n" +
        "3. Keep Brand Voice from Brand Brain. Series tone/style/cta override only when explicit.\n" +
        "4. Fit the named audience and episode objective.\n" +
        "5. The piece must have standalone value for a first-time reader (local value).\n" +
        "6. Still advance the series narrative (continuity) without being a recap.";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new FlexibleInt32JsonConverter() },
    };

    public static int ClampEpisodeCount(int requested)
    {
        if (AllowedLengths.Contains(requested)) return requested;
        return AllowedLengths.OrderBy(n => Math.Abs(n - requested)).First();
    }

    public static int DefaultPlanBatch(int episodeCount) =>
        Math.Clamp(Math.Min(10, ClampEpisodeCount(episodeCount)), 1, 100);

    public static int ClampPlanBatch(int requested, int episodeCount) =>
        Math.Clamp(requested <= 0 ? DefaultPlanBatch(episodeCount) : requested, 1, ClampEpisodeCount(episodeCount));

    public static string NormalizeBrandCode(string? brandCode)
    {
        var raw = (brandCode ?? "BRAND").Trim().ToUpperInvariant();
        var chars = raw.Where(c => char.IsLetterOrDigit(c)).ToArray();
        return chars.Length == 0 ? "BRAND" : new string(chars);
    }

    public static string SeriesCode(string? brandCode, int sequence) =>
        $"{NormalizeBrandCode(brandCode)}-S{Math.Max(1, sequence):000}";

    public static string EpisodeCode(string seriesCode, int episodeNo) =>
        $"{seriesCode.Trim()}-E{Math.Max(1, episodeNo):000}";

    public static bool CanGenerateContent(string? seriesStatus) =>
        seriesStatus is ContentArticleSeriesStatuses.Planned or ContentArticleSeriesStatuses.Active;

    public static bool CanMutatePlan(string? seriesStatus) =>
        seriesStatus is ContentArticleSeriesStatuses.Draft
            or ContentArticleSeriesStatuses.Planned
            or ContentArticleSeriesStatuses.Active;

    public static bool IsTerminalEpisode(string? status) =>
        status is ContentArticleEpisodeStatuses.Published
            or ContentArticleEpisodeStatuses.Analyzed
            or ContentArticleEpisodeStatuses.Cancelled;

    public static string MapTopicToEpisodeStatus(string? topicStatus) =>
        topicStatus switch
        {
            "Generating" => ContentArticleEpisodeStatuses.Generating,
            "Review" => ContentArticleEpisodeStatuses.Review,
            "Approved" => ContentArticleEpisodeStatuses.Approved,
            "Scheduled" => ContentArticleEpisodeStatuses.Scheduled,
            "Published" => ContentArticleEpisodeStatuses.Published,
            "BudgetBlocked" => ContentArticleEpisodeStatuses.Paused,
            "Rejected" => ContentArticleEpisodeStatuses.Review,
            _ => ContentArticleEpisodeStatuses.Ready,
        };

    public static bool QualityGateBlocksApprove(ContentQualityGateDto? gate) =>
        gate is { CanPublish: false } || gate is { Passed: false, BlockingIssues: { Count: > 0 } };

    /// <summary>
    /// Episode packages inherit the Series plan. If the source góc has no Creative Brief,
    /// fill objective + format so Quality Gate can approve.
    /// </summary>
    /// <summary>Brief for an episode article. Objective comes from the episode, not a separate story.</summary>
    public static ContentCreativeBriefDto EpisodeContentBrief(
        string? episodeObjective,
        string? keyMessage,
        string? seriesObjective)
    {
        var objective = FirstNonEmpty(episodeObjective, keyMessage, seriesObjective)
                        ?? "Mở đúng bước của episode này và giữ một nút chưa đóng.";
        return new ContentCreativeBriefDto(objective, null, "web_long");
    }

    public static ContentCreativeBriefDto EpisodeBrief(
        string? seriesObjective,
        string? episodeObjective,
        string? episodeAngle)
    {
        var objective = FirstNonEmpty(episodeObjective, seriesObjective, episodeAngle)
                        ?? "Giữ độc giả đọc hết bài và nhớ một việc nhỏ cần làm.";
        return new ContentCreativeBriefDto(objective, null, "article", null, null);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    public static ContentArticleSeriesBlueprint ParseBlueprint(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return ContentArticleSeriesBlueprint.Empty;
        try
        {
            return JsonSerializer.Deserialize<ContentArticleSeriesBlueprint>(json, JsonOpts)
                   ?? ContentArticleSeriesBlueprint.Empty;
        }
        catch (JsonException)
        {
            return ContentArticleSeriesBlueprint.Empty;
        }
    }

    public static string SerializeBlueprint(ContentArticleSeriesBlueprint? blueprint) =>
        JsonSerializer.Serialize(blueprint ?? ContentArticleSeriesBlueprint.Empty, JsonOpts);

    public static ContentArticleEpisodeContinuity ParseContinuity(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return ContentArticleEpisodeContinuity.Empty;
        try
        {
            return JsonSerializer.Deserialize<ContentArticleEpisodeContinuity>(json, JsonOpts)
                   ?? ContentArticleEpisodeContinuity.Empty;
        }
        catch (JsonException)
        {
            return ContentArticleEpisodeContinuity.Empty;
        }
    }

    public static string SerializeContinuity(ContentArticleEpisodeContinuity? continuity) =>
        JsonSerializer.Serialize(continuity ?? ContentArticleEpisodeContinuity.Empty, JsonOpts);

    public static string BuildContinuityContext(
        ContentArticleSeriesDto series,
        ContentArticleEpisodeDto current,
        ContentArticleEpisodeDto? previous,
        ContentArticleEpisodeDto? next,
        ContentCoreIdeaDto? core,
        string? brandBrainBlock,
        string? adaptationBlock)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BRAND");
        sb.AppendLine(series.BrandName + " (" + series.BrandCode + ")");
        if (!string.IsNullOrWhiteSpace(brandBrainBlock))
            sb.AppendLine(brandBrainBlock.Trim());
        sb.AppendLine();
        sb.AppendLine("CORE IDEA");
        sb.AppendLine(series.CoreIdeaTitle);
        if (core is not null)
        {
            if (!string.IsNullOrWhiteSpace(core.Insight)) sb.AppendLine("Insight: " + core.Insight);
            if (!string.IsNullOrWhiteSpace(core.Problem)) sb.AppendLine("Problem: " + core.Problem);
            if (!string.IsNullOrWhiteSpace(core.CoreMessage)) sb.AppendLine("Core message: " + core.CoreMessage);
        }

        sb.AppendLine();
        sb.AppendLine("BRAND ADAPTATION");
        sb.AppendLine(series.SourcePackageTitle);
        if (!string.IsNullOrWhiteSpace(adaptationBlock))
            sb.AppendLine(adaptationBlock.Trim());
        sb.AppendLine();
        sb.AppendLine("SERIES");
        sb.AppendLine(series.Name + " [" + series.Code + "]");
        sb.AppendLine("Objective: " + (series.Objective ?? ""));
        sb.AppendLine("Audience: " + (series.Audience ?? ""));
        sb.AppendLine("Core message: " + (series.CoreMessage ?? ""));
        sb.AppendLine();
        sb.AppendLine("SERIES BLUEPRINT");
        sb.AppendLine(series.Blueprint.GetRawText());
        sb.AppendLine();
        sb.AppendLine("CURRENT EPISODE");
        AppendEpisode(sb, current);
        sb.AppendLine();
        sb.AppendLine("PREVIOUS EPISODES");
        if (previous is null) sb.AppendLine("(none)");
        else AppendEpisode(sb, previous);
        sb.AppendLine();
        sb.AppendLine("NEXT EPISODE");
        if (next is null) sb.AppendLine("(none — last planned in this window)");
        else AppendEpisode(sb, next);
        sb.AppendLine();
        sb.AppendLine("CONTENT OBJECTIVE");
        sb.AppendLine(current.Objective ?? current.Title);
        sb.AppendLine();
        sb.AppendLine("CONTINUITY");
        sb.AppendLine(current.Continuity.GetRawText());
        return sb.ToString().Trim();
    }

    public static string BuildOutlineBlock(
        ContentArticleSeriesDto series,
        ContentArticleEpisodeDto current,
        ContentArticleEpisodeDto? previous,
        ContentArticleEpisodeDto? next,
        string continuityContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== " + OutlineMarker + " ===");
        sb.AppendLine("Series: " + series.Name + " (" + series.Code + ")");
        sb.AppendLine("Episode: " + current.Code + " · " + current.Title);
        sb.AppendLine("Objective: " + (current.Objective ?? ""));
        sb.AppendLine("Angle: " + (current.Angle ?? ""));
        sb.AppendLine("Key message: " + (current.KeyMessage ?? ""));
        if (previous is not null)
            sb.AppendLine("Previous: " + previous.Code + " — " + previous.Title + " / " + (previous.KeyMessage ?? previous.Angle ?? ""));
        if (next is not null)
            sb.AppendLine("Next direction: " + next.Code + " — " + next.Title);
        sb.AppendLine();
        sb.AppendLine(continuityContext);
        sb.AppendLine("=== END " + OutlineMarker + " ===");
        return sb.ToString().Trim();
    }

    public static JsonElement ToElement<T>(T value) =>
        JsonSerializer.SerializeToElement(value, JsonOpts);

    private static void AppendEpisode(StringBuilder sb, ContentArticleEpisodeDto ep)
    {
        sb.AppendLine(ep.Code + " · " + ep.Title);
        if (!string.IsNullOrWhiteSpace(ep.Objective)) sb.AppendLine("  Objective: " + ep.Objective);
        if (!string.IsNullOrWhiteSpace(ep.Angle)) sb.AppendLine("  Angle: " + ep.Angle);
        if (!string.IsNullOrWhiteSpace(ep.KeyMessage)) sb.AppendLine("  Key message: " + ep.KeyMessage);
    }
}

public sealed record ContentArticleSeriesBlueprint(
    int Version,
    IReadOnlyList<ContentArticleNarrativeBeat> NarrativeArc,
    IReadOnlyList<string> ContentPillars,
    string? AudienceNeed,
    string? CoreMessage,
    string? Tone,
    string? Style,
    IReadOnlyList<string> RecommendedFormats,
    IReadOnlyList<string> RecommendedChannels,
    int EpisodeCount,
    IReadOnlyList<string> ContinuityRules,
    IReadOnlyList<string> AvoidRepetition,
    string? CtaStrategy,
    string? SuccessDefinition,
    string? SeriesTone = null,
    string? SeriesStyle = null,
    string? SeriesCta = null,
    string? Territory = null,
    string? TerritoryId = null,
    string? BrandAngle = null)
{
    public static ContentArticleSeriesBlueprint Empty { get; } = new(
        1, [], [], null, null, null, null, [], [], 10, [], [], null, null);
}

/// <summary>Gemini often emits version/episodeNo as "1", "1.0", or "v1".</summary>
public sealed class FlexibleInt32JsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number when reader.TryGetInt32(out var n):
                return n;
            case JsonTokenType.Number:
                return reader.TryGetDouble(out var d) ? (int)d : 0;
            case JsonTokenType.String:
            {
                var raw = reader.GetString();
                if (int.TryParse(raw, out var i)) return i;
                if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var f))
                    return (int)f;
                var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
                return int.TryParse(digits, out var fromDigits) ? fromDigits : 0;
            }
            default:
                return 0;
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

public sealed record ContentArticleNarrativeBeat(
    int EpisodeNo,
    string? Title,
    string? Beat,
    string? Objective,
    string? Angle,
    string? KeyMessage);

public sealed record ContentArticleEpisodeContinuity(
    string? PreviousSummary,
    IReadOnlyList<string> PreviousKeyPoints,
    IReadOnlyList<string> MustContinueFrom,
    IReadOnlyList<string> MustNotRepeat,
    string? NextEpisodeDirection,
    IReadOnlyList<string> OpenLoops,
    IReadOnlyList<string> References)
{
    public static ContentArticleEpisodeContinuity Empty { get; } = new(
        null, [], [], [], null, [], []);
}
