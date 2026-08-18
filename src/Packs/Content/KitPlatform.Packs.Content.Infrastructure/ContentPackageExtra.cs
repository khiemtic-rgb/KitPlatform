using System.Text.Json;
using System.Text.Json.Serialization;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal static class ContentPackageExtra
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static (ContentCoreIdeaDto Core, IReadOnlyList<ContentBrandFitDto> Fits) Parse(string? extraJson)
    {
        var root = ParseRoot(extraJson);
        var core = root.CoreIdea ?? new ContentCoreIdeaDto(null, null, null, [], null);
        return (core, root.BrandFit ?? []);
    }

    public static ContentQualityGateDto? ParseGate(string? extraJson)
    {
        var gate = ParseRoot(extraJson).QualityGate;
        return gate is null ? null : ContentQualityGate.Normalize(gate);
    }

    public static ContentCreativeBriefDto? ParseBrief(string? extraJson) =>
        SanitizeBrief(ParseRoot(extraJson).CreativeBrief);

    public static string Merge(
        string? existing,
        ContentCoreIdeaDto? core,
        IReadOnlyList<ContentBrandFitDto>? fits)
    {
        var root = ParseRoot(existing);
        if (core is not null) root.CoreIdea = core;
        if (fits is not null) root.BrandFit = fits.ToList();
        return JsonSerializer.Serialize(root, JsonOpts);
    }

    public static ContentCoreIdeaDto FromRequest(UpsertContentPackageRequest request, ContentCoreIdeaDto? previous = null)
    {
        var prev = previous ?? new ContentCoreIdeaDto(null, null, null, [], null);
        var keywords = request.Keywords is { Count: > 0 }
            ? request.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList()
            : prev.Keywords;
        return new ContentCoreIdeaDto(
            NullIfEmpty(request.Insight) ?? prev.Insight,
            NullIfEmpty(request.Problem) ?? prev.Problem,
            NullIfEmpty(request.CoreMessage) ?? prev.CoreMessage,
            keywords,
            NullIfEmpty(request.Source) ?? prev.Source,
            NullIfEmpty(request.SourceUrl) ?? prev.SourceUrl,
            NullIfEmpty(request.SourceType) ?? prev.SourceType,
            NullIfEmpty(request.Evidence) ?? prev.Evidence,
            NullIfEmpty(request.FactOrOpinion) ?? prev.FactOrOpinion);
    }

    public static string MergeGate(string? existing, ContentQualityGateDto gate)
    {
        var root = ParseRoot(existing);
        root.QualityGate = ContentQualityGate.Normalize(gate);
        return JsonSerializer.Serialize(root, JsonOpts);
    }

    public static string MergeBrief(string? existing, ContentCreativeBriefDto? brief)
    {
        var root = ParseRoot(existing);
        root.CreativeBrief = SanitizeBrief(brief);
        return JsonSerializer.Serialize(root, JsonOpts);
    }

    public static ContentCreativeBriefDto? SanitizeBrief(ContentCreativeBriefDto? brief)
    {
        if (brief is null) return null;
        var duration = brief.DurationSec is > 0 and < 600 ? brief.DurationSec : null;
        var clean = new ContentCreativeBriefDto(
            NullIfEmpty(brief.Objective),
            NullIfEmpty(brief.Emotion),
            NullIfEmpty(brief.Format),
            NullIfEmpty(brief.VisualDirection),
            duration);
        if (clean.Objective is null
            && clean.Emotion is null
            && clean.Format is null
            && clean.VisualDirection is null
            && clean.DurationSec is null)
        {
            return null;
        }

        return clean;
    }

    private static ExtraRoot ParseRoot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            return new ExtraRoot();
        try
        {
            return JsonSerializer.Deserialize<ExtraRoot>(json, JsonOpts) ?? new ExtraRoot();
        }
        catch
        {
            return new ExtraRoot();
        }
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private sealed class ExtraRoot
    {
        public ContentCoreIdeaDto? CoreIdea { get; set; }
        public List<ContentBrandFitDto>? BrandFit { get; set; }
        public ContentQualityGateDto? QualityGate { get; set; }
        public ContentCreativeBriefDto? CreativeBrief { get; set; }
    }
}
