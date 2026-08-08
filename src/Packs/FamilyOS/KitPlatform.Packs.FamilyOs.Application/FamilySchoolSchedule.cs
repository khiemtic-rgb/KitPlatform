using System.Globalization;
using System.Text.Json;

namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// School Season schedule — SoT path layers.members.&lt;id&gt;.schoolSchedule.
/// Phase / quietNow must stay parity with client school-season.ts (SCH-01b).
/// Spec: docs/novixa/03-solution/famixa-school-schedule-api-v1.md
/// </summary>
public static class FamilySchoolPhases
{
    public const string Weekend = "weekend";
    public const string SeasonOff = "season_off";
    public const string BeforeSchool = "before_school";
    public const string AtSchool = "at_school";
    public const string AfterSchool = "after_school";
    public const string Evening = "evening";
}

public static class FamilySchoolDayModes
{
    public const string Off = "off";
    public const string Morning = "morning";
    public const string Full = "full";
}

/// <summary>V1 payload under layers.members.&lt;memberId&gt;.schoolSchedule.</summary>
public sealed record FamilySchoolScheduleV1(
    int SchemaVersion,
    bool SeasonOn,
    string Mode,
    IReadOnlyList<int> Weekdays,
    string SchoolStart,
    string SchoolEnd,
    bool HasExtraClass,
    string? ExtraEnd,
    string Source,
    string UpdatedAt,
    Guid? UpdatedByMemberId = null);

public sealed record FamilySchoolDerived(
    string Phase,
    bool QuietNow,
    string QuietEnd,
    DateTimeOffset AsOf,
    string TimeZone);

public static class FamilySchoolSchedule
{
    public const string DefaultTimeZone = "Asia/Ho_Chi_Minh";
    public const string LayersMembersKey = "members";
    public const string LayersScheduleKey = "schoolSchedule";

    private static readonly int[] DefaultWeekdays = [1, 2, 3, 4, 5];
    private static readonly TimeSpan LandingFloor = new(19, 30, 0);

    /// <summary>Quiet end clock (HH:mm) used while season is on.</summary>
    public static string EffectiveQuietEnd(FamilySchoolScheduleV1 schedule)
    {
        if (!schedule.SeasonOn || schedule.Mode == FamilySchoolDayModes.Off)
            return PadTime(schedule.SchoolStart);
        if (schedule.Mode == FamilySchoolDayModes.Morning)
            return PadTime(schedule.SchoolEnd);
        if (schedule.HasExtraClass && !string.IsNullOrWhiteSpace(schedule.ExtraEnd))
            return PadTime(schedule.ExtraEnd!);
        return PadTime(schedule.SchoolEnd);
    }

    /// <summary>
    /// Pure phase resolve — parity with school-season.ts resolveSchoolPhase /
    /// scripts/check-school-season.mjs.
    /// </summary>
    /// <param name="isoWeekday">Mon=1 … Sun=7</param>
    public static string ResolvePhase(
        FamilySchoolScheduleV1? schedule,
        int isoWeekday,
        TimeOnly localTime)
    {
        if (schedule is null || !schedule.SeasonOn || schedule.Mode == FamilySchoolDayModes.Off)
            return FamilySchoolPhases.SeasonOff;

        if (!schedule.Weekdays.Contains(isoWeekday))
            return FamilySchoolPhases.Weekend;

        var t = (int)localTime.ToTimeSpan().TotalMinutes;
        var start = MinutesOf(schedule.SchoolStart);
        var quietEnd = MinutesOf(EffectiveQuietEnd(schedule));
        var schoolEnd = MinutesOf(schedule.SchoolEnd);

        if (t < start) return FamilySchoolPhases.BeforeSchool;
        if (t < quietEnd) return FamilySchoolPhases.AtSchool;

        // Landing window: tan học → max(schoolEnd+30, 19:30)
        var landingEnd = Math.Max(schoolEnd + 30, (int)LandingFloor.TotalMinutes);
        if (t < landingEnd) return FamilySchoolPhases.AfterSchool;
        return FamilySchoolPhases.Evening;
    }

    public static string ResolvePhase(
        FamilySchoolScheduleV1? schedule,
        DateTimeOffset asOf,
        string? timezoneId = null)
    {
        var tz = FamilyTimeZones.Resolve(timezoneId);
        var local = TimeZoneInfo.ConvertTime(asOf, tz);
        return ResolvePhase(schedule, IsoWeekday(local.DateTime), TimeOnly.FromTimeSpan(local.TimeOfDay));
    }

    public static bool IsQuietNow(
        FamilySchoolScheduleV1? schedule,
        int isoWeekday,
        TimeOnly localTime) =>
        ResolvePhase(schedule, isoWeekday, localTime) == FamilySchoolPhases.AtSchool;

    public static bool IsQuietNow(
        FamilySchoolScheduleV1? schedule,
        DateTimeOffset asOf,
        string? timezoneId = null) =>
        ResolvePhase(schedule, asOf, timezoneId) == FamilySchoolPhases.AtSchool;

    public static FamilySchoolDerived Derive(
        FamilySchoolScheduleV1? schedule,
        DateTimeOffset asOfUtcOrOffset,
        string? timezoneId = null)
    {
        var tzId = string.IsNullOrWhiteSpace(timezoneId) ? DefaultTimeZone : timezoneId.Trim();
        var tz = FamilyTimeZones.Resolve(tzId);
        var local = TimeZoneInfo.ConvertTime(asOfUtcOrOffset, tz);
        var phase = ResolvePhase(
            schedule,
            IsoWeekday(local.DateTime),
            TimeOnly.FromTimeSpan(local.TimeOfDay));
        var quietEnd = schedule is null
            ? "07:00"
            : EffectiveQuietEnd(schedule);
        return new FamilySchoolDerived(
            phase,
            phase == FamilySchoolPhases.AtSchool,
            quietEnd,
            local,
            FamilyTimeZones.ToPostgresId(tzId));
    }

    public static FamilySchoolScheduleV1? ReadMemberSchedule(string? layersJson, Guid memberId)
    {
        if (memberId == Guid.Empty || string.IsNullOrWhiteSpace(layersJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(layersJson);
            if (!doc.RootElement.TryGetProperty(LayersMembersKey, out var members)
                || members.ValueKind != JsonValueKind.Object)
                return null;

            var key = memberId.ToString();
            if (!members.TryGetProperty(key, out var row) &&
                !TryFindMemberIgnoreCase(members, key, out row))
                return null;

            if (!row.TryGetProperty(LayersScheduleKey, out var sch)
                || sch.ValueKind != JsonValueKind.Object)
                return null;

            return ParseSchedule(sch);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyDictionary<Guid, FamilySchoolScheduleV1> ReadAllSchedules(string? layersJson)
    {
        var map = new Dictionary<Guid, FamilySchoolScheduleV1>();
        if (string.IsNullOrWhiteSpace(layersJson)) return map;

        try
        {
            using var doc = JsonDocument.Parse(layersJson);
            if (!doc.RootElement.TryGetProperty(LayersMembersKey, out var members)
                || members.ValueKind != JsonValueKind.Object)
                return map;

            foreach (var prop in members.EnumerateObject())
            {
                if (!Guid.TryParse(prop.Name, out var memberId)) continue;
                if (!prop.Value.TryGetProperty(LayersScheduleKey, out var sch)
                    || sch.ValueKind != JsonValueKind.Object)
                    continue;
                var parsed = ParseSchedule(sch);
                if (parsed is not null) map[memberId] = parsed;
            }
        }
        catch (JsonException)
        {
            return map;
        }

        return map;
    }

    /// <summary>Batch quiet flags for SCH-01c parent-push filter.</summary>
    public static IReadOnlyDictionary<Guid, bool> QuietMap(
        string? layersJson,
        DateTimeOffset asOf,
        string? timezoneId = null)
    {
        var result = new Dictionary<Guid, bool>();
        foreach (var (memberId, schedule) in ReadAllSchedules(layersJson))
            result[memberId] = IsQuietNow(schedule, asOf, timezoneId);
        return result;
    }

    public static FamilySchoolSchedulePayloadDto? ToPayloadDto(FamilySchoolScheduleV1? schedule)
    {
        if (schedule is null) return null;
        return new FamilySchoolSchedulePayloadDto(
            schedule.SchemaVersion,
            schedule.SeasonOn,
            schedule.Mode,
            schedule.Weekdays,
            schedule.SchoolStart,
            schedule.SchoolEnd,
            schedule.HasExtraClass,
            schedule.ExtraEnd,
            schedule.Source,
            schedule.UpdatedAt,
            schedule.UpdatedByMemberId);
    }

    public static int IsoWeekday(DateTime local) =>
        local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;

    public static string PadTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "07:00";
        var m = System.Text.RegularExpressions.Regex.Match(raw.Trim(), @"^(\d{1,2}):(\d{2})$");
        if (!m.Success) return "07:00";
        var h = Math.Clamp(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 0, 23);
        var min = Math.Clamp(int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), 0, 59);
        return $"{h:D2}:{min:D2}";
    }

    public static int MinutesOf(string hhmm)
    {
        var parts = PadTime(hhmm).Split(':');
        return int.Parse(parts[0], CultureInfo.InvariantCulture) * 60
               + int.Parse(parts[1], CultureInfo.InvariantCulture);
    }

    private static bool TryFindMemberIgnoreCase(
        JsonElement members,
        string key,
        out JsonElement row)
    {
        foreach (var prop in members.EnumerateObject())
        {
            if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                row = prop.Value;
                return true;
            }
        }

        row = default;
        return false;
    }

    private static FamilySchoolScheduleV1? ParseSchedule(JsonElement sch)
    {
        if (!sch.TryGetProperty("schoolStart", out var startEl)
            || startEl.ValueKind != JsonValueKind.String
            || !sch.TryGetProperty("schoolEnd", out var endEl)
            || endEl.ValueKind != JsonValueKind.String)
            return null;

        var mode = NormalizeMode(sch.TryGetProperty("mode", out var modeEl) ? modeEl.GetString() : null);
        var seasonOn = true;
        if (sch.TryGetProperty("seasonOn", out var onEl))
        {
            seasonOn = onEl.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => !string.Equals(onEl.GetString(), "false", StringComparison.OrdinalIgnoreCase),
                _ => true,
            };
        }

        if (mode == FamilySchoolDayModes.Off) seasonOn = false;

        var weekdays = ParseWeekdays(sch);
        var hasExtra = sch.TryGetProperty("hasExtraClass", out var extraEl)
                       && extraEl.ValueKind == JsonValueKind.True;
        string? extraEnd = null;
        if (sch.TryGetProperty("extraEnd", out var extraEndEl)
            && extraEndEl.ValueKind == JsonValueKind.String)
            extraEnd = PadTime(extraEndEl.GetString());

        var source = "parent_settings";
        if (sch.TryGetProperty("source", out var srcEl) && srcEl.ValueKind == JsonValueKind.String)
        {
            var s = srcEl.GetString() ?? source;
            if (s is "parent_settings" or "onboarding_seed" or "migrated_local")
                source = s;
        }

        var updatedAt = sch.TryGetProperty("updatedAt", out var uaEl)
                        && uaEl.ValueKind == JsonValueKind.String
            ? uaEl.GetString() ?? DateTimeOffset.UtcNow.ToString("O")
            : DateTimeOffset.UtcNow.ToString("O");

        Guid? updatedBy = null;
        if (sch.TryGetProperty("updatedByMemberId", out var byEl)
            && byEl.ValueKind == JsonValueKind.String
            && Guid.TryParse(byEl.GetString(), out var byId))
            updatedBy = byId;

        var schemaVersion = 1;
        if (sch.TryGetProperty("schemaVersion", out var svEl) && svEl.TryGetInt32(out var sv))
            schemaVersion = sv;

        return new FamilySchoolScheduleV1(
            schemaVersion,
            seasonOn,
            mode,
            weekdays,
            PadTime(startEl.GetString()),
            PadTime(endEl.GetString()),
            hasExtra,
            extraEnd,
            source,
            updatedAt!,
            updatedBy);
    }

    private static string NormalizeMode(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant() switch
        {
            FamilySchoolDayModes.Off => FamilySchoolDayModes.Off,
            FamilySchoolDayModes.Morning => FamilySchoolDayModes.Morning,
            FamilySchoolDayModes.Full => FamilySchoolDayModes.Full,
            _ => FamilySchoolDayModes.Full,
        };

    private static IReadOnlyList<int> ParseWeekdays(JsonElement sch)
    {
        if (!sch.TryGetProperty("weekdays", out var wd) || wd.ValueKind != JsonValueKind.Array)
            return DefaultWeekdays;

        var list = new List<int>();
        foreach (var item in wd.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var n) && n is >= 1 and <= 7)
                list.Add(n);
            else if (item.ValueKind == JsonValueKind.String
                     && int.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)
                     && n is >= 1 and <= 7)
                list.Add(n);
        }

        return list.Count > 0 ? list : DefaultWeekdays;
    }
}
