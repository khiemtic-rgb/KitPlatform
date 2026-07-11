using System.Data;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.Pharmacy.Rx;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class PrescriptionRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly InventoryRepository _inventory;

    public PrescriptionRepository(
        IDbConnectionFactory db,
        ITenantContext tenant,
        InventoryRepository inventory)
    {
        _db = db;
        _tenant = tenant;
        _inventory = inventory;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<LinkedPrescriberDto>> GetPrescribersAsync(
        string? search,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "tenant_id = @TenantId",
            "deleted_at IS NULL",
        };
        var parameters = new DynamicParameters(new { TenantId });

        if (activeOnly)
            conditions.Add("status = 1");

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("""
                (
                    full_name ILIKE @Search
                    OR COALESCE(phone, '') ILIKE @Search
                    OR COALESCE(license_number, '') ILIKE @Search
                )
                """);
            parameters.Add("Search", $"%{search.Trim()}%");
        }

        var sql = $"""
            SELECT
                id AS Id,
                full_name AS FullName,
                license_number AS LicenseNumber,
                phone AS Phone,
                specialty AS Specialty,
                status AS Status,
                notes AS Notes,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM pack_pharmacy.linked_prescribers
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY full_name
            LIMIT 200
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<LinkedPrescriberDto>(sql, parameters)).ToList();
    }

    public async Task<LinkedPrescriberDto?> GetPrescriberAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                full_name AS FullName,
                license_number AS LicenseNumber,
                phone AS Phone,
                specialty AS Specialty,
                status AS Status,
                notes AS Notes,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM pack_pharmacy.linked_prescribers
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<LinkedPrescriberDto>(sql, new { Id = id, TenantId });
    }

    public async Task<Guid> CreatePrescriberAsync(CreateLinkedPrescriberRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_pharmacy.linked_prescribers (
                tenant_id, full_name, license_number, phone, specialty, notes
            )
            VALUES (
                @TenantId, @FullName, @LicenseNumber, @Phone, @Specialty, @Notes
            )
            RETURNING id
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            FullName = request.FullName.Trim(),
            LicenseNumber = request.LicenseNumber?.Trim(),
            Phone = request.Phone?.Trim(),
            Specialty = request.Specialty?.Trim(),
            Notes = request.Notes?.Trim(),
        });
    }

    public async Task<bool> UpdatePrescriberAsync(
        Guid id,
        UpdateLinkedPrescriberRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_pharmacy.linked_prescribers
            SET
                full_name = @FullName,
                license_number = @LicenseNumber,
                phone = @Phone,
                specialty = @Specialty,
                status = @Status,
                notes = @Notes,
                updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId,
            FullName = request.FullName.Trim(),
            LicenseNumber = request.LicenseNumber?.Trim(),
            Phone = request.Phone?.Trim(),
            Specialty = request.Specialty?.Trim(),
            request.Status,
            Notes = request.Notes?.Trim(),
        }) > 0;
    }

    public async Task<bool> DeletePrescriberAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_pharmacy.linked_prescribers
            SET deleted_at = NOW(), updated_at = NOW(), status = 2
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new { Id = id, TenantId }) > 0;
    }

    public async Task<PrescriptionPagedListResult> GetPrescriptionsAsync(
        PrescriptionListFilter filter,
        Guid[]? allowedBranchIds,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        var conditions = new List<string> { "ep.tenant_id = @TenantId" };
        var parameters = new DynamicParameters(new { TenantId, Offset = offset, PageSize = pageSize });

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            conditions.Add("ep.status = @Status");
            parameters.Add("Status", filter.Status.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(filter.PhoneSearch))
        {
            conditions.Add("COALESCE(ep.patient_phone, '') ILIKE @Phone");
            parameters.Add("Phone", $"%{filter.PhoneSearch.Trim()}%");
        }

        if (allowedBranchIds is { Length: > 0 })
        {
            conditions.Add("(ep.branch_id IS NULL OR ep.branch_id = ANY(@AllowedBranchIds))");
            parameters.Add("AllowedBranchIds", allowedBranchIds);
        }

        var whereSql = string.Join(" AND ", conditions);

        var countSql = $"""
            SELECT COUNT(*)::int
            FROM pack_pharmacy.electronic_prescriptions ep
            WHERE {whereSql}
            """;

        var sql = $"""
            SELECT
                ep.id AS Id,
                ep.prescription_code AS PrescriptionCode,
                ep.branch_id AS BranchId,
                ep.linked_prescriber_id AS LinkedPrescriberId,
                lp.full_name AS PrescriberName,
                ep.customer_id AS CustomerId,
                ep.patient_name AS PatientName,
                ep.patient_phone AS PatientPhone,
                ep.status AS Status,
                ep.source AS Source,
                ep.verified_at AS VerifiedAt,
                ep.expires_at AS ExpiresAt,
                ep.created_at AS CreatedAt,
                COALESCE(line_count.total_lines, 0) AS LineCount,
                COALESCE(line_count.qty_remaining, 0) AS QtyRemaining
            FROM pack_pharmacy.electronic_prescriptions ep
            INNER JOIN pack_pharmacy.linked_prescribers lp
                ON lp.id = ep.linked_prescriber_id
            LEFT JOIN (
                SELECT
                    prescription_id,
                    COUNT(*)::int AS total_lines,
                    SUM(GREATEST(qty_prescribed - qty_dispensed, 0)) AS qty_remaining
                FROM pack_pharmacy.electronic_prescription_lines
                WHERE tenant_id = @TenantId
                GROUP BY prescription_id
            ) line_count ON line_count.prescription_id = ep.id
            WHERE {whereSql}
            ORDER BY ep.created_at DESC
            LIMIT @PageSize OFFSET @Offset
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.QuerySingleAsync<int>(countSql, parameters);
        var items = (await conn.QueryAsync<PrescriptionListItemDto>(sql, parameters)).ToList();
        return new PrescriptionPagedListResult(items, total, page, pageSize);
    }

    public async Task<PrescriptionDetailDto?> GetPrescriptionAsync(Guid id, CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                ep.id AS Id,
                ep.prescription_code AS PrescriptionCode,
                ep.branch_id AS BranchId,
                ep.linked_prescriber_id AS LinkedPrescriberId,
                lp.full_name AS PrescriberName,
                ep.customer_id AS CustomerId,
                ep.patient_name AS PatientName,
                ep.patient_phone AS PatientPhone,
                ep.status AS Status,
                ep.source AS Source,
                ep.verification_method AS VerificationMethod,
                ep.verified_by AS VerifiedBy,
                ep.verified_at AS VerifiedAt,
                ep.signed_at AS SignedAt,
                ep.expires_at AS ExpiresAt,
                ep.dispensed_at AS DispensedAt,
                ep.notes AS Notes,
                ep.created_by AS CreatedBy,
                ep.created_at AS CreatedAt,
                ep.updated_at AS UpdatedAt,
                ep.cancelled_at AS CancelledAt
            FROM pack_pharmacy.electronic_prescriptions ep
            INNER JOIN pack_pharmacy.linked_prescribers lp
                ON lp.id = ep.linked_prescriber_id
            WHERE ep.id = @Id
              AND ep.tenant_id = @TenantId
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
              AND l.tenant_id = @TenantId
            ORDER BY l.sort_order, l.created_at
            """;

        const string attachmentSql = """
            SELECT
                id AS Id,
                file_url AS FileUrl,
                file_name AS FileName,
                uploaded_by AS UploadedBy,
                created_at AS CreatedAt
            FROM pack_pharmacy.prescription_attachments
            WHERE prescription_id = @PrescriptionId
              AND tenant_id = @TenantId
            ORDER BY created_at DESC
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<PrescriptionHeaderRow>(headerSql, new { Id = id, TenantId });
        if (header is null)
            return null;

        var lines = (await conn.QueryAsync<PrescriptionLineDto>(
            linesSql, new { PrescriptionId = id, TenantId })).ToList();
        var attachments = (await conn.QueryAsync<PrescriptionAttachmentDto>(
            attachmentSql, new { PrescriptionId = id, TenantId })).ToList();
        return header.ToDto(lines, attachments);
    }

    public async Task<Guid> CreatePrescriptionAsync(
        CreatePrescriptionRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var lines = NormalizeLines(request.Lines);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await EnsurePrescriberAsync(conn, tx, request.LinkedPrescriberId, cancellationToken);

        var prescriptionCode = await _inventory.NextDocumentNumberAsync(
            conn,
            tx,
            "RX",
            "pack_pharmacy.electronic_prescriptions",
            cancellationToken);

        const string sql = """
            INSERT INTO pack_pharmacy.electronic_prescriptions (
                tenant_id, branch_id, prescription_code, linked_prescriber_id,
                customer_id, patient_name, patient_phone, status, source, notes, created_by
            )
            VALUES (
                @TenantId, @BranchId, @PrescriptionCode, @LinkedPrescriberId,
                @CustomerId, @PatientName, @PatientPhone, @Status, @Source, @Notes, @CreatedBy
            )
            RETURNING id
            """;
        var id = await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            request.BranchId,
            PrescriptionCode = prescriptionCode,
            request.LinkedPrescriberId,
            request.CustomerId,
            PatientName = request.PatientName?.Trim(),
            PatientPhone = request.PatientPhone?.Trim(),
            Status = PrescriptionStatuses.Draft,
            Source = NormalizeSource(request.Source),
            Notes = request.Notes?.Trim(),
            CreatedBy = userId,
        }, tx);

        await InsertLinesAsync(conn, tx, id, lines, cancellationToken);
        await InsertAuditAsync(conn, tx, id, "create", userId, new { lineCount = lines.Count }, cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<bool> UpdatePrescriptionAsync(
        Guid id,
        UpdatePrescriptionRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var lines = NormalizeLines(request.Lines);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var status = await conn.QuerySingleOrDefaultAsync<string?>(
            """
            SELECT status
            FROM pack_pharmacy.electronic_prescriptions
            WHERE id = @Id
              AND tenant_id = @TenantId
            FOR UPDATE
            """,
            new { Id = id, TenantId },
            tx);

        if (status is null)
            return false;
        if (status is PrescriptionStatuses.Verified
            or PrescriptionStatuses.Signed
            or PrescriptionStatuses.PartiallyDispensed
            or PrescriptionStatuses.Dispensed
            or PrescriptionStatuses.Cancelled
            or PrescriptionStatuses.Expired)
        {
            throw new InvalidOperationException("Đơn thuốc đã khóa, không thể chỉnh sửa.");
        }

        await EnsurePrescriberAsync(conn, tx, request.LinkedPrescriberId, cancellationToken);

        const string updateSql = """
            UPDATE pack_pharmacy.electronic_prescriptions
            SET
                branch_id = @BranchId,
                linked_prescriber_id = @LinkedPrescriberId,
                customer_id = @CustomerId,
                patient_name = @PatientName,
                patient_phone = @PatientPhone,
                source = @Source,
                notes = @Notes,
                updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
            """;
        await conn.ExecuteAsync(updateSql, new
        {
            Id = id,
            TenantId,
            request.BranchId,
            request.LinkedPrescriberId,
            request.CustomerId,
            PatientName = request.PatientName?.Trim(),
            PatientPhone = request.PatientPhone?.Trim(),
            Source = NormalizeSource(request.Source),
            Notes = request.Notes?.Trim(),
        }, tx);

        await conn.ExecuteAsync(
            """
            DELETE FROM pack_pharmacy.electronic_prescription_lines
            WHERE prescription_id = @PrescriptionId
              AND tenant_id = @TenantId
            """,
            new { PrescriptionId = id, TenantId },
            tx);

        await InsertLinesAsync(conn, tx, id, lines, cancellationToken);
        await InsertAuditAsync(conn, tx, id, "update", userId, new { lineCount = lines.Count }, cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SubmitPrescriptionAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_pharmacy.electronic_prescriptions
            SET status = @PendingVerification, updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND status = @Draft
            """,
            new
            {
                Id = id,
                TenantId,
                PendingVerification = PrescriptionStatuses.PendingVerification,
                Draft = PrescriptionStatuses.Draft,
            },
            tx) > 0;

        if (updated)
        {
            await InsertAuditAsync(conn, tx, id, "submit", userId, null, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        else
        {
            await tx.RollbackAsync(cancellationToken);
        }

        return updated;
    }

    public async Task<PrescriptionAttachmentDto> AddAttachmentAsync(
        Guid prescriptionId,
        AddPrescriptionAttachmentRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var exists = await conn.QuerySingleAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1
                FROM pack_pharmacy.electronic_prescriptions
                WHERE id = @Id
                  AND tenant_id = @TenantId
                  AND status <> @Cancelled
            )
            """,
            new { Id = prescriptionId, TenantId, Cancelled = PrescriptionStatuses.Cancelled },
            tx);
        if (!exists)
            throw new InvalidOperationException("Đơn thuốc không tồn tại hoặc đã hủy.");

        const string sql = """
            INSERT INTO pack_pharmacy.prescription_attachments (
                tenant_id, prescription_id, file_url, file_name, uploaded_by
            )
            VALUES (@TenantId, @PrescriptionId, @FileUrl, @FileName, @UploadedBy)
            RETURNING
                id AS Id,
                file_url AS FileUrl,
                file_name AS FileName,
                uploaded_by AS UploadedBy,
                created_at AS CreatedAt
            """;
        var item = await conn.QuerySingleAsync<PrescriptionAttachmentDto>(sql, new
        {
            TenantId,
            PrescriptionId = prescriptionId,
            FileUrl = request.FileUrl.Trim(),
            FileName = request.FileName?.Trim(),
            UploadedBy = userId,
        }, tx);

        await InsertAuditAsync(conn, tx, prescriptionId, "add_attachment", userId, new { item.Id }, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return item;
    }

    public async Task<bool> VerifyPrescriptionAsync(
        Guid id,
        VerifyPrescriptionRequest request,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var header = await conn.QuerySingleOrDefaultAsync<(string Status, string Source)>(
            """
            SELECT status AS Status, source AS Source
            FROM pack_pharmacy.electronic_prescriptions
            WHERE id = @Id
              AND tenant_id = @TenantId
            FOR UPDATE
            """,
            new { Id = id, TenantId },
            tx);

        if (string.IsNullOrWhiteSpace(header.Status))
            return false;

        if (header.Status is not (PrescriptionStatuses.PendingVerification or PrescriptionStatuses.Draft))
            throw new InvalidOperationException("Đơn thuốc không ở trạng thái chờ xác minh.");

        if (header.Source == "staff_entry")
        {
            var count = await conn.QuerySingleAsync<int>(
                """
                SELECT COUNT(*)::int
                FROM pack_pharmacy.prescription_attachments
                WHERE tenant_id = @TenantId
                  AND prescription_id = @PrescriptionId
                """,
                new { TenantId, PrescriptionId = id },
                tx);
            if (count <= 0)
                throw new InvalidOperationException("Đơn nhập tay phải có ít nhất 1 ảnh toa trước khi xác minh.");
        }

        var validityDays = await GetPrescriptionValidityDaysAsync(conn, tx, cancellationToken);
        var verifiedAt = DateTime.UtcNow;
        var expiresAt = verifiedAt.AddDays(validityDays);

        await conn.ExecuteAsync(
            """
            UPDATE pack_pharmacy.electronic_prescriptions
            SET
                status = @Verified,
                verification_method = @VerificationMethod,
                verified_by = @VerifiedBy,
                verified_at = @VerifiedAt,
                signed_at = COALESCE(@SignedAt, signed_at),
                expires_at = @ExpiresAt,
                updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
            """,
            new
            {
                Id = id,
                TenantId,
                Verified = PrescriptionStatuses.Verified,
                VerificationMethod = string.IsNullOrWhiteSpace(request.VerificationMethod)
                    ? "manual_check"
                    : request.VerificationMethod.Trim(),
                VerifiedBy = userId,
                VerifiedAt = verifiedAt,
                request.SignedAt,
                ExpiresAt = expiresAt,
            },
            tx);

        await InsertAuditAsync(
            conn,
            tx,
            id,
            "verify",
            userId,
            new { verifiedAt, expiresAt, validityDays },
            cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelPrescriptionAsync(
        Guid id,
        string? reason,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_pharmacy.electronic_prescriptions
            SET
                status = @Cancelled,
                cancelled_at = NOW(),
                updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND status <> @Dispensed
              AND status <> @Cancelled
            """,
            new
            {
                Id = id,
                TenantId,
                Cancelled = PrescriptionStatuses.Cancelled,
                Dispensed = PrescriptionStatuses.Dispensed,
            },
            tx) > 0;

        if (updated)
        {
            await InsertAuditAsync(conn, tx, id, "cancel", userId, new { reason }, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        else
        {
            await tx.RollbackAsync(cancellationToken);
        }

        return updated;
    }

    public async Task<PrescriptionPosLoadDto?> GetPosLoadAsync(
        Guid id,
        Guid warehouseId,
        short priceType,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT
                ep.id AS Id,
                ep.prescription_code AS PrescriptionCode,
                ep.status AS Status,
                ep.branch_id AS BranchId,
                ep.linked_prescriber_id AS LinkedPrescriberId,
                lp.full_name AS PrescriberName,
                ep.customer_id AS CustomerId,
                ep.patient_name AS PatientName,
                ep.patient_phone AS PatientPhone,
                ep.verified_at AS VerifiedAt,
                ep.expires_at AS ExpiresAt
            FROM pack_pharmacy.electronic_prescriptions ep
            INNER JOIN pack_pharmacy.linked_prescribers lp ON lp.id = ep.linked_prescriber_id
            WHERE ep.id = @Id
              AND ep.tenant_id = @TenantId
              AND ep.status <> @Cancelled
            """;

        const string linesSql = """
            SELECT
                l.id AS PrescriptionLineId,
                l.product_id AS ProductId,
                p.product_code AS ProductCode,
                p.product_name AS ProductName,
                COALESCE(l.product_unit_id, (
                    SELECT pu.id
                    FROM product_units pu
                    WHERE pu.product_id = l.product_id
                      AND pu.tenant_id = @TenantId
                      AND pu.status = 1
                    ORDER BY pu.is_base_unit DESC, pu.unit_name ASC
                    LIMIT 1
                )) AS ProductUnitId,
                u.unit_name AS UnitName,
                COALESCE(pr.price, 0) AS UnitPrice,
                l.qty_prescribed AS QtyPrescribed,
                l.qty_dispensed AS QtyDispensed,
                GREATEST(l.qty_prescribed - l.qty_dispensed, 0) AS QtyRemaining,
                COALESCE((
                    SELECT SUM(b.quantity_available)
                    FROM inventory_batches b
                    WHERE b.tenant_id = @TenantId
                      AND b.warehouse_id = @WarehouseId
                      AND b.product_id = l.product_id
                ), 0) AS StockAvailable,
                l.line_dispensing_class AS LineDispensingClass,
                l.dosage_instruction AS DosageInstruction
            FROM pack_pharmacy.electronic_prescription_lines l
            INNER JOIN products p ON p.id = l.product_id
            LEFT JOIN product_units u ON u.id = COALESCE(l.product_unit_id, (
                SELECT pu.id
                FROM product_units pu
                WHERE pu.product_id = l.product_id
                  AND pu.tenant_id = @TenantId
                  AND pu.status = 1
                ORDER BY pu.is_base_unit DESC, pu.unit_name ASC
                LIMIT 1
            ))
            LEFT JOIN product_prices pr
                ON pr.tenant_id = l.tenant_id
               AND pr.product_id = l.product_id
               AND pr.product_unit_id = COALESCE(l.product_unit_id, (
                   SELECT pu.id
                   FROM product_units pu
                   WHERE pu.product_id = l.product_id
                     AND pu.tenant_id = @TenantId
                     AND pu.status = 1
                   ORDER BY pu.is_base_unit DESC, pu.unit_name ASC
                   LIMIT 1
               ))
               AND pr.price_type = @PriceType
               AND pr.status = 1
               AND pr.effective_from <= NOW()
               AND (pr.effective_to IS NULL OR pr.effective_to > NOW())
            WHERE l.prescription_id = @PrescriptionId
              AND l.tenant_id = @TenantId
            ORDER BY l.sort_order, l.created_at
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var header = await conn.QuerySingleOrDefaultAsync<PrescriptionPosLoadHeaderRow>(headerSql, new
        {
            Id = id,
            TenantId,
            Cancelled = PrescriptionStatuses.Cancelled,
        });
        if (header is null)
            return null;

        var lines = (await conn.QueryAsync<PrescriptionPosLoadLineDto>(linesSql, new
        {
            PrescriptionId = id,
            TenantId,
            WarehouseId = warehouseId,
            PriceType = priceType,
        })).ToList();

        return new PrescriptionPosLoadDto(
            header.Id,
            header.PrescriptionCode,
            header.Status,
            header.BranchId,
            header.LinkedPrescriberId,
            header.PrescriberName,
            header.CustomerId,
            header.PatientName,
            header.PatientPhone,
            header.VerifiedAt,
            header.ExpiresAt,
            lines);
    }

    public async Task ApplyDispenseFromSaleAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid prescriptionId,
        Guid salesOrderId,
        IReadOnlyList<PrescriptionDispenseSaleItem> items,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var appliedItems = items
            .Where(i => i.PrescriptionLineId != Guid.Empty && i.Quantity > 0)
            .ToList();
        if (appliedItems.Count == 0)
            return;

        var header = await conn.QuerySingleOrDefaultAsync<PrescriptionDispenseHeaderRow>(
            """
            SELECT
                id AS Id,
                branch_id AS BranchId,
                status AS Status,
                expires_at AS ExpiresAt
            FROM pack_pharmacy.electronic_prescriptions
            WHERE id = @PrescriptionId
              AND tenant_id = @TenantId
            FOR UPDATE
            """,
            new { PrescriptionId = prescriptionId, TenantId },
            tx);
        if (header is null)
            throw new InvalidOperationException("Đơn thuốc không tồn tại.");

        if (header.Status is not (PrescriptionStatuses.Verified or PrescriptionStatuses.Signed or PrescriptionStatuses.PartiallyDispensed))
            throw new InvalidOperationException("Đơn thuốc chưa đủ điều kiện để cấp phát.");
        if (header.ExpiresAt.HasValue && header.ExpiresAt.Value < DateTime.UtcNow)
            throw new InvalidOperationException("Đơn thuốc đã hết hạn.");

        var orderBranchId = await conn.QuerySingleOrDefaultAsync<Guid?>(
            """
            SELECT branch_id
            FROM sales_orders
            WHERE id = @OrderId
              AND tenant_id = @TenantId
            """,
            new { OrderId = salesOrderId, TenantId },
            tx);

        foreach (var item in appliedItems)
        {
            var line = await conn.QuerySingleOrDefaultAsync<PrescriptionLineLockRow>(
                """
                SELECT
                    id AS Id,
                    qty_prescribed AS QtyPrescribed,
                    qty_dispensed AS QtyDispensed
                FROM pack_pharmacy.electronic_prescription_lines
                WHERE id = @LineId
                  AND prescription_id = @PrescriptionId
                  AND tenant_id = @TenantId
                FOR UPDATE
                """,
                new
                {
                    LineId = item.PrescriptionLineId,
                    PrescriptionId = prescriptionId,
                    TenantId,
                },
                tx) ?? throw new InvalidOperationException("Dòng đơn thuốc không tồn tại.");

            var remaining = Math.Max(0, line.QtyPrescribed - line.QtyDispensed);
            if (item.Quantity > remaining + 0.0001m)
                throw new InvalidOperationException("Số lượng cấp phát vượt quá số lượng còn lại trên đơn.");

            await conn.ExecuteAsync(
                """
                UPDATE pack_pharmacy.electronic_prescription_lines
                SET qty_dispensed = qty_dispensed + @Quantity
                WHERE id = @LineId
                  AND tenant_id = @TenantId
                """,
                new
                {
                    LineId = item.PrescriptionLineId,
                    TenantId,
                    item.Quantity,
                },
                tx);

            await conn.ExecuteAsync(
                """
                INSERT INTO pack_pharmacy.prescription_dispense_events (
                    tenant_id, prescription_id, prescription_line_id, sales_order_id,
                    sales_order_item_id, branch_id, qty, dispensed_by
                )
                VALUES (
                    @TenantId, @PrescriptionId, @PrescriptionLineId, @SalesOrderId,
                    @SalesOrderItemId, @BranchId, @Qty, @DispensedBy
                )
                """,
                new
                {
                    TenantId,
                    PrescriptionId = prescriptionId,
                    PrescriptionLineId = item.PrescriptionLineId,
                    SalesOrderId = salesOrderId,
                    item.SalesOrderItemId,
                    BranchId = orderBranchId ?? header.BranchId,
                    Qty = item.Quantity,
                    DispensedBy = userId,
                },
                tx);
        }

        var totalRemaining = await conn.QuerySingleAsync<decimal>(
            """
            SELECT COALESCE(SUM(GREATEST(qty_prescribed - qty_dispensed, 0)), 0)
            FROM pack_pharmacy.electronic_prescription_lines
            WHERE prescription_id = @PrescriptionId
              AND tenant_id = @TenantId
            """,
            new { PrescriptionId = prescriptionId, TenantId },
            tx);

        var nextStatus = totalRemaining <= 0.0001m
            ? PrescriptionStatuses.Dispensed
            : PrescriptionStatuses.PartiallyDispensed;

        await conn.ExecuteAsync(
            """
            UPDATE pack_pharmacy.electronic_prescriptions
            SET
                status = @Status,
                dispensed_at = CASE WHEN @Status = @Dispensed THEN NOW() ELSE dispensed_at END,
                updated_at = NOW()
            WHERE id = @PrescriptionId
              AND tenant_id = @TenantId
            """,
            new
            {
                PrescriptionId = prescriptionId,
                TenantId,
                Status = nextStatus,
                Dispensed = PrescriptionStatuses.Dispensed,
            },
            tx);

        await InsertAuditAsync(
            conn,
            tx,
            prescriptionId,
            "dispense_from_sale",
            userId,
            new { salesOrderId, itemCount = appliedItems.Count, status = nextStatus },
            cancellationToken);
    }

    public async Task<PrescriptionBasicInfo?> GetPrescriptionBasicInfoAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                branch_id AS BranchId,
                status AS Status,
                expires_at AS ExpiresAt
            FROM pack_pharmacy.electronic_prescriptions
            WHERE id = @Id
              AND tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PrescriptionBasicInfo>(sql, new { Id = id, TenantId });
    }

    public async Task<PrescriptionLineSimple?> GetPrescriptionLineAsync(Guid prescriptionLineId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                prescription_id AS PrescriptionId,
                product_id AS ProductId,
                qty_prescribed AS QtyPrescribed,
                qty_dispensed AS QtyDispensed
            FROM pack_pharmacy.electronic_prescription_lines
            WHERE id = @Id
              AND tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PrescriptionLineSimple>(sql, new { Id = prescriptionLineId, TenantId });
    }

    private static string NormalizeSource(string source) =>
        source.Trim().ToLowerInvariant() switch
        {
            "staff_entry" => "staff_entry",
            "prescriber_portal" => "prescriber_portal",
            "customer_upload" => "customer_upload",
            _ => "staff_entry",
        };

    private static IReadOnlyList<CreatePrescriptionLineRequest> NormalizeLines(IReadOnlyList<CreatePrescriptionLineRequest>? lines)
    {
        if (lines is null || lines.Count == 0)
            throw new InvalidOperationException("Đơn thuốc phải có ít nhất 1 dòng thuốc.");
        return lines;
    }

    private async Task EnsurePrescriberAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid prescriberId,
        CancellationToken cancellationToken)
    {
        var ok = await conn.QuerySingleAsync<bool>(
            """
            SELECT EXISTS(
                SELECT 1
                FROM pack_pharmacy.linked_prescribers
                WHERE id = @Id
                  AND tenant_id = @TenantId
                  AND deleted_at IS NULL
            )
            """,
            new { Id = prescriberId, TenantId },
            tx);
        if (!ok)
            throw new InvalidOperationException("Bác sĩ liên kết không tồn tại.");
    }

    private async Task InsertLinesAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid prescriptionId,
        IReadOnlyList<CreatePrescriptionLineRequest> lines,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
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
                new { line.ProductId, TenantId },
                tx);
            if (product is null)
                throw new InvalidOperationException("Sản phẩm trong đơn thuốc không tồn tại.");

            if (line.ProductUnitId is Guid unitId)
            {
                var unitOk = await conn.QuerySingleAsync<bool>(
                    """
                    SELECT EXISTS(
                        SELECT 1
                        FROM product_units
                        WHERE id = @UnitId
                          AND product_id = @ProductId
                          AND tenant_id = @TenantId
                          AND status = 1
                    )
                    """,
                    new { UnitId = unitId, ProductId = line.ProductId, TenantId },
                    tx);
                if (!unitOk)
                    throw new InvalidOperationException("Đơn vị kê đơn không hợp lệ.");
            }

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
                    TenantId,
                    PrescriptionId = prescriptionId,
                    line.ProductId,
                    line.ProductUnitId,
                    LineDispensingClass = product.DispensingClass,
                    line.QtyPrescribed,
                    DosageInstruction = line.DosageInstruction?.Trim(),
                    SortOrder = line.SortOrder == 0 ? i + 1 : line.SortOrder,
                },
                tx);
        }
    }

    private async Task InsertAuditAsync(
        IDbConnection conn,
        IDbTransaction tx,
        Guid prescriptionId,
        string action,
        Guid actorId,
        object? metadata,
        CancellationToken cancellationToken)
    {
        var metadataJson = metadata is null
            ? "{}"
            : JsonSerializer.Serialize(metadata);

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_pharmacy.prescription_audit_log (
                tenant_id, prescription_id, action, actor_id, metadata
            )
            VALUES (
                @TenantId, @PrescriptionId, @Action, @ActorId, @Metadata::jsonb
            )
            """,
            new
            {
                TenantId,
                PrescriptionId = prescriptionId,
                Action = action,
                ActorId = actorId,
                Metadata = metadataJson,
            },
            tx);
    }

    private async Task<int> GetPrescriptionValidityDaysAsync(
        IDbConnection conn,
        IDbTransaction tx,
        CancellationToken cancellationToken)
    {
        var days = await conn.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT NULLIF(settings->'rx'->>'prescription_validity_days', '')::int
            FROM tenants
            WHERE id = @TenantId
            """,
            new { TenantId },
            tx);
        if (days is null or <= 0)
            return 7;
        return Math.Clamp(days.Value, 1, 365);
    }

    private sealed record ProductLineInfo(Guid ProductId, string DispensingClass);

    private sealed class PrescriptionHeaderRow
    {
        public Guid Id { get; init; }
        public string PrescriptionCode { get; init; } = "";
        public Guid? BranchId { get; init; }
        public Guid LinkedPrescriberId { get; init; }
        public string PrescriberName { get; init; } = "";
        public Guid? CustomerId { get; init; }
        public string? PatientName { get; init; }
        public string? PatientPhone { get; init; }
        public string Status { get; init; } = "";
        public string Source { get; init; } = "";
        public string? VerificationMethod { get; init; }
        public Guid? VerifiedBy { get; init; }
        public DateTime? VerifiedAt { get; init; }
        public DateTime? SignedAt { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public DateTime? DispensedAt { get; init; }
        public string? Notes { get; init; }
        public Guid? CreatedBy { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        public DateTime? CancelledAt { get; init; }

        public PrescriptionDetailDto ToDto(
            IReadOnlyList<PrescriptionLineDto> lines,
            IReadOnlyList<PrescriptionAttachmentDto> attachments) =>
            new(
                Id,
                PrescriptionCode,
                BranchId,
                LinkedPrescriberId,
                PrescriberName,
                CustomerId,
                PatientName,
                PatientPhone,
                Status,
                Source,
                VerificationMethod,
                VerifiedBy,
                VerifiedAt,
                SignedAt,
                ExpiresAt,
                DispensedAt,
                Notes,
                CreatedBy,
                CreatedAt,
                UpdatedAt,
                CancelledAt,
                lines,
                attachments);
    }

    private sealed class PrescriptionPosLoadHeaderRow
    {
        public Guid Id { get; init; }
        public string PrescriptionCode { get; init; } = "";
        public string Status { get; init; } = "";
        public Guid? BranchId { get; init; }
        public Guid LinkedPrescriberId { get; init; }
        public string PrescriberName { get; init; } = "";
        public Guid? CustomerId { get; init; }
        public string? PatientName { get; init; }
        public string? PatientPhone { get; init; }
        public DateTime? VerifiedAt { get; init; }
        public DateTime? ExpiresAt { get; init; }
    }

    private sealed class PrescriptionDispenseHeaderRow
    {
        public Guid Id { get; init; }
        public Guid? BranchId { get; init; }
        public string Status { get; init; } = "";
        public DateTime? ExpiresAt { get; init; }
    }

    private sealed class PrescriptionLineLockRow
    {
        public Guid Id { get; init; }
        public decimal QtyPrescribed { get; init; }
        public decimal QtyDispensed { get; init; }
    }

    public async Task<TenantRxDashboardDto> GetTenantDashboardAsync(CancellationToken cancellationToken)
    {
        const string summarySql = """
            SELECT
                COUNT(*) FILTER (
                    WHERE ep.status <> @Cancelled
                      AND ep.signed_at >= date_trunc('month', NOW() AT TIME ZONE 'UTC')
                )::int AS SignedThisMonth,
                COUNT(*) FILTER (WHERE ep.status <> @Cancelled)::int AS SignedTotal,
                COUNT(*) FILTER (
                    WHERE ep.status IN (@Signed, @Partial)
                )::int AS PendingDispense,
                COUNT(*) FILTER (
                    WHERE ep.status IN (@Signed, @Partial)
                      AND COALESCE(ep.signed_at, ep.created_at) < NOW() - INTERVAL '24 hours'
                )::int AS OverduePendingDispense,
                COUNT(*) FILTER (
                    WHERE ep.status = @PendingVerification
                )::int AS PendingVerification,
                COUNT(*) FILTER (
                    WHERE ep.status <> @Cancelled
                      AND ep.source = @PortalSource
                      AND ep.signed_at >= date_trunc('month', NOW() AT TIME ZONE 'UTC')
                )::int AS PortalSignedThisMonth,
                COUNT(*) FILTER (
                    WHERE ep.status <> @Cancelled
                      AND COALESCE(ep.source, '') <> @PortalSource
                      AND ep.signed_at >= date_trunc('month', NOW() AT TIME ZONE 'UTC')
                )::int AS StaffSignedThisMonth,
                AVG(
                    EXTRACT(EPOCH FROM (ep.dispensed_at - ep.signed_at)) / 3600.0
                ) FILTER (
                    WHERE ep.dispensed_at IS NOT NULL
                      AND ep.signed_at IS NOT NULL
                      AND ep.signed_at >= NOW() - INTERVAL '90 days'
                ) AS AvgHoursToDispense
            FROM pack_pharmacy.electronic_prescriptions ep
            WHERE ep.tenant_id = @TenantId
            """;

        const string linksSql = """
            SELECT
                COUNT(*) FILTER (WHERE link_status = @PendingApproval)::int AS PendingLinkApprovals,
                COUNT(*) FILTER (WHERE link_status = @Active)::int AS ActivePrescriberLinks
            FROM pack_pharmacy.prescriber_tenant_links
            WHERE tenant_id = @TenantId
            """;

        const string topSql = """
            SELECT
                ep.prescriber_id AS "PrescriberId",
                COALESCE(p.full_name, lp.full_name, '—') AS "PrescriberName",
                COALESCE(p.phone, lp.phone) AS "Phone",
                COUNT(*) FILTER (
                    WHERE ep.signed_at >= date_trunc('month', NOW() AT TIME ZONE 'UTC')
                )::int AS "SignedThisMonth",
                COUNT(*)::int AS "SignedTotal"
            FROM pack_pharmacy.electronic_prescriptions ep
            LEFT JOIN pack_pharmacy.prescribers p ON p.id = ep.prescriber_id
            LEFT JOIN pack_pharmacy.linked_prescribers lp ON lp.id = ep.linked_prescriber_id
            WHERE ep.tenant_id = @TenantId
              AND ep.status <> @Cancelled
            GROUP BY ep.prescriber_id, p.full_name, lp.full_name, p.phone, lp.phone
            ORDER BY "SignedThisMonth" DESC, "SignedTotal" DESC, "PrescriberName"
            LIMIT 10
            """;

        const string pendingSql = """
            SELECT
                ep.id AS "Id",
                ep.prescription_code AS "PrescriptionCode",
                COALESCE(p.full_name, lp.full_name) AS "PrescriberName",
                ep.patient_name AS "PatientName",
                ep.status AS "Status",
                ep.signed_at AS "SignedAt",
                (EXTRACT(EPOCH FROM (NOW() - COALESCE(ep.signed_at, ep.created_at))) / 3600.0)::float8 AS "HoursWaiting"
            FROM pack_pharmacy.electronic_prescriptions ep
            LEFT JOIN pack_pharmacy.prescribers p ON p.id = ep.prescriber_id
            LEFT JOIN pack_pharmacy.linked_prescribers lp ON lp.id = ep.linked_prescriber_id
            WHERE ep.tenant_id = @TenantId
              AND ep.status IN (@Signed, @Partial)
            ORDER BY COALESCE(ep.signed_at, ep.created_at) ASC
            LIMIT 15
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var args = new
        {
            TenantId,
            Cancelled = PrescriptionStatuses.Cancelled,
            Signed = PrescriptionStatuses.Signed,
            Partial = PrescriptionStatuses.PartiallyDispensed,
            PendingVerification = PrescriptionStatuses.PendingVerification,
            PortalSource = "prescriber_portal",
            PendingApproval = PrescriberLinkStatuses.PendingNtApproval,
            Active = PrescriberLinkStatuses.Active,
        };

        var summary = await conn.QuerySingleAsync<TenantRxDashboardSummaryRow>(summarySql, args);
        var links = await conn.QuerySingleAsync<(int PendingLinkApprovals, int ActivePrescriberLinks)>(linksSql, args);
        var topRows = (await conn.QueryAsync<TenantRxTopPrescriberRow>(topSql, args)).ToList();
        var pendingRows = (await conn.QueryAsync<TenantRxPendingItemRow>(pendingSql, args)).ToList();

        return new TenantRxDashboardDto(
            summary.SignedThisMonth,
            summary.SignedTotal,
            summary.PendingDispense,
            summary.OverduePendingDispense,
            summary.PendingVerification,
            links.PendingLinkApprovals,
            links.ActivePrescriberLinks,
            summary.PortalSignedThisMonth,
            summary.StaffSignedThisMonth,
            summary.AvgHoursToDispense,
            topRows.Select(r => new TenantRxTopPrescriberDto(
                r.PrescriberId,
                r.PrescriberName,
                r.Phone,
                r.SignedThisMonth,
                r.SignedTotal)).ToList(),
            pendingRows.Select(r => new TenantRxPendingItemDto(
                r.Id,
                r.PrescriptionCode,
                r.PrescriberName,
                r.PatientName,
                r.Status,
                r.SignedAt,
                r.HoursWaiting.HasValue ? (double)r.HoursWaiting.Value : null)).ToList());
    }

    private sealed class TenantRxDashboardSummaryRow
    {
        public int SignedThisMonth { get; init; }
        public int SignedTotal { get; init; }
        public int PendingDispense { get; init; }
        public int OverduePendingDispense { get; init; }
        public int PendingVerification { get; init; }
        public int PortalSignedThisMonth { get; init; }
        public int StaffSignedThisMonth { get; init; }
        public double? AvgHoursToDispense { get; init; }
    }

    private sealed class TenantRxTopPrescriberRow
    {
        public Guid? PrescriberId { get; init; }
        public string PrescriberName { get; init; } = "—";
        public string? Phone { get; init; }
        public int SignedThisMonth { get; init; }
        public int SignedTotal { get; init; }
    }

    private sealed class TenantRxPendingItemRow
    {
        public Guid Id { get; init; }
        public string PrescriptionCode { get; init; } = "";
        public string? PrescriberName { get; init; }
        public string? PatientName { get; init; }
        public string Status { get; init; } = "";
        public DateTime? SignedAt { get; init; }
        public decimal? HoursWaiting { get; init; }
    }
}

internal sealed record PrescriptionBasicInfo(
    Guid Id,
    Guid? BranchId,
    string Status,
    DateTime? ExpiresAt);

internal sealed record PrescriptionLineSimple(
    Guid Id,
    Guid PrescriptionId,
    Guid ProductId,
    decimal QtyPrescribed,
    decimal QtyDispensed);
