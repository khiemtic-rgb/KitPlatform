using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyCalendarPeriodRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyCalendarPeriodRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<bool> FamilyExistsAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM pack_family.family
                WHERE tenant_id = @TenantId AND id = @FamilyId AND deleted_at IS NULL
            )
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task<bool> RoutineExistsAsync(
        Guid familyId,
        Guid routineId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM pack_family.routine
                WHERE tenant_id = @TenantId AND family_id = @FamilyId AND id = @RoutineId
                  AND deleted_at IS NULL AND is_active
            )
            """,
            new { TenantId, FamilyId = familyId, RoutineId = routineId });
    }

    public async Task<IReadOnlyList<PeriodRow>> ListPeriodsAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<PeriodRow>(
            """
            SELECT id AS Id, family_id AS FamilyId, code AS Code, display_name AS DisplayName,
                   kind AS Kind, start_date AS StartDate, end_date AS EndDate,
                   priority AS Priority, is_active AS IsActive, notes AS Notes
            FROM pack_family.calendar_period
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND deleted_at IS NULL
            ORDER BY start_date DESC, priority DESC, created_at
            """,
            new { TenantId, FamilyId = familyId });
        return rows.AsList();
    }

    public async Task<PeriodRow?> GetPeriodAsync(
        Guid familyId,
        Guid periodId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PeriodRow>(
            """
            SELECT id AS Id, family_id AS FamilyId, code AS Code, display_name AS DisplayName,
                   kind AS Kind, start_date AS StartDate, end_date AS EndDate,
                   priority AS Priority, is_active AS IsActive, notes AS Notes
            FROM pack_family.calendar_period
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND id = @PeriodId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, PeriodId = periodId });
    }

    public async Task<IReadOnlyList<SlotRow>> ListSlotsAsync(
        Guid periodId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<SlotRow>(
            """
            SELECT s.id AS Id, s.period_id AS PeriodId, s.weekdays::int[] AS Weekdays,
                   s.routine_id AS RoutineId, r.display_name AS RoutineDisplayName,
                   s.sort_order AS SortOrder
            FROM pack_family.calendar_period_slot s
            JOIN pack_family.routine r ON r.id = s.routine_id AND r.deleted_at IS NULL
            WHERE s.tenant_id = @TenantId AND s.period_id = @PeriodId AND s.deleted_at IS NULL
            ORDER BY s.sort_order, s.created_at
            """,
            new { TenantId, PeriodId = periodId });
        return rows.AsList();
    }

    public async Task<IReadOnlyList<SlotRow>> ListSlotsForPeriodsAsync(
        IReadOnlyList<Guid> periodIds,
        CancellationToken cancellationToken)
    {
        if (periodIds.Count == 0) return [];
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<SlotRow>(
            """
            SELECT s.id AS Id, s.period_id AS PeriodId, s.weekdays::int[] AS Weekdays,
                   s.routine_id AS RoutineId, r.display_name AS RoutineDisplayName,
                   s.sort_order AS SortOrder
            FROM pack_family.calendar_period_slot s
            JOIN pack_family.routine r ON r.id = s.routine_id AND r.deleted_at IS NULL
            WHERE s.tenant_id = @TenantId
              AND s.period_id = ANY(@PeriodIds)
              AND s.deleted_at IS NULL
            ORDER BY s.period_id, s.sort_order, s.created_at
            """,
            new { TenantId, PeriodIds = periodIds.ToArray() });
        return rows.AsList();
    }

    public async Task<Guid> InsertPeriodAsync(
        Guid familyId,
        string code,
        string displayName,
        string kind,
        DateOnly startDate,
        DateOnly endDate,
        int priority,
        bool isActive,
        string? notes,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.calendar_period (
                tenant_id, family_id, code, display_name, kind,
                start_date, end_date, priority, is_active, notes
            )
            VALUES (
                @TenantId, @FamilyId, @Code, @DisplayName, @Kind,
                @StartDate, @EndDate, @Priority, @IsActive, @Notes
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                Code = code,
                DisplayName = displayName,
                Kind = kind,
                StartDate = startDate,
                EndDate = endDate,
                Priority = priority,
                IsActive = isActive,
                Notes = notes,
            });
    }

    public async Task UpdatePeriodAsync(
        Guid familyId,
        Guid periodId,
        string displayName,
        string kind,
        DateOnly startDate,
        DateOnly endDate,
        int priority,
        bool isActive,
        string? notes,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.calendar_period
            SET display_name = @DisplayName,
                kind = @Kind,
                start_date = @StartDate,
                end_date = @EndDate,
                priority = @Priority,
                is_active = @IsActive,
                notes = @Notes,
                updated_at = NOW()
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND id = @PeriodId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                PeriodId = periodId,
                DisplayName = displayName,
                Kind = kind,
                StartDate = startDate,
                EndDate = endDate,
                Priority = priority,
                IsActive = isActive,
                Notes = notes,
            });
    }

    public async Task SoftDeletePeriodAsync(
        Guid familyId,
        Guid periodId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.calendar_period_slot
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @TenantId AND period_id = @PeriodId AND deleted_at IS NULL
            """,
            new { TenantId, PeriodId = periodId },
            tx);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.calendar_period
            SET deleted_at = NOW(), is_active = FALSE, updated_at = NOW()
            WHERE tenant_id = @TenantId AND family_id = @FamilyId AND id = @PeriodId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, PeriodId = periodId },
            tx);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task ReplaceSlotsAsync(
        Guid periodId,
        IReadOnlyList<(IReadOnlyList<int> Weekdays, Guid RoutineId, int SortOrder)> slots,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await conn.ExecuteAsync(
            """
            UPDATE pack_family.calendar_period_slot
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @TenantId AND period_id = @PeriodId AND deleted_at IS NULL
            """,
            new { TenantId, PeriodId = periodId },
            tx);

        foreach (var slot in slots)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.calendar_period_slot (
                    tenant_id, period_id, weekdays, routine_id, sort_order
                )
                VALUES (
                    @TenantId, @PeriodId, @Weekdays, @RoutineId, @SortOrder
                )
                """,
                new
                {
                    TenantId,
                    PeriodId = periodId,
                    Weekdays = slot.Weekdays.ToArray(),
                    RoutineId = slot.RoutineId,
                    SortOrder = slot.SortOrder,
                },
                tx);
        }

        await tx.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Highest-priority active period covering the date that has a slot for the ISO weekday.
    /// Tie-break: shorter range first, then lower sort_order.
    /// </summary>
    public async Task<PeriodPickRow?> PickPeriodRoutineAsync(
        Guid familyId,
        DateOnly flowDate,
        short isoDow,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PeriodPickRow>(
            """
            SELECT
                p.id AS PeriodId,
                p.display_name AS PeriodDisplayName,
                p.kind AS PeriodKind,
                r.id AS RoutineId,
                r.display_name AS RoutineDisplayName
            FROM pack_family.calendar_period p
            JOIN pack_family.calendar_period_slot s
              ON s.period_id = p.id
             AND s.tenant_id = p.tenant_id
             AND s.deleted_at IS NULL
             AND @Dow = ANY (s.weekdays)
            JOIN pack_family.routine r
              ON r.id = s.routine_id
             AND r.tenant_id = p.tenant_id
             AND r.deleted_at IS NULL
             AND r.is_active
            WHERE p.tenant_id = @TenantId
              AND p.family_id = @FamilyId
              AND p.deleted_at IS NULL
              AND p.is_active
              AND @FlowDate BETWEEN p.start_date AND p.end_date
            ORDER BY
                p.priority DESC,
                (p.end_date - p.start_date) ASC,
                s.sort_order ASC,
                p.created_at ASC
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, FlowDate = flowDate, Dow = isoDow });
    }

    internal sealed class PeriodRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public string Code { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Kind { get; init; } = "";
        public DateOnly StartDate { get; init; }
        public DateOnly EndDate { get; init; }
        public int Priority { get; init; }
        public bool IsActive { get; init; }
        public string? Notes { get; init; }
    }

    internal sealed class SlotRow
    {
        public Guid Id { get; init; }
        public Guid PeriodId { get; init; }
        public int[] Weekdays { get; init; } = [];
        public Guid RoutineId { get; init; }
        public string? RoutineDisplayName { get; init; }
        public int SortOrder { get; init; }
    }

    internal sealed class PeriodPickRow
    {
        public Guid PeriodId { get; init; }
        public string PeriodDisplayName { get; init; } = "";
        public string PeriodKind { get; init; } = "";
        public Guid RoutineId { get; init; }
        public string RoutineDisplayName { get; init; } = "";
    }
}
