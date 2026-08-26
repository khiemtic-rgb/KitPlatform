using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesDeskService : IKitSalesDeskService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public KitSalesDeskService(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public Task<KitSalesHealthDto> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new KitSalesHealthDto(SalesPackDefinition.PackCode, "v1", true));

    public async Task<IReadOnlyList<KitSalesProductDto>> ListProductsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<KitSalesProductDto>(
            """
            SELECT code AS Code, display_name AS DisplayName, status AS Status
            FROM pack_sales.product
            ORDER BY code
            """);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<KitSalesBusinessDto>> ListBusinessesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<KitSalesBusinessDto>(
            """
            SELECT id AS Id, name AS Name, business_type AS BusinessType,
                   province AS Province, phone AS Phone, status AS Status,
                   source AS Source, created_at AS CreatedAt, updated_at AS UpdatedAt
            FROM pack_sales.business
            WHERE tenant_id = @TenantId
            ORDER BY updated_at DESC
            LIMIT @Limit
            """,
            new { TenantId, Limit = limit });
        return rows.ToList();
    }

    public async Task<KitSalesBusinessDto> CreateBusinessAsync(
        CreateKitSalesBusinessRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên business là bắt buộc.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<KitSalesBusinessDto>(
            """
            INSERT INTO pack_sales.business (
                tenant_id, name, business_type, province, district, phone, email, source, description
            )
            VALUES (
                @TenantId, @Name, @BusinessType, @Province, @District, @Phone, @Email, @Source, @Notes
            )
            RETURNING id AS Id, name AS Name, business_type AS BusinessType,
                      province AS Province, phone AS Phone, status AS Status,
                      source AS Source, created_at AS CreatedAt, updated_at AS UpdatedAt
            """,
            new
            {
                TenantId,
                Name = name,
                BusinessType = NormalizeCode(request.BusinessType, "pharmacy"),
                Province = TrimOrNull(request.Province),
                District = TrimOrNull(request.District),
                Phone = TrimOrNull(request.Phone),
                Email = TrimOrNull(request.Email),
                Source = TrimOrNull(request.Source),
                Notes = TrimOrNull(request.Notes),
            });
    }

    public async Task<IReadOnlyList<KitSalesLeadDto>> ListLeadsAsync(
        string? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var statusFilter = TrimOrNull(status);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<KitSalesLeadDto>(
            """
            SELECT l.id AS Id, l.business_id AS BusinessId, b.name AS BusinessName,
                   l.product_code AS ProductCode, l.lead_status AS LeadStatus,
                   l.lead_temperature AS LeadTemperature, l.total_score AS TotalScore,
                   l.source AS Source, l.owner_user_id AS OwnerUserId,
                   l.next_action_code AS NextActionCode, l.next_action_at AS NextActionAt,
                   l.last_interaction_at AS LastInteractionAt,
                   l.created_at AS CreatedAt, l.updated_at AS UpdatedAt
            FROM pack_sales.lead l
            INNER JOIN pack_sales.business b ON b.id = l.business_id
            WHERE l.tenant_id = @TenantId
              AND (@Status IS NULL OR l.lead_status = @Status)
            ORDER BY l.updated_at DESC
            LIMIT @Limit
            """,
            new { TenantId, Status = statusFilter, Limit = limit });
        return rows.ToList();
    }

    public async Task<KitSalesLeadDto> CreateLeadAsync(
        CreateKitSalesLeadRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var businessExists = await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1 FROM pack_sales.business
                WHERE id = @BusinessId AND tenant_id = @TenantId
            )
            """,
            new { request.BusinessId, TenantId });
        if (!businessExists)
            throw new InvalidOperationException("Business không tồn tại trong tenant.");

        var productCode = NormalizeCode(request.ProductCode, "novixa");
        await EnsureProductAsync(conn, productCode);

        return await conn.QuerySingleAsync<KitSalesLeadDto>(
            """
            WITH inserted AS (
                INSERT INTO pack_sales.lead (
                    tenant_id, business_id, product_code, source, lead_status, lead_temperature, notes
                )
                VALUES (
                    @TenantId, @BusinessId, @ProductCode, @Source, @LeadStatus, @LeadTemperature, @Notes
                )
                RETURNING *
            )
            SELECT i.id AS Id, i.business_id AS BusinessId, b.name AS BusinessName,
                   i.product_code AS ProductCode, i.lead_status AS LeadStatus,
                   i.lead_temperature AS LeadTemperature, i.total_score AS TotalScore,
                   i.source AS Source, i.owner_user_id AS OwnerUserId,
                   i.next_action_code AS NextActionCode, i.next_action_at AS NextActionAt,
                   i.last_interaction_at AS LastInteractionAt,
                   i.created_at AS CreatedAt, i.updated_at AS UpdatedAt
            FROM inserted i
            INNER JOIN pack_sales.business b ON b.id = i.business_id
            """,
            new
            {
                TenantId,
                request.BusinessId,
                ProductCode = productCode,
                Source = TrimOrNull(request.Source),
                LeadStatus = NormalizeCode(request.LeadStatus, "discovered"),
                LeadTemperature = NormalizeCode(request.LeadTemperature, "cold"),
                Notes = TrimOrNull(request.Notes),
            });
    }

    public async Task<KitSalesLeadDto> CreateProspectAsync(
        CreateKitSalesProspectRequest request,
        CancellationToken cancellationToken = default)
    {
        var business = await CreateBusinessAsync(
            new CreateKitSalesBusinessRequest(
                request.BusinessName,
                request.BusinessType,
                request.Province,
                null,
                request.Phone,
                null,
                request.Source,
                request.Notes),
            cancellationToken);

        return await CreateLeadAsync(
            new CreateKitSalesLeadRequest(
                business.Id,
                request.ProductCode,
                request.Source,
                "discovered",
                "cold",
                request.Notes),
            cancellationToken);
    }

    public async Task<KitSalesPipelineSummaryDto> GetPipelineSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var buckets = await conn.QueryAsync<KitSalesPipelineBucketDto>(
            """
            SELECT lead_status AS Status, COUNT(*)::int AS Count
            FROM pack_sales.lead
            WHERE tenant_id = @TenantId
            GROUP BY lead_status
            ORDER BY Count DESC, lead_status
            """,
            new { TenantId });
        var list = buckets.ToList();
        return new KitSalesPipelineSummaryDto(list.Sum(b => b.Count), list);
    }

    private static async Task EnsureProductAsync(System.Data.IDbConnection conn, string productCode)
    {
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM pack_sales.product WHERE code = @ProductCode)",
            new { ProductCode = productCode });
        if (!exists)
            throw new ArgumentException($"Product '{productCode}' chưa được khai báo.");
    }

    private static string NormalizeCode(string? value, string fallback)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed.ToLowerInvariant();
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
