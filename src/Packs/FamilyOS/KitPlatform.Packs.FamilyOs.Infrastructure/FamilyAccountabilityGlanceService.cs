using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyAccountabilityGlanceService : IFamilyAccountabilityGlanceService
{
    private readonly FamilyAccountabilityGlanceRepository _repo;
    private readonly FamilyGraphRepository _families;

    public FamilyAccountabilityGlanceService(
        FamilyAccountabilityGlanceRepository repo,
        FamilyGraphRepository families)
    {
        _repo = repo;
        _families = families;
    }

    public async Task<AccountabilityGlanceDto> GetGlanceAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var (rangeFrom, rangeTo) = ResolveRange(today, from, to);

        // Streak may need days before the week window — load a bit more history
        var historyFrom = rangeFrom.AddDays(-30);
        if (historyFrom > rangeFrom)
            historyFrom = rangeFrom;

        var aggs = await _repo.ListDayAggregatesAsync(
            familyId, historyFrom, rangeTo, family.Timezone, cancellationToken);
        var byDate = aggs.ToDictionary(a => a.FlowDate);

        var days = new List<AccountabilityDayGlanceDto>();
        for (var d = rangeFrom; d <= rangeTo; d = d.AddDays(1))
            days.Add(MapDay(d, byDate.GetValueOrDefault(d)));

        var todayGlance = MapDay(today, byDate.GetValueOrDefault(today));
        var streak = ComputeStreak(today, byDate);

        return new AccountabilityGlanceDto(
            rangeFrom,
            rangeTo,
            today,
            todayGlance.IsBeautifulDay,
            streak,
            days);
    }

    private static (DateOnly From, DateOnly To) ResolveRange(
        DateOnly today,
        DateOnly? from,
        DateOnly? to)
    {
        if (from is null && to is null)
        {
            // ISO week Mon–Sun
            var offset = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
            var monday = today.AddDays(-offset);
            return (monday, monday.AddDays(6));
        }

        var start = from ?? to!.Value.AddDays(-6);
        var end = to ?? from!.Value.AddDays(6);
        if (end < start)
            (start, end) = (end, start);

        if (end.DayNumber - start.DayNumber > 31)
            throw new InvalidOperationException("Khoảng glance tối đa 31 ngày.");

        return (start, end);
    }

    private static AccountabilityDayGlanceDto MapDay(
        DateOnly date,
        FamilyAccountabilityGlanceRepository.DayAggRow? row)
    {
        if (row is null || row.ChildTotal <= 0)
        {
            return new AccountabilityDayGlanceDto(
                date,
                IsScored: false,
                IsBeautifulDay: false,
                ChildDone: 0,
                ChildSkipped: 0,
                ChildOpen: 0,
                ChildLateDone: 0,
                AppliedConsequences: 0);
        }

        // Ngày đẹp: hết việc con (done đúng giờ hoặc skipped), không hậu quả applied, không late-done
        var beautiful = row.ChildOpen == 0
            && row.AppliedConsequences == 0
            && row.ChildLateDone == 0;

        return new AccountabilityDayGlanceDto(
            date,
            IsScored: true,
            IsBeautifulDay: beautiful,
            row.ChildDone,
            row.ChildSkipped,
            row.ChildOpen,
            row.ChildLateDone,
            row.AppliedConsequences);
    }

    private static int ComputeStreak(
        DateOnly today,
        IReadOnlyDictionary<DateOnly, FamilyAccountabilityGlanceRepository.DayAggRow> byDate)
    {
        var streak = 0;
        for (var d = today; d >= today.AddDays(-60); d = d.AddDays(-1))
        {
            var glance = MapDay(d, byDate.GetValueOrDefault(d));
            if (!glance.IsScored)
            {
                // Today unscored (still in progress / no child tasks) — skip without breaking
                if (d == today)
                    continue;
                // Past unscored days also skip (weekend without child routine)
                continue;
            }

            if (!glance.IsBeautifulDay)
                break;

            streak++;
        }

        return streak;
    }
}
