using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyChildRequestRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyChildRequestRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectColumns = """
        r.id AS Id,
        r.family_id AS FamilyId,
        r.member_id AS MemberId,
        m.display_name AS MemberName,
        r.flow_date AS FlowDate,
        r.kind AS Kind,
        r.amount_minutes AS AmountMinutes,
        r.title_vi AS TitleVi,
        r.window_start AS WindowStart,
        r.window_end AS WindowEnd,
        r.reason_codes AS ReasonCodes,
        r.reason_note AS ReasonNote,
        r.status AS Status,
        r.ai_summary_vi AS AiSummaryVi,
        r.ai_recommend AS AiRecommend,
        r.granted_minutes AS GrantedMinutes,
        r.created_at AS CreatedAt,
        r.decided_at AS DecidedAt
        """;

    public async Task<IReadOnlyList<ChildRequestRow>> ListAsync(
        Guid familyId,
        string? status,
        Guid? memberId,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ChildRequestRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.child_request r
            INNER JOIN pack_family.membership m
                ON m.tenant_id = r.tenant_id AND m.id = r.member_id AND m.deleted_at IS NULL
            WHERE r.tenant_id = @TenantId
              AND r.family_id = @FamilyId
              AND r.deleted_at IS NULL
              AND (@Status::text IS NULL OR r.status = @Status)
              AND (@MemberId::uuid IS NULL OR r.member_id = @MemberId)
            ORDER BY
              CASE WHEN r.status = 'pending' THEN 0 ELSE 1 END,
              r.created_at DESC
            LIMIT @Limit
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                Status = status,
                MemberId = memberId,
                Limit = Math.Clamp(limit, 1, 100),
            });
        return rows.AsList();
    }

    public async Task<ChildRequestRow?> GetAsync(
        Guid familyId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ChildRequestRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.child_request r
            INNER JOIN pack_family.membership m
                ON m.tenant_id = r.tenant_id AND m.id = r.member_id AND m.deleted_at IS NULL
            WHERE r.tenant_id = @TenantId
              AND r.family_id = @FamilyId
              AND r.id = @RequestId
              AND r.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, RequestId = requestId });
    }

    public async Task<int> CountPendingOrRecentWeekAsync(
        Guid familyId,
        Guid memberId,
        DateOnly weekStart,
        DateOnly weekEnd,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.child_request
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
              AND deleted_at IS NULL
              AND flow_date BETWEEN @WeekStart AND @WeekEnd
              AND status IN ('pending', 'approved', 'partial')
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
            });
    }

    public async Task<ChildRequestRow?> GetPendingTodayAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ChildRequestRow>(
            $"""
            SELECT {SelectColumns}
            FROM pack_family.child_request r
            INNER JOIN pack_family.membership m
                ON m.tenant_id = r.tenant_id AND m.id = r.member_id AND m.deleted_at IS NULL
            WHERE r.tenant_id = @TenantId
              AND r.family_id = @FamilyId
              AND r.member_id = @MemberId
              AND r.flow_date = @FlowDate
              AND r.status = 'pending'
              AND r.deleted_at IS NULL
            ORDER BY r.created_at DESC
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId, FlowDate = flowDate });
    }

    public async Task<Guid> InsertAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        string kind,
        int? amountMinutes,
        string? titleVi,
        TimeOnly? windowStart,
        TimeOnly? windowEnd,
        string[] reasonCodes,
        string? reasonNote,
        string aiSummaryVi,
        string aiRecommend,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.child_request (
                tenant_id, family_id, member_id, flow_date, kind,
                amount_minutes, title_vi, window_start, window_end,
                reason_codes, reason_note,
                status, ai_summary_vi, ai_recommend
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @FlowDate, @Kind,
                @AmountMinutes, @TitleVi, @WindowStart, @WindowEnd,
                @ReasonCodes, @ReasonNote,
                'pending', @AiSummaryVi, @AiRecommend
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                FlowDate = flowDate,
                Kind = kind,
                AmountMinutes = amountMinutes,
                TitleVi = titleVi,
                // Dapper/Npgsql: bind TIME as TimeSpan (same pattern as FamilyRoutineRepository).
                WindowStart = windowStart?.ToTimeSpan(),
                WindowEnd = windowEnd?.ToTimeSpan(),
                ReasonCodes = reasonCodes,
                ReasonNote = reasonNote,
                AiSummaryVi = aiSummaryVi,
                AiRecommend = aiRecommend,
            });
    }

    public async Task DecideAsync(
        Guid familyId,
        Guid requestId,
        string status,
        Guid decidedBy,
        int? grantedMinutes,
        string? decisionNote,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.child_request
            SET status = @Status,
                decided_by = @DecidedBy,
                decided_at = NOW(),
                granted_minutes = @GrantedMinutes,
                decision_note = @DecisionNote,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @RequestId
              AND deleted_at IS NULL
              AND status = 'pending'
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                RequestId = requestId,
                Status = status,
                DecidedBy = decidedBy,
                GrantedMinutes = grantedMinutes,
                DecisionNote = decisionNote,
            });
    }

    internal sealed class ChildRequestRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = "";
        public DateOnly FlowDate { get; init; }
        public string Kind { get; init; } = "";
        public int? AmountMinutes { get; init; }
        public string? TitleVi { get; init; }
        public TimeOnly? WindowStart { get; init; }
        public TimeOnly? WindowEnd { get; init; }
        public string[]? ReasonCodes { get; init; }
        public string? ReasonNote { get; init; }
        public string Status { get; init; } = "";
        public string? AiSummaryVi { get; init; }
        public string? AiRecommend { get; init; }
        public int? GrantedMinutes { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? DecidedAt { get; init; }
    }
}
