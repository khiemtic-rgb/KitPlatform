using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsListingService : ILocalOsListingService
{
    internal const string SelectColumns = """
        id AS Id, kind AS Kind, title AS Title, summary AS Summary,
        organization_name AS OrganizationName, place_text AS PlaceText,
        audience AS Audience, city_code AS CityCode,
        source_kind AS SourceKind, source_url AS SourceUrl,
        contact_phone AS ContactPhone, contact_name AS ContactName,
        salary_text AS SalaryText, working_time AS WorkingTime, employment_type AS EmploymentType,
        category AS Category, requirements AS Requirements,
        start_at AS StartAt, end_at AS EndAt, registration_url AS RegistrationUrl,
        price_month AS PriceMonth, room_type AS RoomType, trust AS Trust,
        safety_flag AS SafetyFlag, status AS Status,
        published_at AS PublishedAt, last_checked_at AS LastCheckedAt, expires_at AS ExpiresAt
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

        var sql = $"""
            SELECT {SelectColumns}
            FROM pack_local.listing
            WHERE city_code = @City
              AND (@Kind IS NULL OR kind = @Kind)
              AND (@Status IS NULL OR status = @Status)
              AND (@Q IS NULL OR title ILIKE @Like OR COALESCE(place_text, '') ILIKE @Like
                   OR COALESCE(organization_name, '') ILIKE @Like)
              AND (
                    @PublicOnly = FALSE
                    OR (
                        status = 'ACTIVE'
                        AND safety_flag = FALSE
                        AND (expires_at IS NULL OR expires_at > NOW())
                    )
                  )
            ORDER BY CASE WHEN status = 'NEEDS_REVIEW' THEN 0 ELSE 1 END,
                     COALESCE(last_checked_at, published_at, created_at) DESC
            LIMIT 200
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
        return rows.Select(Map).ToList();
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
                FROM pack_local.listing
                WHERE id = @Id
                  AND (
                        @PublicOnly = FALSE
                        OR (
                            status = 'ACTIVE'
                            AND safety_flag = FALSE
                            AND (expires_at IS NULL OR expires_at > NOW())
                        )
                      )
                """,
                new { Id = id, PublicOnly = publicOnly },
                cancellationToken: cancellationToken));
        return row is null ? null : Map(row);
    }

    public async Task<LocalListingDto> CreateAsync(
        UpsertLocalListingRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.CreateVersion7();
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_local.listing (
                    id, kind, title, summary, organization_name, place_text, audience, city_code,
                    source_kind, source_url, contact_phone, contact_name, salary_text, working_time,
                    employment_type, category, requirements,
                    start_at, end_at, registration_url, price_month, room_type, trust, safety_flag,
                    status, published_at, last_checked_at, expires_at
                ) VALUES (
                    @Id, @Kind, @Title, @Summary, @OrganizationName, @PlaceText, @Audience, @CityCode,
                    @SourceKind, @SourceUrl, @ContactPhone, @ContactName, @SalaryText, @WorkingTime,
                    @EmploymentType, @Category, @Requirements,
                    @StartAt, @EndAt, @RegistrationUrl, @PriceMonth, @RoomType, @Trust, @SafetyFlag,
                    @Status, CASE WHEN @Status = 'ACTIVE' THEN NOW() ELSE NULL END, NOW(),
                    NOW() + INTERVAL '14 days'
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
                    contact_phone = @ContactPhone, contact_name = @ContactName,
                    salary_text = @SalaryText, working_time = @WorkingTime, employment_type = @EmploymentType,
                    category = @Category, requirements = @Requirements,
                    start_at = @StartAt, end_at = @EndAt, registration_url = @RegistrationUrl,
                    price_month = @PriceMonth, room_type = @RoomType,
                    trust = @Trust, safety_flag = @SafetyFlag, status = @Status,
                    last_checked_at = NOW(), updated_at = NOW()
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

    internal static object Bind(Guid id, UpsertLocalListingRequest r)
    {
        var kind = (r.Kind ?? "job").Trim().ToLowerInvariant();
        if (kind is not ("job" or "event" or "room"))
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
            r.ContactPhone,
            r.ContactName,
            r.SalaryText,
            r.WorkingTime,
            r.EmploymentType,
            r.Category,
            r.Requirements,
            r.StartAt,
            r.EndAt,
            r.RegistrationUrl,
            r.PriceMonth,
            r.RoomType,
            Trust = trust,
            SafetyFlag = r.SafetyFlag ?? false,
            Status = status,
        };
    }

    internal static LocalListingDto Map(ListingRow r) => new(
        r.Id, r.Kind, r.Title, r.Summary, r.OrganizationName, r.PlaceText,
        r.Audience ?? ["student"], r.CityCode, r.SourceKind, r.SourceUrl, r.ContactPhone, r.ContactName,
        r.SalaryText, r.WorkingTime, r.EmploymentType, r.Category, r.Requirements,
        r.StartAt, r.EndAt, r.RegistrationUrl,
        r.PriceMonth, r.RoomType, r.Trust, r.SafetyFlag, r.Status,
        r.PublishedAt, r.LastCheckedAt, r.ExpiresAt);

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
    }
}
