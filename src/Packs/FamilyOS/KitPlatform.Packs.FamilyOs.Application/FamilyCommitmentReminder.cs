namespace KitPlatform.Packs.FamilyOs;

public static class FamilyReminderStates
{
    public const string None = "none";
    public const string Upcoming = "upcoming";
    public const string DueNow = "due_now";
    public const string Overdue = "overdue";

    /// <summary>Lead time before window_start to surface "sắp tới".</summary>
    public static readonly TimeSpan UpcomingLead = TimeSpan.FromMinutes(15);

    /// <summary>When only window_start exists, treat as due until this duration after start.</summary>
    public static readonly TimeSpan OpenWindowGrace = TimeSpan.FromMinutes(45);
}

/// <summary>F3 — rule-based contextual reminder from commitment time windows (no AI).</summary>
public static class FamilyCommitmentReminder
{
    public static (string State, string? Label) Evaluate(
        string status,
        TimeOnly? windowStart,
        TimeOnly? windowEnd,
        TimeOnly nowLocal,
        string? habitStage = null,
        bool reminderSuppressed = false)
    {
        if (status is FamilyCommitmentStatuses.Done or FamilyCommitmentStatuses.Skipped)
            return (FamilyReminderStates.None, null);

        if (windowStart is null && windowEnd is null)
            return (FamilyReminderStates.None, null);

        var now = nowLocal.ToTimeSpan();
        var start = windowStart?.ToTimeSpan();
        var end = windowEnd?.ToTimeSpan();

        if (start is null && end is not null)
            start = end.Value > FamilyReminderStates.OpenWindowGrace
                ? end.Value - FamilyReminderStates.OpenWindowGrace
                : TimeSpan.Zero;

        if (end is null && start is not null)
            end = start.Value + FamilyReminderStates.OpenWindowGrace;

        // Both should be set after normalization above when any window exists.
        start ??= TimeSpan.Zero;
        end ??= start.Value + FamilyReminderStates.OpenWindowGrace;

        string state;
        string? label;
        if (now > end.Value)
        {
            state = FamilyReminderStates.Overdue;
            label = "Quá giờ rồi";
        }
        else if (now >= start.Value)
        {
            state = FamilyReminderStates.DueNow;
            label = "Đến giờ rồi";
        }
        else if (start.Value - now <= FamilyReminderStates.UpcomingLead)
        {
            state = FamilyReminderStates.Upcoming;
            label = "Sắp tới";
        }
        else
        {
            return (FamilyReminderStates.None, null);
        }

        return FamilyHabitLifecycle.ApplyReminderBudget(
            state, label, habitStage, reminderSuppressed);
    }

    public static int SortRank(string reminderState, string status)
    {
        if (status is FamilyCommitmentStatuses.Done or FamilyCommitmentStatuses.Skipped)
            return 50;

        return reminderState switch
        {
            FamilyReminderStates.Overdue => 0,
            FamilyReminderStates.DueNow => 1,
            FamilyReminderStates.Upcoming => 2,
            _ => 10,
        };
    }

    /// <summary>
    /// Done after window_end (family local) — still counts as done, but not "ngày đẹp".
    /// </summary>
    public static bool IsLateDone(
        string status,
        DateTimeOffset? completedAt,
        TimeOnly? windowEnd,
        DateOnly flowDate,
        string? timezoneId,
        int onTimeGraceMinutes = 0)
    {
        if (status != FamilyCommitmentStatuses.Done || completedAt is null || windowEnd is null)
            return false;

        var local = TimeZoneInfo.ConvertTime(completedAt.Value, FamilyTimeZones.Resolve(timezoneId));
        var localDate = DateOnly.FromDateTime(local.DateTime);
        if (localDate > flowDate)
            return true;
        if (localDate < flowDate)
            return false;

        var grace = Math.Max(0, onTimeGraceMinutes);
        var effectiveEnd = windowEnd.Value.AddMinutes(grace);
        return TimeOnly.FromTimeSpan(local.TimeOfDay) > effectiveEnd;
    }
}

public static class FamilyTimeZones
{
    public static TimeZoneInfo Resolve(string? timezoneId)
    {
        var id = string.IsNullOrWhiteSpace(timezoneId) ? "Asia/Ho_Chi_Minh" : timezoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows often uses this display id for Vietnam.
            if (id is "Asia/Ho_Chi_Minh" or "Asia/Bangkok")
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
                catch (TimeZoneNotFoundException) { /* fall through */ }
            }

            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static DateTimeOffset NowIn(string? timezoneId)
    {
        var tz = Resolve(timezoneId);
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
    }

    /// <summary>IANA id for PostgreSQL AT TIME ZONE / timezone().</summary>
    public static string ToPostgresId(string? timezoneId)
    {
        var id = string.IsNullOrWhiteSpace(timezoneId) ? "Asia/Ho_Chi_Minh" : timezoneId.Trim();
        return id switch
        {
            "SE Asia Standard Time" => "Asia/Ho_Chi_Minh",
            _ => id,
        };
    }
}
