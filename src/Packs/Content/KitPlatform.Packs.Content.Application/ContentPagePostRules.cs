namespace KitPlatform.Packs.Content;

/// <summary>
/// Bài viết Fanpage (không có file video) đăng lúc 15:00 giờ Việt Nam.
/// Giờ này lệch các khung thước phim 07:00, 09:00, 19:30 và 20:00.
/// </summary>
public static class ContentPagePostRules
{
    public const int OffsetHours = 7;
    public static readonly TimeOnly Slot = new(15, 0);
    private const int HorizonDays = 21;

    public static bool IsArticle(string? format, bool hasVideoAsset)
    {
        if (hasVideoAsset) return false;
        return !string.Equals(format, "long", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(format, "short", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Mốc 15:00 kế tiếp còn trống. Ngày đã có bài đúng 15:00 thì nhường sang ngày sau.</summary>
    public static DateTimeOffset? NextOpen(
        DateTimeOffset nowUtc,
        IReadOnlyCollection<DateTimeOffset> occupiedUtc,
        int offsetHours = OffsetHours)
    {
        var offset = TimeSpan.FromHours(Math.Clamp(offsetHours, -12, 14));
        var taken = new HashSet<DateOnly>();
        foreach (var at in occupiedUtc)
        {
            var local = at.ToOffset(offset);
            if (TimeOnly.FromDateTime(local.DateTime) == Slot)
                taken.Add(DateOnly.FromDateTime(local.DateTime));
        }

        var first = DateOnly.FromDateTime(nowUtc.ToOffset(offset).DateTime);
        var earliest = nowUtc.AddMinutes(ContentScheduleApprovalRules.MinLeadMinutes);
        for (var i = 0; i < HorizonDays; i++)
        {
            var day = first.AddDays(i);
            if (taken.Contains(day)) continue;
            var local = new DateTimeOffset(day.ToDateTime(Slot), offset);
            if (local.ToUniversalTime() < earliest) continue;
            return local.ToUniversalTime();
        }

        return null;
    }
}
