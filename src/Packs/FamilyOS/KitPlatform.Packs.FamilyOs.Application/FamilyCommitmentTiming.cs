namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// Time-anchored vs flexible completion — meals/hygiene/sleep block early done;
/// reading/exercise may complete before window_start when allow_early_complete is set.
/// </summary>
public static class FamilyCommitmentTiming
{
    public static bool InferAllowEarlyComplete(string? title)
    {
        var t = (title ?? "").Trim().ToLowerInvariant();
        if (t.Length == 0) return false;
        if (t.Contains("đọc") || t.Contains("doc") || t.Contains("sách") || t.Contains("kể chuyện"))
            return true;
        if (t.Contains("thể dục") || t.Contains("the duc") || t.Contains("vận động")
            || t.Contains("van dong") || t.Contains("chạy bộ") || t.Contains("bơi"))
            return true;
        return false;
    }

    /// <summary>Default early lead: 0 (strict or unlimited via allow_early_complete).</summary>
    public static int InferEarlyLeadMinutes(string? title, bool allowEarlyComplete)
    {
        _ = title;
        return allowEarlyComplete ? 0 : 0;
    }

    /// <summary>Grace after window_end before late star tiers kick in.</summary>
    public static int InferOnTimeGraceMinutes(string? title, bool allowEarlyComplete)
    {
        var t = (title ?? "").Trim().ToLowerInvariant();
        if (allowEarlyComplete) return 10;
        if (t.Contains("bài") || t.Contains("học") || t.Contains("toán")) return 10;
        return 0;
    }

    /// <summary>
    /// Unlock rules:
    /// - No window_start → always allowed.
    /// - allow_early_complete + early_lead_minutes=0 → unlimited early (any time before start).
    /// - allow_early_complete + early_lead_minutes&gt;0 → unlock at window_start − lead.
    /// - !allow_early_complete → unlock at window_start (early_lead_minutes ignored).
    /// </summary>
    public static bool CanCompleteNow(
        bool allowEarlyComplete,
        int earlyLeadMinutes,
        TimeOnly? windowStart,
        TimeOnly localNow)
    {
        if (windowStart is null) return true;
        if (allowEarlyComplete && earlyLeadMinutes <= 0) return true;

        var now = localNow.ToTimeSpan();
        var unlock = ResolveUnlockSpan(allowEarlyComplete, earlyLeadMinutes, windowStart.Value);
        return now >= unlock;
    }

    public static bool IsTooEarlyToComplete(
        bool allowEarlyComplete,
        int earlyLeadMinutes,
        TimeOnly? windowStart,
        TimeOnly localNow) =>
        !CanCompleteNow(allowEarlyComplete, earlyLeadMinutes, windowStart, localNow);

    public static TimeOnly? ResolveUnlockTime(
        bool allowEarlyComplete,
        int earlyLeadMinutes,
        TimeOnly? windowStart)
    {
        if (windowStart is null) return null;
        if (allowEarlyComplete && earlyLeadMinutes <= 0) return null;
        var span = ResolveUnlockSpan(allowEarlyComplete, earlyLeadMinutes, windowStart.Value);
        return TimeOnly.FromTimeSpan(span);
    }

    internal static TimeSpan ResolveUnlockSpan(
        bool allowEarlyComplete,
        int earlyLeadMinutes,
        TimeOnly windowStart)
    {
        if (!allowEarlyComplete)
            return windowStart.ToTimeSpan();

        var lead = Math.Max(0, earlyLeadMinutes);
        var unlock = windowStart.ToTimeSpan() - TimeSpan.FromMinutes(lead);
        return unlock < TimeSpan.Zero ? TimeSpan.Zero : unlock;
    }

    public static string EarlyCompleteMessageVi(
        string title,
        TimeOnly? windowStart,
        bool allowEarlyComplete,
        int earlyLeadMinutes)
    {
        var unlock = ResolveUnlockTime(allowEarlyComplete, earlyLeadMinutes, windowStart);
        var t = title.Trim().ToLowerInvariant();
        if (t.Contains("ăn cơm") || t.Contains("ăn tối") || t.Contains("bữa tối"))
            return unlock is TimeOnly u
                ? $"Chưa tới giờ — làm lúc {u:HH\\:mm} nhé"
                : "Chưa tới giờ — làm lúc ăn tối nhé";
        if (t.Contains("ăn sáng") || t.Contains("bữa sáng"))
            return unlock is TimeOnly u2
                ? $"Chưa tới giờ — làm lúc {u2:HH\\:mm} nhé"
                : "Chưa tới giờ — làm lúc ăn sáng nhé";
        if (t.Contains("đánh răng") && (t.Contains("tối") || t.Contains("ngủ")))
            return "Chưa tới giờ — đánh răng trước khi ngủ nhé";
        if (t.Contains("đánh răng"))
            return "Chưa tới giờ — đánh răng đúng giờ nhé";
        if (t.Contains("đi ngủ") || t.Contains("ngủ"))
            return "Chưa tới giờ — đi ngủ đúng giờ nhé";
        if (t.Contains("đồng phục") || t.Contains("mặc"))
            return "Chưa tới giờ — mặc đồng phục đúng giờ nhé";
        if (t.Contains("cặp") || t.Contains("balo") || t.Contains("chuẩn bị"))
            return "Chưa tới giờ — chuẩn bị cặp đúng giờ nhé";
        if (t.Contains("dậy"))
            return "Chưa tới giờ — dậy đúng giờ nhé";
        if (t.Contains("tắm"))
            return "Chưa tới giờ — đi tắm đúng giờ nhé";
        if (unlock is TimeOnly start)
            return $"Chưa tới giờ — làm lúc {start:HH\\:mm} nhé";
        if (windowStart is TimeOnly ws)
            return $"Chưa tới giờ — làm lúc {ws:HH\\:mm} nhé";
        return "Chưa tới giờ — chờ đến giờ nhé";
    }
}
