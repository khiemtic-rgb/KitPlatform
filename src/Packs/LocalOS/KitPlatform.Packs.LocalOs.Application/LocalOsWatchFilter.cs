namespace KitPlatform.Packs.LocalOs;

public enum LocalOsWatchDecision
{
    Allow,
    DenyPolitics,
    DenyPast,
    DenyNoise,
}

/// <summary>Official-index filter: student-useful job/event only. Never invents dates or prices.</summary>
public static class LocalOsWatchFilter
{
    private static readonly string[] DenyPolitics =
    [
        "hđnd", "hdnd", "chỉ đạo", "chi dao", "gpmb", "giải phóng mặt bằng",
        "đại hội đảng", "dai hoi dang", "kỳ họp", "ky hop", "nghị quyết", "nghi quyet",
        "kỷ luật", "ky luat", "khởi tố", "khoi to", "truy nã", "bầu cử", "bau cu",
        "tiếp xúc cử tri", "công văn", "cong van", "thanh tra", "kiểm toán",
        "tai nạn", "cháy lớn", "an táng",
    ];

    private static readonly string[] DenyPast =
    [
        "đã kết thúc", "da ket thuc", "đã diễn ra", "da dien ra", "đã bế mạc", "da be mac",
        "2019", "2020", "2021", "2022", "2023", "2024", "2025",
    ];

    private static readonly string[] AllowEvent =
    [
        "lễ hội", "le hoi", "ngày hội", "ngay hoi", "festival", "workshop",
        "hội thảo", "hoi thao", "hội nghị khoa học", "khai mạc", "khai mac",
        "đêm nhạc", "triển lãm", "trien lam", "hội chợ", "hoi cho",
        "giải thể thao", "giải bóng", "ngày hội việc",
        "văn hóa", "van hoa", "dân ca", "dân vũ", "di sản", "am thuc", "ẩm thực",
        "thể thao", "the thao", "bóng đá", "bong da", "bóng chuyền", "golf",
        "giải chạy", "giai chay",
        "du lịch", "du lich", "vùng chè", "vung che", "ocop", "phố trà",
        "liên hoan", "lien hoan",
        "học bổng", "hoc bong", "ưu đãi", "uu dai", "khuyến mãi", "khuyen mai",
        "học phí", "hoc phi", "giảm giá", "giam gia", "voucher", "suất học",
    ];

    private static readonly string[] AllowJob =
    [
        "tuyển", "tuyen dung", "tuyển dụng", "việc làm", "viec lam",
        "thực tập", "thuc tap", "intern", "part-time", "part time", "parttime",
        "nhân viên", "nhan vien",
    ];

    public static LocalOsWatchDecision Decide(string title, string? url, string? sourceCategory)
    {
        var blob = $"{title} {url}".ToLowerInvariant();
        if (DenyPolitics.Any(w => blob.Contains(w, StringComparison.Ordinal)))
            return LocalOsWatchDecision.DenyPolitics;

        var has2026 = blob.Contains("2026", StringComparison.Ordinal);
        if (!has2026 && DenyPast.Any(w => blob.Contains(w, StringComparison.Ordinal)))
            return LocalOsWatchDecision.DenyPast;

        var cat = (sourceCategory ?? "").Trim().ToLowerInvariant();
        var jobOk = AllowJob.Any(w => blob.Contains(w, StringComparison.Ordinal));
        var eventOk = AllowEvent.Any(w => blob.Contains(w, StringComparison.Ordinal));
        if (cat == "job")
            return jobOk ? LocalOsWatchDecision.Allow : LocalOsWatchDecision.DenyNoise;
        if (cat == "event")
            return eventOk ? LocalOsWatchDecision.Allow : LocalOsWatchDecision.DenyNoise;
        return jobOk || eventOk ? LocalOsWatchDecision.Allow : LocalOsWatchDecision.DenyNoise;
    }
}
