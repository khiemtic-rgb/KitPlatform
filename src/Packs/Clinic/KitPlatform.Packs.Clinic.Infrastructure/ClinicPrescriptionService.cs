using System.Security.Cryptography;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Workspace;
using KitPlatform.Packs.Clinic;
using KitPlatform.Packs.Connect;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace KitPlatform.Packs.Clinic.Infrastructure;

internal sealed class ClinicPrescriptionRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ClinicPrescriptionRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Guid CurrentTenantId => _tenant.TenantId;
    public Guid CurrentUserId => _tenant.UserId;

    private const string HeaderSql = """
        SELECT
            r.id AS Id,
            r.visit_id AS VisitId,
            r.customer_id AS CustomerId,
            c.full_name AS CustomerName,
            c.phone AS CustomerPhone,
            r.provider_id AS ProviderId,
            p.display_name AS ProviderDisplayName,
            r.prescription_code AS PrescriptionCode,
            r.prescription_status AS PrescriptionStatus,
            r.diagnosis_text AS DiagnosisText,
            r.notes AS Notes,
            r.finalized_at AS FinalizedAt,
            r.pdf_sha256 AS PdfSha256,
            h.pharmacy_tenant_id AS PharmacyTenantId,
            h.created_at AS SentAt,
            h.id AS ConnectHandoffId,
            sig.signed_at AS SignedAt,
            sig.signature_provider AS SignatureProvider,
            r.created_at AS CreatedAt
        FROM pack_clinic.clinic_prescription r
        LEFT JOIN public.customers c ON c.id = r.customer_id AND c.tenant_id = r.tenant_id
        LEFT JOIN pack_clinic.clinic_provider p ON p.id = r.provider_id AND p.tenant_id = r.tenant_id
        LEFT JOIN pack_connect.rx_handoffs h
            ON h.clinic_tenant_id = r.tenant_id
           AND h.clinic_prescription_id = r.id
        LEFT JOIN pack_connect.clinic_rx_signatures sig
            ON sig.clinic_tenant_id = r.tenant_id
           AND sig.clinic_prescription_id = r.id
        """;

    public async Task<IReadOnlyList<PrescriptionHeaderRow>> ListHeadersByVisitAsync(
        Guid? workspaceId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {HeaderSql}
            WHERE r.tenant_id = @TenantId
              AND r.visit_id = @VisitId
              AND r.deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR r.workspace_id = @WorkspaceId)
            ORDER BY r.created_at DESC
            LIMIT 50
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<PrescriptionHeaderRow>(sql, new
        {
            TenantId = CurrentTenantId,
            VisitId = visitId,
            WorkspaceId = workspaceId,
        })).ToList();
    }

    public async Task<PrescriptionHeaderRow?> GetHeaderAsync(
        Guid? workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {HeaderSql}
            WHERE r.tenant_id = @TenantId
              AND r.id = @Id
              AND r.deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR r.workspace_id = @WorkspaceId)
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<PrescriptionHeaderRow>(sql, new
        {
            TenantId = CurrentTenantId,
            Id = id,
            WorkspaceId = workspaceId,
        });
    }

    public async Task<IReadOnlyList<ClinicPrescriptionLineDto>> ListLinesAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                drug_name AS DrugName,
                strength AS Strength,
                quantity AS Quantity,
                unit AS Unit,
                dosage_instruction AS DosageInstruction,
                sort_order AS SortOrder
            FROM pack_clinic.clinic_prescription_line
            WHERE prescription_id = @Id
              AND tenant_id = @TenantId
            ORDER BY sort_order, created_at
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ClinicPrescriptionLineDto>(sql, new
        {
            Id = prescriptionId,
            TenantId = CurrentTenantId,
        })).ToList();
    }

    /// <summary>
    /// Gắn bác sĩ còn thiếu lên đơn (kể cả đã finalized) — dùng trước gửi NT / khi visit vừa gán BS.
    /// </summary>
    public async Task<bool> AttachProviderIfMissingAsync(
        Guid? workspaceId,
        Guid prescriptionId,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        const string updateSql = """
            UPDATE pack_clinic.clinic_prescription
            SET provider_id = @ProviderId,
                updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
              AND provider_id IS NULL
              AND prescription_status <> @Cancelled
              AND (@WorkspaceId IS NULL OR workspace_id = @WorkspaceId)
            """;
        var n = await conn.ExecuteAsync(updateSql, new
        {
            Id = prescriptionId,
            TenantId = CurrentTenantId,
            WorkspaceId = workspaceId,
            ProviderId = providerId,
            Cancelled = ClinicPrescriptionStatuses.Cancelled,
        });
        return n > 0;
    }

    public async Task<int> SyncProviderToVisitPrescriptionsAsync(
        Guid visitId,
        Guid providerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_clinic.clinic_prescription
            SET provider_id = @ProviderId,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND visit_id = @VisitId
              AND deleted_at IS NULL
              AND provider_id IS NULL
              AND prescription_status <> @Cancelled
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteAsync(sql, new
        {
            TenantId = CurrentTenantId,
            VisitId = visitId,
            ProviderId = providerId,
            Cancelled = ClinicPrescriptionStatuses.Cancelled,
        });
    }

    public async Task SyncHandoffProviderDisplayNameAsync(
        Guid clinicPrescriptionId,
        string providerDisplayName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(providerDisplayName)) return;
        const string sql = """
            UPDATE pack_connect.rx_handoffs
            SET provider_display_name = @Name,
                updated_at = NOW()
            WHERE clinic_prescription_id = @RxId
              AND clinic_tenant_id = @TenantId
              AND (provider_display_name IS NULL OR BTRIM(provider_display_name) = '')
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            RxId = clinicPrescriptionId,
            TenantId = CurrentTenantId,
            Name = providerDisplayName.Trim(),
        });
    }

    public async Task<VisitSnap?> GetVisitAsync(
        Guid? workspaceId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                customer_id AS CustomerId,
                provider_id AS ProviderId,
                visit_status AS VisitStatus,
                diagnosis_summary AS DiagnosisSummary
            FROM pack_clinic.clinic_visit
            WHERE tenant_id = @TenantId
              AND id = @Id
              AND deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR workspace_id = @WorkspaceId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<VisitSnap>(sql, new
        {
            TenantId = CurrentTenantId,
            Id = visitId,
            WorkspaceId = workspaceId,
        });
    }

    /// <summary>
    /// Nhà thuốc nguồn Connect (appointment metadata → booking → referral). Null = walk-in / tự do chọn.
    /// </summary>
    public async Task<Guid?> ResolvePreferredPharmacyTenantIdAsync(
        Guid? workspaceId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COALESCE(
                NULLIF(a.metadata->>'pharmacy_tenant_id', '')::uuid,
                b.pharmacy_tenant_id,
                r.pharmacy_tenant_id
            )
            FROM pack_clinic.clinic_visit v
            LEFT JOIN pack_clinic.clinic_appointment a
                ON a.id = v.appointment_id AND a.tenant_id = v.tenant_id AND a.deleted_at IS NULL
            LEFT JOIN pack_connect.bookings b
                ON b.id = NULLIF(a.metadata->>'connect_booking_id', '')::uuid
               AND b.clinic_tenant_id = v.tenant_id
            LEFT JOIN pack_connect.referrals r
                ON r.id = COALESCE(
                    NULLIF(a.metadata->>'connect_referral_id', '')::uuid,
                    b.referral_id)
               AND r.clinic_tenant_id = v.tenant_id
            WHERE v.tenant_id = @TenantId
              AND v.id = @VisitId
              AND v.deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR v.workspace_id = @WorkspaceId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<Guid?>(sql, new
        {
            TenantId = CurrentTenantId,
            VisitId = visitId,
            WorkspaceId = workspaceId,
        });
    }

    public async Task<string> NextCodeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::int
            FROM pack_clinic.clinic_prescription
            WHERE tenant_id = @TenantId
              AND created_at::date = CURRENT_DATE
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteScalarAsync<int>(sql, new { TenantId = CurrentTenantId });
        return $"CLX-{DateTime.UtcNow:yyyyMMdd}-{(n + 1):D4}";
    }

    public async Task<Guid> InsertAsync(
        Guid workspaceId,
        Guid visitId,
        Guid customerId,
        Guid? providerId,
        string code,
        string? diagnosis,
        string? notes,
        IReadOnlyList<ClinicPrescriptionLineInput> lines,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string insertRx = """
            INSERT INTO pack_clinic.clinic_prescription (
                tenant_id, workspace_id, visit_id, customer_id, provider_id,
                prescription_code, prescription_status, diagnosis_text, notes, created_by
            )
            VALUES (
                @TenantId, @WorkspaceId, @VisitId, @CustomerId, @ProviderId,
                @Code, @Status, @Diagnosis, @Notes, @Actor
            )
            RETURNING id
            """;
        var id = await conn.QuerySingleAsync<Guid>(insertRx, new
        {
            TenantId = CurrentTenantId,
            WorkspaceId = workspaceId,
            VisitId = visitId,
            CustomerId = customerId,
            ProviderId = providerId,
            Code = code,
            Status = ClinicPrescriptionStatuses.Draft,
            Diagnosis = diagnosis,
            Notes = notes,
            Actor = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId,
        }, tx);

        await InsertLinesAsync(conn, tx, id, lines, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<bool> UpdateDraftAsync(
        Guid? workspaceId,
        Guid id,
        Guid? providerId,
        string? diagnosis,
        string? notes,
        IReadOnlyList<ClinicPrescriptionLineInput>? lines,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        const string updateSql = """
            UPDATE pack_clinic.clinic_prescription
            SET provider_id = COALESCE(@ProviderId, provider_id),
                diagnosis_text = COALESCE(@Diagnosis, diagnosis_text),
                notes = COALESCE(@Notes, notes),
                updated_at = NOW()
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
              AND prescription_status = @Draft
              AND (@WorkspaceId IS NULL OR workspace_id = @WorkspaceId)
            """;
        var n = await conn.ExecuteAsync(updateSql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
            WorkspaceId = workspaceId,
            ProviderId = providerId,
            Diagnosis = diagnosis,
            Notes = notes,
            Draft = ClinicPrescriptionStatuses.Draft,
        }, tx);
        if (n == 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        if (lines is not null)
        {
            await conn.ExecuteAsync(
                """
                DELETE FROM pack_clinic.clinic_prescription_line
                WHERE prescription_id = @Id AND tenant_id = @TenantId
                """,
                new { Id = id, TenantId = CurrentTenantId },
                tx);
            await InsertLinesAsync(conn, tx, id, lines, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetStatusAsync(
        Guid? workspaceId,
        Guid id,
        string fromStatus,
        string toStatus,
        string? pdfSha256,
        CancellationToken cancellationToken)
    {
        var finalizeCols = toStatus == ClinicPrescriptionStatuses.Finalized
            ? ", finalized_at = NOW(), finalized_by = @Actor, pdf_sha256 = COALESCE(@PdfSha, pdf_sha256)"
            : "";
        var sql = $"""
            UPDATE pack_clinic.clinic_prescription
            SET prescription_status = @ToStatus,
                updated_at = NOW()
                {finalizeCols}
            WHERE id = @Id
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
              AND prescription_status = @FromStatus
              AND (@WorkspaceId IS NULL OR workspace_id = @WorkspaceId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
            WorkspaceId = workspaceId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            PdfSha = pdfSha256,
            Actor = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId,
        });
        return n > 0;
    }

    public async Task InsertSignatureAsync(
        Guid prescriptionId,
        string pdfSha256,
        string signatureAlg,
        string signatureValue,
        string signatureProvider,
        string? signerCertThumbprint,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_connect.clinic_rx_signatures (
                clinic_tenant_id, clinic_prescription_id, pdf_sha256,
                signature_alg, signature_value, signer_cert_thumbprint,
                signature_provider, signed_by, signed_at
            )
            VALUES (
                @TenantId, @PrescriptionId, @PdfSha,
                @Alg, @Value, @Thumbprint,
                @Provider, @Actor, NOW()
            )
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = CurrentTenantId,
            PrescriptionId = prescriptionId,
            PdfSha = pdfSha256,
            Alg = signatureAlg,
            Value = signatureValue,
            Thumbprint = signerCertThumbprint,
            Provider = signatureProvider,
            Actor = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId,
        });
    }

    public async Task SetPdfHashAsync(Guid id, string sha256, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_clinic.clinic_prescription
            SET pdf_sha256 = @Sha, updated_at = NOW()
            WHERE id = @Id AND tenant_id = @TenantId AND deleted_at IS NULL
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { Id = id, TenantId = CurrentTenantId, Sha = sha256 });
    }

    /// <summary>Best-effort: widen status to signed when CHECK allows; Soft-CKS overlays via signature row.</summary>
    public async Task TrySetSignedStatusAsync(
        Guid? workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await SetStatusAsync(
                workspaceId,
                id,
                ClinicPrescriptionStatuses.Finalized,
                ClinicPrescriptionStatuses.Signed,
                null,
                cancellationToken);
        }
        catch
        {
            // Constraint may still be draft|finalized|cancelled — signature table is source of truth.
        }
    }

    private async Task InsertLinesAsync(
        System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction tx,
        Guid prescriptionId,
        IReadOnlyList<ClinicPrescriptionLineInput> lines,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_clinic.clinic_prescription_line (
                tenant_id, prescription_id, drug_name, strength, quantity, unit, dosage_instruction, sort_order
            )
            VALUES (
                @TenantId, @RxId, @DrugName, @Strength, @Qty, @Unit, @Dosage, @Sort
            )
            """;
        var sort = 0;
        foreach (var line in lines)
        {
            await conn.ExecuteAsync(sql, new
            {
                TenantId = CurrentTenantId,
                RxId = prescriptionId,
                DrugName = line.DrugName.Trim(),
                Strength = string.IsNullOrWhiteSpace(line.Strength) ? null : line.Strength.Trim(),
                Qty = line.Quantity <= 0 ? 1m : line.Quantity,
                Unit = string.IsNullOrWhiteSpace(line.Unit) ? null : line.Unit.Trim(),
                Dosage = string.IsNullOrWhiteSpace(line.DosageInstruction) ? null : line.DosageInstruction.Trim(),
                Sort = sort++,
            }, tx);
        }
    }

    internal sealed class PrescriptionHeaderRow
    {
        public Guid Id { get; init; }
        public Guid VisitId { get; init; }
        public Guid CustomerId { get; init; }
        public string? CustomerName { get; init; }
        public string? CustomerPhone { get; init; }
        public Guid? ProviderId { get; init; }
        public string? ProviderDisplayName { get; init; }
        public string PrescriptionCode { get; init; } = "";
        public string PrescriptionStatus { get; init; } = "";
        public string? DiagnosisText { get; init; }
        public string? Notes { get; init; }
        public DateTime? FinalizedAt { get; init; }
        public string? PdfSha256 { get; init; }
        public Guid? PharmacyTenantId { get; init; }
        public DateTime? SentAt { get; init; }
        public Guid? ConnectHandoffId { get; init; }
        public DateTime? SignedAt { get; init; }
        public string? SignatureProvider { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    internal sealed class VisitSnap
    {
        public Guid Id { get; init; }
        public Guid CustomerId { get; init; }
        public Guid? ProviderId { get; init; }
        public string VisitStatus { get; init; } = "";
        public string? DiagnosisSummary { get; init; }
    }
}

internal sealed class ClinicPrescriptionService : IClinicPrescriptionService
{
    private readonly ClinicPrescriptionRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly IWorkspaceResolver _workspace;
    private readonly IConnectRxHandoffService _handoffs;
    private readonly IClinicPrescriptionSigner _signer;
    private readonly ClinicCksSettings _cks;
    private readonly IClinicTenantSettingsService _clinicSettings;

    public ClinicPrescriptionService(
        ClinicPrescriptionRepository repo,
        ITenantContext tenant,
        IWorkspaceResolver workspace,
        IConnectRxHandoffService handoffs,
        IClinicPrescriptionSigner signer,
        Microsoft.Extensions.Options.IOptions<ClinicCksSettings> cks,
        IClinicTenantSettingsService clinicSettings)
    {
        _repo = repo;
        _tenant = tenant;
        _workspace = workspace;
        _handoffs = handoffs;
        _signer = signer;
        _cks = cks.Value;
        _clinicSettings = clinicSettings;
    }

    public async Task<IReadOnlyList<ClinicPrescriptionDto>> ListByVisitAsync(
        Guid visitId,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveWorkspaceAsync(cancellationToken);
        var headers = await _repo.ListHeadersByVisitAsync(workspaceId, visitId, cancellationToken);
        var list = new List<ClinicPrescriptionDto>();
        foreach (var h in headers)
            list.Add(await ToDtoAsync(h, cancellationToken));
        return list;
    }

    public async Task<ClinicPrescriptionDto?> GetAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveWorkspaceAsync(cancellationToken);
        var header = await _repo.GetHeaderAsync(workspaceId, prescriptionId, cancellationToken);
        return header is null ? null : await ToDtoAsync(header, cancellationToken);
    }

    public async Task<ClinicPrescriptionDto> CreateAsync(
        CreateClinicPrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveWorkspaceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Workspace clinic_crm chưa được provision.");

        var visit = await _repo.GetVisitAsync(workspaceId, request.VisitId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy lượt khám.");
        if (visit.VisitStatus == "cancelled")
            throw new InvalidOperationException("Không kê đơn trên visit đã hủy.");

        var lines = NormalizeLines(request.Lines);
        if (lines.Count == 0)
            throw new InvalidOperationException("Đơn phải có ít nhất một dòng thuốc.");

        var code = await _repo.NextCodeAsync(cancellationToken);
        var diagnosis = string.IsNullOrWhiteSpace(request.DiagnosisText)
            ? visit.DiagnosisSummary
            : request.DiagnosisText.Trim();
        var providerId = request.ProviderId is Guid p && p != Guid.Empty ? p : visit.ProviderId;
        if (providerId is null || providerId == Guid.Empty)
            throw new InvalidOperationException(
                "Phải chọn bác sĩ kê đơn trước khi tạo đơn — dùng để truy trách nhiệm khi gửi nhà thuốc.");

        var id = await _repo.InsertAsync(
            workspaceId,
            request.VisitId,
            visit.CustomerId,
            providerId,
            code,
            diagnosis,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            lines,
            cancellationToken);

        return (await GetAsync(id, cancellationToken))!;
    }

    public async Task<ClinicPrescriptionDto?> UpdateAsync(
        Guid prescriptionId,
        UpdateClinicPrescriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveWorkspaceAsync(cancellationToken);
        var header = await _repo.GetHeaderAsync(workspaceId, prescriptionId, cancellationToken);
        if (header is null) return null;
        if (header.PrescriptionStatus != ClinicPrescriptionStatuses.Draft)
            throw new InvalidOperationException("Chỉ sửa được đơn đang nháp.");

        IReadOnlyList<ClinicPrescriptionLineInput>? lines = null;
        if (request.Lines is not null)
        {
            lines = NormalizeLines(request.Lines);
            if (lines.Count == 0)
                throw new InvalidOperationException("Đơn phải có ít nhất một dòng thuốc.");
        }

        var ok = await _repo.UpdateDraftAsync(
            workspaceId,
            prescriptionId,
            request.ProviderId,
            request.DiagnosisText is null ? null : request.DiagnosisText.Trim(),
            request.Notes is null ? null : (string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()),
            lines,
            cancellationToken);
        if (!ok) return null;
        return await GetAsync(prescriptionId, cancellationToken);
    }

    public async Task<ClinicPrescriptionDto?> FinalizeAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveWorkspaceAsync(cancellationToken);
        var dto = await GetAsync(prescriptionId, cancellationToken);
        if (dto is null) return null;
        if (dto.PrescriptionStatus != ClinicPrescriptionStatuses.Draft)
            throw new InvalidOperationException("Chỉ hoàn tất đơn đang nháp.");
        if (dto.Lines.Count == 0)
            throw new InvalidOperationException("Đơn trống — không hoàn tất được.");

        dto = await EnsurePrescriptionHasProviderAsync(workspaceId, dto, cancellationToken)
            ?? throw new InvalidOperationException(
                "Chưa có bác sĩ trên đơn — chọn bác sĩ khám rồi lưu lượt khám trước khi hoàn tất.");

        var clinicHeader = await _clinicSettings.GetAsync(cancellationToken);
        var (pdf, sha) = BuildPdf(dto, clinicHeader);
        var ok = await _repo.SetStatusAsync(
            workspaceId,
            prescriptionId,
            ClinicPrescriptionStatuses.Draft,
            ClinicPrescriptionStatuses.Finalized,
            sha,
            cancellationToken);
        if (!ok) return null;
        _ = pdf;
        return await GetAsync(prescriptionId, cancellationToken);
    }

    public async Task<ClinicPrescriptionDto?> SignAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        if (!_cks.Enabled)
            throw new InvalidOperationException("Soft-CKS chưa bật (Clinic:Cks:Enabled).");

        var workspaceId = await ResolveWorkspaceAsync(cancellationToken);
        var dto = await GetAsync(prescriptionId, cancellationToken);
        if (dto is null) return null;

        if (dto.PrescriptionStatus == ClinicPrescriptionStatuses.Signed || dto.SignedAt is not null)
            throw new InvalidOperationException("Đơn đã được ký.");
        if (dto.PrescriptionStatus != ClinicPrescriptionStatuses.Finalized)
            throw new InvalidOperationException("Chỉ ký đơn đã hoàn tất (nội bộ).");
        if (dto.Lines.Count == 0)
            throw new InvalidOperationException("Đơn trống — không ký được.");
        if (dto.ProviderId is null || dto.ProviderId == Guid.Empty ||
            string.IsNullOrWhiteSpace(dto.ProviderDisplayName))
            throw new InvalidOperationException(
                "Chưa có bác sĩ trên đơn — không ký được.");

        // Rebuild PDF with mock-signed footer so hash matches GetPdf after sign.
        var signedPreview = dto with
        {
            PrescriptionStatus = ClinicPrescriptionStatuses.Signed,
            SignatureProvider = _signer.ProviderCode,
            SignedAt = null,
        };
        var clinicHeader = await _clinicSettings.GetAsync(cancellationToken);
        var built = BuildPdf(signedPreview, clinicHeader);
        await _repo.SetPdfHashAsync(prescriptionId, built.Sha256, cancellationToken);

        var result = await _signer.SignAsync(
            new ClinicSignRequest(
                prescriptionId,
                dto.PrescriptionCode,
                built.Sha256,
                _tenant.UserId == Guid.Empty ? null : _tenant.UserId,
                dto.ProviderDisplayName),
            cancellationToken);

        await _repo.InsertSignatureAsync(
            prescriptionId,
            built.Sha256,
            result.SignatureAlg,
            result.SignatureValue,
            result.SignatureProvider,
            result.SignerCertThumbprint,
            cancellationToken);

        await _repo.TrySetSignedStatusAsync(workspaceId, prescriptionId, cancellationToken);
        return await GetAsync(prescriptionId, cancellationToken);
    }

    public async Task<ClinicPrescriptionDto?> CancelAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveWorkspaceAsync(cancellationToken);
        var dto = await GetAsync(prescriptionId, cancellationToken);
        if (dto is null) return null;
        if (dto.PrescriptionStatus == ClinicPrescriptionStatuses.Cancelled)
            return dto;
        if (dto.PrescriptionStatus is ClinicPrescriptionStatuses.Finalized or ClinicPrescriptionStatuses.Signed
            || dto.SignedAt is not null)
            throw new InvalidOperationException("Không hủy đơn đã hoàn tất / đã ký.");

        var ok = await _repo.SetStatusAsync(
            workspaceId,
            prescriptionId,
            ClinicPrescriptionStatuses.Draft,
            ClinicPrescriptionStatuses.Cancelled,
            null,
            cancellationToken);
        if (!ok) return null;
        return await GetAsync(prescriptionId, cancellationToken);
    }

    public async Task<ClinicPrescriptionDto?> SendToPharmacyAsync(
        Guid prescriptionId,
        SendClinicPrescriptionToPharmacyRequest request,
        CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync(prescriptionId, cancellationToken);
        if (dto is null) return null;

        var isFinalized = dto.PrescriptionStatus == ClinicPrescriptionStatuses.Finalized;
        var isSigned = dto.PrescriptionStatus == ClinicPrescriptionStatuses.Signed || dto.SignedAt is not null;
        if (_cks.RequireSignedBeforeSend)
        {
            if (!isSigned)
                throw new InvalidOperationException("Cần ký Soft-CKS trước khi gửi nhà thuốc (RequireSignedBeforeSend).");
        }
        else if (!isFinalized && !isSigned)
        {
            throw new InvalidOperationException("Chỉ gửi đơn đã hoàn tất hoặc đã ký tới nhà thuốc.");
        }

        if (dto.SentAt is not null || dto.ConnectHandoffId is not null)
            throw new InvalidOperationException("Đơn này đã được gửi tới nhà thuốc.");
        if (request.PharmacyTenantId == Guid.Empty)
            throw new InvalidOperationException("PharmacyTenantId không hợp lệ.");
        if (dto.Lines.Count == 0)
            throw new InvalidOperationException("Đơn trống — không gửi được.");

        var workspaceId = await ResolveWorkspaceAsync(cancellationToken);

        // Lượt từ Connect (NT giới thiệu) → khóa đúng NT nguồn; walk-in thì được chọn tự do trong partner list.
        var preferredPharmacy = await _repo.ResolvePreferredPharmacyTenantIdAsync(
            workspaceId, dto.VisitId, cancellationToken);
        var targetPharmacyId = request.PharmacyTenantId;
        if (preferredPharmacy is Guid locked && locked != Guid.Empty)
        {
            if (targetPharmacyId != locked)
                throw new InvalidOperationException(
                    "Lượt khám này đến từ nhà thuốc Connect — chỉ gửi lại đúng nhà thuốc đã giới thiệu, không đổi sang NT khác.");
            targetPharmacyId = locked;
        }

        dto = await EnsurePrescriptionHasProviderAsync(workspaceId, dto, cancellationToken)
            ?? throw new InvalidOperationException(
                "Không gửi nhà thuốc khi đơn chưa có bác sĩ — chọn BS trên lượt khám rồi lưu trước khi gửi.");

        var handoff = await _handoffs.CreateFromClinicAsync(
            new CreateConnectRxHandoffRequest(
                targetPharmacyId,
                dto.Id,
                dto.PrescriptionCode,
                dto.CustomerName,
                dto.CustomerPhone,
                dto.ProviderDisplayName,
                dto.DiagnosisText,
                dto.Notes,
                dto.PdfSha256,
                dto.Lines.Select(l => new ConnectRxHandoffLineDto(
                    l.DrugName,
                    l.Strength,
                    l.Quantity,
                    l.Unit,
                    l.DosageInstruction,
                    l.SortOrder)).ToList()),
            cancellationToken);

        _ = handoff;
        return await GetAsync(prescriptionId, cancellationToken);
    }

    public async Task<(byte[] Pdf, string FileName, string Sha256)?> GetPdfAsync(
        Guid prescriptionId,
        CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync(prescriptionId, cancellationToken);
        if (dto is null) return null;
        if (dto.PrescriptionStatus == ClinicPrescriptionStatuses.Cancelled)
            throw new InvalidOperationException("Đơn đã hủy — không xuất PDF.");

        var header = await _clinicSettings.GetAsync(cancellationToken);
        var built = BuildPdf(dto, header);
        if (string.IsNullOrEmpty(dto.PdfSha256) ||
            !string.Equals(dto.PdfSha256, built.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            await _repo.SetPdfHashAsync(prescriptionId, built.Sha256, cancellationToken);
        }

        return (built.Pdf, $"{dto.PrescriptionCode}.pdf", built.Sha256);
    }

    private async Task<ClinicPrescriptionDto> ToDtoAsync(
        ClinicPrescriptionRepository.PrescriptionHeaderRow h,
        CancellationToken cancellationToken)
    {
        var lines = await _repo.ListLinesAsync(h.Id, cancellationToken);
        var hasSig = h.SignedAt is not null || !string.IsNullOrWhiteSpace(h.SignatureProvider);
        var status = hasSig && h.PrescriptionStatus != ClinicPrescriptionStatuses.Cancelled
            ? ClinicPrescriptionStatuses.Signed
            : h.PrescriptionStatus;
        return new ClinicPrescriptionDto(
            h.Id,
            h.VisitId,
            h.CustomerId,
            h.CustomerName,
            h.CustomerPhone,
            h.ProviderId,
            h.ProviderDisplayName,
            h.PrescriptionCode,
            status,
            h.DiagnosisText,
            h.Notes,
            h.FinalizedAt,
            h.PdfSha256,
            h.PharmacyTenantId,
            h.SentAt,
            h.ConnectHandoffId,
            h.SignedAt,
            h.SignatureProvider,
            h.CreatedAt,
            lines);
    }

    /// <summary>
    /// Gắn bác sĩ từ visit vào đơn nếu đơn chưa có — bắt buộc trước hoàn tất/gửi NT.
    /// </summary>
    private async Task<ClinicPrescriptionDto?> EnsurePrescriptionHasProviderAsync(
        Guid? workspaceId,
        ClinicPrescriptionDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.ProviderId is Guid existing && existing != Guid.Empty &&
            !string.IsNullOrWhiteSpace(dto.ProviderDisplayName))
            return dto;

        var visit = await _repo.GetVisitAsync(workspaceId, dto.VisitId, cancellationToken);
        if (visit?.ProviderId is not Guid visitProvider || visitProvider == Guid.Empty)
            return null;

        await _repo.AttachProviderIfMissingAsync(workspaceId, dto.Id, visitProvider, cancellationToken);
        var refreshed = await GetAsync(dto.Id, cancellationToken);
        if (refreshed is null) return null;
        if (refreshed.ProviderId is null || string.IsNullOrWhiteSpace(refreshed.ProviderDisplayName))
            return null;

        await _repo.SyncHandoffProviderDisplayNameAsync(
            refreshed.Id,
            refreshed.ProviderDisplayName!,
            cancellationToken);
        return refreshed;
    }

    private static List<ClinicPrescriptionLineInput> NormalizeLines(
        IReadOnlyList<ClinicPrescriptionLineInput>? lines)
    {
        if (lines is null || lines.Count == 0) return [];
        var result = new List<ClinicPrescriptionLineInput>();
        foreach (var line in lines)
        {
            var name = line.DrugName?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length < 2) continue;
            result.Add(new ClinicPrescriptionLineInput(
                name,
                line.Strength,
                line.Quantity <= 0 ? 1 : line.Quantity,
                line.Unit,
                line.DosageInstruction));
        }
        return result;
    }

    private static (byte[] Pdf, string Sha256) BuildPdf(
        ClinicPrescriptionDto dto,
        ClinicTenantSettingsDto clinic)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var statusLabel = dto.PrescriptionStatus switch
        {
            ClinicPrescriptionStatuses.Signed when dto.SignatureProvider == ClinicSignatureProviders.Mock
                => "Da ky Soft-CKS (mock) — KHONG phai chu ky so CA",
            ClinicPrescriptionStatuses.Signed
                => $"Da ky Soft-CKS ({dto.SignatureProvider ?? "unknown"}) — kiem tra provider",
            ClinicPrescriptionStatuses.Finalized => "Da hoan tat (noi bo) — chua ky so CKS",
            ClinicPrescriptionStatuses.Draft => "Nhap — chua hoan tat",
            _ => dto.PrescriptionStatus,
        };

        var footerNote = dto.PrescriptionStatus switch
        {
            ClinicPrescriptionStatuses.Signed when dto.SignatureProvider == ClinicSignatureProviders.Mock
                => "Soft-CKS mock (thu nghiem) — khong co gia tri phap ly CA.  ",
            ClinicPrescriptionStatuses.Signed
                => "Da ky Soft-CKS — xem signature_provider.  ",
            _ => "Khong phai chu ky so (CKS). Gui nha thuoc qua Connect (CL1.3).  ",
        };

        var clinicName = string.IsNullOrWhiteSpace(clinic.Name) ? "Novixa Clinic" : clinic.Name.Trim();
        var contactBits = new List<string>();
        if (!string.IsNullOrWhiteSpace(clinic.Address)) contactBits.Add(clinic.Address.Trim());
        if (!string.IsNullOrWhiteSpace(clinic.Phone)) contactBits.Add($"DT: {clinic.Phone.Trim()}");
        if (!string.IsNullOrWhiteSpace(clinic.WorkingHours))
            contactBits.Add($"Gio: {clinic.WorkingHours.Trim()}");

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));
                page.Header().Column(col =>
                {
                    col.Item().Text(clinicName).Bold().FontSize(16);
                    col.Item().Text("Don thuoc noi bo").FontSize(11);
                    if (contactBits.Count > 0)
                        col.Item().Text(string.Join(" · ", contactBits)).FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(4).Text(statusLabel).FontSize(9).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Ma: {dto.PrescriptionCode}").FontSize(10);
                });
                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text($"Benh nhan: {dto.CustomerName ?? dto.CustomerId.ToString()}");
                    if (!string.IsNullOrWhiteSpace(dto.CustomerPhone))
                        col.Item().Text($"SDT: {dto.CustomerPhone}");
                    col.Item().Text($"Bac si: {dto.ProviderDisplayName ?? "—"}");
                    if (!string.IsNullOrWhiteSpace(dto.DiagnosisText))
                        col.Item().Text($"Chan doan: {dto.DiagnosisText}");
                    col.Item().PaddingTop(8).Text("Chi dinh thuoc").Bold();
                    var i = 1;
                    foreach (var line in dto.Lines)
                    {
                        var strength = string.IsNullOrWhiteSpace(line.Strength) ? "" : $" ({line.Strength})";
                        var unit = string.IsNullOrWhiteSpace(line.Unit) ? "" : $" {line.Unit}";
                        col.Item().Text($"{i}. {line.DrugName}{strength} — SL: {line.Quantity:0.####}{unit}");
                        if (!string.IsNullOrWhiteSpace(line.DosageInstruction))
                            col.Item().PaddingLeft(12).Text($"Cach dung: {line.DosageInstruction}").FontSize(10);
                        i++;
                    }
                    if (!string.IsNullOrWhiteSpace(dto.Notes))
                        col.Item().PaddingTop(8).Text($"Ghi chu: {dto.Notes}");
                    if (dto.FinalizedAt is DateTime fa)
                        col.Item().PaddingTop(8).Text($"Hoan tat luc: {fa:yyyy-MM-dd HH:mm} UTC").FontSize(9);
                    if (dto.PrescriptionStatus == ClinicPrescriptionStatuses.Signed)
                        col.Item().Text($"Soft-CKS provider: {dto.SignatureProvider ?? "—"}").FontSize(9);
                });
                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span(footerNote).FontSize(8);
                    txt.CurrentPageNumber();
                    txt.Span(" / ");
                    txt.TotalPages();
                });
            });
        }).GeneratePdf();

        var sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return (bytes, sha);
    }

    private Task<Guid?> ResolveWorkspaceAsync(CancellationToken cancellationToken) =>
        _workspace.ResolveWorkspaceIdAsync(
            _tenant.TenantId,
            _tenant.WorkspaceId,
            ClinicPackDefinition.PackCode,
            cancellationToken);
}
