using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Pharmacy;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Pharmacy;

internal sealed class PharmacyStorefrontRepository
{
    private readonly IDbConnectionFactory _db;

    public PharmacyStorefrontRepository(IDbConnectionFactory db) => _db = db;

    public async Task<ProfileRow?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.slug AS Slug,
                p.is_published AS IsPublished,
                p.content::text AS ContentJson,
                p.updated_at AS UpdatedAt
            FROM pack_pharmacy.pharmacy_storefront_profiles p
            WHERE p.tenant_id = @TenantId
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ProfileRow>(sql, new { TenantId = tenantId });
    }

    public async Task<string?> GetTenantCodeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT tenant_code
            FROM public.tenants
            WHERE id = @TenantId AND deleted_at IS NULL
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<string?>(sql, new { TenantId = tenantId });
    }

    public async Task UpsertAsync(
        Guid tenantId,
        string slug,
        bool isPublished,
        string contentJson,
        Guid updatedBy,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_pharmacy.pharmacy_storefront_profiles
                (tenant_id, slug, is_published, content, updated_at, updated_by)
            VALUES
                (@TenantId, @Slug, @IsPublished, @ContentJson::jsonb, NOW(), @UpdatedBy)
            ON CONFLICT (tenant_id) DO UPDATE SET
                slug = EXCLUDED.slug,
                is_published = EXCLUDED.is_published,
                content = EXCLUDED.content,
                updated_at = NOW(),
                updated_by = EXCLUDED.updated_by
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = tenantId,
            Slug = slug,
            IsPublished = isPublished,
            ContentJson = contentJson,
            UpdatedBy = updatedBy,
        });
    }

    public async Task<PublicRow?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.slug AS Slug,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                p.content::text AS ContentJson
            FROM pack_pharmacy.pharmacy_storefront_profiles p
            INNER JOIN public.tenants t ON t.id = p.tenant_id
            WHERE p.slug = @Slug
              AND p.is_published = true
              AND t.deleted_at IS NULL
              AND t.status = 1
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PublicRow>(sql, new { Slug = slug });
    }

    public async Task<PublicRow?> GetPublishedByTenantCodeAsync(
        string tenantCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.slug AS Slug,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                p.content::text AS ContentJson
            FROM pack_pharmacy.pharmacy_storefront_profiles p
            INNER JOIN public.tenants t ON t.id = p.tenant_id
            WHERE t.tenant_code = @TenantCode
              AND p.is_published = true
              AND t.deleted_at IS NULL
              AND t.status = 1
            LIMIT 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PublicRow>(sql, new { TenantCode = tenantCode });
    }

    internal sealed class ProfileRow
    {
        public string Slug { get; set; } = "";
        public bool IsPublished { get; set; }
        public string ContentJson { get; set; } = "{}";
        public DateTimeOffset UpdatedAt { get; set; }
    }

    internal sealed class PublicRow
    {
        public string Slug { get; set; } = "";
        public string TenantCode { get; set; } = "";
        public string TenantName { get; set; } = "";
        public string ContentJson { get; set; } = "{}";
    }
}

internal sealed class PharmacyStorefrontService : IPharmacyStorefrontService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly PharmacyStorefrontRepository _repo;
    private readonly ITenantContext _tenant;

    public PharmacyStorefrontService(PharmacyStorefrontRepository repo, ITenantContext tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<PharmacyStorefrontProfileDto> GetProfileAsync(CancellationToken cancellationToken)
    {
        var row = await _repo.GetByTenantIdAsync(_tenant.TenantId, cancellationToken);
        if (row is null)
        {
            var code = await _repo.GetTenantCodeAsync(_tenant.TenantId, cancellationToken) ?? "pharmacy";
            var defaultSlug = SuggestSlug(code);
            return new PharmacyStorefrontProfileDto(
                defaultSlug,
                false,
                $"{defaultSlug}.novixa.vn",
                JsonSerializer.SerializeToElement(new { }, JsonOptions),
                DateTimeOffset.UtcNow);
        }

        return ToDto(row);
    }

    public async Task<PharmacyStorefrontProfileDto> UpsertProfileAsync(
        UpdatePharmacyStorefrontProfileRequest request,
        CancellationToken cancellationToken)
    {
        var slug = NormalizeSlug(request.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidOperationException("Slug không hợp lệ.");

        var contentJson = request.Content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? "{}"
            : request.Content.GetRawText();

        try
        {
            await _repo.UpsertAsync(
                _tenant.TenantId,
                slug,
                request.IsPublished,
                contentJson,
                _tenant.UserId,
                cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("uq_pharmacy_storefront_slug", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Slug đã được nhà thuốc khác sử dụng.");
        }

        var saved = await _repo.GetByTenantIdAsync(_tenant.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được hồ sơ storefront.");
        return ToDto(saved);
    }

    public async Task<PublicPharmacyStorefrontDto?> GetPublishedBySlugAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSlug(slug);
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        var row = await _repo.GetPublishedBySlugAsync(normalized, cancellationToken);
        return row is null ? null : ToPublic(row);
    }

    public async Task<PublicPharmacyStorefrontDto?> GetPublishedByTenantCodeAsync(
        string tenantCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantCode)) return null;
        var row = await _repo.GetPublishedByTenantCodeAsync(tenantCode.Trim(), cancellationToken);
        return row is null ? null : ToPublic(row);
    }

    private static PharmacyStorefrontProfileDto ToDto(PharmacyStorefrontRepository.ProfileRow row)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.ContentJson) ? "{}" : row.ContentJson);
        return new PharmacyStorefrontProfileDto(
            row.Slug,
            row.IsPublished,
            $"{row.Slug}.novixa.vn",
            doc.RootElement.Clone(),
            row.UpdatedAt);
    }

    private static PublicPharmacyStorefrontDto ToPublic(PharmacyStorefrontRepository.PublicRow row)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.ContentJson) ? "{}" : row.ContentJson);
        return new PublicPharmacyStorefrontDto(
            row.Slug,
            row.TenantCode,
            row.TenantName,
            doc.RootElement.Clone());
    }

    private static string SuggestSlug(string tenantCode)
    {
        var raw = tenantCode.Trim().ToLowerInvariant()
            .Replace("nt_", "", StringComparison.Ordinal)
            .Replace('_', '-');
        return NormalizeSlug(raw);
    }

    private static string NormalizeSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return "";
        var s = slug.Trim().ToLowerInvariant();
        var chars = s.Select(c =>
            char.IsAsciiLetterOrDigit(c) || c == '-' ? c : '-').ToArray();
        s = new string(chars);
        while (s.Contains("--", StringComparison.Ordinal))
            s = s.Replace("--", "-", StringComparison.Ordinal);
        return s.Trim('-');
    }
}
