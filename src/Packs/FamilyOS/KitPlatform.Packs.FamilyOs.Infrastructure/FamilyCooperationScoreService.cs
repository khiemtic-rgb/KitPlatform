using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyCooperationScoreService : IFamilyCooperationScoreService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyTeamUnlockService _team;
    private readonly FamilyTeamNudgeRepository _nudges;
    private readonly IFamilyAccountabilityGlanceService _glance;

    public FamilyCooperationScoreService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        FamilyGraphRepository families,
        IFamilyTeamUnlockService team,
        FamilyTeamNudgeRepository nudges,
        IFamilyAccountabilityGlanceService glance)
    {
        _db = db;
        _tenant = tenant;
        _families = families;
        _team = team;
        _nudges = nudges;
        _glance = glance;
    }

    public async Task<FamilyCooperationScoreDto> GetAsync(
        Guid familyId,
        string? period = "week",
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var periodKey = string.IsNullOrWhiteSpace(period) ? "week" : period.Trim().ToLowerInvariant();
        var days = periodKey == "month" ? 30 : 7;
        var from = today.AddDays(-(days - 1));

        var glance = await _glance.GetGlanceAsync(familyId, from, today, cancellationToken);
        var streak = glance.CurrentStreak;
        var beautifulDays = glance.Days.Count(d => d.IsBeautifulDay);

        var teamCompleteDays = 0;
        var missionDays = 0;
        var openOrOverdue = 0;
        var sparkline = new List<FamilyCooperationDayPointDto>();

        var sent = await _nudges.CountSentInRangeAsync(familyId, from, today, cancellationToken);
        var thanks = await _nudges.CountThanksInRangeAsync(familyId, from, today, cancellationToken);
        var unlocks = await CountUnlocksConfirmedAsync(familyId, from, today, cancellationToken);

        for (var d = from; d <= today; d = d.AddDays(1))
        {
            var team = await _team.GetTeamDayAsync(familyId, d, cancellationToken);
            if (team.TeamTotal > 0)
            {
                missionDays++;
                if (team.TeamComplete) teamCompleteDays++;
                openOrOverdue += team.RemainingMissions;
            }

            var dayBeautiful = glance.Days.Any(x => x.Date == d && x.IsBeautifulDay) ? 1 : 0;
            var dayPillars = ComputePillars(
                teamCompleteDays: team.TeamComplete && team.TeamTotal > 0 ? 1 : 0,
                missionDays: team.TeamTotal > 0 ? 1 : 0,
                streak,
                beautifulDays: dayBeautiful,
                helpSent: 0,
                helpThanks: 0,
                unlockConfirmed: 0,
                openOrOverdue: team.RemainingMissions,
                periodDays: 1);

            var dayTotal = WeightedTotal(dayPillars);
            sparkline.Add(new FamilyCooperationDayPointDto(d, dayTotal));
            await UpsertCacheAsync(familyId, d, dayPillars, dayTotal, cancellationToken);
        }

        var pillars = ComputePillars(
            teamCompleteDays,
            missionDays,
            streak,
            beautifulDays,
            sent,
            thanks,
            unlocks,
            openOrOverdue,
            days);

        var total = WeightedTotal(pillars);
        var headline = total >= 80
            ? $"Tuần này nhà hợp tác {total}/100 — cả đội đang ăn ý."
            : total >= 50
                ? $"Tuần này nhà hợp tác {total}/100 — đang cùng nhau tiến."
                : $"Tuần này nhà hợp tác {total}/100 — còn chỗ để sát cánh hơn.";

        return new FamilyCooperationScoreDto(
            periodKey,
            from,
            today,
            total,
            headline,
            pillars,
            sparkline);
    }

    private static FamilyCooperationPillarsDto ComputePillars(
        int teamCompleteDays,
        int missionDays,
        int streak,
        int beautifulDays,
        int helpSent,
        int helpThanks,
        int unlockConfirmed,
        int openOrOverdue,
        int periodDays)
    {
        var teamCompletion = missionDays > 0
            ? (int)Math.Round(100.0 * teamCompleteDays / missionDays)
            : 0;

        var familyStreak = Math.Min(100, streak * 8 + Math.Min(20, beautifulDays * 5));

        var helpRaw = helpSent * 12 + helpThanks * 20;
        var helpEachOther = Math.Min(100, helpRaw);

        var teamUnlock = Math.Min(100, unlockConfirmed * 35);

        var pressure = openOrOverdue;
        var familyHarmony = Math.Max(0, 100 - pressure * 8 - (pressure > periodDays * 2 ? 10 : 0));

        return new FamilyCooperationPillarsDto(
            teamCompletion,
            familyStreak,
            helpEachOther,
            teamUnlock,
            familyHarmony);
    }

    private static int WeightedTotal(FamilyCooperationPillarsDto p) =>
        Math.Max(0, Math.Min(100, (int)Math.Round(
            p.TeamCompletion * 0.35 +
            p.FamilyStreak * 0.25 +
            p.HelpEachOther * 0.2 +
            p.TeamUnlock * 0.1 +
            p.FamilyHarmony * 0.1)));

    private async Task<int> CountUnlocksConfirmedAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.team_unlock_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND flow_date >= @From
              AND flow_date <= @To
              AND status = 'confirmed'
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId = _tenant.TenantId,
                FamilyId = familyId,
                From = from,
                To = to,
            });
    }

    private async Task UpsertCacheAsync(
        Guid familyId,
        DateOnly scoreDate,
        FamilyCooperationPillarsDto pillars,
        int total,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(new
            {
                teamCompletion = pillars.TeamCompletion,
                familyStreak = pillars.FamilyStreak,
                helpEachOther = pillars.HelpEachOther,
                teamUnlock = pillars.TeamUnlock,
                familyHarmony = pillars.FamilyHarmony,
            });
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.cooperation_score_day (
                    tenant_id, family_id, score_date, pillars, total
                )
                VALUES (@TenantId, @FamilyId, @ScoreDate, @Pillars::jsonb, @Total)
                ON CONFLICT (tenant_id, family_id, score_date)
                DO UPDATE SET
                    pillars = EXCLUDED.pillars,
                    total = EXCLUDED.total,
                    updated_at = NOW(),
                    deleted_at = NULL
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    FamilyId = familyId,
                    ScoreDate = scoreDate,
                    Pillars = json,
                    Total = total,
                });
        }
        catch
        {
            // Cache is optional — never fail the read path.
        }
    }
}
