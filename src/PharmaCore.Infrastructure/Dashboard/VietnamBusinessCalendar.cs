namespace PharmaCore.Infrastructure.Dashboard;

/// <summary>Business calendar for Vietnam (UTC+7), used by dashboard KPI day boundaries.</summary>
internal static class VietnamBusinessCalendar
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static (DateTime StartUtc, DateTime EndUtc) TodayRangeUtc(DateTime utcNow)
    {
        var vn = utcNow.Add(Offset);
        var startVn = new DateTime(vn.Year, vn.Month, vn.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endVn = startVn.AddDays(1);
        return (startVn - Offset, endVn - Offset);
    }

    /// <summary>Last N calendar days in VN including today, as UTC half-open interval.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) RollingDaysRangeUtc(DateTime utcNow, int daysIncludingToday)
    {
        if (daysIncludingToday < 1) daysIncludingToday = 1;
        var (todayStartUtc, todayEndUtc) = TodayRangeUtc(utcNow);
        return (todayStartUtc.AddDays(-(daysIncludingToday - 1)), todayEndUtc);
    }

    public static DateOnly Today(DateTime utcNow)
    {
        var vn = utcNow.Add(Offset);
        return DateOnly.FromDateTime(vn);
    }
}
