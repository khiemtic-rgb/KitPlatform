namespace PharmaCore.Infrastructure.Reports;

internal static class ReportsDateHelper
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static (DateTime FromUtc, DateTime ToUtc) ResolveRangeUtc(DateTime? fromUtc, DateTime? toUtc, DateTime utcNow)
    {
        if (fromUtc.HasValue && toUtc.HasValue)
            return (fromUtc.Value, toUtc.Value);

        var vn = utcNow.Add(VietnamOffset);
        var monthStartVn = new DateTime(vn.Year, vn.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var todayEndVn = new DateTime(vn.Year, vn.Month, vn.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1);
        var from = fromUtc ?? monthStartVn - VietnamOffset;
        var to = toUtc ?? todayEndVn - VietnamOffset;
        if (to <= from)
            to = from.AddDays(1);
        return (from, to);
    }

    public static string FormatVnDateRange(DateTime fromUtc, DateTime toUtc)
    {
        var from = fromUtc.Add(VietnamOffset).ToString("dd/MM/yyyy");
        var toExclusive = toUtc.Add(VietnamOffset).AddTicks(-1);
        return $"{from} – {toExclusive:dd/MM/yyyy}";
    }
}
