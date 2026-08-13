using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

/// <summary>Structured brand voice — stored in <c>brand.tone_json</c> + <c>visual_kit_json</c>.</summary>
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
    string? ImageNotes);

public static class ContentBrandKnowledge
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ContentBrandKnowledgeDto Empty { get; } = new(
        null, null, [], [], [], [], [], null, null, null, null, null);

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
            GetString(visual, "imageNotes"));
    }

    public static (string ToneJson, string VisualKitJson) Serialize(ContentBrandKnowledgeDto? knowledge)
    {
        var k = knowledge ?? Empty;
        var tone = new Dictionary<string, object?>
        {
            ["positioning"] = NullIfEmpty(k.Positioning),
            ["audience"] = NullIfEmpty(k.Audience),
            ["tone"] = k.Tone.Count > 0 ? k.Tone : null,
            ["forbiddenTopics"] = k.ForbiddenTopics.Count > 0 ? k.ForbiddenTopics : null,
            ["preferredTerms"] = k.PreferredTerms.Count > 0 ? k.PreferredTerms : null,
            ["avoidTerms"] = k.AvoidTerms.Count > 0 ? k.AvoidTerms : null,
            ["hashtags"] = k.Hashtags.Count > 0 ? k.Hashtags : null,
            ["ctaStyle"] = NullIfEmpty(k.CtaStyle),
            ["voiceNotes"] = NullIfEmpty(k.VoiceNotes),
        };
        var visual = new Dictionary<string, object?>
        {
            ["style"] = NullIfEmpty(k.VisualStyle),
            ["colors"] = NullIfEmpty(k.VisualColors),
            ["imageNotes"] = NullIfEmpty(k.ImageNotes),
        };
        return (JsonSerializer.Serialize(tone, JsonOpts), JsonSerializer.Serialize(visual, JsonOpts));
    }

    public static bool HasEnoughForGenerate(string? operationalBrief, ContentBrandKnowledgeDto knowledge)
    {
        if (!string.IsNullOrWhiteSpace(operationalBrief) && operationalBrief.Trim().Length >= 40)
            return true;
        return !string.IsNullOrWhiteSpace(knowledge.Positioning)
               && knowledge.Positioning.Trim().Length >= 20;
    }

    /// <summary>Compact block injected into Gemini user prompt.</summary>
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

        sb.AppendLine("=== BRAND KNOWLEDGE (structured) ===");
        Append(sb, "Positioning", k.Positioning);
        Append(sb, "Audience", k.Audience);
        AppendList(sb, "Tone", k.Tone);
        AppendList(sb, "Forbidden topics", k.ForbiddenTopics);
        AppendList(sb, "Prefer terms", k.PreferredTerms);
        AppendList(sb, "Avoid terms", k.AvoidTerms);
        AppendList(sb, "Hashtags", k.Hashtags);
        Append(sb, "CTA style", k.CtaStyle);
        Append(sb, "Voice notes", k.VoiceNotes);
        Append(sb, "Visual style", k.VisualStyle);
        Append(sb, "Visual colors", k.VisualColors);
        Append(sb, "Image notes", k.ImageNotes);
        sb.AppendLine("=== END BRAND KNOWLEDGE ===");
        return sb.ToString().Trim();
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
