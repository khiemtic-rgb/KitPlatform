namespace KitPlatform.Packs.Content;

public sealed record ContentQualityGateDto(
    bool Passed,
    IReadOnlyList<string> Issues,
    DateTimeOffset CheckedAt,
    IReadOnlyList<string>? BlockingIssues = null,
    IReadOnlyList<string>? ApproveIssues = null)
{
    public bool CanPublish => (BlockingIssues ?? []).Count == 0;

    public bool CanApprove => (ApproveIssues ?? BlockingIssues ?? []).Count == 0;
}

public static class ContentQualityGate
{
    public const string BriefMissing =
        "Thiếu Creative Brief (mục tiêu + format) — điền trước khi duyệt";

    public static ContentQualityGateDto Evaluate(
        ContentBrandKnowledgeDto brain,
        ContentCoreIdeaDto? core,
        string? angle,
        IReadOnlyList<(string Kind, string Body)> variants,
        string? brandName = null,
        ContentCreativeBriefDto? brief = null)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(angle))
            issues.Add("Thiếu góc nhìn (angle) riêng brand");

        var fact = string.Equals(core?.FactOrOpinion, "fact", StringComparison.OrdinalIgnoreCase);
        if (fact
            && string.IsNullOrWhiteSpace(core?.Source)
            && string.IsNullOrWhiteSpace(core?.SourceUrl))
        {
            issues.Add("Ý tưởng kiểu fact nhưng chưa có nguồn");
        }

        if (!HasMinimumBrief(brief))
            issues.Add(BriefMissing);

        foreach (var forbid in brain.ClaimsForbidden)
        {
            if (string.IsNullOrWhiteSpace(forbid)) continue;
            foreach (var v in variants)
            {
                if (ContainsLoose(v.Body, forbid) || ContainsLoose(v.Kind, forbid))
                    issues.Add($"{v.Kind}: dính claim cấm «{forbid.Trim()}»");
            }
        }

        foreach (var v in variants)
        {
            var kind = (v.Kind ?? "").Trim().ToLowerInvariant();
            var len = (v.Body ?? "").Trim().Length;
            if (kind is "fb_page" or "fb_short" or "social_caption" or "instagram")
            {
                if (len > 2200)
                    issues.Add($"{v.Kind}: quá dài cho kênh ngắn ({len} ký tự)");
            }

            if (kind == "tiktok_script" && len < 80)
                issues.Add("tiktok_script: quá ngắn");
            if (kind == "web_long")
            {
                if (len < 2200)
                    issues.Add("web_long: quá mỏng — cần bài một luận điểm, khoảng 800–1400 từ");
                var h2 = 0;
                foreach (var line in (v.Body ?? "").Split('\n'))
                {
                    if (line.TrimStart().StartsWith("## ", StringComparison.Ordinal)) h2++;
                }
                if (h2 < 2)
                    issues.Add("web_long: thiếu mục ## — bài phải có 3 luận điểm, mỗi cái một H2");
            }
            if (kind == "group_suggested")
                issues.AddRange(GroupShareIssues(v.Body ?? "", brain, brandName));
        }

        return Finalize(issues);
    }

    public static ContentQualityGateDto Normalize(ContentQualityGateDto gate) =>
        Finalize(gate.Issues ?? [], gate.CheckedAt);

    public static ContentQualityGateDto Finalize(
        IReadOnlyList<string> issues,
        DateTimeOffset? checkedAt = null)
    {
        var list = issues.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        return new(
            list.Count == 0,
            list,
            checkedAt ?? DateTimeOffset.UtcNow,
            SelectPublishBlocking(list),
            SelectApproveBlocking(list));
    }

    public static bool IsPublishBlocking(string issue)
    {
        var t = issue ?? "";
        if (t.Contains("web_long: quá mỏng", StringComparison.OrdinalIgnoreCase)) return true;
        if (t.Contains("web_long: thiếu mục", StringComparison.OrdinalIgnoreCase)) return true;
        if (t.Contains("Thiếu góc nhìn", StringComparison.OrdinalIgnoreCase)) return true;
        if (t.Contains("chưa có nguồn", StringComparison.OrdinalIgnoreCase)) return true;
        return t.Contains("dính claim cấm", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsApproveBlocking(string issue) =>
        IsPublishBlocking(issue)
        || string.Equals(issue, BriefMissing, StringComparison.Ordinal);

    public static IReadOnlyList<string> SelectPublishBlocking(IEnumerable<string> issues) =>
        issues.Where(IsPublishBlocking).Distinct(StringComparer.Ordinal).ToList();

    public static IReadOnlyList<string> SelectApproveBlocking(IEnumerable<string> issues) =>
        issues.Where(IsApproveBlocking).Distinct(StringComparer.Ordinal).ToList();

    public static bool HasMinimumBrief(ContentCreativeBriefDto? brief) =>
        !string.IsNullOrWhiteSpace(brief?.Objective)
        && !string.IsNullOrWhiteSpace(brief?.Format);

    public static string RefuseApprove(IReadOnlyList<string> issues) =>
        "Quality gate chặn duyệt: " + string.Join("; ", issues.Take(5));

    public static string RefusePublish(IReadOnlyList<string> issues) =>
        "Quality gate chặn đăng: " + string.Join("; ", issues.Take(5));

    private static readonly string[] GroupPromoNeedles =
    [
        "đăng ký ngay", "mua ngay", "liên hệ ngay", "inbox ngay", "inbox để",
        "dùng thử", "khuyến mãi", "giảm giá", "click vào", "xem tại",
        "sản phẩm của chúng tôi", "giải pháp của chúng tôi", "landing",
        "mình đang dùng", "nên thử", "nên dùng",
        "sản phẩm", "giải pháp", "phần mềm", "nền tảng",
        "ứng dụng", "dùng app", "cài app", "tải app", "app này",
        "công cụ này", "nên thử app", "nên dùng app",
    ];

    public static IEnumerable<string> GroupShareIssues(
        string body,
        ContentBrandKnowledgeDto? brain = null,
        string? brandName = null)
    {
        var t = body.Trim();
        if (t.Length == 0) yield break;
        if (t.Contains("http://", StringComparison.OrdinalIgnoreCase)
            || t.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || t.Contains("www.", StringComparison.OrdinalIgnoreCase))
        {
            yield return "group_suggested: có link — group hay gỡ bài quảng cáo; viết dạng chia sẻ, không dán URL";
        }

        foreach (var needle in GroupPromoNeedles)
        {
            if (ContainsLoose(t, needle))
                yield return $"group_suggested: giọng bán hàng («{needle}») — viết lại kiểu chia sẻ/thảo luận";
        }

        foreach (var term in GroupBrandTerms(brain, brandName))
        {
            if (ContainsLoose(t, term))
                yield return $"group_suggested: còn tên thương hiệu/sản phẩm «{term}» — bài nhóm không được nhắc";
        }

        var hashes = 0;
        foreach (var ch in t)
        {
            if (ch == '#') hashes++;
        }
        if (hashes > 5)
            yield return "group_suggested: quá nhiều hashtag — giữ 2–4 hashtag chủ đề, không gắn thương hiệu";
    }

    public static IReadOnlyList<string> GroupBrandTerms(ContentBrandKnowledgeDto? brain, string? brandName)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? s)
        {
            var x = (s ?? "").Trim().TrimStart('#');
            if (x.Length < 4) return;
            set.Add(x);
        }

        Add(brandName);
        if (brain is not null)
        {
            foreach (var p in brain.Products) Add(p);
            foreach (var p in brain.Services) Add(p);
            foreach (var p in brain.Hashtags) Add(p);
        }

        foreach (var extra in new[] { "Famixa", "Novixa", "KIT Tech" })
            Add(extra);
        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool ContainsLoose(string? hay, string needle)
    {
        if (string.IsNullOrWhiteSpace(hay) || string.IsNullOrWhiteSpace(needle)) return false;
        return hay.Contains(needle.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
