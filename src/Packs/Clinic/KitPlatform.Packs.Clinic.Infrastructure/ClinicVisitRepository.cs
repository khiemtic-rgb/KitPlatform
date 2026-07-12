using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Workspace;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Packs.Clinic.Infrastructure;

internal sealed class ClinicVisitRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ClinicVisitRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<ClinicVisitDto>> ListAsync(
        Guid? workspaceId,
        Guid? customerId,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var sql = """
            SELECT
                v.id AS Id,
                v.appointment_id AS AppointmentId,
                v.customer_id AS CustomerId,
                c.full_name AS CustomerName,
                c.phone AS CustomerPhone,
                v.provider_id AS ProviderId,
                p.display_name AS ProviderDisplayName,
                v.visit_status AS VisitStatus,
                v.encounter_modality AS EncounterModality,
                v.chief_complaint AS ChiefComplaint,
                v.diagnosis_summary AS DiagnosisSummary,
                v.started_at AS StartedAt,
                v.closed_at AS ClosedAt,
                v.created_at AS CreatedAt,
                COALESCE(
                    NULLIF(a.metadata->>'pharmacy_tenant_id', '')::uuid,
                    b.pharmacy_tenant_id,
                    r.pharmacy_tenant_id
                ) AS PreferredPharmacyTenantId,
                COALESCE(pt.tenant_name, bt.tenant_name, rt.tenant_name) AS PreferredPharmacyName,
                COALESCE(pt.tenant_code, bt.tenant_code, rt.tenant_code) AS PreferredPharmacyCode,
                CASE
                    WHEN COALESCE(
                        NULLIF(a.metadata->>'pharmacy_tenant_id', '')::uuid,
                        b.pharmacy_tenant_id,
                        r.pharmacy_tenant_id
                    ) IS NOT NULL THEN
                        CASE
                            WHEN a.metadata->>'connect_referral_id' IS NOT NULL OR r.id IS NOT NULL THEN 'referral'
                            WHEN a.metadata->>'connect_booking_id' IS NOT NULL OR b.id IS NOT NULL THEN 'booking'
                            ELSE 'connect'
                        END
                    ELSE NULL
                END AS ConnectSource
            FROM pack_clinic.clinic_visit v
            LEFT JOIN public.customers c ON c.id = v.customer_id AND c.tenant_id = v.tenant_id
            LEFT JOIN pack_clinic.clinic_provider p ON p.id = v.provider_id AND p.tenant_id = v.tenant_id
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
            LEFT JOIN public.tenants pt
                ON pt.id = NULLIF(a.metadata->>'pharmacy_tenant_id', '')::uuid
            LEFT JOIN public.tenants bt ON bt.id = b.pharmacy_tenant_id
            LEFT JOIN public.tenants rt ON rt.id = r.pharmacy_tenant_id
            WHERE v.tenant_id = @TenantId
              AND v.deleted_at IS NULL
            """;

        var parameters = new DynamicParameters();
        parameters.Add("TenantId", TenantId);

        if (workspaceId is Guid ws)
        {
            sql += " AND v.workspace_id = @WorkspaceId";
            parameters.Add("WorkspaceId", ws);
        }

        if (customerId is Guid cid)
        {
            sql += " AND v.customer_id = @CustomerId";
            parameters.Add("CustomerId", cid);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            sql += " AND v.visit_status = @Status";
            parameters.Add("Status", status.Trim());
        }

        if (from is DateTimeOffset f)
        {
            sql += " AND v.started_at >= @From";
            parameters.Add("From", f.UtcDateTime);
        }

        if (to is DateTimeOffset t)
        {
            sql += " AND v.started_at <= @To";
            parameters.Add("To", t.UtcDateTime);
        }

        sql += """
             ORDER BY v.started_at DESC
             LIMIT 200
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ClinicVisitDto>(sql, parameters)).ToList();
    }

    public async Task<ClinicVisitDto?> GetAsync(
        Guid? workspaceId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                v.id AS Id,
                v.appointment_id AS AppointmentId,
                v.customer_id AS CustomerId,
                c.full_name AS CustomerName,
                c.phone AS CustomerPhone,
                v.provider_id AS ProviderId,
                p.display_name AS ProviderDisplayName,
                v.visit_status AS VisitStatus,
                v.encounter_modality AS EncounterModality,
                v.chief_complaint AS ChiefComplaint,
                v.diagnosis_summary AS DiagnosisSummary,
                v.started_at AS StartedAt,
                v.closed_at AS ClosedAt,
                v.created_at AS CreatedAt,
                COALESCE(
                    NULLIF(a.metadata->>'pharmacy_tenant_id', '')::uuid,
                    b.pharmacy_tenant_id,
                    r.pharmacy_tenant_id
                ) AS PreferredPharmacyTenantId,
                COALESCE(pt.tenant_name, bt.tenant_name, rt.tenant_name) AS PreferredPharmacyName,
                COALESCE(pt.tenant_code, bt.tenant_code, rt.tenant_code) AS PreferredPharmacyCode,
                CASE
                    WHEN COALESCE(
                        NULLIF(a.metadata->>'pharmacy_tenant_id', '')::uuid,
                        b.pharmacy_tenant_id,
                        r.pharmacy_tenant_id
                    ) IS NOT NULL THEN
                        CASE
                            WHEN a.metadata->>'connect_referral_id' IS NOT NULL OR r.id IS NOT NULL THEN 'referral'
                            WHEN a.metadata->>'connect_booking_id' IS NOT NULL OR b.id IS NOT NULL THEN 'booking'
                            ELSE 'connect'
                        END
                    ELSE NULL
                END AS ConnectSource
            FROM pack_clinic.clinic_visit v
            LEFT JOIN public.customers c ON c.id = v.customer_id AND c.tenant_id = v.tenant_id
            LEFT JOIN pack_clinic.clinic_provider p ON p.id = v.provider_id AND p.tenant_id = v.tenant_id
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
            LEFT JOIN public.tenants pt
                ON pt.id = NULLIF(a.metadata->>'pharmacy_tenant_id', '')::uuid
            LEFT JOIN public.tenants bt ON bt.id = b.pharmacy_tenant_id
            LEFT JOIN public.tenants rt ON rt.id = r.pharmacy_tenant_id
            WHERE v.tenant_id = @TenantId
              AND v.id = @VisitId
              AND v.deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR v.workspace_id = @WorkspaceId)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ClinicVisitDto>(sql, new
        {
            TenantId,
            VisitId = visitId,
            WorkspaceId = workspaceId,
        });
    }

    public async Task<Guid> CreateAsync(
        Guid workspaceId,
        CreateClinicVisitRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_clinic.clinic_visit (
                tenant_id, workspace_id, appointment_id, customer_id, provider_id,
                chief_complaint, encounter_modality
            )
            VALUES (
                @TenantId, @WorkspaceId, @AppointmentId, @CustomerId, @ProviderId,
                @ChiefComplaint, @EncounterModality
            )
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            WorkspaceId = workspaceId,
            request.AppointmentId,
            request.CustomerId,
            request.ProviderId,
            request.ChiefComplaint,
            EncounterModality = ClinicEncounterModalities.Normalize(request.EncounterModality),
        });
    }

    public async Task<ClinicVisitDto?> FindOpenByAppointmentAsync(
        Guid? workspaceId,
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                v.id AS Id,
                v.appointment_id AS AppointmentId,
                v.customer_id AS CustomerId,
                c.full_name AS CustomerName,
                c.phone AS CustomerPhone,
                v.provider_id AS ProviderId,
                p.display_name AS ProviderDisplayName,
                v.visit_status AS VisitStatus,
                v.encounter_modality AS EncounterModality,
                v.chief_complaint AS ChiefComplaint,
                v.diagnosis_summary AS DiagnosisSummary,
                v.started_at AS StartedAt,
                v.closed_at AS ClosedAt,
                v.created_at AS CreatedAt
            FROM pack_clinic.clinic_visit v
            LEFT JOIN public.customers c ON c.id = v.customer_id AND c.tenant_id = v.tenant_id
            LEFT JOIN pack_clinic.clinic_provider p ON p.id = v.provider_id AND p.tenant_id = v.tenant_id
            WHERE v.tenant_id = @TenantId
              AND v.appointment_id = @AppointmentId
              AND v.visit_status = 'open'
              AND v.deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR v.workspace_id = @WorkspaceId)
            ORDER BY v.started_at DESC
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ClinicVisitDto>(sql, new
        {
            TenantId,
            AppointmentId = appointmentId,
            WorkspaceId = workspaceId,
        });
    }

    public async Task<bool> UpdateAsync(
        Guid? workspaceId,
        Guid visitId,
        UpdateClinicVisitRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_clinic.clinic_visit
            SET
                chief_complaint = COALESCE(@ChiefComplaint, chief_complaint),
                diagnosis_summary = COALESCE(@DiagnosisSummary, diagnosis_summary),
                visit_status = COALESCE(@VisitStatus, visit_status),
                provider_id = COALESCE(@ProviderId, provider_id),
                closed_at = CASE
                    WHEN @VisitStatus = 'closed' AND closed_at IS NULL THEN NOW()
                    WHEN @VisitStatus IS NOT NULL AND @VisitStatus <> 'closed' THEN NULL
                    ELSE closed_at
                END,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND id = @VisitId
              AND deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR workspace_id = @WorkspaceId)
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.ExecuteAsync(sql, new
        {
            TenantId,
            VisitId = visitId,
            WorkspaceId = workspaceId,
            request.ChiefComplaint,
            request.DiagnosisSummary,
            request.VisitStatus,
            request.ProviderId,
        });
        if (rows <= 0) return false;

        // Gán BS trên visit → copy sang đơn còn thiếu + handoff Connect (POS đọc từ đây).
        if (request.ProviderId is Guid providerId && providerId != Guid.Empty)
        {
            await conn.ExecuteAsync(
                """
                UPDATE pack_clinic.clinic_prescription
                SET provider_id = @ProviderId, updated_at = NOW()
                WHERE tenant_id = @TenantId
                  AND visit_id = @VisitId
                  AND deleted_at IS NULL
                  AND provider_id IS NULL
                  AND prescription_status <> 'cancelled'
                """,
                new { TenantId, VisitId = visitId, ProviderId = providerId });

            await conn.ExecuteAsync(
                """
                UPDATE pack_connect.rx_handoffs h
                SET provider_display_name = p.display_name,
                    updated_at = NOW()
                FROM pack_clinic.clinic_prescription r
                INNER JOIN pack_clinic.clinic_provider p
                    ON p.id = @ProviderId AND p.tenant_id = @TenantId
                WHERE r.visit_id = @VisitId
                  AND r.tenant_id = @TenantId
                  AND h.clinic_prescription_id = r.id
                  AND h.clinic_tenant_id = @TenantId
                  AND (h.provider_display_name IS NULL OR BTRIM(h.provider_display_name) = '')
                """,
                new { TenantId, VisitId = visitId, ProviderId = providerId });
        }

        return true;
    }

    public async Task<IReadOnlyList<ClinicVisitNoteDto>> ListNotesAsync(
        Guid? workspaceId,
        Guid visitId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                n.id AS Id,
                n.visit_id AS VisitId,
                n.note_type AS NoteType,
                n.note_body AS NoteBody,
                n.author_user_id AS AuthorUserId,
                n.created_at AS CreatedAt
            FROM pack_clinic.clinic_visit_note n
            INNER JOIN pack_clinic.clinic_visit v ON v.id = n.visit_id
            WHERE n.tenant_id = @TenantId
              AND n.visit_id = @VisitId
              AND n.deleted_at IS NULL
              AND v.deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR v.workspace_id = @WorkspaceId)
            ORDER BY n.created_at DESC
            LIMIT 200
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ClinicVisitNoteDto>(sql, new
        {
            TenantId,
            VisitId = visitId,
            WorkspaceId = workspaceId,
        })).ToList();
    }

    public async Task<Guid> AddNoteAsync(
        Guid workspaceId,
        Guid visitId,
        Guid? authorUserId,
        CreateClinicVisitNoteRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_clinic.clinic_visit_note (
                tenant_id, workspace_id, visit_id, note_type, note_body, author_user_id
            )
            SELECT
                v.tenant_id, v.workspace_id, v.id, @NoteType, @NoteBody, @AuthorUserId
            FROM pack_clinic.clinic_visit v
            WHERE v.tenant_id = @TenantId
              AND v.id = @VisitId
              AND v.deleted_at IS NULL
              AND v.workspace_id = @WorkspaceId
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            WorkspaceId = workspaceId,
            VisitId = visitId,
            request.NoteType,
            request.NoteBody,
            AuthorUserId = authorUserId,
        });
    }
}

internal sealed class ClinicVisitService : IClinicVisitService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "closed", "cancelled",
    };

    private static readonly HashSet<string> AllowedNoteTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "clinical", "admin", "follow_up",
    };

    private readonly ClinicVisitRepository _repo;
    private readonly EncounterSessionRepository _sessions;
    private readonly IEncounterMediaProvider _media;
    private readonly ITenantContext _tenant;
    private readonly IWorkspaceResolver _workspace;

    public ClinicVisitService(
        ClinicVisitRepository repo,
        EncounterSessionRepository sessions,
        IEncounterMediaProvider media,
        ITenantContext tenant,
        IWorkspaceResolver workspace)
    {
        _repo = repo;
        _sessions = sessions;
        _media = media;
        _tenant = tenant;
        _workspace = workspace;
    }

    public async Task<IReadOnlyList<ClinicVisitDto>> ListAsync(
        Guid? customerId,
        string? status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        if (status is not null && !AllowedStatuses.Contains(status))
            throw new InvalidOperationException("Trạng thái lượt khám không hợp lệ.");

        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken);
        return await _repo.ListAsync(workspaceId, customerId, status, from, to, cancellationToken);
    }

    public async Task<ClinicVisitDto?> GetAsync(Guid visitId, CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken);
        return await _repo.GetAsync(workspaceId, visitId, cancellationToken);
    }

    public async Task<ClinicVisitDto> CreateAsync(
        CreateClinicVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.EncounterModality)
            && !ClinicEncounterModalities.IsKnown(request.EncounterModality))
            throw new InvalidOperationException("Hình thức khám không hợp lệ.");

        var modality = ClinicEncounterModalities.Normalize(request.EncounterModality);
        if (modality == ClinicEncounterModalities.RemoteVideo)
            throw new InvalidOperationException(
                "Khám video trong Novixa chưa bật (CL3-B). Dùng khám từ xa (gọi ngoài) hoặc tại phòng khám.");

        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Workspace clinic_crm chưa được provision.");

        var id = await _repo.CreateAsync(
            workspaceId,
            request with { EncounterModality = modality },
            cancellationToken);
        await _sessions.EnsureRemoteStubAsync(
            workspaceId,
            id,
            request.AppointmentId,
            modality,
            cancellationToken);
        if (ClinicEncounterModalities.IsRemote(modality))
            await _media.StartAsync(id, modality, cancellationToken);
        return (await _repo.GetAsync(workspaceId, id, cancellationToken))!;
    }

    public async Task<ClinicVisitDto?> UpdateAsync(
        Guid visitId,
        UpdateClinicVisitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.VisitStatus is not null && !AllowedStatuses.Contains(request.VisitStatus))
            throw new InvalidOperationException("Trạng thái lượt khám không hợp lệ.");

        if (request.ChiefComplaint is null
            && request.DiagnosisSummary is null
            && request.VisitStatus is null
            && request.ProviderId is null)
            throw new InvalidOperationException("Không có dữ liệu để cập nhật.");

        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken);
        var updated = await _repo.UpdateAsync(workspaceId, visitId, request, cancellationToken);
        if (!updated) return null;
        return await _repo.GetAsync(workspaceId, visitId, cancellationToken);
    }

    public async Task<IReadOnlyList<ClinicVisitNoteDto>> ListNotesAsync(
        Guid visitId,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken);
        return await _repo.ListNotesAsync(workspaceId, visitId, cancellationToken);
    }

    public async Task<ClinicVisitNoteDto> AddNoteAsync(
        Guid visitId,
        CreateClinicVisitNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NoteBody))
            throw new InvalidOperationException("Nội dung ghi chú không được để trống.");
        if (!AllowedNoteTypes.Contains(request.NoteType))
            throw new InvalidOperationException("Loại ghi chú không hợp lệ.");

        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Workspace clinic_crm chưa được provision.");

        var noteId = await _repo.AddNoteAsync(
            workspaceId,
            visitId,
            _tenant.UserId,
            request,
            cancellationToken);

        var notes = await _repo.ListNotesAsync(workspaceId, visitId, cancellationToken);
        return notes.First(n => n.Id == noteId);
    }

    private Task<Guid?> ResolveClinicWorkspaceAsync(CancellationToken cancellationToken) =>
        _workspace.ResolveWorkspaceIdAsync(
            _tenant.TenantId,
            _tenant.WorkspaceId,
            ClinicPackDefinition.PackCode,
            cancellationToken);
}
