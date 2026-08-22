using System.Globalization;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.LocalOs;

/// <summary>Read calendar dates already written on the listing. Never invents a date.</summary>
public static class LocalOsEventDate
{
    public static DateOnly TodayVietnam()
    {
        try
        {
            var tz = ResolveVietnam();
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        }
    }

    public static bool IsPastInText(string? text, DateOnly? today = null)
    {
        var last = TryLastDate(text, today);
        return last is DateOnly d && d < (today ?? TodayVietnam());
    }

    public static bool IsPastListing(
        string? kind,
        DateTimeOffset? startAt,
        DateTimeOffset? endAt,
        string? title,
        string? summary,
        string? workingTime,
        DateOnly? today = null)
    {
        if (kind is not ("event" or "grant"))
            return false;
        var day = today ?? TodayVietnam();
        var stamped = endAt ?? startAt;
        if (stamped is DateTimeOffset at)
        {
            DateOnly local;
            try
            {
                local = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(at.UtcDateTime, ResolveVietnam()));
            }
            catch (TimeZoneNotFoundException)
            {
                local = DateOnly.FromDateTime(at.UtcDateTime.AddHours(7));
            }
            if (local < day)
                return true;
        }

        var text = LooksLikeDatedPublicEvent(title)
            ? $"{title} {summary} {workingTime}"
            : $"{title} {workingTime}";
        return IsPastInText(text, day);
    }

    private static bool LooksLikeDatedPublicEvent(string? title)
    {
        var t = (title ?? "").ToLowerInvariant();
        return t.Contains("lễ hội") || t.Contains("le hoi") || t.Contains("festival")
            || t.Contains("hội chợ") || t.Contains("hoi cho") || t.Contains("phiên chợ")
            || t.Contains("giao hữu") || t.Contains("giao huu")
            || t.Contains("đêm nhạc") || t.Contains("tuần phim") || t.Contains("ngày hội")
            || t.Contains("chợ tình") || t.Contains("cho tinh");
    }

    public static DateOnly? TryLastDate(string? text, DateOnly? today = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = text.Replace('–', '-').Replace('—', '-');
        var day = today ?? TodayVietnam();
        var yearHint = 0;
        var yearMatch = Regex.Match(t, @"năm\s*(20\d{2})", RegexOptions.IgnoreCase);
        if (yearMatch.Success)
            yearHint = int.Parse(yearMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        var year = yearHint > 0 ? yearHint : day.Year;
        var dates = new List<DateOnly>();

        foreach (Match m in Regex.Matches(t, @"\b(\d{1,2})/(\d{1,2})\s*-\s*(\d{1,2})/(\d{1,2})/(\d{4})\b"))
            Add(dates, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value);
        foreach (Match m in Regex.Matches(t, @"\b(\d{1,2})\s*-\s*(\d{1,2})/(\d{1,2})/(\d{4})\b"))
            Add(dates, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
        foreach (Match m in Regex.Matches(t, @"\b(\d{1,2})/(\d{1,2})/(\d{4})\b"))
            Add(dates, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
        foreach (Match m in Regex.Matches(t, @"\b(\d{1,2})\s*-\s*(\d{1,2})/(\d{1,2})(?!/\d)\b"))
            Add(dates, m.Groups[2].Value, m.Groups[3].Value, year.ToString(CultureInfo.InvariantCulture));
        foreach (Match m in Regex.Matches(t, @"\b(\d{1,2})/(\d{1,2})(?!/\d)\b"))
            Add(dates, m.Groups[1].Value, m.Groups[2].Value, year.ToString(CultureInfo.InvariantCulture));

        return dates.Count == 0 ? null : dates.Max();
    }

    private static void Add(List<DateOnly> dates, string day, string month, string year)
    {
        if (!int.TryParse(day, CultureInfo.InvariantCulture, out var d)
            || !int.TryParse(month, CultureInfo.InvariantCulture, out var m)
            || !int.TryParse(year, CultureInfo.InvariantCulture, out var y))
            return;
        if (!DateOnly.TryParse($"{y:D4}-{m:D2}-{d:D2}", CultureInfo.InvariantCulture, out var value))
            return;
        dates.Add(value);
    }

    private static TimeZoneInfo ResolveVietnam()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
