using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Brand Narrative DNA — authoritative source for narrative fit.
/// Operational briefs must not override this when choosing a territory.
/// </summary>
public sealed record BrandNarrativeDna(
    int Version,
    string BrandCode,
    string CoreStatement,
    IReadOnlyList<string> PrimaryAudience,
    IReadOnlyList<string> SecondaryAudience,
    IReadOnlyList<NarrativeTerritory> Territories,
    IReadOnlyList<string> HardBoundaries,
    string MustNotBecome,
    NarrativeGuard? Guard = null)
{
    [JsonIgnore]
    public bool HasTerritories => Territories is { Count: > 0 };

    public NarrativeTerritory? Find(string? territoryId) =>
        string.IsNullOrWhiteSpace(territoryId)
            ? null
            : Territories.FirstOrDefault(t => string.Equals(t.Id, territoryId.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record NarrativeTerritory(
    string Id,
    string Name,
    string Type,
    string Description,
    string WhyItBelongs,
    string Audience,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> Boundaries,
    int Priority);

public sealed record NarrativeGuard(
    IReadOnlyList<string> OffBrandIf,
    IReadOnlyList<string> UnlessAlso);

public sealed record NarrativeModelSuggestion(
    string? Territory,
    string? TerritoryId,
    bool? Fit,
    int Score,
    string? BrandAngle,
    string? Reason,
    IReadOnlyList<string>? BoundaryWarnings);

public sealed record BrandNarrativeDecision(
    bool Fit,
    string Territory,
    string? TerritoryId,
    string Reason,
    string? BrandAngle,
    double Confidence,
    IReadOnlyList<string> BoundaryWarnings,
    bool Locked)
{
    public bool AllowsAngle =>
        Fit && Territory is "core" or "supporting" or "adjacent";

    public bool AllowsSeries => AllowsAngle;

    public bool AllowsAutoAngle => Fit && Territory is "core" or "supporting";

    public string LegacyVerdict => Territory switch
    {
        "core" or "supporting" => "fit",
        "adjacent" => "maybe",
        _ => "skip",
    };
}

public static class BrandNarrativeFit
{
    public const string AuthoritativeMarker = "AUTHORITATIVE NARRATIVE SOURCE";
    public const string NarrativeFitRequired = "NarrativeFitRequired";
    public const string OffBrandStop = "OFF-BRAND";

    /// <summary>
    /// Series may start only from a package whose own brand row has Territory.
    /// Score and verdict are ignored. Missing Territory is not CORE.
    /// </summary>
    public static string? SeriesEntryBlock(ContentBrandFitDto? own) =>
        SeriesEntryBlock(own?.Territory, own?.NarrativeFit);

    public static string? SeriesEntryBlock(string? territory, bool? narrativeFit)
    {
        if (string.IsNullOrWhiteSpace(territory))
            return NarrativeFitRequired;
        if (BlocksAngle(territory, narrativeFit))
            return OffBrandStop;
        var type = territory.Trim().ToLowerInvariant();
        if (type is not ("core" or "supporting" or "adjacent"))
            return NarrativeFitRequired;
        return null;
    }

    public static bool ShouldMaterializeAngle(
        BrandNarrativeDecision? decision,
        bool createPackages,
        bool includeMaybe,
        string legacyVerdict)
    {
        if (!createPackages) return false;
        if (decision is null)
            return legacyVerdict == "fit" || (legacyVerdict == "maybe" && includeMaybe);
        return decision.AllowsAngle
            && (decision.AllowsAutoAngle || (decision.Territory == "adjacent" && includeMaybe));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "nhung", "những", "cua", "của", "va", "và", "cho", "mot", "một", "cac", "các",
        "la", "là", "duoc", "được", "co", "có", "trong", "khi", "nay", "này", "de", "để",
        "voi", "với", "hon", "hơn", "rat", "rất", "cach", "cách", "vao", "vào", "tren", "trên",
        "tu", "từ", "nhu", "như", "thi", "thì", "da", "đã", "se", "sẽ", "khong", "không",
        "phai", "phải", "gi", "gì", "o", "ở", "di", "đi", "moi", "mỗi", "nen", "nên",
        "tao", "tạo", "len", "lên", "den", "đến", "can", "cần", "dung", "đúng",
    };

    public static BrandNarrativeDna? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() is "{}" or "null")
            return null;
        try
        {
            var dna = JsonSerializer.Deserialize<BrandNarrativeDna>(json, JsonOpts);
            if (dna is null || !dna.HasTerritories) return null;
            return dna;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(BrandNarrativeDna dna) =>
        JsonSerializer.Serialize(dna, JsonOpts);

    public static string FormatAuthoritative(BrandNarrativeDna dna)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== " + AuthoritativeMarker + " ===");
        sb.AppendLine("BrandCode: " + dna.BrandCode);
        sb.AppendLine("CoreStatement: " + dna.CoreStatement);
        sb.AppendLine("PrimaryAudience: " + string.Join("; ", dna.PrimaryAudience));
        if (dna.SecondaryAudience.Count > 0)
            sb.AppendLine("SecondaryAudience: " + string.Join("; ", dna.SecondaryAudience));
        sb.AppendLine("MustNotBecome: " + dna.MustNotBecome);
        if (dna.HardBoundaries.Count > 0)
            sb.AppendLine("HardBoundaries: " + string.Join(" | ", dna.HardBoundaries));
        foreach (var type in new[] { "core", "supporting", "adjacent", "off-brand" })
        {
            foreach (var t in dna.Territories.Where(x => x.Type == type).OrderBy(x => x.Priority))
            {
                sb.AppendLine("---");
                sb.AppendLine("TerritoryId: " + t.Id);
                sb.AppendLine("Type: " + t.Type);
                sb.AppendLine("Name: " + t.Name);
                sb.AppendLine("Description: " + t.Description);
                sb.AppendLine("WhyItBelongs: " + t.WhyItBelongs);
                sb.AppendLine("Audience: " + t.Audience);
                if (t.Examples.Count > 0)
                    sb.AppendLine("Examples: " + string.Join(" || ", t.Examples));
                if (t.Boundaries.Count > 0)
                    sb.AppendLine("Boundaries: " + string.Join(" || ", t.Boundaries));
            }
        }

        sb.AppendLine("Question: Does this Core Idea belong to a story this brand has a reason to tell its audience? Keyword overlap is not enough.");
        sb.AppendLine("off-brand => fit false. A high score must not override off-brand.");
        sb.AppendLine("=== END " + AuthoritativeMarker + " ===");
        return sb.ToString().Trim();
    }

    public static BrandNarrativeDecision Classify(BrandNarrativeDna dna, string idea) =>
        Reconcile(dna, idea, model: null);

    public static BrandNarrativeDecision Reconcile(
        BrandNarrativeDna dna,
        string idea,
        NarrativeModelSuggestion? model)
    {
        var text = Normalize(idea);
        var guard = GuardHit(dna, text);
        if (guard)
        {
            var off = BestExample(dna, text, "off-brand") ?? FirstOfType(dna, "off-brand");
            return Block(off, "Đụng hard boundary của Narrative DNA. Numeric score không được cứu OFF-BRAND.", 0.96, model);
        }

        var example = BestExample(dna, text, type: null);
        if (example is not null)
        {
            if (example.Type == "off-brand")
                return Block(example, "Khớp ví dụ OFF-BRAND trong Narrative DNA.", 0.94, model);
            return Allow(example, "Khớp territory «" + example.Name + "» trong Narrative DNA.", 0.9, model?.BrandAngle);
        }

        if (model is not null && IsOffBrand(model.Territory))
        {
            var named = dna.Find(model.TerritoryId) ?? FirstOfType(dna, "off-brand");
            return Block(named, model.Reason ?? "Model gắn OFF-BRAND.", 0.85, model);
        }

        var scored = BestScored(dna, text);
        if (scored.Territory is { Type: "off-brand" } offScored && scored.Score > 0)
            return Block(offScored, "Gần territory OFF-BRAND hơn các câu chuyện brand được kể.", 0.8, model);

        if (scored.Territory is { } positive && positive.Type != "off-brand" && scored.Score >= 2)
        {
            if (model is not null)
            {
                var fromModel = dna.Find(model.TerritoryId);
                if (fromModel is not null && fromModel.Type != "off-brand" && model.Fit != false)
                    return Allow(fromModel, model.Reason ?? fromModel.WhyItBelongs, model.Score <= 0 ? 0.7 : Math.Clamp(model.Score / 100d, 0, 1), model.BrandAngle);
            }

            return Allow(positive, "Thuộc «" + positive.Name + "»: " + positive.WhyItBelongs, 0.72, model?.BrandAngle);
        }

        if (model is not null)
        {
            var fromModel = dna.Find(model.TerritoryId);
            if (fromModel is not null && fromModel.Type != "off-brand" && model.Fit != false && scored.Score >= 2)
                return Allow(fromModel, model.Reason ?? fromModel.WhyItBelongs, 0.7, model.BrandAngle);
        }

        var fallback = FirstOfType(dna, "off-brand");
        return Block(
            fallback,
            "Core Idea không thuộc câu chuyện brand này có lý do để kể. Không tạo Brand Angle.",
            0.84,
            model);
    }

    public static bool BlocksAngle(string? territory, bool? narrativeFit)
    {
        if (string.Equals(territory, "off-brand", StringComparison.OrdinalIgnoreCase))
            return true;
        if (narrativeFit == false)
            return true;
        return false;
    }

    private static BrandNarrativeDecision Block(
        NarrativeTerritory? territory,
        string reason,
        double confidence,
        NarrativeModelSuggestion? model)
    {
        var warnings = new List<string>();
        if (model?.BoundaryWarnings is { Count: > 0 })
            warnings.AddRange(model.BoundaryWarnings);
        if (model is { Score: >= 45 })
            warnings.Add("numeric score " + model.Score + " ignored because territory is off-brand");
        if (model is not null && !IsOffBrand(model.Territory) && model.Fit != false)
            warnings.Add("model suggested " + (model.Territory ?? "fit") + "; server refused to rescue OFF-BRAND");
        return new BrandNarrativeDecision(
            false,
            "off-brand",
            territory?.Id,
            reason,
            null,
            confidence,
            warnings,
            Locked: true);
    }

    private static BrandNarrativeDecision Allow(
        NarrativeTerritory territory,
        string reason,
        double confidence,
        string? brandAngle)
    {
        var fit = territory.Type is "core" or "supporting" or "adjacent";
        return new BrandNarrativeDecision(
            fit,
            fit ? territory.Type : "off-brand",
            territory.Id,
            reason,
            string.IsNullOrWhiteSpace(brandAngle) ? null : brandAngle.Trim(),
            confidence,
            [],
            Locked: true);
    }

    private static bool GuardHit(BrandNarrativeDna dna, string text)
    {
        if (dna.Guard is null || text.Length == 0) return false;
        var triggered = dna.Guard.OffBrandIf.Any(s => text.Contains(Normalize(s), StringComparison.Ordinal));
        if (!triggered) return false;
        var excused = dna.Guard.UnlessAlso.Any(s => text.Contains(Normalize(s), StringComparison.Ordinal));
        return !excused;
    }

    private static NarrativeTerritory? BestExample(BrandNarrativeDna dna, string text, string? type)
    {
        NarrativeTerritory? best = null;
        var bestLen = 0;
        foreach (var territory in dna.Territories)
        {
            if (type is not null && territory.Type != type) continue;
            foreach (var example in territory.Examples)
            {
                var needle = Normalize(example);
                if (needle.Length < 12) continue;
                if (!text.Contains(needle, StringComparison.Ordinal)) continue;
                if (needle.Length > bestLen)
                {
                    best = territory;
                    bestLen = needle.Length;
                }
            }
        }

        return best;
    }

    private static (NarrativeTerritory? Territory, int Score) BestScored(BrandNarrativeDna dna, string text)
    {
        var tokens = Tokens(text);
        NarrativeTerritory? best = null;
        var bestScore = 0;
        foreach (var territory in dna.Territories.OrderBy(t => t.Priority))
        {
            var vocab = Tokens(string.Join(
                " ",
                new[] { territory.Name, territory.Description, territory.WhyItBelongs, territory.Audience }
                    .Concat(territory.Examples)
                    .Concat(territory.Boundaries)));
            var score = tokens.Count(tok => vocab.Contains(tok));
            if (score > bestScore)
            {
                best = territory;
                bestScore = score;
            }
        }

        return (best, bestScore);
    }

    private static NarrativeTerritory? FirstOfType(BrandNarrativeDna dna, string type) =>
        dna.Territories.Where(t => t.Type == type).OrderBy(t => t.Priority).FirstOrDefault();

    private static bool IsOffBrand(string? territory) =>
        string.Equals(territory, "off-brand", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> Tokens(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in Regex.Split(text, @"[^\p{L}\p{Nd}]+"))
        {
            var tok = raw.Trim();
            if (tok.Length < 3) continue;
            if (Stop.Contains(tok)) continue;
            set.Add(tok);
        }

        return set;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var lowered = value.Trim().ToLower(CultureInfo.InvariantCulture);
        return Regex.Replace(lowered, @"\s+", " ");
    }
}
