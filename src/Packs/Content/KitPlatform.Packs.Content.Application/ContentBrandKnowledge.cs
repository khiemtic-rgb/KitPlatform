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
    IReadOnlyList<string> BadExamples,
    /// <summary>
    /// Brand knowledge base: one self-contained unit per entry (topic map rows, verified formulas,
    /// mistakes to avoid, process rules). Selected by relevance and budget at prompt time, so a brand
    /// can carry far more knowledge than a single prompt fits. Null on input means "not sent".
    /// </summary>
    IReadOnlyList<string>? KnowledgeUnits = null);

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
            GetStrings(tone, "badExamples"),
            GetUnits(tone, "knowledgeUnits"));
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
            ["knowledgeUnits"] = NonEmpty(Units(k)),
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

    public static string FormatForPrompt(
        ContentBrandKnowledgeDto k,
        string? operationalBrief,
        string? focus = null,
        int knowledgeBudget = DefaultKnowledgeBudget)
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
        AppendKnowledge(sb, SelectKnowledge(k, focus, knowledgeBudget));
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

    /// <summary>
    /// Script draft: brand knowledge packed by priority inside <paramref name="maxChars"/>.
    /// Hard limits (forbidden claims/topics, positioning, voice) always come first, then the knowledge
    /// units most relevant to <paramref name="focus"/>, then the rest. Nothing critical is lost to clipping.
    /// Never dump source PDFs.
    /// </summary>
    public static string FormatForSeriesDraft(
        ContentBrandKnowledgeDto k,
        string? operationalBrief,
        int maxChars = 4500,
        string? focus = null)
    {
        const string footer = "Apply this locked brand knowledge. Do not reinvent the brand, philosophy, or characters.";
        var sections = new List<string>();

        var core = new StringBuilder();
        core.AppendLine("=== BRAND BRAIN ===");
        Append(core, "Positioning", k.Positioning);
        Append(core, "Audience", k.Audience);
        AppendList(core, "Claims FORBIDDEN", k.ClaimsForbidden);
        AppendList(core, "Forbidden topics", k.ForbiddenTopics);
        AppendList(core, "Avoid terms", k.AvoidTerms);
        AppendList(core, "Tone", k.Tone);
        Append(core, "Voice notes", k.VoiceNotes);
        Append(core, "CTA style", k.CtaStyle);
        if (!string.IsNullOrWhiteSpace(operationalBrief))
            core.AppendLine("Brief: " + Clip(operationalBrief.Trim(), 700));
        sections.Add(core.ToString().TrimEnd());

        var used = sections[0].Length + footer.Length + 40;
        var knowledgeRoom = Math.Max(0, Math.Min(DefaultKnowledgeBudget, (maxChars - used) * 3 / 5));
        var units = SelectKnowledge(k, focus, knowledgeRoom);
        if (units.Count > 0)
        {
            var kb = new StringBuilder();
            AppendKnowledge(kb, units);
            sections.Add(kb.ToString().TrimEnd());
        }

        var optional = new List<(string Label, IReadOnlyList<string> Values)>
        {
            ("Content pillars", k.ContentPillars),
            ("Proof points", k.ProofPoints),
            ("Prefer terms", k.PreferredTerms),
            ("Differentiators", k.Differentiators),
            ("Good content examples", k.GoodExamples),
            ("Bad content examples (do not imitate)", k.BadExamples),
            ("Problems (pain)", k.Problems),
            ("Needs", k.Needs),
            ("Desires", k.Desires),
            ("Claims allowed", k.ClaimsAllowed),
            ("Products", k.Products),
            ("Services", k.Services),
            ("Competitors / context", k.Competitors),
            ("Hashtags", k.Hashtags),
        };
        foreach (var (label, values) in optional)
        {
            var line = ListText(label, values);
            if (line is not null) sections.Add(line);
        }
        foreach (var (label, value) in new[]
                 {
                     ("Visual style", k.VisualStyle),
                     ("Visual colors", k.VisualColors),
                     ("Image notes", k.ImageNotes),
                 })
        {
            if (!string.IsNullOrWhiteSpace(value)) sections.Add(label + ": " + value.Trim());
        }

        if (sections.Count == 1 && sections[0] == "=== BRAND BRAIN ===") return "";

        var sb = new StringBuilder();
        var budget = Math.Max(0, maxChars - footer.Length - 40);
        foreach (var section in sections)
        {
            var room = budget - sb.Length;
            if (room <= 0) break;
            if (section.Length + 1 <= room)
            {
                sb.AppendLine(section);
            }
            else if (room > 160)
            {
                sb.AppendLine(section[..(room - 2)].TrimEnd() + "…");
                break;
            }
        }

        sb.AppendLine("=== END BRAND BRAIN ===");
        sb.AppendLine();
        sb.Append(footer);
        return sb.ToString().Trim();
    }

    /// <summary>Default character budget for knowledge units inside one prompt.</summary>
    public const int DefaultKnowledgeBudget = 2400;

    public static IReadOnlyList<string> Units(ContentBrandKnowledgeDto k) => k.KnowledgeUnits ?? [];

    /// <summary>
    /// Picks the knowledge units that matter for <paramref name="focus"/> (topic, seed, angle) within a
    /// character budget. Units whose code (e.g. "B2") appears in the focus win, then units sharing the most
    /// words with the focus, then the remaining units in their saved order. Without a focus the saved
    /// order is kept, so brands control priority by ordering their units.
    /// </summary>
    public static IReadOnlyList<string> SelectKnowledge(ContentBrandKnowledgeDto k, string? focus, int budget)
    {
        var units = Units(k)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .ToList();
        if (units.Count == 0 || budget <= 0) return [];

        var focusText = Fold(focus);
        var focusWords = Words(focusText);
        var focusCodes = Codes(focus);
        var ranked = units
            .Select((u, i) =>
            {
                var score = 0;
                if (focusCodes.Count > 0 && Codes(u).Overlaps(focusCodes)) score += 1000;
                if (focusWords.Count > 0)
                    score += Words(Fold(u)).Count(focusWords.Contains) * 10;
                return (Unit: u, Index: i, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .ToList();

        var picked = new List<(string Unit, int Index)>();
        var used = 0;
        foreach (var x in ranked)
        {
            var cost = x.Unit.Length + 3;
            if (used + cost > budget)
            {
                if (picked.Count == 0 && budget > 200)
                {
                    picked.Add((x.Unit[..(budget - 4)].TrimEnd() + "…", x.Index));
                    used = budget;
                }
                continue;
            }
            picked.Add((x.Unit, x.Index));
            used += cost;
        }

        return picked.Select(p => p.Unit).ToList();
    }

    /// <summary>
    /// Keeps the saved knowledge units when an update did not send them (older admin UI, API clients),
    /// so editing other Brand fields never wipes the brand knowledge base.
    /// </summary>
    public static string CarryKnowledgeUnits(string newToneJson, string? existingToneJson)
    {
        var existing = ParseObj(existingToneJson);
        if (!existing.TryGetValue("knowledgeUnits", out var units) || units.ValueKind != JsonValueKind.Array)
            return newToneJson;
        var next = ParseObj(newToneJson);
        if (next.TryGetValue("knowledgeUnits", out var sent) && sent.ValueKind == JsonValueKind.Array)
            return newToneJson;
        next["knowledgeUnits"] = units;
        return JsonSerializer.Serialize(next, JsonOpts);
    }

    private static void AppendKnowledge(StringBuilder sb, IReadOnlyList<string> units)
    {
        if (units.Count == 0) return;
        sb.AppendLine("Brand knowledge (locked; follow exactly, do not contradict):");
        foreach (var u in units)
            sb.Append("- ").AppendLine(u);
    }

    private static string? ListText(string label, IReadOnlyList<string> values)
    {
        var items = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        return items.Count == 0 ? null : label + ": " + string.Join("; ", items);
    }

    private static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var normalized = value.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(ch == 'đ' ? 'd' : ch);
        }
        return sb.ToString();
    }

    private static HashSet<string> Words(string folded) =>
        folded
            .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !StopWords.Contains(w))
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> Codes(string? value)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) return set;
        foreach (System.Text.RegularExpressions.Match m in CodePattern.Matches(value))
            set.Add(m.Value);
        return set;
    }

    private static readonly System.Text.RegularExpressions.Regex CodePattern =
        new(@"\b[A-Z]\d{1,2}\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly char[] WordSeparators =
        [' ', '\t', '\n', '\r', ',', '.', ';', ':', '-', '(', ')', '/', '·', '"', '\'', '?', '!', '=', '+', '%', '[', ']'];

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "cac", "cua", "cho", "voi", "nhung", "mot", "duoc", "khong", "trong", "theo", "hay", "hoac",
        "the", "and", "for", "with", "this", "that", "from", "nao", "nay", "khi", "can", "phai",
        "bai", "video", "noi", "dung", "lam",
    };

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

    private static IReadOnlyList<string>? GetUnits(Dictionary<string, JsonElement> map, string key)
    {
        if (!map.TryGetValue(key, out var el)) return null;
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
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return null;
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
