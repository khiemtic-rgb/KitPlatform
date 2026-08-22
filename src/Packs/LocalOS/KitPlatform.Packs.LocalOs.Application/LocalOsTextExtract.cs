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

    public static bool LooksLikeBenefit(string text)
    {
        var t = text.ToLowerInvariant();
        string[] keys =
        [
            "học bổng", "hoc bong", "ưu đãi", "uu dai", "khuyến mãi", "khuyen mai",
            "học phí", "hoc phi", "miễn phí", "mien phi", "giảm giá", "giam gia",
            "voucher", "suất học", "suat hoc", "trợ cấp học", "tro cap hoc",
        ];
        return keys.Any(k => t.Contains(k, StringComparison.Ordinal));
    }

    public static string GuessEventCategory(string text)
    {
        if (LooksLikeBenefit(text))
            return "benefit";
        var t = text.ToLowerInvariant();
        if (HasAny(t, "bóng đá", "bong da", "giao hữu", "giao huu", "v.league", "sân vận động",
                "the-thao", "thể thao", "bóng chuyền", "giải chạy", "fc thái nguyên"))
            return "sport";
        if (HasAny(t, "giáo dục", "giao duc", "học sinh", "sinh viên", "đại học", "năm học",
                "khai giảng", "tân sinh viên", "trường học", "học bổng"))
            return "education";
        if (HasAny(t, "hội chợ", "hoi cho", "festival", "ngày hội", "ngay hoi", "ocop", "phiên chợ"))
            return "fair";
        if (HasAny(t, "lễ hội", "le hoi", "văn hóa", "van hoa", "dân ca", "di sản", "nghệ thuật"))
            return "culture";
        if (HasAny(t, "du lịch", "du lich", "núi cốc", "vùng chè", "ba bể"))
            return "tourism";
        if (t.Contains("workshop", StringComparison.Ordinal))
            return "workshop";
        if (HasAny(t, "hội thảo", "hoi thao", "hội nghị khoa học", "conference"))
            return "conference";
        if (HasAny(t, "đêm nhạc", "concert", "ca nhạc", "âm nhạc"))
            return "music";
        return "news";
    }

    private static bool HasAny(string blob, params string[] keys) =>
        keys.Any(k => blob.Contains(k, StringComparison.Ordinal));

    public static string GuessKind(string text, string? hint)
    {
        if (hint is "grant" or "offer")
            return "event";
        if (hint is "job" or "event" or "room")
            return hint;
        var t = text.ToLowerInvariant();
        if (t.Contains("phòng") || t.Contains("phong tro") || t.Contains("trọ") || t.Contains("cho thuê") || t.Contains("ở ghép"))
            return "room";
        if (LooksLikeBenefit(text)
            || t.Contains("workshop") || t.Contains("sự kiện") || t.Contains("su kien")
            || t.Contains("đêm nhạc") || t.Contains("ngày hội"))
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

    /// <summary>Lead still short: meta-only, or cut mid-word with …</summary>
    public static bool IsThinLead(string? summary)
    {
        var s = (summary ?? "").Trim();
        if (s.Length < 400 || HasScrapedChrome(s))
            return true;
        return s.EndsWith("...", StringComparison.Ordinal) || s.EndsWith("…", StringComparison.Ordinal);
    }

    public static bool IsBetterLead(string? current, string next)
    {
        if (next.Length < 80 || HasScrapedChrome(next))
            return false;
        var cur = (current ?? "").Trim();
        if (cur.Length == 0)
            return true;
        if (string.Equals(cur, next, StringComparison.Ordinal))
            return false;
        if (IsThinLead(cur))
            return true;
        return next.Length > cur.Length + 20;
    }

    public static bool HasScrapedChrome(string text) =>
        Regex.IsMatch(
            text,
            @"giúp chúng tôi|góp ý|festivalindex|đăng nhập để|theo dõi sự kiện|mở form góp ý",
            RegexOptions.IgnoreCase);

    /// <summary>Lead for the public site: drop repeated title, keep a few sentences. Never invents.</summary>
    public static string GuessSummary(string? title, string text, int maxChars = 1200)
    {
        if (maxChars < 80)
            maxChars = 80;
        var raw = Regex.Replace(text ?? "", @"[ \t]+", " ");
        raw = Regex.Replace(raw, @"\n{3,}", "\n\n").Trim();
        if (raw.Length == 0)
            return "";

        var t = Regex.Replace(title ?? "", @"\s+", " ").Trim();
        if (t.Length >= 12)
        {
            var head = t.TrimEnd('…', '.', '|', ' ');
            if (head.Length >= 12 && raw.StartsWith(head, StringComparison.OrdinalIgnoreCase))
                raw = raw[head.Length..].TrimStart(' ', '|', '·', '-', '–', '\n');
        }

        var compact = Regex.Replace(raw, @"[ \t]+", " ");
        compact = Regex.Replace(compact, @" *\n *", "\n");
        compact = Regex.Replace(compact, @"\n{3,}", "\n\n").Trim();
        if (compact.Length <= maxChars)
            return compact;
        var slice = compact[..maxChars];
        var para = slice.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (para >= 160)
            return slice[..para].Trim();
        var cut = Math.Max(slice.LastIndexOf(". ", StringComparison.Ordinal), slice.LastIndexOf("! ", StringComparison.Ordinal));
        cut = Math.Max(cut, slice.LastIndexOf("? ", StringComparison.Ordinal));
        if (cut < 120)
            return slice.TrimEnd() + "…";
        return slice[..(cut + 1)].Trim();
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
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = text.Replace('\u00a0', ' ');
        string[] patterns =
        [
            @"\d{1,3}(?:[.\s]\d{3})+\s*đ?\s*[-–~]\s*\d{1,3}(?:[.\s]\d{3})+\s*đ?\s*/?\s*(?:giờ|gio|h)\b",
            @"\d+(?:[.,]\d+)?\s*k\s*[-–~]\s*\d+(?:[.,]\d+)?\s*k(?:\s*/?\s*(?:giờ|gio|h))?",
            @"\d+(?:[.,]\d+)?\s*k\s*/\s*(?:giờ|gio|h)\b",
            @"\d+(?:[.,]\d+)?\s*tr(?:iệu)?\d?\s*[-–~]\s*\d+(?:[.,]\d+)?\s*tr(?:iệu)?",
            @"\d+[.,]\d+\s*tr(?:iệu)?(?:\s*/\s*tháng)?",
            @"\d+\s*tr(?:iệu)?\d?(?:\s*/\s*(?:tháng|khóa|khoa))?",
            @"(?<=(?:lương|thu nhập|lcb)\s*[:：]\s*)[^\n]{4,56}",
        ];
        string? best = null;
        var bestAt = int.MaxValue;
        foreach (var p in patterns)
        {
            var m = Regex.Match(t, p, RegexOptions.IgnoreCase);
            if (!m.Success || m.Index >= bestAt)
                continue;
            var s = CleanPay(m.Value);
            if (s is null)
                continue;
            best = s;
            bestAt = m.Index;
        }
        return best;
    }

    private static string? CleanPay(string raw)
    {
        var s = Regex.Replace(raw.Trim(), @"\s+", " ");
        s = Regex.Split(s, @"\s+[•·|(]|,\s+(?:tùy|chưa|cam)")[0].Trim().TrimEnd('.', ',', ';', ':');
        if (s.Length < 3 || s.Length > 48)
            return s.Length > 48 ? s[..48].TrimEnd() + "…" : null;
        if (Regex.IsMatch(s, @"^0\d{8,}") || Regex.IsMatch(s, @"20\d{2}"))
            return null;
        return s;
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

    public static string? GuessStreetPlace(string text)
    {
        var bits = new List<string>();
        string[] patterns =
        [
            @"cầu vượt\s+[^,.\n()\-]{2,40}",
            @"(?:phường|xã|p\.)\s+[^,.\n()]{2,28}",
            @"(?:tp\.?|thành phố)\s*thái nguyên",
            @"gần\s+(?:đh|đại học|cao đẳng|trường)[^,.\n()]{0,36}",
            @"\d{1,4}[a-zA-Z]?\s+(?:đường|phố|ngõ|hẻm)[^,.\n]{2,40}",
            @"(?:phường\s+)?quyết thắng",
            @"phan đình phùng",
        ];
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
            if (!m.Success)
                continue;
            var s = Regex.Replace(m.Value, @"\s+", " ").Trim();
            if (s.Length is < 4 or > 56)
                continue;
            if (bits.Any(b => b.Contains(s, StringComparison.OrdinalIgnoreCase) || s.Contains(b, StringComparison.OrdinalIgnoreCase)))
                continue;
            bits.Add(char.ToUpperInvariant(s[0]) + s[1..]);
        }

        if (bits.Count == 0)
            return null;
        var joined = string.Join(", ", bits);
        return joined.Length <= 80 ? joined : joined[..80].TrimEnd();
    }

    public static string GuessShortTitle(string kind, string text)
    {
        var t = text.ToLowerInvariant();
        if (kind == "room")
        {
            var closed = t.Contains("khép kín") || t.Contains("khep kin");
            var shared = t.Contains("ở ghép") || t.Contains("o ghep");
            var near = Regex.Match(text, @"gần\s+([^,.\n()]{3,32})", RegexOptions.IgnoreCase);
            var title = shared ? "Cho thuê chỗ ở ghép" : closed ? "Cho thuê phòng khép kín" : "Cho thuê phòng";
            if (near.Success)
                title += " gần " + Regex.Replace(near.Groups[1].Value, @"\s+", " ").Trim();
            return title.Length <= 80 ? title : title[..80].TrimEnd();
        }

        if (kind == "event")
        {
            var named = Regex.Match(text, @"(?:sự kiện|workshop|đêm nhạc|ngày hội)\s+([^,.\n]{4,48})", RegexOptions.IgnoreCase);
            if (named.Success)
            {
                var s = Regex.Replace(named.Groups[1].Value, @"\s+", " ").Trim();
                return s.Length <= 80 ? s : s[..80].TrimEnd();
            }
            return "Sự kiện tại Thái Nguyên";
        }

        var role = Regex.Match(text, @"(?:tuyển|tuyen)\s+([^,.\n]{4,40})", RegexOptions.IgnoreCase);
        if (role.Success)
        {
            var s = "Tuyển " + Regex.Replace(role.Groups[1].Value, @"\s+", " ").Trim();
            return s.Length <= 80 ? s : s[..80].TrimEnd();
        }

        return GuessTitle(text);
    }

    public static string? GuessContactName(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var labeled = Regex.Match(
            text,
            @"(?:liên hệ|lh|zalo)\s*[:\-–]?\s*((?:anh|chị|cô|chú|em)\s+[A-Za-zÀ-ỹ][A-Za-zÀ-ỹ ]{0,28}?)(?=\s*(?:[-–:,]|sđt|sdt|0|\+84|$))",
            RegexOptions.IgnoreCase);
        if (labeled.Success)
        {
            var n = CleanPersonName(labeled.Groups[1].Value);
            if (n is not null)
                return n;
        }

        var full = Regex.Match(
            text,
            @"(?:liên hệ|lh)\s*[:\-–]\s*([A-Za-zÀ-ỹ][A-Za-zÀ-ỹ. ]{1,36}?)(?=\s*(?:[-–:]|sđt|sdt|0|\+84))",
            RegexOptions.IgnoreCase);
        if (full.Success)
        {
            var n = CleanPersonName(full.Groups[1].Value);
            if (n is not null)
                return n;
        }

        var role = Regex.Match(
            text,
            @"\b((?:anh|chị|cô|chú)\s+[A-ZÀ-Ỹ][a-zà-ỹ]{1,20}(?:\s+[A-ZÀ-Ỹ][a-zà-ỹ]{1,20}){0,3})\b");
        return role.Success ? CleanPersonName(role.Groups[1].Value) : null;
    }

    public static string? GuessWorkingTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var labeled = Regex.Match(
            text,
            @"(?:thời gian(?: làm việc)?|ca làm(?: việc)?|giờ làm)\s*[:\-–]\s*([^\n]{3,72})",
            RegexOptions.IgnoreCase);
        if (labeled.Success)
        {
            var s = ClipField(labeled.Groups[1].Value, 72);
            if (s is not null && !LooksLikePay(s))
                return s;
        }

        var range = Regex.Match(
            text,
            @"\d{1,2}\s*h(?:\d{0,2})?\s*[-–]\s*\d{1,2}\s*h(?:\d{0,2})?(?:\s*(?:hoặc|/)\s*\d{1,2}\s*h(?:\d{0,2})?\s*[-–]\s*\d{1,2}\s*h(?:\d{0,2})?)?(?:\s*,?\s*(?:T[2-7]|CN|thứ|cuối tuần)[^.\n]{0,28})?",
            RegexOptions.IgnoreCase);
        return range.Success ? ClipField(range.Value, 72) : null;
    }

    public static string? GuessRequirements(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var m = Regex.Match(
            text,
            @"(?:yêu cầu|yc)\s*[:\-–]\s*([^\n]{4,140})",
            RegexOptions.IgnoreCase);
        if (m.Success)
            return ClipField(m.Groups[1].Value, 140);
        m = Regex.Match(text, @"(?:từ\s*)?\d{2}\s*[-–]\s*\d{2}\s*tuổi|(?:từ\s+)?\d{2}\s*tuổi", RegexOptions.IgnoreCase);
        return m.Success ? ClipField(m.Value, 80) : null;
    }

    public static string? GuessOrganizationName(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var m = Regex.Match(
            text,
            @"(?:quán|nhà hàng|công ty|cửa hàng|tiệm|khách sạn|cơ sở)\s+([A-Za-zÀ-ỹ0-9][A-Za-zÀ-ỹ0-9 &'’\-]{0,32}?)(?=\s+(?:cần|tuyển|tuyen|nv|nhân viên|pt|ft|lh|liên hệ|,|$))",
            RegexOptions.IgnoreCase);
        if (!m.Success)
            return null;
        var s = ClipField(m.Value, 48);
        if (s is null)
            return null;
        if (Regex.IsMatch(s, @"tuyển|nhân viên|liên hệ|lương", RegexOptions.IgnoreCase))
            return null;
        return s;
    }

    public static string? GuessEmploymentType(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = text.ToLowerInvariant();
        if (Regex.IsMatch(t, @"thực tập|intern"))
            return "internship";
        if (Regex.IsMatch(t, @"bán thời gian|part[\s-]?time|partime|passtime|(?<![a-z])pt(?![a-z])"))
            return "part_time";
        if (Regex.IsMatch(t, @"toàn thời gian|full[\s-]?time|(?<![a-z])ft(?![a-z])"))
            return "full_time";
        if (t.Contains("cuối tuần"))
            return "weekend";
        return null;
    }

    public static string? KeepIfFromSource(string source, string? proposed)
    {
        var clean = OneLine(proposed);
        if (string.IsNullOrWhiteSpace(clean))
            return null;
        var foldedSource = Fold(source);
        var folded = Fold(clean);
        if (folded.Length < 2)
            return null;
        if (foldedSource.Contains(folded))
            return clean;
        var digits = Regex.Replace(clean, @"\D", "");
        if (digits.Length >= 3 && Regex.Replace(source, @"\D", "").Contains(digits))
            return clean;
        return null;
    }

    public static string StructuredBody(
        string kind,
        string title,
        string? organization,
        string? place,
        string? phone,
        string? contactName,
        string? salary,
        string? workingTime,
        string? requirements)
    {
        var lines = new List<string>();
        if (kind == "job" && !string.IsNullOrWhiteSpace(organization))
        {
            var lead = organization.Trim() + " tuyển nhân viên.";
            if (!Fold(title).Contains(Fold(organization)))
                lines.Add(lead);
        }
        else if (kind == "room")
        {
            lines.Add(title);
        }

        if (kind == "job" && !string.IsNullOrWhiteSpace(salary))
            lines.Add("Thu nhập: " + salary.Trim());
        if (!string.IsNullOrWhiteSpace(workingTime))
            lines.Add("Thời gian: " + workingTime.Trim());
        if (!string.IsNullOrWhiteSpace(place))
            lines.Add((kind == "room" ? "Địa chỉ: " : "Địa điểm: ") + place.Trim());
        if (!string.IsNullOrWhiteSpace(requirements))
            lines.Add("Yêu cầu: " + requirements.Trim());
        var contact = FormatContact(contactName, phone);
        if (contact is not null)
            lines.Add("Liên hệ: " + contact);
        var body = string.Join("\n", lines.Where(l => l.Length > 0)).Trim();
        return body.Length >= 8 ? body : title;
    }

    public static string? FormatContact(string? name, string? phone)
    {
        var n = OneLine(name);
        var p = OneLine(phone);
        if (string.IsNullOrWhiteSpace(n) && string.IsNullOrWhiteSpace(p))
            return null;
        if (string.IsNullOrWhiteSpace(n))
            return p;
        if (string.IsNullOrWhiteSpace(p))
            return n;
        return $"{n} — {p}";
    }

    private static string? CleanPersonName(string? raw)
    {
        var s = OneLine(raw);
        if (string.IsNullOrWhiteSpace(s))
            return null;
        s = Regex.Replace(s, @"\d", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim(" :-–.,;".ToCharArray());
        if (s.Length is < 2 or > 40)
            return null;
        if (Regex.IsMatch(s, @"^(liên hệ|lh|zalo|sđt|sdt|tuyển|nhân viên|quán|nhà hàng)$", RegexOptions.IgnoreCase))
            return null;
        if (Regex.IsMatch(s, @"thái nguyên|thu nhập|lương|địa điểm", RegexOptions.IgnoreCase))
            return null;
        return s;
    }

    private static bool LooksLikePay(string s) =>
        Regex.IsMatch(s, @"triệu|\d+\s*k\b|000\s*đ", RegexOptions.IgnoreCase)
        && !Regex.IsMatch(s, @"\d{1,2}\s*h", RegexOptions.IgnoreCase);

    private static string? ClipField(string raw, int max)
    {
        var s = OneLine(raw).TrimEnd('.', ',', ';', ':');
        if (s.Length < 3)
            return null;
        return s.Length <= max ? s : s[..max].TrimEnd();
    }

    private static string OneLine(string? s) =>
        Regex.Replace((s ?? "").Replace('\n', ' '), @"\s+", " ").Trim();

    public static bool LooksLikeChatDump(string? title, string? place, string? body, string source)
    {
        var t = (title ?? "").Trim();
        var p = (place ?? "").Trim();
        var b = (body ?? "").Trim();
        if (t.Length is 0 or > 72)
            return true;
        if (Regex.IsMatch(t, @"^(mình|em|tớ|tôi|mk|m)\s+(có|còn|đang|cần)\b", RegexOptions.IgnoreCase))
            return true;
        if (p.Length > 72)
            return true;
        if (Regex.IsMatch(p, @"phòng", RegexOptions.IgnoreCase) && Regex.IsMatch(p, @"triệu|cho thuê", RegexOptions.IgnoreCase))
            return true;
        var compact = Regex.Replace(source, @"\s+", " ").Trim();
        var head = t.TrimEnd('…', '.').Length > 40 ? t[..40] : t;
        if (compact.Length > 60 && head.Length > 24 && compact.StartsWith(head, StringComparison.OrdinalIgnoreCase))
            return true;
        var addr = Regex.Match(b, @"địa chỉ:\s*([^\n]+)", RegexOptions.IgnoreCase);
        return addr.Success && t.Length > 20 && addr.Groups[1].Value.Contains(t[..Math.Min(24, t.Length)], StringComparison.OrdinalIgnoreCase);
    }

    public static string StripHtml(string html)
    {
        var title = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var og = Regex.Match(html, @"property\s*=\s*[""']og:title[""']\s+content\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);
        var desc = Regex.Match(html, @"property\s*=\s*[""']og:description[""']\s+content\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);
        if (!desc.Success)
            desc = Regex.Match(html, @"name\s*=\s*[""']description[""']\s+content\s*=\s*[""'](.*?)[""']", RegexOptions.IgnoreCase);
        var heading = og.Success ? Decode(og.Groups[1].Value) : (title.Success ? Decode(title.Groups[1].Value) : "");
        var cleaned = Regex.Replace(html, @"<(script|style|noscript|nav|footer)\b[\s\S]*?</\1>", " ", RegexOptions.IgnoreCase);
        var paras = new List<string>();
        foreach (Match m in Regex.Matches(cleaned, @"<p\b([^>]*)>([\s\S]*?)</p>", RegexOptions.IgnoreCase))
        {
            if (LooksLikeChromeClass(m.Groups[1].Value))
                continue;
            var p = Decode(Regex.Replace(m.Groups[2].Value, @"<[^>]+>", " "));
            p = UnwrapQuote(p);
            if (!UsefulParagraph(p, heading))
                continue;
            if (paras.Exists(x => x.Equals(p, StringComparison.OrdinalIgnoreCase)))
                continue;
            paras.Add(p);
            if (paras.Count >= 10)
                break;
        }

        var highlights = new List<string>();
        foreach (Match m in Regex.Matches(cleaned, @"<li\b[^>]*>([\s\S]*?)</li>", RegexOptions.IgnoreCase))
        {
            if (m.Groups[1].Value.Contains("<ul", StringComparison.OrdinalIgnoreCase)
                || m.Groups[1].Value.Contains("<ol", StringComparison.OrdinalIgnoreCase))
                continue;
            var li = Decode(Regex.Replace(m.Groups[1].Value, @"<[^>]+>", " "));
            if (!UsefulHighlight(li))
                continue;
            if (paras.Exists(p => p.Contains(li, StringComparison.OrdinalIgnoreCase))
                || highlights.Exists(x => x.Equals(li, StringComparison.OrdinalIgnoreCase)))
                continue;
            highlights.Add(li);
            if (highlights.Count >= 6)
                break;
        }

        var parts = new List<string>();
        if (heading.Length > 0)
            parts.Add(heading);
        if (paras.Count > 0)
            parts.AddRange(paras);
        if (highlights.Count > 0)
            parts.AddRange(highlights);
        else if (paras.Count == 0 && desc.Success)
        {
            var meta = Decode(desc.Groups[1].Value);
            if (meta.Length > 0)
                parts.Add(meta);
        }
        return string.Join("\n\n", parts.Where(p => p.Length > 0));
    }

    private static bool UsefulParagraph(string text, string heading)
    {
        if (text.Length is < 40 or > 900)
            return false;
        if (heading.Length >= 12
            && text.StartsWith(heading, StringComparison.OrdinalIgnoreCase)
            && text.Length <= heading.Length + 24)
            return false;
        return !LooksLikeChrome(text);
    }

    private static bool UsefulHighlight(string text) =>
        text.Length is >= 18 and <= 180 && !LooksLikeChrome(text);

    private static bool LooksLikeChromeClass(string attrs) =>
        Regex.IsMatch(
            attrs,
            @"quote|muted|follow|cta|login|lunar|subscribe|góp|gop-y|feedback|bell|hot-title",
            RegexOptions.IgnoreCase);

    private static string UnwrapQuote(string text)
    {
        var s = text.Trim();
        if (s.Length >= 2 && ((s[0] == '“' && s[^1] == '”') || (s[0] == '"' && s[^1] == '"')))
            return s[1..^1].Trim();
        return s;
    }

    private static bool LooksLikeChrome(string text) =>
        Regex.IsMatch(
            text,
            @"cookie|đăng nhập|javascript|copyright|all rights|theo dõi sự kiện|khám phá theo địa điểm|đi sâu vào|nhận thông báo|giúp chúng tôi|góp ý|festivalindex|mở form góp ý|cải thiện chất lượng",
            RegexOptions.IgnoreCase);

    private static string Decode(string s) =>
        System.Net.WebUtility.HtmlDecode(Regex.Replace(s, @"\s+", " ").Trim());
}
