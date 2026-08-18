using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Content;

/// <summary>Pick CTA by brand + topic theme. Novixa health-check posts go to the PHC landing, not homepage.</summary>
public static class ContentCtaRouter
{
    public const string NovixaHealthCheck = "https://novixa.vn/vi/health-check/";
    public const string NovixaSpaHealthCheck = "https://novixa.vn/vi/spa-health-check/";

    public static string? Resolve(
        string? brandCode,
        string? title,
        string? pillar,
        string? goal,
        string? outline,
        string? topicCta,
        string? brandDefault = null)
    {
        if (IsNovixa(brandCode))
        {
            var blob = Fold($"{title} {pillar} {goal} {outline}");
            if (LooksLikeSpaHealthCheck(blob))
                return KeepIfSameLanding(topicCta, NovixaSpaHealthCheck) ?? NovixaSpaHealthCheck;
            if (LooksLikeHealthCheck(blob))
                return KeepIfSameLanding(topicCta, NovixaHealthCheck) ?? NovixaHealthCheck;
        }

        return FirstNonEmpty(topicCta, brandDefault);
    }

    public static string RewriteBody(string? brandCode, string body, string? resolvedCta)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(resolvedCta))
            return body ?? "";
        if (!IsNovixa(brandCode)) return body;
        if (!resolvedCta.StartsWith("https://novixa.vn/vi/", StringComparison.OrdinalIgnoreCase))
            return body;

        var next = body;
        foreach (var wrong in new[]
                 {
                     "https://www.novixa.vn/",
                     "https://www.novixa.vn",
                     "https://novixa.vn/",
                     "https://novixa.vn",
                 })
        {
            next = ReplaceStandalone(next, wrong, resolvedCta);
        }

        return next;
    }

    public static bool LooksLikeHealthCheck(string? title, string? pillar, string? goal, string? outline) =>
        LooksLikeHealthCheck(Fold($"{title} {pillar} {goal} {outline}"));

    private static bool IsNovixa(string? brandCode) =>
        string.Equals(brandCode?.Trim(), "novixa", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSpaHealthCheck(string folded) =>
        folded.Contains("spa-health-check", StringComparison.Ordinal)
        || folded.Contains("spa health check", StringComparison.Ordinal)
        || folded.Contains("suc khoe spa", StringComparison.Ordinal)
        || folded.Contains("spa health-check", StringComparison.Ordinal);

    private static bool LooksLikeHealthCheck(string folded)
    {
        if (LooksLikeSpaHealthCheck(folded)) return false;
        return folded.Contains("kiem tra suc khoe", StringComparison.Ordinal)
               || folded.Contains("health-check", StringComparison.Ordinal)
               || folded.Contains("health check", StringComparison.Ordinal)
               || folded.Contains("healthcheck", StringComparison.Ordinal)
               || folded.Contains("suc khoe nha thuoc", StringComparison.Ordinal)
               || folded.Contains("danh gia suc khoe", StringComparison.Ordinal)
               || folded.Contains("pharmacy health check", StringComparison.Ordinal);
    }

    private static string? KeepIfSameLanding(string? topicCta, string landing)
    {
        var t = topicCta?.Trim();
        if (string.IsNullOrWhiteSpace(t)) return null;
        var path = t.Split('?', 2)[0].TrimEnd('/');
        var want = landing.TrimEnd('/');
        return path.Equals(want, StringComparison.OrdinalIgnoreCase) ? t : null;
    }

    private static string ReplaceStandalone(string text, string from, string to)
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) return text;
        var idx = 0;
        while (idx < text.Length)
        {
            var at = text.IndexOf(from, idx, StringComparison.OrdinalIgnoreCase);
            if (at < 0) break;
            var after = at + from.Length;
            if (after < text.Length)
            {
                var next = text[after];
                if (next is '/' or '?' or '#' || char.IsLetterOrDigit(next))
                {
                    idx = after;
                    continue;
                }
            }

            text = text[..at] + to + text[after..];
            idx = at + to.Length;
        }

        return text;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }

        return null;
    }

    private static string Fold(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var n = raw.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(n.Length);
        foreach (var ch in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
