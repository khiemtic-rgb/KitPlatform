using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using Npgsql;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyMemoryRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyMemoryRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectColumns = """
        m.id AS Id,
        m.family_id AS FamilyId,
        m.member_id AS MemberId,
        mb.display_name AS MemberName,
        m.flow_date AS FlowDate,
        m.kind AS Kind,
        m.title_vi AS TitleVi,
        m.note_vi AS NoteVi,
        m.icon AS Icon,
        m.photo_url AS PhotoUrl,
        m.is_favorite AS IsFavorite,
        m.happened_at AS HappenedAt
        """;

    private const string FromJoin = """
        FROM pack_family.family_memory m
        LEFT JOIN pack_family.membership mb
            ON mb.tenant_id = m.tenant_id
           AND mb.id = m.member_id
           AND mb.deleted_at IS NULL
        """;

    public async Task<IReadOnlyList<MemoryRow>> ListAsync(
        Guid familyId,
        DateOnly? from,
        DateOnly? to,
        bool favoritesOnly,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<MemoryRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE m.tenant_id = @TenantId
              AND m.family_id = @FamilyId
              AND m.deleted_at IS NULL
              AND (@From::date IS NULL OR m.flow_date >= @From::date)
              AND (@To::date IS NULL OR m.flow_date <= @To::date)
              AND (NOT @FavoritesOnly OR m.is_favorite)
            ORDER BY m.happened_at DESC
            LIMIT @Limit
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                From = from,
                To = to,
                FavoritesOnly = favoritesOnly,
                Limit = limit,
            });
        return rows.AsList();
    }

    public async Task<MemoryRow?> GetAsync(
        Guid familyId,
        Guid memoryId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<MemoryRow>(
            $"""
            SELECT {SelectColumns}
            {FromJoin}
            WHERE m.tenant_id = @TenantId
              AND m.family_id = @FamilyId
              AND m.id = @MemoryId
              AND m.deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MemoryId = memoryId });
    }

    public async Task<Guid> InsertAsync(
        Guid familyId,
        Guid? memberId,
        DateOnly flowDate,
        string kind,
        string titleVi,
        string? noteVi,
        string? icon,
        string? photoUrl,
        string? sourceRef,
        DateTimeOffset? happenedAt,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.family_memory (
                tenant_id, family_id, member_id, flow_date, kind,
                title_vi, note_vi, icon, photo_url, source_ref, happened_at
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @FlowDate, @Kind,
                @TitleVi, @NoteVi, @Icon, @PhotoUrl, @SourceRef, COALESCE(@HappenedAt, NOW())
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
                TitleVi = titleVi,
                NoteVi = noteVi,
                Icon = icon,
                PhotoUrl = photoUrl,
                SourceRef = sourceRef,
                HappenedAt = happenedAt,
            });
    }

    /// <summary>Insert with explicit tenant (worker / event hooks outside a request scope).</summary>
    public async Task<bool> TryInsertForTenantAsync(
        Guid tenantId,
        Guid familyId,
        Guid? memberId,
        DateOnly flowDate,
        string kind,
        string titleVi,
        string? noteVi,
        string? icon,
        string? photoUrl,
        string? sourceRef,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        try
        {
            var n = await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.family_memory (
                    tenant_id, family_id, member_id, flow_date, kind,
                    title_vi, note_vi, icon, photo_url, source_ref
                )
                VALUES (
                    @TenantId, @FamilyId, @MemberId, @FlowDate, @Kind,
                    @TitleVi, @NoteVi, @Icon, @PhotoUrl, @SourceRef
                )
                ON CONFLICT DO NOTHING
                """,
                new
                {
                    TenantId = tenantId,
                    FamilyId = familyId,
                    MemberId = memberId,
                    FlowDate = flowDate,
                    Kind = kind,
                    TitleVi = titleVi,
                    NoteVi = noteVi,
                    Icon = icon,
                    PhotoUrl = photoUrl,
                    SourceRef = sourceRef,
                });
            return n > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return false;
        }
    }

    public async Task<bool> SetFavoriteAsync(
        Guid familyId,
        Guid memoryId,
        bool isFavorite,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_memory
            SET is_favorite = @IsFavorite, updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @MemoryId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MemoryId = memoryId, IsFavorite = isFavorite });
        return n > 0;
    }

    public async Task<bool> SoftDeleteAsync(
        Guid familyId,
        Guid memoryId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_memory
            SET deleted_at = NOW(), updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @MemoryId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, MemoryId = memoryId });
        return n > 0;
    }

    public async Task<RecapAggRow> GetRecapAggregateAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<RecapAggRow>(
            """
            SELECT
                COUNT(*)::int AS TotalCount,
                COUNT(*) FILTER (WHERE kind = 'beautiful_day')::int AS BeautifulDays,
                COUNT(*) FILTER (WHERE kind = 'gratitude')::int AS GratitudeCount,
                COUNT(*) FILTER (WHERE photo_url IS NOT NULL)::int AS PhotoCount,
                COUNT(*) FILTER (WHERE kind IN ('team_unlock', 'reward'))::int AS CelebrationCount
            FROM pack_family.family_memory
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND flow_date BETWEEN @From AND @To
            """,
            new { TenantId, FamilyId = familyId, From = from, To = to });
    }

    /// <summary>Longest run of consecutive beautiful-day memories inside the window.</summary>
    public async Task<int> GetBestStreakAsync(
        Guid familyId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var dates = await conn.QueryAsync<DateOnly>(
            """
            SELECT DISTINCT flow_date
            FROM pack_family.family_memory
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND kind = 'beautiful_day'
              AND flow_date BETWEEN @From AND @To
            ORDER BY flow_date
            """,
            new { TenantId, FamilyId = familyId, From = from, To = to });

        var best = 0;
        var run = 0;
        DateOnly? prev = null;
        foreach (var d in dates)
        {
            run = prev is DateOnly p && p.AddDays(1) == d ? run + 1 : 1;
            prev = d;
            if (run > best)
                best = run;
        }

        return best;
    }

    internal sealed class MemoryRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid? MemberId { get; init; }
        public string? MemberName { get; init; }
        public DateOnly FlowDate { get; init; }
        public string Kind { get; init; } = "";
        public string TitleVi { get; init; } = "";
        public string? NoteVi { get; init; }
        public string? Icon { get; init; }
        public string? PhotoUrl { get; init; }
        public bool IsFavorite { get; init; }
        public DateTimeOffset HappenedAt { get; init; }
    }

    internal sealed class RecapAggRow
    {
        public int TotalCount { get; init; }
        public int BeautifulDays { get; init; }
        public int GratitudeCount { get; init; }
        public int PhotoCount { get; init; }
        public int CelebrationCount { get; init; }
    }
}
