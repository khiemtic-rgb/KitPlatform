using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Classify a stored Facebook URL as personal profile vs fanpage.
/// Does not call Facebook. Staff can override the kind when adding a lead.
/// </summary>
internal static class NovixaFacebookSource
{
    public const string Profile = "profile";
    public const string Page = "page";

    public static bool IsFacebookUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var text = value.Trim();
        return text.Contains("facebook.com", StringComparison.OrdinalIgnoreCase)
            || text.Contains("fb.com/", StringComparison.OrdinalIgnoreCase)
            || text.Contains("m.me/", StringComparison.OrdinalIgnoreCase);
    }

    public static (string? Kind, string? Url, string? Username) Resolve(string? storedKind, string? source)
    {
        var url = NormalizeUrl(source);
        if (string.IsNullOrWhiteSpace(url) && !IsFacebookUrl(source))
            return (NormalizeKind(storedKind), null, null);

        var kind = NormalizeKind(storedKind) ?? Classify(url ?? source);
        return (kind, url, Username(url ?? source));
    }

    public static string? Classify(string? source)
    {
        if (!IsFacebookUrl(source))
            return null;
        var path = PathOf(source);
        if (path.Contains("profile.php", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/people/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/profile.php", StringComparison.OrdinalIgnoreCase))
            return Profile;
        if (path.Contains("/pages/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/pg/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("m.me/", StringComparison.OrdinalIgnoreCase))
            return Page;

        var vanity = Username(source) ?? "";
        if (Regex.IsMatch(vanity, "(nha.?thuoc|quay.?thuoc|pharmacy|nhathuoc)", RegexOptions.IgnoreCase))
            return Page;
        return Profile;
    }

    public static string? NormalizeUrl(string? source)
    {
        if (!IsFacebookUrl(source))
            return null;
        var text = source!.Trim();
        if (!text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            text = "https://" + text.TrimStart('/');
        var idMatch = Regex.Match(text, @"[?&]id=(\d+)");
        var path = text.Split('?', 2)[0].TrimEnd('/');
        return idMatch.Success ? $"{path}?id={idMatch.Groups[1].Value}" : path;
    }

    public static string NormalizeProvince(string? value)
    {
        var text = value?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var fold = text.ToLowerInvariant()
            .Replace("tỉnh ", "", StringComparison.Ordinal)
            .Replace("thanh pho ", "", StringComparison.Ordinal)
            .Replace("tp.", "", StringComparison.Ordinal);
        if (fold.Contains("thai nguyen", StringComparison.Ordinal)
            || fold.Contains("thái nguyên", StringComparison.Ordinal))
            return "Thái Nguyên";
        return text;
    }

    public static IReadOnlyList<(string Name, string Url)> ParseImportLines(string? text)
    {
        var rows = new List<(string Name, string Url)>();
        if (string.IsNullOrWhiteSpace(text))
            return rows;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var match = Regex.Match(
                line,
                @"(https?://\S+|www\.\S+|(?:www\.)?(?:facebook|fb)\.com/\S+|m\.me/\S+)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                continue;
            var url = NormalizeUrl(match.Value);
            if (string.IsNullOrWhiteSpace(url) || !seen.Add(url))
                continue;
            var name = line.Replace(match.Value, "", StringComparison.OrdinalIgnoreCase)
                .Trim()
                .Trim('|', ',', ';', '-', '\t', ' ');
            rows.Add((string.IsNullOrWhiteSpace(name) ? DisplayName(url) : name, url));
        }

        return rows;
    }

    public static string DisplayName(string? source)
    {
        var user = Username(source) ?? "Facebook";
        if (Regex.IsMatch(user, @"^\d+$"))
            return "Facebook " + (user.Length <= 6 ? user : user[^6..]);
        return user.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Trim();
    }

    public static bool SameFacebook(string? left, string? right)
    {
        var a = Resolve(null, left);
        var b = Resolve(null, right);
        if (a.Url is not null && b.Url is not null
            && string.Equals(a.Url, b.Url, StringComparison.OrdinalIgnoreCase))
            return true;
        var userA = a.Username ?? Username(left);
        var userB = b.Username ?? Username(right);
        return userA is not null
            && userB is not null
            && string.Equals(userA, userB, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ProvinceMatches(string? stored, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;
        return string.Equals(
            NormalizeProvince(stored),
            NormalizeProvince(filter),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeKind(string? kind)
    {
        var code = kind?.Trim().ToLowerInvariant();
        return code is Profile or Page ? code : null;
    }

    private static string PathOf(string? source) => (source ?? "").ToLowerInvariant();

    private static string? Username(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;
        var match = Regex.Match(source, @"[?&]id=(\d+)");
        if (match.Success)
            return match.Groups[1].Value;
        var parts = source.Trim().TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : parts[^1];
    }
}
