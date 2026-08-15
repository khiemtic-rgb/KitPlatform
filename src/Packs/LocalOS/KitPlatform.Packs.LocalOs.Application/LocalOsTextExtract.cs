using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.LocalOs;

public static class LocalOsTextExtract
{
    private static readonly string[] BlockWords =
    [
        "livestream", "tiktok", "nạp tiền", "nap tien", "đa cấp", "da cap",
        "crypto", "giữ chỗ", "giu cho", "phí ứng tuyển", "phi ung tuyen",
        "triệu/ngày", "trieu/ngay", "triệu / ngày",
    ];

    public static bool LooksUnsafe(string text) =>
        BlockWords.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

    public static string GuessKind(string text, string? hint)
    {
        if (hint is "job" or "event" or "room")
            return hint;
        var t = text.ToLowerInvariant();
        if (t.Contains("phòng") || t.Contains("phong tro") || t.Contains("trọ") || t.Contains("cho thuê") || t.Contains("ở ghép"))
            return "room";
        if (t.Contains("workshop") || t.Contains("sự kiện") || t.Contains("su kien") || t.Contains("đêm nhạc") || t.Contains("ngày hội"))
            return "event";
        return "job";
    }

    public static string GuessTitle(string text)
    {
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            var t = Regex.Replace(line, @"\s+", " ").Trim();
            if (t.Length < 8)
                continue;
            return t.Length <= 140 ? t : t[..140].TrimEnd() + "…";
        }
        var compact = Regex.Replace(text, @"\s+", " ").Trim();
        if (compact.Length == 0)
            return "Tin từ nguồn (chờ sửa)";
        return compact.Length <= 140 ? compact : compact[..140].TrimEnd() + "…";
    }

    public static string Fold(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var nfd = raw.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(nfd.Length);
        foreach (var c in nfd)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            var ch = c is 'đ' or 'Ð' ? 'd' : c;
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }

    public static IReadOnlyList<string> PhonesIn(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        var found = new List<string>();
        foreach (Match m in Regex.Matches(text, @"(?:\+?84|0)(?:\s|\.|-)?[35789](?:\s|\.|-)?\d(?:\s|\.|-){0,2}\d{3}(?:\s|\.|-){0,2}\d{3,4}"))
        {
            var digits = Regex.Replace(m.Value, @"\D", "");
            if (digits.StartsWith("84") && digits.Length >= 11)
                digits = "0" + digits[2..];
            if (digits.Length is >= 9 and <= 12 && !found.Contains(digits))
                found.Add(digits);
        }
        return found;
    }

    public static bool SameListing(
        string kindA, string titleA, string? placeA, string? phoneA, string? summaryA, string? urlA,
        string kindB, string titleB, string? placeB, string? phoneB, string? summaryB, string? urlB)
    {
        if (!string.Equals(kindA, kindB, StringComparison.OrdinalIgnoreCase))
            return false;
        var url1 = (urlA ?? "").Trim();
        var url2 = (urlB ?? "").Trim();
        if (url1.Length > 8 && url2.Length > 8 && string.Equals(url1, url2, StringComparison.OrdinalIgnoreCase))
            return true;
        var t1 = Fold(titleA);
        var t2 = Fold(titleB);
        if (t1.Length < 10 || t2.Length < 10)
            return false;
        var titleHit = t1 == t2 || (t1.Length >= 16 && t2.Length >= 16 && (t1.Contains(t2) || t2.Contains(t1)));
        if (!titleHit)
        {
            var s1 = Fold(summaryA);
            var s2 = Fold(summaryB);
            if (s1.Length >= 40 && s2.Length >= 40 && s1[..40] == s2[..40])
                titleHit = true;
        }
        if (!titleHit)
            return false;
        var phonesA = PhonesIn($"{phoneA} {summaryA}");
        var phonesB = PhonesIn($"{phoneB} {summaryB}");
        if (phonesA.Any(p => phonesB.Contains(p)))
            return true;
        var p1 = Fold(placeA);
        var p2 = Fold(placeB);
        return p1.Length >= 6 && p2.Length >= 6 && (p1 == p2 || p1.Contains(p2) || p2.Contains(p1));
    }

    public static string? FirstHttpUrl(string text)
    {
        var m = Regex.Match(text, @"https?://[^\s<>""']+", RegexOptions.IgnoreCase);
        return m.Success ? m.Value.TrimEnd('.', ',', ')', ']', '"', '\'') : null;
    }

    public static string? GuessPhone(string text)
    {
        var m = Regex.Match(text, @"(?:\+?84|0)(?:\s|\.|-)?(?:3|5|7|8|9)(?:\s|\.|-)?\d(?:\s|\.|-){0,2}\d{3}(?:\s|\.|-){0,2}\d{3,4}");
        if (!m.Success)
            return null;
        var digits = Regex.Replace(m.Value, @"\D", "");
        if (digits.StartsWith("84") && digits.Length >= 11)
            digits = "0" + digits[2..];
        return digits.Length is >= 9 and <= 12 ? digits : null;
    }

    public static string? GuessSalary(string text)
    {
        var m = Regex.Match(text, @"(\d+[.,]?\d*)\s*(k/giờ|k/h|nghìn/giờ|triệu/tháng|tr/tháng|triệu|k)", RegexOptions.IgnoreCase);
        return m.Success ? m.Value.Trim() : null;
    }

    public static string GuessPlace(string text)
    {
        if (text.Contains("Sông Công", StringComparison.OrdinalIgnoreCase))
            return "Sông Công";
        if (text.Contains("Phổ Yên", StringComparison.OrdinalIgnoreCase) || text.Contains("Pho Yen", StringComparison.OrdinalIgnoreCase))
            return "Phổ Yên";
        if (text.Contains("Đại Từ", StringComparison.OrdinalIgnoreCase))
            return "Đại Từ";
        if (text.Contains("Thái Nguyên", StringComparison.OrdinalIgnoreCase) || text.Contains("Thai Nguyen", StringComparison.OrdinalIgnoreCase))
            return "TP. Thái Nguyên";
        return "Thái Nguyên";
    }

    public static string StripHtml(string html)
    {
        var title = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var og = Regex.Match(html, @"property\s*=\s*[""']og:title[""']\s+content\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);
        var desc = Regex.Match(html, @"property\s*=\s*[""']og:description[""']\s+content\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);
        if (!desc.Success)
            desc = Regex.Match(html, @"name\s*=\s*[""']description[""']\s+content\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);
        var parts = new List<string>();
        if (og.Success)
            parts.Add(Decode(og.Groups[1].Value));
        else if (title.Success)
            parts.Add(Decode(title.Groups[1].Value));
        if (desc.Success)
            parts.Add(Decode(desc.Groups[1].Value));
        return string.Join("\n", parts.Where(p => p.Length > 0));
    }

    private static string Decode(string s) =>
        System.Net.WebUtility.HtmlDecode(Regex.Replace(s, @"\s+", " ").Trim());
}
