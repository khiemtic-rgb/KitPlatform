using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>Brand Brain — stored in <c>brand.tone_json</c> + <c>visual_kit_json</c>. No extra table.</summary>
public sealed record ContentBrandKnowledgeDto(
    string? Positioning,
    string? Audience,
    IReadOnlyList<string> Tone,
    IReadOnlyList<string> ForbiddenTopics,
    IReadOnlyList<string> PreferredTerms,
    IReadOnlyList<string> AvoidTerms,
    IReadOnlyList<string> Hashtags,
    string? CtaStyle,
    string? VoiceNotes,
    string? VisualStyle,
    string? VisualColors,
    string? ImageNotes,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Needs,
    IReadOnlyList<string> Desires,
    IReadOnlyList<string> ContentPillars,
    IReadOnlyList<string> ClaimsAllowed,
    IReadOnlyList<string> ClaimsForbidden,
    IReadOnlyList<string> Products,
    IReadOnlyList<string> Services,
    IReadOnlyList<string> Differentiators,
    IReadOnlyList<string> ProofPoints,
    IReadOnlyList<string> Competitors,
    IReadOnlyList<string> GoodExamples,
    IReadOnlyList<string> BadExamples);

public static class ContentBrandKnowledge
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ContentBrandKnowledgeDto Empty { get; } = new(
        null, null, [], [], [], [], [], null, null, null, null, null,
        [], [], [], [], [], [], [], [], [], [], [], [], []);

    public static ContentBrandKnowledgeDto Parse(string? toneJson, string? visualKitJson)
    {
        var tone = ParseObj(toneJson);
        var visual = ParseObj(visualKitJson);
        return new ContentBrandKnowledgeDto(
            GetString(tone, "positioning"),
            GetString(tone, "audience"),
            GetStrings(tone, "tone"),
            GetStrings(tone, "forbiddenTopics"),
            GetStrings(tone, "preferredTerms"),
            GetStrings(tone, "avoidTerms"),
            GetStrings(tone, "hashtags"),
            GetString(tone, "ctaStyle"),
            GetString(tone, "voiceNotes"),
            GetString(visual, "style") ?? GetString(visual, "visualStyle"),
            GetString(visual, "colors") ?? GetString(visual, "visualColors"),
            GetString(visual, "imageNotes"),
            GetStrings(tone, "problems"),
            GetStrings(tone, "needs"),
            GetStrings(tone, "desires"),
            GetStrings(tone, "contentPillars"),
            GetStrings(tone, "claimsAllowed"),
            GetStrings(tone, "claimsForbidden"),
            GetStrings(tone, "products"),
            GetStrings(tone, "services"),
            GetStrings(tone, "differentiators"),
            GetStrings(tone, "proofPoints"),
            GetStrings(tone, "competitors"),
            GetStrings(tone, "goodExamples"),
            GetStrings(tone, "badExamples"));
    }

    public static (string ToneJson, string VisualKitJson) Serialize(ContentBrandKnowledgeDto? knowledge)
    {
        var k = knowledge ?? Empty;
        var tone = new Dictionary<string, object?>
        {
            ["positioning"] = NullIfEmpty(k.Positioning),
            ["audience"] = NullIfEmpty(k.Audience),
            ["tone"] = NonEmpty(k.Tone),
            ["forbiddenTopics"] = NonEmpty(k.ForbiddenTopics),
            ["preferredTerms"] = NonEmpty(k.PreferredTerms),
            ["avoidTerms"] = NonEmpty(k.AvoidTerms),
            ["hashtags"] = NonEmpty(k.Hashtags),
            ["ctaStyle"] = NullIfEmpty(k.CtaStyle),
            ["voiceNotes"] = NullIfEmpty(k.VoiceNotes),
            ["problems"] = NonEmpty(k.Problems),
            ["needs"] = NonEmpty(k.Needs),
            ["desires"] = NonEmpty(k.Desires),
            ["contentPillars"] = NonEmpty(k.ContentPillars),
            ["claimsAllowed"] = NonEmpty(k.ClaimsAllowed),
            ["claimsForbidden"] = NonEmpty(k.ClaimsForbidden),
            ["products"] = NonEmpty(k.Products),
            ["services"] = NonEmpty(k.Services),
            ["differentiators"] = NonEmpty(k.Differentiators),
            ["proofPoints"] = NonEmpty(k.ProofPoints),
            ["competitors"] = NonEmpty(k.Competitors),
            ["goodExamples"] = NonEmpty(k.GoodExamples),
            ["badExamples"] = NonEmpty(k.BadExamples),
        };
        var visual = new Dictionary<string, object?>
        {
            ["style"] = NullIfEmpty(k.VisualStyle),
            ["colors"] = NullIfEmpty(k.VisualColors),
            ["imageNotes"] = NullIfEmpty(k.ImageNotes),
        };
        return (JsonSerializer.Serialize(tone, JsonOpts), JsonSerializer.Serialize(visual, JsonOpts));
    }

    public static bool HasVoice(string? operationalBrief, ContentBrandKnowledgeDto knowledge)
    {
        if (!string.IsNullOrWhiteSpace(operationalBrief) && operationalBrief.Trim().Length >= 40)
            return true;
        return !string.IsNullOrWhiteSpace(knowledge.Positioning)
               && knowledge.Positioning.Trim().Length >= 20;
    }

    public static bool HasEnoughForGenerate(string? operationalBrief, ContentBrandKnowledgeDto knowledge) =>
        MissingBrain(operationalBrief, knowledge).Count == 0;

    public static IReadOnlyList<string> MissingBrain(string? operationalBrief, ContentBrandKnowledgeDto knowledge)
    {
        var miss = new List<string>();
        if (!HasVoice(operationalBrief, knowledge))
            miss.Add("Positioning (≥20) hoặc Brief vận hành (≥40)");
        if (knowledge.ClaimsForbidden is not { Count: > 0 })
            miss.Add("Claims forbidden (ít nhất 1)");
        if (knowledge.ProofPoints is not { Count: > 0 })
            miss.Add("Proof points (ít nhất 1)");
        if (knowledge.GoodExamples is not { Count: > 0 })
            miss.Add("Ví dụ nội dung tốt (ít nhất 1)");
        return miss;
    }

    public static string FormatForPrompt(ContentBrandKnowledgeDto k, string? operationalBrief)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(operationalBrief))
        {
            sb.AppendLine("=== BRAND OPERATIONAL BRIEF ===");
            sb.AppendLine(operationalBrief.Trim());
            sb.AppendLine("=== END BRIEF ===");
            sb.AppendLine();
        }

        sb.AppendLine("=== BRAND BRAIN ===");
        Append(sb, "Positioning", k.Positioning);
        Append(sb, "Audience", k.Audience);
        AppendList(sb, "Problems (pain)", k.Problems);
        AppendList(sb, "Needs", k.Needs);
        AppendList(sb, "Desires", k.Desires);
        AppendList(sb, "Content pillars", k.ContentPillars);
        AppendList(sb, "Tone", k.Tone);
        AppendList(sb, "Forbidden topics", k.ForbiddenTopics);
        AppendList(sb, "Claims allowed", k.ClaimsAllowed);
        AppendList(sb, "Claims FORBIDDEN", k.ClaimsForbidden);
        AppendList(sb, "Products", k.Products);
        AppendList(sb, "Services", k.Services);
        AppendList(sb, "Differentiators", k.Differentiators);
        AppendList(sb, "Proof points", k.ProofPoints);
        AppendList(sb, "Competitors / context", k.Competitors);
        AppendList(sb, "Prefer terms", k.PreferredTerms);
        AppendList(sb, "Avoid terms", k.AvoidTerms);
        AppendList(sb, "Hashtags", k.Hashtags);
        Append(sb, "CTA style", k.CtaStyle);
        Append(sb, "Voice notes", k.VoiceNotes);
        AppendList(sb, "Good content examples", k.GoodExamples);
        AppendList(sb, "Bad content examples (do not imitate)", k.BadExamples);
        Append(sb, "Visual style", k.VisualStyle);
        Append(sb, "Visual colors", k.VisualColors);
        Append(sb, "Image notes", k.ImageNotes);
        sb.AppendLine("=== END BRAND BRAIN ===");
        return sb.ToString().Trim();
    }

    /// <summary>Short card for Brand Fit — full brain + 6 brands overflows Gemini output.</summary>
    public static string FormatForFitScore(ContentBrandKnowledgeDto k, string? operationalBrief, int maxChars = 1400)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(operationalBrief))
            sb.AppendLine("Brief: " + Clip(operationalBrief.Trim(), 280));
        Append(sb, "Audience", k.Audience);
        AppendList(sb, "Pillars", k.ContentPillars);
        AppendList(sb, "Problems", k.Problems);
        AppendList(sb, "Products", k.Products);
        AppendList(sb, "Claims FORBIDDEN", k.ClaimsForbidden);
        AppendList(sb, "Forbidden topics", k.ForbiddenTopics);
        var text = sb.ToString().Trim();
        return text.Length <= maxChars ? text : text[..maxChars].TrimEnd() + "…";
    }

    private static string Clip(string value, int max)
    {
        var t = value.Trim();
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }

    /// <summary>Script draft: full brain, clipped. Never dump source PDFs.</summary>
    public static string FormatForSeriesDraft(ContentBrandKnowledgeDto k, string? operationalBrief, int maxChars = 4500)
    {
        var text = FormatForPrompt(k, operationalBrief);
        if (string.IsNullOrWhiteSpace(text)) return "";
        text += "\n\nApply this locked Famixa knowledge. Do not reinvent the brand, philosophy, or characters.";
        return text.Length <= maxChars ? text : text[..maxChars].TrimEnd() + "…";
    }

    /// <summary>I2V slice only — visual + forbidden. English, short.</summary>
    public static string FormatForVideoContext(ContentBrandKnowledgeDto k, int maxChars = 360)
    {
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(k.VisualStyle)) bits.Add(k.VisualStyle.Trim());
        if (!string.IsNullOrWhiteSpace(k.VisualColors)) bits.Add(k.VisualColors.Trim());
        var forbid = k.ClaimsForbidden
            .Concat(k.ForbiddenTopics)
            .Concat(k.AvoidTerms)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
        if (forbid.Count > 0) bits.Add("Forbidden: " + string.Join("; ", forbid));
        bits.Add("No looking at the camera. No extra people. Do not change faces, age, clothes or location.");
        var text = string.Join(". ", bits);
        return text.Length <= maxChars ? text : text[..Math.Max(0, maxChars - 1)].TrimEnd() + "…";
    }

    private static void Append(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        sb.Append(label).Append(": ").AppendLine(value.Trim());
    }

    private static void AppendList(StringBuilder sb, string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0) return;
        sb.Append(label).Append(": ").AppendLine(string.Join("; ", values.Where(v => !string.IsNullOrWhiteSpace(v))));
    }

    private static object? NonEmpty(IReadOnlyList<string> values) =>
        values.Count > 0 ? values : null;

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static Dictionary<string, JsonElement> ParseObj(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOpts)
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? GetString(Dictionary<string, JsonElement> map, string key)
    {
        if (!map.TryGetValue(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static IReadOnlyList<string> GetStrings(Dictionary<string, JsonElement> map, string key)
    {
        if (!map.TryGetValue(key, out var el)) return [];
        if (el.ValueKind == JsonValueKind.Array)
        {
            return el.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!.Trim())
                .ToList();
        }

        if (el.ValueKind == JsonValueKind.String)
        {
            return el.GetString()!
                .Split(['\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .ToList();
        }

        return [];
    }
}
