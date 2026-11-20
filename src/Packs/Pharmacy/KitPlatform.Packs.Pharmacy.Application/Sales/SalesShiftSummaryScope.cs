namespace KitPlatform.Packs.Pharmacy.Sales;

/// <summary>
/// Thu ngân xem tổng kết ca trong ngày làm việc VN. Khoảng ngày khác là doanh thu cửa hàng.
/// </summary>
public static class SalesShiftSummaryScope
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static (DateTime StartUtc, DateTime EndUtc) CurrentVietnamDayUtc(DateTime utcNow)
    {
        var nowUtc = AsUtc(utcNow);
        var vn = nowUtc.Add(VietnamOffset);
        var startUtc = new DateTime(vn.Year, vn.Month, vn.Day, 0, 0, 0, DateTimeKind.Utc).Subtract(VietnamOffset);
        return (startUtc, startUtc.AddDays(1));
    }

    public static bool IsContainedInCurrentVietnamDay(DateTime from, DateTime to, DateTime utcNow)
    {
        var (startUtc, endUtc) = CurrentVietnamDayUtc(utcNow);
        var start = AsUtc(from);
        var end = AsUtc(to);
        if (end < start)
            return false;
        return start >= startUtc.AddMinutes(-2) && end <= endUtc.AddMinutes(2);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}
