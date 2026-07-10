using System.Data;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Rx;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class PrescriberPortalPrescriptionRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly IPlatformEventWriter _platformEvents;

    public PrescriberPortalPrescriptionRepository(
        IDbConnectionFactory db,
        IPlatformEventWriter platformEvents)
    {
        _db = db;
        _platformEvents = platformEvents;
    }

    public async Task<ActivePrescriberLinkRow?> GetActiveLinkAsync(
        Guid prescriberId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                l.id AS LinkId,
                l.linked_prescriber_id AS LinkedPrescriberId,
                l.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                p.status AS PrescriberStatus
            FROM pack_pharmacy.prescriber_tenant_links l
            INNER JOIN tenants t ON t.id = l.tenant_id
            INNER JOIN pack_pharmacy.prescribers p ON p.id = l.prescriber_id
            WHERE l.prescriber_id = @PrescriberId
              AND l.tenant_id = @TenantId
              AND l.link_status = @Active
              AND l.linked_prescriber_id IS NOT NULL
              AND p.deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ActivePrescriberLinkRow>(sql, new
        {
            PrescriberId = prescriberId,
            TenantId = tenantId,
            Active = PrescriberLinkStatuses.Active,
        });
    }

    public async Task<IReadOnlyList<PortalCustomerSearchItemDto>> SearchCustomersAsync(
        Guid tenantId,
        string? query,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "c.tenant_id = @TenantId", "c.deleted_at IS NULL" };
        var parameters = new DynamicParameters(new { TenantId = tenantId });

        if (!string.IsNullOrWhiteSpace(query))
        {
            conditions.Add("""
                (
                    c.full_name ILIKE @Search
                    OR c.phone ILIKE @Search
                    OR c.customer_code ILIKE @Search
                )
                """);
            parameters.Add("Search", $"%{query.Trim()}%");
        }

        var sql = $"""
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName,
                c.phone AS Phone
            FROM customers c
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY c.full_name
            LIMIT 30
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<PortalCustomerSearchItemDto>(sql, parameters)).ToList();
    }

    public async Task<IReadOnlyList<PortalProductSearchItemDto>> SearchProductsAsync(
        Guid tenantId,
        string? query,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "p.tenant_id = @TenantId",
            "p.deleted_at IS NULL",
            "p.status = 1",
            // Portal: Rx + OTC; controlled chỉ quầy (D16) — không hiện khi gõ tìm.
            "COALESCE(p.dispensing_class, CASE p.drug_type WHEN 2 THEN 'prescription' WHEN 3 THEN 'controlled' ELSE 'otc' END) <> 'controlled'",
        };
        var parameters = new DynamicParameters(new { TenantId = tenantId });

        if (!string.IsNullOrWhiteSpace(query))
        {
            conditions.Add("""
                (
                    p.product_code ILIKE @Search
                    OR p.product_name ILIKE @Search
                )
                """);
            parameters.Add("Search", $"%{query.Trim()}%");
        }

        var sql = $"""
            SELECT
                p.id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                COALESCE(p.dispensing_class, CASE p.drug_type WHEN 2 THEN 'prescription' WHEN 3 THEN 'controlled' ELSE 'otc' END) AS DispensingClass,
                u.id AS DefaultUnitId,
                u.unit_name AS DefaultUnitName,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = p.tenant_id
                      AND b.product_id = p.id
                      AND b.quantity_available > 0
                ), 0)::numeric AS StockAvailableQty
            FROM products p
            LEFT JOIN LATERAL (
                SELECT pu.id, pu.unit_name
                FROM product_units pu
                WHERE pu.product_id = p.id
                  AND pu.tenant_id = p.tenant_id
                  AND pu.status = 1
                ORDER BY pu.is_base_unit DESC, pu.unit_name ASC
                LIMIT 1
            ) u ON TRUE
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY p.product_name
            LIMIT 30
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<ProductSearchRow>(sql, parameters)).ToList();
        if (rows.Count == 0)
            return Array.Empty<PortalProductSearchItemDto>();

        var productIds = rows.Select(r => r.ProductId).ToArray();
        var unitRows = (await conn.QueryAsync<ProductUnitRow>(
            """
            SELECT
                pu.product_id AS ProductId,
                pu.id AS Id,
                pu.unit_name AS UnitName,
                pu.is_base_unit AS IsBaseUnit,
                pu.conversion_factor AS ConversionFactor
            FROM product_units pu
            WHERE pu.tenant_id = @TenantId
              AND pu.product_id = ANY(@ProductIds)
              AND pu.status = 1
            ORDER BY pu.is_base_unit DESC, pu.unit_name ASC
            """,
            new { TenantId = tenantId, ProductIds = productIds })).ToList();

        var unitsByProduct = unitRows
            .GroupBy(u => u.ProductId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<PortalProductUnitDto>)g
                    .Select(u => new PortalProductUnitDto(u.Id, u.UnitName, u.IsBaseUnit, u.ConversionFactor))
                    .ToList());

        return rows.Select(r =>
        {
            var units = unitsByProduct.GetValueOrDefault(r.ProductId) ?? Array.Empty<PortalProductUnitDto>();
            var defaultUnit = units.FirstOrDefault(u => u.Id == r.DefaultUnitId) ?? units.FirstOrDefault();
            return new PortalProductSearchItemDto(
                r.ProductId,
                r.ProductCode,
                r.ProductName,
                r.DispensingClass,
                defaultUnit?.Id ?? r.DefaultUnitId,
                defaultUnit?.UnitName ?? r.DefaultUnitName,
                units,
                r.StockAvailableQty);
        }).ToList();
    }

    private sealed class ProductSearchRow
    {
        public Guid ProductId { get; init; }
        public string ProductCode { get; init; } = "";
        public string ProductName { get; init; } = "";
        public string DispensingClass { get; init; } = "";
        public Guid? DefaultUnitId { get; init; }
        public string? DefaultUnitName { get; init; }
        public decimal StockAvailableQty { get; init; }
    }

    private sealed class ProductUnitRow
    {
        public Guid ProductId { get; init; }
        public Guid Id { get; init; }
        public string UnitName { get; init; } = "";
        public bool IsBaseUnit { get; init; }
        public decimal ConversionFactor { get; init; }
    }

    public async Task<Guid> CreateSignedPrescriptionAsync(
        Guid tenantId,
        Guid prescriberId,
        Guid linkedPrescriberId,
        PortalCreatePrescriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            throw new InvalidOperationException("Đơn thuốc phải có ít nhất 1 dòng thuốc.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var seq = await conn.QuerySingleAsync<int>(
            """
            SELECT COUNT(*)::int + 1
            FROM pack_pharmacy.electronic_prescriptions
            WHERE tenant_id = @TenantId
            """,
            new { TenantId = tenantId },
            tx);
        var prescriptionCode = $"RX-{seq:D6}";

        var validityDays = await GetPrescriptionValidityDaysAsync(conn, tx, tenantId, cancellationToken);
        var signedAt = DateTime.UtcNow;
        var expiresAt = signedAt.AddDays(validityDays);

        var prescriptionId = await conn.QuerySingleAsync<Guid>(
            """
            INSERT INTO pack_pharmacy.electronic_prescriptions (
                tenant_id, prescription_code, linked_prescriber_id, prescriber_id,
                customer_id, patient_name, patient_phone, status, source,
                signed_at, expires_at, notes, created_by
            )
            VALUES (
                @TenantId, @PrescriptionCode, @LinkedPrescriberId, @PrescriberId,
                @CustomerId, @PatientName, @PatientPhone, @Signed, @Source,
                @SignedAt, @ExpiresAt, @Notes, NULL
            )
            RETURNING id
            """,
            new
            {
                TenantId = tenantId,
                PrescriptionCode = prescriptionCode,
                LinkedPrescriberId = linkedPrescriberId,
                PrescriberId = prescriberId,
                request.CustomerId,
                PatientName = request.PatientName?.Trim(),
                PatientPhone = request.PatientPhone?.Trim(),
                Signed = PrescriptionStatuses.Signed,
                Source = "prescriber_portal",
                SignedAt = signedAt,
                ExpiresAt = expiresAt,
                Notes = request.Notes?.Trim(),
            },
            tx);

        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            if (line.QtyPrescribed <= 0)
                throw new InvalidOperationException("Số lượng kê phải lớn hơn 0.");

            var product = await conn.QuerySingleOrDefaultAsync<ProductLineInfo>(
                """
                SELECT
                    p.id AS ProductId,
                    COALESCE(p.dispensing_class, CASE p.drug_type WHEN 2 THEN 'prescription' WHEN 3 THEN 'controlled' ELSE 'otc' END) AS DispensingClass
                FROM products p
                WHERE p.id = @ProductId
                  AND p.tenant_id = @TenantId
                  AND p.deleted_at IS NULL
                """,
                new { line.ProductId, TenantId = tenantId },
                tx) ?? throw new InvalidOperationException("Sản phẩm không tồn tại.");

            if (string.Equals(product.DispensingClass, DispensingClass.Controlled, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Thuốc kiểm soát không được kê qua portal (phase 1).");

            var productUnitId = line.ProductUnitId;
            if (productUnitId is null)
            {
                productUnitId = await conn.QuerySingleOrDefaultAsync<Guid?>(
                    """
                    SELECT pu.id
                    FROM product_units pu
                    WHERE pu.product_id = @ProductId
                      AND pu.tenant_id = @TenantId
                      AND pu.status = 1
                    ORDER BY pu.is_base_unit DESC, pu.unit_name ASC
                    LIMIT 1
                    """,
                    new { line.ProductId, TenantId = tenantId },
                    tx);
            }

            if (productUnitId is null)
                throw new InvalidOperationException("Sản phẩm không có đơn vị tính.");

            await conn.ExecuteAsync(
                """
                INSERT INTO pack_pharmacy.electronic_prescription_lines (
                    tenant_id, prescription_id, product_id, product_unit_id, line_dispensing_class,
                    qty_prescribed, qty_dispensed, dosage_instruction, sort_order
                )
                VALUES (
                    @TenantId, @PrescriptionId, @ProductId, @ProductUnitId, @LineDispensingClass,
                    @QtyPrescribed, 0, @DosageInstruction, @SortOrder
                )
                """,
                new
                {
                    TenantId = tenantId,
                    PrescriptionId = prescriptionId,
                    line.ProductId,
                    ProductUnitId = productUnitId,
                    LineDispensingClass = product.DispensingClass,
                    line.QtyPrescribed,
                    DosageInstruction = line.DosageInstruction?.Trim(),
                    SortOrder = line.SortOrder == 0 ? i + 1 : line.SortOrder,
                },
                tx);
        }

        await InsertAuditAsync(conn, tx, tenantId, prescriptionId, "portal_signed", prescriberId, null, cancellationToken);

        await _platformEvents.WriteForTenantAsync(
            conn,
            tx,
            tenantId,
            PlatformEventTypes.HealthcarePrescriptionSigned,
            PlatformEventAggregateTypes.ElectronicPrescription,
            prescriptionId,
            new
            {
                prescriptionId,
                prescriptionCode,
                prescriberId,
                tenantId,
                customerId = request.CustomerId,
                source = "prescriber_portal",
                lineCount = request.Lines.Count,
            },
            actorUserId: prescriberId,
            source: PlatformEventSources.HealthcareNetwork,
            cancellationToken: cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return prescriptionId;
    }

    public async Task<IReadOnlyList<PortalPrescriptionSummaryDto>> ListForPrescriberAsync(
        Guid prescriberId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string> { "ep.prescriber_id = @PrescriberId" };
        var parameters = new DynamicParameters(new { PrescriberId = prescriberId });
        if (tenantId is Guid tid)
        {
            conditions.Add("ep.tenant_id = @TenantId");
            parameters.Add("TenantId", tid);
        }

        var sql = $"""
            SELECT
                ep.id AS Id,
                ep.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                ep.prescription_code AS PrescriptionCode,
                ep.status AS Status,
                ep.source AS Source,
                ep.patient_name AS PatientName,
                ep.patient_phone AS PatientPhone,
                ep.signed_at AS SignedAt,
                ep.expires_at AS ExpiresAt,
                ep.created_at AS CreatedAt,
                COALESCE(lc.line_count, 0) AS LineCount
            FROM pack_pharmacy.electronic_prescriptions ep
            INNER JOIN tenants t ON t.id = ep.tenant_id
            LEFT JOIN (
                SELECT prescription_id, COUNT(*)::int AS line_count
                FROM pack_pharmacy.electronic_prescription_lines
                GROUP BY prescription_id
            ) lc ON lc.prescription_id = ep.id
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY ep.created_at DESC
            LIMIT 100
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<PortalPrescriptionSummaryDto>(sql, parameters)).ToList();
    }

    public async Task<PortalPrescriberDashboardDto> GetDashboardAsync(
        Guid prescriberId,
        CancellationToken cancellationToken)
    {
        const string summarySql = """
            SELECT
                COUNT(*) FILTER (
                    WHERE ep.signed_at >= date_trunc('month', NOW() AT TIME ZONE 'UTC')
                )::int AS SignedThisMonth,
                COUNT(*)::int AS SignedTotal
            FROM pack_pharmacy.electronic_prescriptions ep
            WHERE ep.prescriber_id = @PrescriberId
              AND ep.status <> @Cancelled
            """;

        const string tenantSql = """
            SELECT
                ep.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                COUNT(*) FILTER (
                    WHERE ep.signed_at >= date_trunc('month', NOW() AT TIME ZONE 'UTC')
                )::int AS SignedThisMonth,
                COUNT(*)::int AS SignedTotal
            FROM pack_pharmacy.electronic_prescriptions ep
            INNER JOIN tenants t ON t.id = ep.tenant_id
            WHERE ep.prescriber_id = @PrescriberId
              AND ep.status <> @Cancelled
            GROUP BY ep.tenant_id, t.tenant_code, t.tenant_name
            ORDER BY SignedTotal DESC, t.tenant_name
            """;

        const string activePharmaciesSql = """
            SELECT COUNT(*)::int
            FROM pack_pharmacy.prescriber_tenant_links ptl
            WHERE ptl.prescriber_id = @PrescriberId
              AND ptl.link_status = @Active
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var summary = await conn.QuerySingleAsync<(int SignedThisMonth, int SignedTotal)>(
            summarySql,
            new { PrescriberId = prescriberId, Cancelled = PrescriptionStatuses.Cancelled });
        var byTenant = (await conn.QueryAsync<PortalPrescriberDashboardTenantRow>(
            tenantSql,
            new { PrescriberId = prescriberId, Cancelled = PrescriptionStatuses.Cancelled })).ToList();
        var activePharmacies = await conn.QuerySingleAsync<int>(
            activePharmaciesSql,
            new { PrescriberId = prescriberId, Active = PrescriberLinkStatuses.Active });

        return new PortalPrescriberDashboardDto(
            summary.SignedThisMonth,
            summary.SignedTotal,
            activePharmacies,
            byTenant);
    }

    public async Task<PortalCustomerSearchItemDto?> GetCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                c.id AS Id,
                c.customer_code AS CustomerCode,
                c.full_name AS FullName,
                c.phone AS Phone
            FROM customers c
            WHERE c.tenant_id = @TenantId
              AND c.id = @CustomerId
              AND c.deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PortalCustomerSearchItemDto>(sql, new
        {
            TenantId = tenantId,
            CustomerId = customerId,
        });
    }

    public async Task<PortalPrescriptionDetailDto?> GetForPrescriberAsync(
        Guid prescriberId,
        Guid prescriptionId,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                ep.id AS Id,
                ep.tenant_id AS TenantId,
                t.tenant_code AS TenantCode,
                t.tenant_name AS TenantName,
                ep.prescription_code AS PrescriptionCode,
                ep.status AS Status,
                ep.source AS Source,
                ep.customer_id AS CustomerId,
                ep.patient_name AS PatientName,
                ep.patient_phone AS PatientPhone,
                ep.signed_at AS SignedAt,
                ep.expires_at AS ExpiresAt,
                ep.notes AS Notes,
                ep.created_at AS CreatedAt
            FROM pack_pharmacy.electronic_prescriptions ep
            INNER JOIN tenants t ON t.id = ep.tenant_id
            WHERE ep.id = @PrescriptionId
              AND ep.prescriber_id = @PrescriberId
            """;

        const string linesSql = """
            SELECT
                l.id AS Id,
                l.product_id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                l.product_unit_id AS ProductUnitId,
                u.unit_name AS UnitName,
                l.line_dispensing_class AS LineDispensingClass,
                l.qty_prescribed AS QtyPrescribed,
                l.qty_dispensed AS QtyDispensed,
                GREATEST(l.qty_prescribed - l.qty_dispensed, 0) AS QtyRemaining,
                l.dosage_instruction AS DosageInstruction,
                l.sort_order AS SortOrder
            FROM pack_pharmacy.electronic_prescription_lines l
            INNER JOIN products p ON p.id = l.product_id
            LEFT JOIN product_units u ON u.id = l.product_unit_id
            WHERE l.prescription_id = @PrescriptionId
            ORDER BY l.sort_order, l.created_at
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<PortalPrescriptionHeaderRow>(headerSql, new
        {
            PrescriptionId = prescriptionId,
            PrescriberId = prescriberId,
        });
        if (header is null)
            return null;

        var lines = (await conn.QueryAsync<PrescriptionLineDto>(linesSql, new { PrescriptionId = prescriptionId })).ToList();
        return header.ToDto(lines);
    }

    private static async Task<int> GetPrescriptionValidityDaysAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var days = await conn.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT NULLIF(settings->'rx'->>'prescription_validity_days', '')::int
            FROM tenants
            WHERE id = @TenantId
            """,
            new { TenantId = tenantId },
            tx);
        if (days is null or <= 0)
            return 7;
        return Math.Clamp(days.Value, 1, 365);
    }

    private static async Task InsertAuditAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid tenantId,
        Guid prescriptionId,
        string action,
        Guid actorId,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var metadataJson = metadata is null ? "{}" : JsonSerializer.Serialize(metadata);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_pharmacy.prescription_audit_log (
                tenant_id, prescription_id, action, actor_id, metadata
            )
            VALUES (@TenantId, @PrescriptionId, @Action, @ActorId, @Metadata::jsonb)
            """,
            new
            {
                TenantId = tenantId,
                PrescriptionId = prescriptionId,
                Action = action,
                ActorId = actorId,
                Metadata = metadataJson,
            },
            tx);
    }

    internal sealed record ActivePrescriberLinkRow(
        Guid LinkId,
        Guid LinkedPrescriberId,
        Guid TenantId,
        string TenantCode,
        string TenantName,
        string PrescriberStatus);

    private sealed record ProductLineInfo(Guid ProductId, string DispensingClass);

    private sealed class PortalPrescriptionHeaderRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public string TenantCode { get; init; } = "";
        public string TenantName { get; init; } = "";
        public string PrescriptionCode { get; init; } = "";
        public string Status { get; init; } = "";
        public string Source { get; init; } = "";
        public Guid? CustomerId { get; init; }
        public string? PatientName { get; init; }
        public string? PatientPhone { get; init; }
        public DateTime? SignedAt { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public string? Notes { get; init; }
        public DateTime CreatedAt { get; init; }

        public PortalPrescriptionDetailDto ToDto(IReadOnlyList<PrescriptionLineDto> lines) =>
            new(
                Id,
                TenantId,
                TenantCode,
                TenantName,
                PrescriptionCode,
                Status,
                Source,
                CustomerId,
                PatientName,
                PatientPhone,
                SignedAt,
                ExpiresAt,
                Notes,
                CreatedAt,
                lines);
    }
}
