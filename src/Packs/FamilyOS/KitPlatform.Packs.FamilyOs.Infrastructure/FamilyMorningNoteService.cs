using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyMorningNoteService : IFamilyMorningNoteService
{
    private readonly IFamilyGraphService _families;
    private readonly IFamilyDayFlowService _dayFlows;
    private readonly IFamilyCommercialService _commercial;
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyMorningNoteService(
        IFamilyGraphService families,
        IFamilyDayFlowService dayFlows,
        IFamilyCommercialService commercial,
        IDbConnectionFactory db,
        ITenantContext tenant)
    {
        _families = families;
        _dayFlows = dayFlows;
        _commercial = commercial;
        _db = db;
        _tenant = tenant;
    }

    public async Task<FamilyMorningNoteDto> GetMorningNoteAsync(
        Guid familyId,
        Guid? memberId,
        DateOnly? flowDate,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureCapabilityAsync(familyId, FamilyCapabilityCodes.CoreRoutine, cancellationToken);

        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Khong tim thay gia dinh.");
        var localNow = FamilyTimeZones.NowIn(family.Timezone);
        var date = flowDate ?? DateOnly.FromDateTime(localNow.DateTime);

        var members = family.Members ?? Array.Empty<FamilyMembershipDto>();
        var child = memberId is Guid mid
            ? members.FirstOrDefault(m => m.Id == mid)
            : members.FirstOrDefault(m =>
                string.Equals(m.RoleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase)
                || string.Equals(m.RoleCode, "child", StringComparison.OrdinalIgnoreCase));
        var childId = child?.Id;
        var shortName = ShortName(child?.DisplayName);
        int? ageYears = null;
        if (child?.DateOfBirth is DateOnly dob)
        {
            ageYears = date.Year - dob.Year;
            if (date < dob.AddYears(ageYears.Value)) ageYears--;
        }

        var flow = await _dayFlows.GetDayFlowAsync(familyId, date, cancellationToken)
            ?? await _dayFlows.EnsureDayFlowAsync(familyId, new EnsureDayFlowRequest(date, null), cancellationToken);

        var commitments = flow.Commitments
            .Where(c => childId is null || c.MemberId is null || c.MemberId == childId)
            .Where(c => c.Status is not (FamilyCommitmentStatuses.Done or FamilyCommitmentStatuses.Skipped))
            .OrderBy(c => FamilyCommitmentReminder.SortRank(c.ReminderState, c.Status))
            .ThenBy(c => c.SortOrder)
            .ToList();
        var titles = commitments.Select(c => c.Title).Where(t => !string.IsNullOrWhiteSpace(t)).Take(6).ToList();
        var studyTitles = commitments
            .Where(c => FamilyCommitmentKinds.Normalize(c.CommitmentKind) == FamilyCommitmentKinds.StudyFocus)
            .Select(c => c.Title)
            .Take(4)
            .ToList();

        var from = date.AddDays(-7);
        var recentRate = await LoadStudyDoneRateAsync(familyId, childId, from, date, cancellationToken);
        var nudges = await LoadParentNudgeCountAsync(familyId, childId, from, date, cancellationToken);
        var streak = await LoadStreakAsync(familyId, childId, cancellationToken);

        var (body, tone) = FamilyMorningNoteComposer.Compose(new FamilyMorningNoteComposer.Input(
            shortName,
            ageYears,
            titles,
            studyTitles,
            recentRate,
            nudges,
            streak));

        return new FamilyMorningNoteDto(
            date,
            childId,
            body,
            tone,
            titles,
            ageYears ?? 0,
            FamilyMorningNoteComposer.AgeBand(ageYears),
            nudges,
            recentRate,
            IsTemplate: true);
    }

    private async Task<double> LoadStudyDoneRateAsync(
        Guid familyId, Guid? memberId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<(int Done, int Total)>(
            """
            SELECT
              COALESCE(SUM(CASE WHEN c.status = 'done' THEN 1 ELSE 0 END), 0)::int AS Done,
              COALESCE(COUNT(*), 0)::int AS Total
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d ON d.id = c.day_flow_id
            WHERE c.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND d.flow_date BETWEEN @From AND @To
              AND c.deleted_at IS NULL
              AND d.deleted_at IS NULL
              AND COALESCE(NULLIF(TRIM(c.commitment_kind), ''), 'chore') = 'study_focus'
              AND (@MemberId IS NULL OR c.member_id = @MemberId)
            """,
            new { TenantId = _tenant.TenantId, FamilyId = familyId, From = from, To = to, MemberId = memberId });
        if (row.Total <= 0) return 0.5;
        return (double)row.Done / row.Total;
    }

    private async Task<int> LoadParentNudgeCountAsync(
        Guid familyId, Guid? memberId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (memberId is null) return 0;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(COUNT(*), 0)::int
            FROM pack_family.behavior_event e
            WHERE e.tenant_id = @TenantId
              AND e.family_id = @FamilyId
              AND e.member_id = @MemberId
              AND e.event_type = 'parent_nudge'
              AND e.created_at::date BETWEEN @From AND @To
            """,
            new { TenantId = _tenant.TenantId, FamilyId = familyId, MemberId = memberId, From = from, To = to });
    }

    private Task<int> LoadStreakAsync(Guid familyId, Guid? memberId, CancellationToken ct)
    {
        // Soft pep-talk only; schema-safe default.
        return Task.FromResult(0);
    }

    private static string ShortName(string? displayName)
    {
        var t = (displayName ?? "").Trim();
        if (t.Length == 0) return "con";
        var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts[^1];
    }
}
