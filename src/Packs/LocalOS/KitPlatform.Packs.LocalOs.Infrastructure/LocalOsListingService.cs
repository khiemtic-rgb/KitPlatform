using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsListingService : ILocalOsListingService
{
    internal const string SelectColumns = """
        l.id AS Id, l.kind AS Kind, l.title AS Title, l.summary AS Summary,
        l.organization_name AS OrganizationName, l.place_text AS PlaceText,
        l.audience AS Audience, l.city_code AS CityCode,
        l.source_kind AS SourceKind, l.source_url AS SourceUrl,
        l.contact_phone AS ContactPhone, l.contact_name AS ContactName,
        l.salary_text AS SalaryText, l.working_time AS WorkingTime, l.employment_type AS EmploymentType,
        l.category AS Category, l.requirements AS Requirements,
        l.start_at AS StartAt, l.end_at AS EndAt, l.registration_url AS RegistrationUrl,
        l.price_month AS PriceMonth, l.room_type AS RoomType, l.trust AS Trust,
        l.safety_flag AS SafetyFlag, l.status AS Status,
        l.published_at AS PublishedAt, l.last_checked_at AS LastCheckedAt, l.expires_at AS ExpiresAt,
        l.source_id AS SourceId, s.name AS SourceName
        """;

    private readonly IDbConnectionFactory _db;

    public LocalOsListingService(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<LocalListingDto>> ListAsync(
        LocalListingQuery query,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var city = string.IsNullOrWhiteSpace(query.CityCode)
            ? LocalOsPackDefinition.DefaultCityCode
            : query.CityCode.Trim();
        var kind = string.IsNullOrWhiteSpace(query.Kind) ? null : query.Kind.Trim().ToLowerInvariant();
        var q = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim();
        var status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status.Trim().ToUpperInvariant();
        await ExpireOverdueAsync(conn, cancellationToken);

        var sql = $"""
            SELECT {SelectColumns}
            FROM pack_local.listing l
            LEFT JOIN pack_local.source s ON s.id = l.source_id
            WHERE l.city_code = @City
              AND (@Kind IS NULL OR l.kind = @Kind OR (@Kind = 'event' AND l.kind = 'grant'))
              AND (@Status IS NULL OR l.status = @Status)
              AND (@Q IS NULL OR l.title ILIKE @Like OR COALESCE(l.place_text, '') ILIKE @Like
                   OR COALESCE(l.organization_name, '') ILIKE @Like)
              AND (
                    @PublicOnly = FALSE
                    OR (
                        l.status = 'ACTIVE'
                        AND l.safety_flag = FALSE
                        AND (l.expires_at IS NULL OR l.expires_at > NOW())
                        AND (l.kind <> 'event' OR COALESCE(l.end_at, l.start_at) IS NULL
                             OR (timezone('Asia/Ho_Chi_Minh', COALESCE(l.end_at, l.start_at)))::date
                                >= (timezone('Asia/Ho_Chi_Minh', NOW()))::date)
                    )
                  )
            ORDER BY CASE WHEN l.status = 'NEEDS_REVIEW' THEN 0 ELSE 1 END,
                     COALESCE(l.last_checked_at, l.published_at, l.created_at) DESC
            LIMIT 2000
            """;

        var rows = await conn.QueryAsync<ListingRow>(
            new CommandDefinition(
                sql,
                new
                {
                    City = city,
                    Kind = kind,
                    Status = status,
                    Q = q,
                    Like = q is null ? null : $"%{q}%",
                    PublicOnly = query.PublicOnly,
                },
                cancellationToken: cancellationToken));
        var list = rows.Select(Map).ToList();
        if (!query.PublicOnly)
            return list;
        return list
            .Where(x => !LocalOsEventDate.IsPastListing(
                x.Kind, x.StartAt, x.EndAt, x.Title, x.Summary, x.WorkingTime))
            .ToList();
    }

    public async Task<LocalListingDto?> GetAsync(
        Guid id,
        bool publicOnly,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<ListingRow>(
            new CommandDefinition(
                $"""
                SELECT {SelectColumns}
                FROM pack_local.listing l
                LEFT JOIN pack_local.source s ON s.id = l.source_id
                WHERE l.id = @Id
                  AND (
                        @PublicOnly = FALSE
                        OR (
                            l.safety_flag = FALSE
                            AND l.published_at IS NOT NULL
                            AND l.status IN ('ACTIVE', 'EXPIRED')
                        )
                      )
                """,
                new { Id = id, PublicOnly = publicOnly },
                cancellationToken: cancellationToken));
        if (row is null)
            return null;
        var mapped = Map(row);
        mapped = await RefreshThinLeadAsync(mapped, cancellationToken);
        if (publicOnly && LocalOsEventDate.IsPastListing(
                mapped.Kind, mapped.StartAt, mapped.EndAt, mapped.Title, mapped.Summary, mapped.WorkingTime))
            return null;
        return mapped;
    }

    public async Task<LocalListingDto> CreateAsync(
        UpsertLocalListingRequest request,
        CancellationToken cancellationToken = default)
    {
        var sourceUrl = (request.SourceUrl ?? "").Trim();
        var skipDup = sourceUrl.StartsWith("content://kit-mkt/", StringComparison.OrdinalIgnoreCase);
        if (!skipDup)
        {
            var dup = await FindDuplicateAsync(
                request.Kind, request.Title, request.PlaceText, request.ContactPhone,
                request.Summary, request.SourceUrl, excludeId: null, onlyActive: false, cancellationToken);
            if (dup is not null)
                throw new InvalidOperationException("Tin này đã có trong danh sách. Không thêm trùng.");
        }

        var id = Guid.CreateVersion7();
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_local.listing (
                    id, kind, title, summary, organization_name, place_text, audience, city_code,
                    source_kind, source_url, source_id, contact_phone, contact_name, salary_text, working_time,
                    employment_type, category, requirements,
                    start_at, end_at, registration_url, price_month, room_type, trust, safety_flag,
                    status, published_at, last_checked_at, expires_at
                ) VALUES (
                    @Id, @Kind, @Title, @Summary, @OrganizationName, @PlaceText, @Audience, @CityCode,
                    @SourceKind, @SourceUrl, @SourceId, @ContactPhone, @ContactName, @SalaryText, @WorkingTime,
                    @EmploymentType, @Category, @Requirements,
                    @StartAt, @EndAt, @RegistrationUrl, @PriceMonth, @RoomType, @Trust, @SafetyFlag,
                    @Status, CASE WHEN @Status = 'ACTIVE' THEN NOW() ELSE NULL END, NOW(),
                    CASE WHEN @Kind = 'article' THEN NULL
                         ELSE NOW() + CASE WHEN @Kind = 'event' THEN INTERVAL '30 days' ELSE INTERVAL '14 days' END
                    END
                )
                """,
                Bind(id, request),
                cancellationToken: cancellationToken));
        return (await GetAsync(id, publicOnly: false, cancellationToken))!;
    }

    public async Task<LocalListingDto?> UpdateAsync(
        Guid id,
        UpsertLocalListingRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_local.listing SET
                    kind = @Kind, title = @Title, summary = @Summary,
                    organization_name = @OrganizationName, place_text = @PlaceText,
                    audience = @Audience, city_code = @CityCode,
                    source_kind = @SourceKind, source_url = @SourceUrl,
                    source_id = COALESCE(@SourceId, source_id),
                    contact_phone = @ContactPhone, contact_name = @ContactName,
                    salary_text = @SalaryText, working_time = @WorkingTime, employment_type = @EmploymentType,
                    category = @Category, requirements = @Requirements,
                    start_at = @StartAt, end_at = @EndAt, registration_url = @RegistrationUrl,
                    price_month = @PriceMonth, room_type = @RoomType,
                    trust = @Trust, safety_flag = @SafetyFlag, status = @Status,
                    last_checked_at = NOW(), updated_at = NOW(),
                    expires_at = CASE WHEN @Kind = 'article' THEN NULL ELSE expires_at END
                WHERE id = @Id
                """,
                Bind(id, request),
                cancellationToken: cancellationToken));
        if (n == 0) return null;
        return await GetAsync(id, publicOnly: false, cancellationToken);
    }

    public async Task<LocalListingDto?> SetStatusAsync(
        Guid id,
        string status,
        CancellationToken cancellationToken = default)
    {
        var next = status.Trim().ToUpperInvariant();
        if (next is not ("ACTIVE" or "NEEDS_REVIEW" or "EXPIRED" or "HIDDEN"))
            next = "NEEDS_REVIEW";
        if (next == "ACTIVE")
        {
            var self = await GetAsync(id, publicOnly: false, cancellationToken);
            if (self is not null)
            {
                var dup = await FindDuplicateAsync(
                    self.Kind, self.Title, self.PlaceText, self.ContactPhone,
                    self.Summary, self.SourceUrl, excludeId: id, onlyActive: true, cancellationToken);
                if (dup is not null)
                    throw new InvalidOperationException("Tin trùng với tin đang đăng. Không đăng lại.");
            }
        }
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_local.listing SET
                    status = @Status,
                    published_at = CASE
                        WHEN @Status = 'ACTIVE' THEN COALESCE(published_at, NOW())
                        ELSE published_at
                    END,
                    last_checked_at = NOW(),
                    updated_at = NOW()
                WHERE id = @Id
                """,
                new { Id = id, Status = next },
                cancellationToken: cancellationToken));
        if (n == 0) return null;
        return await GetAsync(id, publicOnly: false, cancellationToken);
    }

    public async Task<LocalListingDto?> FindBySourceUrlAsync(
        string sourceUrl,
        CancellationToken cancellationToken = default)
    {
        var url = (sourceUrl ?? "").Trim();
        if (url.Length == 0) return null;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QueryFirstOrDefaultAsync<ListingRow>(
            new CommandDefinition(
                $"""
                SELECT {SelectColumns}
                FROM pack_local.listing l
                LEFT JOIN pack_local.source s ON s.id = l.source_id
                WHERE l.source_url = @Url AND l.status <> 'HIDDEN'
                ORDER BY COALESCE(l.last_checked_at, l.created_at) DESC
                LIMIT 1
                """,
                new { Url = url },
                cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<LocalListingDto?> FindDuplicateAsync(
        string kind,
        string title,
        string? placeText,
        string? contactPhone,
        string? summary,
        string? sourceUrl,
        Guid? excludeId,
        bool onlyActive,
        CancellationToken cancellationToken = default)
    {
        var k = (kind ?? "job").Trim().ToLowerInvariant();
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<ListingRow>(
            new CommandDefinition(
                $"""
                SELECT {SelectColumns}
                FROM pack_local.listing l
                LEFT JOIN pack_local.source s ON s.id = l.source_id
                WHERE l.kind = @Kind
                  AND l.status <> 'HIDDEN'
                  AND (@OnlyActive = FALSE OR l.status = 'ACTIVE')
                  AND (@ExcludeId IS NULL OR l.id <> @ExcludeId)
                ORDER BY COALESCE(l.last_checked_at, l.created_at) DESC
                LIMIT 300
                """,
                new { Kind = k, OnlyActive = onlyActive, ExcludeId = excludeId },
                cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            if (LocalOsTextExtract.SameListing(
                    k, title, placeText, contactPhone, summary, sourceUrl,
                    row.Kind, row.Title, row.PlaceText, row.ContactPhone, row.Summary, row.SourceUrl))
                return Map(row);
        }
        return null;
    }

    internal static object Bind(Guid id, UpsertLocalListingRequest r)
    {
        var kind = (r.Kind ?? "job").Trim().ToLowerInvariant();
        if (kind is "grant" or "offer")
            kind = "event";
        if (kind is not ("job" or "event" or "room" or "article"))
            kind = "job";
        var audience = (r.Audience is { Count: > 0 } ? r.Audience : ["student"]).ToArray();
        var status = string.IsNullOrWhiteSpace(r.Status) ? "NEEDS_REVIEW" : r.Status.Trim().ToUpperInvariant();
        var trust = string.IsNullOrWhiteSpace(r.Trust) ? "UNVERIFIED" : r.Trust.Trim().ToUpperInvariant();
        return new
        {
            Id = id,
            Kind = kind,
            Title = r.Title.Trim(),
            r.Summary,
            r.OrganizationName,
            r.PlaceText,
            Audience = audience,
            CityCode = string.IsNullOrWhiteSpace(r.CityCode) ? LocalOsPackDefinition.DefaultCityCode : r.CityCode.Trim(),
            SourceKind = string.IsNullOrWhiteSpace(r.SourceKind) ? "group_manual" : r.SourceKind.Trim(),
            r.SourceUrl,
            r.SourceId,
            r.ContactPhone,
            r.ContactName,
            r.SalaryText,
            r.WorkingTime,
            r.EmploymentType,
            r.Category,
            r.Requirements,
            StartAt = r.StartAt?.ToUniversalTime(),
            EndAt = r.EndAt?.ToUniversalTime(),
            r.RegistrationUrl,
            r.PriceMonth,
            r.RoomType,
            Trust = trust,
            SafetyFlag = r.SafetyFlag ?? false,
            Status = status,
        };
    }

    private async Task<LocalListingDto> RefreshThinLeadAsync(
        LocalListingDto mapped,
        CancellationToken cancellationToken)
    {
        if (mapped.Kind != "event" || !LocalOsTextExtract.IsThinLead(mapped.Summary))
            return mapped;
        if (!LocalOsEventLeadRefresh.TryParsePublicUri(mapped.SourceUrl, out var uri))
            return mapped;
        var next = await LocalOsEventLeadRefresh.TryExtractAsync(uri, cancellationToken);
        if (next is null || !LocalOsTextExtract.IsBetterLead(mapped.Summary, next))
            return mapped;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        if (!await LocalOsEventLeadRefresh.TryStoreAsync(conn, mapped.Id, next, cancellationToken))
            return mapped;
        var updated = mapped with { Summary = next, LastCheckedAt = DateTimeOffset.UtcNow };
        if (updated.Status == "EXPIRED"
            && updated.StartAt is null
            && updated.EndAt is null
            && (updated.ExpiresAt is null || updated.ExpiresAt > DateTimeOffset.UtcNow)
            && !LocalOsEventDate.IsPastListing(
                updated.Kind, updated.StartAt, updated.EndAt, updated.Title, next, updated.WorkingTime))
        {
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pack_local.listing
                    SET status = 'ACTIVE', updated_at = NOW()
                    WHERE id = @Id AND status = 'EXPIRED' AND kind = 'event'
                    """,
                    new { updated.Id },
                    cancellationToken: cancellationToken));
            updated = updated with { Status = "ACTIVE" };
        }
        return updated;
    }

    private static async Task ExpireOverdueAsync(System.Data.IDbConnection conn, CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_local.listing
                SET status = 'EXPIRED', updated_at = NOW()
                WHERE status = 'ACTIVE'
                  AND (
                    (expires_at IS NOT NULL AND expires_at <= NOW())
                    OR (
                        kind IN ('event', 'grant')
                        AND COALESCE(end_at, start_at) IS NOT NULL
                        AND (timezone('Asia/Ho_Chi_Minh', COALESCE(end_at, start_at)))::date
                            < (timezone('Asia/Ho_Chi_Minh', NOW()))::date
                    )
                  )
                """,
                cancellationToken: cancellationToken));

        var open = await conn.QueryAsync<ListingRow>(
            new CommandDefinition(
                $"""
                SELECT {SelectColumns}
                FROM pack_local.listing l
                LEFT JOIN pack_local.source s ON s.id = l.source_id
                WHERE l.status = 'ACTIVE'
                  AND l.kind IN ('event', 'grant')
                """,
                cancellationToken: cancellationToken));
        var pastIds = open
            .Where(r => LocalOsEventDate.IsPastListing(
                r.Kind, r.StartAt, r.EndAt, r.Title, r.Summary, r.WorkingTime))
            .Select(r => r.Id)
            .ToArray();
        if (pastIds.Length == 0)
            return;
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE pack_local.listing
                SET status = 'EXPIRED', updated_at = NOW()
                WHERE id = ANY(@Ids) AND status = 'ACTIVE'
                """,
                new { Ids = pastIds },
                cancellationToken: cancellationToken));
    }

    internal static LocalListingDto Map(ListingRow r) => new(
        r.Id, r.Kind, r.Title, r.Summary, r.OrganizationName, r.PlaceText,
        r.Audience ?? ["student"], r.CityCode, r.SourceKind, r.SourceUrl, r.ContactPhone, r.ContactName,
        r.SalaryText, r.WorkingTime, r.EmploymentType, r.Category, r.Requirements,
        r.StartAt, r.EndAt, r.RegistrationUrl,
        r.PriceMonth, r.RoomType, r.Trust, r.SafetyFlag, r.Status,
        r.PublishedAt, r.LastCheckedAt, r.ExpiresAt, r.SourceId, r.SourceName);

    internal sealed class ListingRow
    {
        public Guid Id { get; set; }
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Summary { get; set; }
        public string? OrganizationName { get; set; }
        public string? PlaceText { get; set; }
        public string[]? Audience { get; set; }
        public string CityCode { get; set; } = "";
        public string SourceKind { get; set; } = "";
        public string? SourceUrl { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactName { get; set; }
        public string? SalaryText { get; set; }
        public string? WorkingTime { get; set; }
        public string? EmploymentType { get; set; }
        public string? Category { get; set; }
        public string? Requirements { get; set; }
        public DateTimeOffset? StartAt { get; set; }
        public DateTimeOffset? EndAt { get; set; }
        public string? RegistrationUrl { get; set; }
        public decimal? PriceMonth { get; set; }
        public string? RoomType { get; set; }
        public string Trust { get; set; } = "";
        public bool SafetyFlag { get; set; }
        public string Status { get; set; } = "";
        public DateTimeOffset? PublishedAt { get; set; }
        public DateTimeOffset? LastCheckedAt { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public Guid? SourceId { get; set; }
        public string? SourceName { get; set; }
    }
}
