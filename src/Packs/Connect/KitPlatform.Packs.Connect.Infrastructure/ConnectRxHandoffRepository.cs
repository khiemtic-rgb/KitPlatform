using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Connect.Infrastructure;

internal sealed class ConnectRxHandoffRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ConnectRxHandoffRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Guid CurrentTenantId => _tenant.TenantId;
    public Guid CurrentUserId => _tenant.UserId;

    private const string SelectSql = """
        SELECT
            h.id AS Id,
            h.clinic_tenant_id AS ClinicTenantId,
            ct.tenant_code AS ClinicTenantCode,
            ct.tenant_name AS ClinicTenantName,
            h.pharmacy_tenant_id AS PharmacyTenantId,
            pt.tenant_code AS PharmacyTenantCode,
            pt.tenant_name AS PharmacyTenantName,
            h.clinic_prescription_id AS ClinicPrescriptionId,
            h.prescription_code AS PrescriptionCode,
            h.patient_display_name AS PatientDisplayName,
            h.patient_phone AS PatientPhone,
            COALESCE(
                NULLIF(BTRIM(h.provider_display_name), ''),
                NULLIF(BTRIM(rxp.display_name), ''),
                NULLIF(BTRIM(vp.display_name), '')
            ) AS ProviderDisplayName,
            h.diagnosis_text AS DiagnosisText,
            h.notes AS Notes,
            h.pdf_sha256 AS PdfSha256,
            h.handoff_status AS HandoffStatus,
            h.status_event_id AS StatusEventId,
            h.created_at AS CreatedAt,
            h.consumed_at AS ConsumedAt,
            h.lines_json::text AS LinesJson
        FROM pack_connect.rx_handoffs h
        INNER JOIN public.tenants ct ON ct.id = h.clinic_tenant_id
        INNER JOIN public.tenants pt ON pt.id = h.pharmacy_tenant_id
        LEFT JOIN pack_clinic.clinic_prescription rx
            ON rx.id = h.clinic_prescription_id AND rx.deleted_at IS NULL
        LEFT JOIN pack_clinic.clinic_provider rxp
            ON rxp.id = rx.provider_id AND rxp.tenant_id = rx.tenant_id
        LEFT JOIN pack_clinic.clinic_visit v
            ON v.id = rx.visit_id AND v.deleted_at IS NULL
        LEFT JOIN pack_clinic.clinic_provider vp
            ON vp.id = v.provider_id AND vp.tenant_id = v.tenant_id
        """;

    public async Task<IReadOnlyList<HandoffRow>> ListForTenantAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var conditions = new List<string>
        {
            "(h.pharmacy_tenant_id = @TenantId OR h.clinic_tenant_id = @TenantId)",
        };
        var parameters = new DynamicParameters(new { TenantId = CurrentTenantId });
        if (!string.IsNullOrWhiteSpace(status))
        {
            conditions.Add("h.handoff_status = @Status");
            parameters.Add("Status", status.Trim());
        }

        var sql = $"""
            {SelectSql}
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY h.created_at DESC
            LIMIT 200
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<HandoffRow>(sql, parameters)).ToList();
    }

    public async Task<HandoffRow?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE h.id = @Id
              AND (h.pharmacy_tenant_id = @TenantId OR h.clinic_tenant_id = @TenantId)
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<HandoffRow>(sql, new
        {
            Id = id,
            TenantId = CurrentTenantId,
        });
    }

    public async Task<HandoffRow?> GetByClinicPrescriptionAsync(
        Guid clinicTenantId,
        Guid clinicPrescriptionId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE h.clinic_tenant_id = @ClinicId
              AND h.clinic_prescription_id = @RxId
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<HandoffRow>(sql, new
        {
            ClinicId = clinicTenantId,
            RxId = clinicPrescriptionId,
        });
    }

    public async Task<Guid> InsertAsync(
        Guid pharmacyTenantId,
        Guid clinicPrescriptionId,
        string prescriptionCode,
        string? patientName,
        string? patientPhone,
        string? providerName,
        string? diagnosis,
        string? notes,
        string? pdfSha,
        IReadOnlyList<ConnectRxHandoffLineDto> lines,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_connect.rx_handoffs (
                clinic_tenant_id, pharmacy_tenant_id, clinic_prescription_id, prescription_code,
                patient_display_name, patient_phone, provider_display_name, diagnosis_text, notes,
                lines_json, pdf_sha256, handoff_status, created_by
            )
            VALUES (
                @ClinicId, @PharmacyId, @RxId, @Code,
                @PatientName, @PatientPhone, @ProviderName, @Diagnosis, @Notes,
                @LinesJson::jsonb, @PdfSha, @Status, @Actor
            )
            RETURNING id
            """;
        var linesJson = JsonSerializer.Serialize(lines, JsonOptions);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            ClinicId = CurrentTenantId,
            PharmacyId = pharmacyTenantId,
            RxId = clinicPrescriptionId,
            Code = prescriptionCode,
            PatientName = patientName,
            PatientPhone = patientPhone,
            ProviderName = providerName,
            Diagnosis = diagnosis,
            Notes = notes,
            LinesJson = linesJson,
            PdfSha = pdfSha,
            Status = ConnectRxHandoffStatuses.PendingPharmacy,
            Actor = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId,
        });
    }

    public async Task SetStatusEventIdAsync(
        Guid handoffId,
        Guid statusEventId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_connect.rx_handoffs
            SET status_event_id = @EventId, updated_at = NOW()
            WHERE id = @Id AND clinic_tenant_id = @TenantId
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            Id = handoffId,
            EventId = statusEventId,
            TenantId = CurrentTenantId,
        });
    }

    public async Task<bool> UpdateStatusByEventAsync(
        Guid statusEventId,
        string toStatus,
        CancellationToken cancellationToken)
    {
        var setConsumed = toStatus == ConnectRxHandoffStatuses.Consumed
            ? ", consumed_at = NOW(), consumed_by = @Actor"
            : "";
        var setDismissed = toStatus == ConnectRxHandoffStatuses.Dismissed
            ? ", dismissed_at = NOW(), dismissed_by = @Actor"
            : "";
        var sql = $"""
            UPDATE pack_connect.rx_handoffs
            SET handoff_status = @ToStatus,
                updated_at = NOW()
                {setConsumed}
                {setDismissed}
            WHERE status_event_id = @EventId
              AND pharmacy_tenant_id = @TenantId
              AND handoff_status = @Pending
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(sql, new
        {
            EventId = statusEventId,
            TenantId = CurrentTenantId,
            ToStatus = toStatus,
            Pending = ConnectRxHandoffStatuses.PendingPharmacy,
            Actor = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId,
        });
        return n > 0;
    }

    public async Task UpdateStatusByIdAsync(
        Guid handoffId,
        string toStatus,
        CancellationToken cancellationToken)
    {
        var setConsumed = toStatus == ConnectRxHandoffStatuses.Consumed
            ? ", consumed_at = NOW(), consumed_by = @Actor"
            : "";
        var setDismissed = toStatus == ConnectRxHandoffStatuses.Dismissed
            ? ", dismissed_at = NOW(), dismissed_by = @Actor"
            : "";
        var sql = $"""
            UPDATE pack_connect.rx_handoffs
            SET handoff_status = @ToStatus,
                updated_at = NOW()
                {setConsumed}
                {setDismissed}
            WHERE id = @Id
              AND pharmacy_tenant_id = @TenantId
              AND handoff_status = @Pending
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            Id = handoffId,
            TenantId = CurrentTenantId,
            ToStatus = toStatus,
            Pending = ConnectRxHandoffStatuses.PendingPharmacy,
            Actor = CurrentUserId == Guid.Empty ? (Guid?)null : CurrentUserId,
        });
    }

    public static ConnectRxHandoffDto ToDto(HandoffRow row)
    {
        var lines = Array.Empty<ConnectRxHandoffLineDto>();
        if (!string.IsNullOrWhiteSpace(row.LinesJson))
        {
            try
            {
                lines = JsonSerializer.Deserialize<ConnectRxHandoffLineDto[]>(row.LinesJson, JsonOptions)
                    ?? [];
            }
            catch (JsonException)
            {
                lines = [];
            }
        }

        return new ConnectRxHandoffDto(
            row.Id,
            row.ClinicTenantId,
            row.ClinicTenantCode,
            row.ClinicTenantName,
            row.PharmacyTenantId,
            row.PharmacyTenantCode,
            row.PharmacyTenantName,
            row.ClinicPrescriptionId,
            row.PrescriptionCode,
            row.PatientDisplayName,
            row.PatientPhone,
            row.ProviderDisplayName,
            row.DiagnosisText,
            row.Notes,
            row.PdfSha256,
            row.HandoffStatus,
            row.StatusEventId,
            row.CreatedAt,
            row.ConsumedAt,
            lines);
    }

    internal sealed class HandoffRow
    {
        public Guid Id { get; init; }
        public Guid ClinicTenantId { get; init; }
        public string ClinicTenantCode { get; init; } = "";
        public string ClinicTenantName { get; init; } = "";
        public Guid PharmacyTenantId { get; init; }
        public string PharmacyTenantCode { get; init; } = "";
        public string PharmacyTenantName { get; init; } = "";
        public Guid ClinicPrescriptionId { get; init; }
        public string PrescriptionCode { get; init; } = "";
        public string? PatientDisplayName { get; init; }
        public string? PatientPhone { get; init; }
        public string? ProviderDisplayName { get; init; }
        public string? DiagnosisText { get; init; }
        public string? Notes { get; init; }
        public string? PdfSha256 { get; init; }
        public string HandoffStatus { get; init; } = "";
        public Guid? StatusEventId { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ConsumedAt { get; init; }
        public string? LinesJson { get; init; }
    }
}
