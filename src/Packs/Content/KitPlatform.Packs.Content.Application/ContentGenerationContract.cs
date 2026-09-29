using System.Text;

namespace KitPlatform.Packs.Content;

/// <summary>
/// What one generation call is allowed to see.
/// Narrative DNA stays authoritative. The operational brief cannot replace it.
/// </summary>
public sealed record ContentNarrativeSelection(
    string? Territory,
    string? TerritoryId,
    string? BrandAngle,
    bool? Fit);

public static class ContentGenerationContract
{
    public const string AuthoritativeMarker = "AUTHORITATIVE NARRATIVE CONTEXT";
    public const string SupplementalMarker = "SUPPLEMENTAL EXECUTION CONTEXT";
    public const string NotNarrativeAuthority = "NOT NARRATIVE AUTHORITY";

    public const string BriefRequired =
        "Thiếu Creative Brief (mục tiêu + format) — điền trước khi generate.";

    public const string WebLongContract =
        "web_long: long-form article, not a short post. One thesis — the Brand Angle. " +
        "Enough depth to show the operational problem and one concrete action. " +
        "800–1400 Vietnamese words. At least 2200 characters. At least 2 markdown ## headings. " +
        "Exactly 3 ## sections, each proving the same thesis. Do not write a caption or a software brochure.";

    public static void EnsureBrief(ContentCreativeBriefDto? brief)
    {
        if (!ContentQualityGate.HasMinimumBrief(brief))
            throw new InvalidOperationException(BriefRequired);
    }

    /// <summary>Matches the website gate. Does not loosen it.</summary>
    public static string? RejectWebLong(string? body)
    {
        var text = (body ?? "").Trim();
        if (text.Length < 2200)
            return "web_long: quá mỏng — cần bài một luận điểm, khoảng 800–1400 từ";
        if (ContentQualityGate.CountMarkdownH2(text) < 2)
            return "web_long: thiếu mục ## — cần ít nhất 2 heading ## (mỗi luận điểm một H2)";
        return null;
    }

    public static string PackSystem() =>
        "You are a Vietnamese multi-channel content writer for KIT Marketing Park.\n" +
        "Return valid JSON only. No markdown fences.\n" +
        AuthoritativeMarker + " decides the story, the territory, and the Brand Angle.\n" +
        SupplementalMarker + " is " + NotNarrativeAuthority + ". If it conflicts with the narrative context, ignore it.\n" +
        "Brand identity supplies voice and forbidden claims. It does not choose the story.\n" +
        "Do not replace the Brand Angle.\n" +
        "If EPISODE CONTEXT is present, write only that episode. The operational brief cannot override it.\n" +
        "Do not invent competitor prices, medical claims, salaries, or guarantees not in the brief or evidence.\n" +
        "If factOrOpinion=fact and source is missing, do not invent numbers — write qualitatively.\n" +
        "Never write in another brand's voice.\n" +
        "Follow each FORMAT CONTRACT. Do not turn a short format into web_long.\n" +
        "Do NOT write web_long or group_suggested here.\n" +
        "ONLY generate the variant kinds listed.\n" +
        "Ban filler: «trong thời đại», «không thể phủ nhận», «hãy cùng tìm hiểu», «điều này cho thấy».";

    public static string WebSystem(string? brandCode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a senior Vietnamese brand editor for KIT Marketing Park.");
        sb.AppendLine("Return valid JSON only: {\"title\":\"...\",\"bodyMarkdown\":\"...\"}. No markdown fences.");
        sb.AppendLine("Write ONE flagship article (web_long). Not a multi-channel pack and not a short post.");
        sb.AppendLine(AuthoritativeMarker + " decides the story. Do not let the operational brief or Brand Brain replace Territory, Brand Angle, or narrative boundaries.");
        sb.AppendLine(SupplementalMarker + " is " + NotNarrativeAuthority + ".");
        sb.AppendLine("ONE thesis only — the Brand Angle. Do not replace it. Every ## heading must prove that thesis.");
        sb.AppendLine("If EPISODE CONTEXT is present, write only that episode. Leave its open loop open. Do not solve the next episode. The operational brief cannot override the episode.");
        sb.AppendLine(WebLongContract);
        sb.AppendLine("No invented numbers or medical claims. Do not write diagnosis, treatment, generic technology, or generic software sales.");
        if (string.Equals(brandCode, "tnlife", StringComparison.OrdinalIgnoreCase))
        {
            sb.AppendLine("Brand tnlife: every public URL must be https://thainguyenlife.vn (paths /viec /tro /tin OK). Never thainguyen.life. No Novixa/Famixa/PHC CTA. No trailing hashtag dump.");
        }

        return sb.ToString().Trim();
    }

    public static string Compose(
        string brandName,
        string brandCode,
        ContentBrandKnowledgeDto knowledge,
        string? operationalBrief,
        BrandNarrativeDna? dna,
        ContentNarrativeSelection? narrative,
        string? packageAngle,
        string? packageAudience,
        ContentCreativeBriefDto? brief,
        string? ctaUrl,
        IReadOnlyList<string> formats,
        string contentFacts,
        string outputFormat,
        string? seriesContext = null,
        string? episodeContext = null)
    {
        EnsureBrief(brief);
        var angle = First(narrative?.BrandAngle, packageAngle);
        var sb = new StringBuilder();

        sb.AppendLine("[BRAND IDENTITY]");
        sb.AppendLine("Brand: " + brandName.Trim() + " (" + brandCode.Trim() + ")");
        sb.AppendLine("This identity is voice and claim limits. It is not the narrative authority.");
        Line(sb, "Positioning", knowledge.Positioning);
        Line(sb, "Audience", knowledge.Audience);
        ListLine(sb, "Tone", knowledge.Tone);
        ListLine(sb, "Claims FORBIDDEN", knowledge.ClaimsForbidden);
        ListLine(sb, "Proof points", knowledge.ProofPoints);
        ListLine(sb, "Prefer terms", knowledge.PreferredTerms);
        ListLine(sb, "Avoid terms", knowledge.AvoidTerms);
        Line(sb, "CTA style", knowledge.CtaStyle);
        Line(sb, "Voice notes", knowledge.VoiceNotes);
        sb.AppendLine();

        sb.AppendLine("[NARRATIVE CONTEXT — AUTHORITATIVE]");
        sb.AppendLine("=== " + AuthoritativeMarker + " ===");
        if (dna is null)
        {
            sb.AppendLine("No Narrative DNA on this brand. Do not invent a territory. Do not treat the operational brief as the story.");
        }
        else
        {
            sb.AppendLine("Brand: " + brandName.Trim() + " (" + dna.BrandCode + ")");
            sb.AppendLine("CoreStatement: " + dna.CoreStatement);
            sb.AppendLine("MustNotBecome: " + dna.MustNotBecome);
            if (dna.HardBoundaries.Count > 0)
                sb.AppendLine("HardBoundaries: " + string.Join(" | ", dna.HardBoundaries));
            var off = dna.Territories.Where(t => t.Type == "off-brand").Select(t => t.Name).ToList();
            if (off.Count > 0)
                sb.AppendLine("Do not cross into off-brand territories: " + string.Join("; ", off) + ".");
            sb.AppendLine("If " + SupplementalMarker + " conflicts with this context, follow this context.");
            sb.AppendLine("Do not override Territory, Brand Angle, or narrative boundaries.");
        }

        sb.AppendLine("=== END " + AuthoritativeMarker + " ===");
        sb.AppendLine();

        sb.AppendLine("[TERRITORY]");
        AppendTerritory(sb, dna, narrative);
        sb.AppendLine();

        sb.AppendLine("[BRAND ANGLE]");
        sb.AppendLine(string.IsNullOrWhiteSpace(angle) ? "(none)" : angle.Trim());
        sb.AppendLine("Do not replace this Brand Angle. Every variant keeps this thesis.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(seriesContext))
        {
            sb.AppendLine("[SERIES CONTEXT]");
            sb.AppendLine(seriesContext.Trim());
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(episodeContext))
        {
            sb.AppendLine("[EPISODE CONTEXT]");
            sb.AppendLine(episodeContext.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("[CREATIVE BRIEF]");
        sb.AppendLine(ContentCreativeBriefDto.FormatForPrompt(brief));
        Line(sb, "Audience", packageAudience);
        Line(sb, "Core point", angle);
        sb.AppendLine("CTA: " + (string.IsNullOrWhiteSpace(ctaUrl) ? "none configured" : ctaUrl.Trim()));
        ListLine(sb, "Tone", knowledge.Tone);
        sb.AppendLine();

        sb.AppendLine("[FORMAT CONTRACT]");
        if (formats.Count == 0)
            sb.AppendLine("(none)");
        foreach (var kind in formats)
            sb.AppendLine(FormatLine(kind));
        sb.AppendLine();

        sb.AppendLine("[CONTENT REQUIREMENTS]");
        if (!string.IsNullOrWhiteSpace(contentFacts))
            sb.AppendLine(contentFacts.Trim());
        sb.AppendLine("Stay inside the selected territory. Do not turn the piece into an off-brand story.");
        sb.AppendLine();
        sb.AppendLine("=== " + SupplementalMarker + " ===");
        sb.AppendLine(NotNarrativeAuthority);
        sb.AppendLine("This block cannot override Territory, Brand Angle, the Series, or the Episode.");
        var supplemental = Clip(operationalBrief, 480);
        sb.AppendLine(string.IsNullOrWhiteSpace(supplemental) ? "(none)" : supplemental);
        sb.AppendLine("=== END " + SupplementalMarker + " ===");
        sb.AppendLine();

        sb.AppendLine("[OUTPUT FORMAT]");
        sb.Append(outputFormat.Trim());
        return sb.ToString().Trim();
    }

    public static string FormatSeriesContext(
        string? name,
        string? objective,
        string? narrativeDirection,
        string? coreMessage,
        string? currentBeat,
        string? ctaStrategy,
        string? seriesCta)
    {
        var sb = new StringBuilder();
        Line(sb, "Series", name);
        Line(sb, "Objective", objective);
        Line(sb, "Narrative direction", narrativeDirection);
        Line(sb, "Core message", coreMessage);
        Line(sb, "This episode in the arc", currentBeat);
        Line(sb, "CTA strategy", ctaStrategy);
        Line(sb, "Series CTA", seriesCta);
        sb.AppendLine("Write this episode's step only. Do not write the rest of the series.");
        return sb.ToString().Trim();
    }

    public static string FormatEpisodeContext(
        int sequence,
        string? title,
        string? objective,
        string? keyMessage,
        string? previousSummary,
        IReadOnlyList<string>? previousKeyPoints,
        IReadOnlyList<string>? mustContinueFrom,
        IReadOnlyList<string>? mustNotRepeat,
        IReadOnlyList<string>? openLoops,
        string? nextEpisodeDirection)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sequence: " + sequence);
        Line(sb, "Title", title);
        Line(sb, "Objective", objective);
        Line(sb, "Role", objective);
        Line(sb, "Key message", keyMessage);
        sb.AppendLine("The article must establish the key message and this role. Do not replace the key message.");
        if (string.IsNullOrWhiteSpace(previousSummary) && (previousKeyPoints is not { Count: > 0 }) && (mustContinueFrom is not { Count: > 0 }))
            sb.AppendLine("Previous: none");
        else
            Line(sb, "Previous summary", previousSummary);
        ListLine(sb, "Previous key points", previousKeyPoints);
        ListLine(sb, "mustContinueFrom", mustContinueFrom);
        ListLine(sb, "mustNotRepeat", mustNotRepeat);
        ListLine(sb, "openLoops", openLoops);
        Line(sb, "nextEpisodeDirection", nextEpisodeDirection);
        sb.AppendLine("Leave the open loop open. Do not answer nextEpisodeDirection. Do not close the series in this episode.");
        return sb.ToString().Trim();
    }

    public static string FormatLine(string? kind)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        return k switch
        {
            "web_long" => WebLongContract,
            "fb_page" => "fb_page: plain text, 120–220 words, ONE thesis (the Brand Angle), 3 concrete beats, 1 CTA or question, no **bold**. Not a long article.",
            "fb_short" => "fb_short: hook plus CTA, plain text, short. Not a long article.",
            "seo_meta" => "seo_meta: title plus description only. Not an article.",
            "social_caption" => "social_caption: short post plus one hashtags line, plain text. Not a long article.",
            "tiktok_script" => "tiktok_script: timed beats HOOK/PROBLEM/INSIGHT/SOLUTION/CTA, 45–60 seconds.",
            "linkedin" => "linkedin: native professional tone. Keep the Brand Angle. Not a long article.",
            "instagram" => "instagram: native caption. Keep the Brand Angle. Not a long article.",
            _ => k + ": native length for this channel. Do not change the Brand Angle.",
        };
    }

    private static void AppendTerritory(StringBuilder sb, BrandNarrativeDna? dna, ContentNarrativeSelection? narrative)
    {
        var id = narrative?.TerritoryId?.Trim();
        var storedType = narrative?.Territory?.Trim();
        var territory = dna?.Find(id);
        if (territory is null && string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(storedType))
        {
            sb.AppendLine("Selected territory: none. Do not invent one.");
            return;
        }

        sb.AppendLine("TerritoryId: " + (territory?.Id ?? id ?? "(unresolved)"));
        sb.AppendLine("Type: " + (territory?.Type ?? storedType ?? "(unresolved)"));
        if (territory is null)
        {
            sb.AppendLine("Description: not resolved from Narrative DNA. Do not invent one.");
            return;
        }

        sb.AppendLine("Name: " + territory.Name);
        sb.AppendLine("Description: " + territory.Description);
        sb.AppendLine("WhyItBelongs: " + territory.WhyItBelongs);
        sb.AppendLine("Audience: " + territory.Audience);
        if (territory.Boundaries.Count > 0)
            sb.AppendLine("Boundaries: " + string.Join(" | ", territory.Boundaries));
    }

    private static void Line(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.AppendLine(label + ": " + value.Trim());
    }

    private static void ListLine(StringBuilder sb, string label, IReadOnlyList<string>? values)
    {
        if (values is not { Count: > 0 }) return;
        var text = string.Join("; ", values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()));
        if (text.Length > 0)
            sb.AppendLine(label + ": " + text);
    }

    private static string? First(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? "").Trim();
        if (text.Length <= max) return text;
        return text[..max].TrimEnd() + "…";
    }
}
