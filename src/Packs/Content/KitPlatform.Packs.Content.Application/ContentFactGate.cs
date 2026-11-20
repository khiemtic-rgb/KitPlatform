using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Series articles may be approved only from a human fact card.
/// Doses and cure claims are refused even when the card mentions them.
/// </summary>
public sealed record ContentFactGateResult(
    bool Applies,
    bool ReadyToWrite,
    bool Passed,
    IReadOnlyList<string> Issues)
{
    public static ContentFactGateResult NotASeries { get; } = new(false, true, true, []);
}

public static class ContentFactGate
{
    public const int MinCardChars = 40;

    private static readonly Regex LongNumber = new(
        @"\d{1,3}(?:[.\s]\d{3})+|\d{3,}",
        RegexOptions.Compiled);

    private static readonly Regex Dose = new(
        @"\d+(?:[.,]\d+)?\s*(?:mg|ml|mcg|µg|ug|iu|viên|gói|ống|gram)\b|\d+\s*g\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Cure = new(
        @"chữa\s*(khỏi|bệnh|được)|trị\s*(khỏi|bệnh|dứt)|hết\s*bệnh|đặc\s*trị|khỏi\s*hẳn",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Url = new(
        @"https?://\S+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex Price = new(
        @"\d[\d.\s]*\s*(đồng|vnđ|vnd|nghìn|ngàn|triệu|tỷ)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string ReadCard(JsonElement continuity) =>
        continuity.ValueKind == JsonValueKind.Object ? ReadCard(continuity.GetRawText()) : "";

    public static string ReadCard(string? continuityJson)
    {
        if (string.IsNullOrWhiteSpace(continuityJson)) return "";
        try
        {
            using var doc = JsonDocument.Parse(continuityJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return "";
            if (!doc.RootElement.TryGetProperty("factCard", out var card)) return "";
            return card.ValueKind == JsonValueKind.String ? (card.GetString() ?? "").Trim() : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    public static bool HasCard(string? factCard) =>
        !string.IsNullOrWhiteSpace(factCard) && factCard.Trim().Length >= MinCardChars;

    public static string MergeCard(string? continuityJson, string? factCard)
    {
        var root = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(continuityJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(continuityJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        root[prop.Name] = prop.Value.Clone();
                }
            }
            catch (JsonException)
            {
                root.Clear();
            }
        }

        var text = (factCard ?? "").Trim();
        if (text.Length == 0)
            root.Remove("factCard");
        else
            root["factCard"] = JsonSerializer.SerializeToElement(text);

        return JsonSerializer.Serialize(root);
    }

    public static string PreserveCard(string newContinuityJson, string? previousJson)
    {
        var card = ReadCard(previousJson);
        return string.IsNullOrWhiteSpace(card) ? newContinuityJson : MergeCard(newContinuityJson, card);
    }

    public static string PromptBlock(string factCard, IReadOnlyList<string>? recentTheses)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PHIẾU SỰ THẬT — chỉ được kể các việc sau. Không thêm số, giá, liều, tên bệnh. Không viết câu chữa bệnh.");
        sb.AppendLine(factCard.Trim());
        var theses = (recentTheses ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).Take(3).ToList();
        if (theses.Count == 0) return sb.ToString().Trim();
        sb.AppendLine();
        sb.AppendLine("Không viết lại các luận điểm đã đăng:");
        foreach (var thesis in theses)
            sb.Append("- ").AppendLine(thesis.Trim());
        return sb.ToString().Trim();
    }

    public static string Refuse(IReadOnlyList<string> issues) =>
        "Cửa sự thật chặn: " + string.Join("; ", issues.Take(5));

    public static ContentFactGateResult Evaluate(
        string? factCard,
        string? title,
        IReadOnlyList<string> bodies,
        IReadOnlyList<string> recentTheses,
        IReadOnlyList<string> otherBrandNames)
    {
        var card = (factCard ?? "").Trim();
        if (!HasCard(card))
            return new ContentFactGateResult(true, false, false, ["Tập chưa có phiếu sự thật."]);

        var issues = new List<string>();
        var article = string.Join("\n", new[] { title ?? "" }.Concat(bodies ?? []));
        var cardDigits = Digits(card);
        var foldedCard = Fold(card);

        foreach (var dose in Distinct(Dose.Matches(article).Select(m => m.Value.Trim())))
            issues.Add("Cấm liều dùng: " + dose);

        foreach (Match match in Cure.Matches(article))
            issues.Add("Cấm câu chữa bệnh: " + match.Value.Trim());

        foreach (var price in Distinct(Price.Matches(article).Select(m => m.Value.Trim())))
        {
            var unit = Fold(price);
            if (!cardDigits.Contains(Digits(price), StringComparison.Ordinal) || !UnitInCard(foldedCard, unit))
                issues.Add("Giá không có trong phiếu: " + price);
        }

        foreach (Match match in LongNumber.Matches(article))
        {
            var raw = match.Value.Trim();
            var digits = Digits(raw);
            if (digits.Length < 3) continue;
            if (cardDigits.Contains(digits, StringComparison.Ordinal)) continue;
            if (CoveredByPrice(raw, article)) continue;
            issues.Add("Số không có trong phiếu: " + raw);
        }

        var prose = StripUrls(article);
        foreach (var name in otherBrandNames ?? [])
        {
            var label = (name ?? "").Trim();
            if (Fold(label).Length < 4) continue;
            if (ContainsPhrase(prose, label))
                issues.Add("Lẫn brand khác: " + label);
        }

        var opening = FirstSentence(bodies);
        foreach (var thesis in (recentTheses ?? []).Take(3))
        {
            if (SameThesis(title, thesis) || SameThesis(opening, thesis))
                issues.Add("Trùng luận điểm bài đã đăng: " + thesis.Trim());
        }

        return new ContentFactGateResult(true, true, issues.Count == 0, issues);
    }

    public static bool SameThesis(string? left, string? right)
    {
        var a = Fold(left);
        var b = Fold(right);
        if (a.Length < 24 || b.Length < 24) return false;
        if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal)) return true;
        var aw = Words(a);
        var bw = Words(b);
        if (aw.Count == 0 || bw.Count == 0) return false;
        var shared = aw.Intersect(bw, StringComparer.Ordinal).Count();
        var shorter = Math.Min(aw.Count, bw.Count);
        return shared >= 3 && shared * 100 / shorter >= 70;
    }

    private static bool CoveredByPrice(string raw, string article)
    {
        foreach (Match price in Price.Matches(article))
        {
            if (price.Value.Contains(raw, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static bool UnitInCard(string foldedCard, string foldedPrice)
    {
        if (foldedPrice.Contains("dong", StringComparison.Ordinal) || foldedPrice.Contains("vnd", StringComparison.Ordinal))
            return foldedCard.Contains("dong", StringComparison.Ordinal) || foldedCard.Contains("vnd", StringComparison.Ordinal);
        if (foldedPrice.Contains("nghin", StringComparison.Ordinal) || foldedPrice.Contains("ngan", StringComparison.Ordinal))
            return foldedCard.Contains("nghin", StringComparison.Ordinal) || foldedCard.Contains("ngan", StringComparison.Ordinal);
        if (foldedPrice.Contains("trieu", StringComparison.Ordinal))
            return foldedCard.Contains("trieu", StringComparison.Ordinal);
        if (foldedPrice.Contains("ty", StringComparison.Ordinal))
            return foldedCard.Contains("ty", StringComparison.Ordinal);
        return false;
    }

    private static string FirstSentence(IReadOnlyList<string>? bodies)
    {
        var text = (bodies ?? []).FirstOrDefault(b => !string.IsNullOrWhiteSpace(b)) ?? "";
        var line = text.Replace("\r", "").Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? "";
        var cut = line.IndexOfAny(['.', '?', '!']);
        return (cut > 0 ? line[..cut] : line).Trim();
    }

    private static string StripUrls(string article) => Url.Replace(article ?? "", " ");

    private static bool ContainsPhrase(string article, string phrase)
    {
        var text = Fold(article);
        var needle = Fold(phrase);
        var at = text.IndexOf(needle, StringComparison.Ordinal);
        if (at < 0) return false;
        var before = at == 0 || !char.IsLetterOrDigit(text[at - 1]);
        var after = at + needle.Length >= text.Length || !char.IsLetterOrDigit(text[at + needle.Length]);
        return before && after;
    }

    private static List<string> Distinct(IEnumerable<string> values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string Digits(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    private static List<string> Words(string folded)
    {
        var list = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in folded)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
                continue;
            }
            if (current.Length >= 4) list.Add(current.ToString());
            current.Clear();
        }
        if (current.Length >= 4) list.Add(current.ToString());
        return list;
    }

    private static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch == 'đ' ? 'd' : ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
