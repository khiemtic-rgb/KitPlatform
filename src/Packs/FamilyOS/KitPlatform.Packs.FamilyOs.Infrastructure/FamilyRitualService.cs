using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyRitualService : IFamilyRitualService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyMemoryService _memories;

    public FamilyRitualService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        FamilyGraphRepository families,
        IFamilyMemoryService memories)
    {
        _db = db;
        _tenant = tenant;
        _families = families;
        _memories = memories;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<FamilyRitualDto>> ListWeekAsync(
        Guid familyId,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = asOf ?? today;
        var periodStart = StartOfWeekMonday(date);

        await EnsureDefaultsAsync(familyId, cancellationToken);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rituals = (await conn.QueryAsync<(string Code, string LabelVi, string Cadence)>(
            """
            SELECT code AS Code, label_vi AS LabelVi, cadence AS Cadence
            FROM pack_family.family_ritual
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND is_active
            ORDER BY sort_order, code
            """,
            new { TenantId, FamilyId = familyId })).AsList();

        var checkins = (await conn.QueryAsync<(string RitualCode, DateTimeOffset DoneAt)>(
            """
            SELECT ritual_code AS RitualCode, done_at AS DoneAt
            FROM pack_family.ritual_checkin
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND period_start = @PeriodStart
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, PeriodStart = periodStart })).AsList();

        var map = checkins.ToDictionary(
            c => c.RitualCode,
            c => c.DoneAt,
            StringComparer.OrdinalIgnoreCase);

        return rituals.Select(r =>
        {
            map.TryGetValue(r.Code, out var doneAt);
            return new FamilyRitualDto(
                r.Code,
                r.LabelVi,
                r.Cadence,
                doneAt != default,
                periodStart,
                doneAt == default ? null : doneAt);
        }).ToList();
    }

    public async Task<FamilyRitualDto> CheckinAsync(
        Guid familyId,
        FamilyRitualCheckinRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var code = (request.RitualCode ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code))
            throw new InvalidOperationException("ritualCode bắt buộc.");

        await EnsureDefaultsAsync(familyId, cancellationToken);

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var periodStart = StartOfWeekMonday(today);
        var note = string.IsNullOrWhiteSpace(request.NoteVi) ? null : request.NoteVi.Trim();

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var label = await conn.ExecuteScalarAsync<string?>(
            """
            SELECT label_vi
            FROM pack_family.family_ritual
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND code = @Code
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, Code = code });
        if (string.IsNullOrEmpty(label))
            throw new InvalidOperationException("Không tìm thấy ritual.");

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.ritual_checkin (
                tenant_id, family_id, ritual_code, period_start, noted_by, note_vi
            )
            VALUES (
                @TenantId, @FamilyId, @Code, @PeriodStart, @NotedBy, @NoteVi
            )
            ON CONFLICT (tenant_id, family_id, ritual_code, period_start)
            DO UPDATE SET
                done_at = NOW(),
                noted_by = COALESCE(EXCLUDED.noted_by, pack_family.ritual_checkin.noted_by),
                note_vi = COALESCE(EXCLUDED.note_vi, pack_family.ritual_checkin.note_vi),
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                Code = code,
                PeriodStart = periodStart,
                NotedBy = request.NotedBy,
                NoteVi = note,
            });

        if (code is FamilyRitualCodes.DinnerTogether or FamilyRitualCodes.ThanksEachOther
            or FamilyRitualCodes.SharedChore)
        {
            try
            {
                await _memories.TryCaptureAsync(
                    TenantId,
                    familyId,
                    today,
                    FamilyMemoryKinds.ParentHabit,
                    label!,
                    noteVi: "Ritual tuần này — bố mẹ cùng giữ nhịp nhà.",
                    icon: code == FamilyRitualCodes.DinnerTogether ? "🍽️"
                        : code == FamilyRitualCodes.ThanksEachOther ? "🙏"
                        : "🧹",
                    sourceRef: $"ritual:{code}:{periodStart:yyyy-MM-dd}",
                    memberId: request.NotedBy,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Best-effort.
            }
        }

        var list = await ListWeekAsync(familyId, today, cancellationToken);
        return list.First(r => r.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureDefaultsAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        foreach (var (code, label, sort) in FamilyRitualCodes.Defaults)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.family_ritual (
                    tenant_id, family_id, code, label_vi, cadence, sort_order
                )
                VALUES (
                    @TenantId, @FamilyId, @Code, @LabelVi, 'weekly', @Sort
                )
                ON CONFLICT (tenant_id, family_id, code) DO NOTHING
                """,
                new
                {
                    TenantId,
                    FamilyId = familyId,
                    Code = code,
                    LabelVi = label,
                    Sort = sort,
                });
        }
    }

    private static DateOnly StartOfWeekMonday(DateOnly date)
    {
        var dow = (int)date.DayOfWeek; // Sunday=0
        var offset = dow == 0 ? 6 : dow - 1;
        return date.AddDays(-offset);
    }
}
