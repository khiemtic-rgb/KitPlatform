using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Kernel.Workspace;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Packs.Clinic.Infrastructure;

internal sealed class ClinicAppointmentRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ClinicAppointmentRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    private const string SelectSql = """
        SELECT
            a.id AS Id,
            a.customer_id AS CustomerId,
            c.full_name AS CustomerName,
            c.phone AS CustomerPhone,
            a.provider_id AS ProviderId,
            p.display_name AS ProviderDisplayName,
            a.branch_id AS BranchId,
            a.appointment_at AS AppointmentAt,
            a.duration_minutes AS DurationMinutes,
            a.appointment_status AS AppointmentStatus,
            a.encounter_modality AS EncounterModality,
            a.reason AS Reason,
            a.notes AS Notes,
            a.created_at AS CreatedAt
        FROM pack_clinic.clinic_appointment a
        LEFT JOIN public.customers c ON c.id = a.customer_id AND c.tenant_id = a.tenant_id
        LEFT JOIN pack_clinic.clinic_provider p ON p.id = a.provider_id AND p.tenant_id = a.tenant_id
        """;

    public async Task<IReadOnlyList<ClinicAppointmentDto>> ListAsync(
        Guid? workspaceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? status,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE a.tenant_id = @TenantId
              AND a.deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR a.workspace_id = @WorkspaceId)
              AND (@From IS NULL OR a.appointment_at >= @From)
              AND (@To IS NULL OR a.appointment_at <= @To)
              AND (@Status IS NULL OR a.appointment_status = @Status)
            ORDER BY a.appointment_at
            LIMIT 200
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return (await conn.QueryAsync<ClinicAppointmentDto>(sql, new
        {
            TenantId,
            WorkspaceId = workspaceId,
            From = from?.UtcDateTime,
            To = to?.UtcDateTime,
            Status = status,
        })).ToList();
    }

    public async Task<ClinicAppointmentDto?> GetAsync(
        Guid? workspaceId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            {SelectSql}
            WHERE a.tenant_id = @TenantId
              AND a.id = @Id
              AND a.deleted_at IS NULL
              AND (@WorkspaceId IS NULL OR a.workspace_id = @WorkspaceId)
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<ClinicAppointmentDto>(sql, new
        {
            TenantId,
            Id = id,
            WorkspaceId = workspaceId,
        });
    }

    public async Task<Guid> CreateAsync(
        Guid workspaceId,
        CreateClinicAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pack_clinic.clinic_appointment (
                tenant_id, workspace_id, customer_id, provider_id, branch_id,
                appointment_at, duration_minutes, reason, notes, encounter_modality
            )
            VALUES (
                @TenantId, @WorkspaceId, @CustomerId, @ProviderId, @BranchId,
                @AppointmentAt, @DurationMinutes, @Reason, @Notes, @EncounterModality
            )
            RETURNING id
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<Guid>(sql, new
        {
            TenantId,
            WorkspaceId = workspaceId,
            request.CustomerId,
            request.ProviderId,
            request.BranchId,
            AppointmentAt = request.AppointmentAt.UtcDateTime,
            request.DurationMinutes,
            request.Reason,
            request.Notes,
            EncounterModality = ClinicEncounterModalities.Normalize(request.EncounterModality),
        });
    }

    public async Task<bool> UpdateStatusAsync(
        Guid? workspaceId,
        Guid id,
        string fromStatus,
        string toStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pack_clinic.clinic_appointment
            SET appointment_status = @ToStatus,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND id = @Id
              AND deleted_at IS NULL
              AND appointment_status = @FromStatus
              AND (@WorkspaceId IS NULL OR workspace_id = @WorkspaceId)
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var n = await conn.ExecuteAsync(sql, new
        {
            TenantId,
            Id = id,
            WorkspaceId = workspaceId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
        });
        return n > 0;
    }
}

internal sealed class ClinicAppointmentService : IClinicAppointmentService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ClinicAppointmentStatuses.Scheduled,
        ClinicAppointmentStatuses.CheckedIn,
        ClinicAppointmentStatuses.Completed,
        ClinicAppointmentStatuses.Cancelled,
        ClinicAppointmentStatuses.NoShow,
    };

    private readonly ClinicAppointmentRepository _repo;
    private readonly ClinicVisitRepository _visits;
    private readonly EncounterSessionRepository _sessions;
    private readonly ITenantContext _tenant;
    private readonly IWorkspaceResolver _workspace;

    public ClinicAppointmentService(
        ClinicAppointmentRepository repo,
        ClinicVisitRepository visits,
        EncounterSessionRepository sessions,
        ITenantContext tenant,
        IWorkspaceResolver workspace)
    {
        _repo = repo;
        _visits = visits;
        _sessions = sessions;
        _tenant = tenant;
        _workspace = workspace;
    }

    public async Task<IReadOnlyList<ClinicAppointmentDto>> ListAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (status is not null && !AllowedStatuses.Contains(status))
            throw new InvalidOperationException("Trạng thái lịch hẹn không hợp lệ.");

        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken);
        return await _repo.ListAsync(workspaceId, from, to, status, cancellationToken);
    }

    public async Task<ClinicAppointmentDto?> GetAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken);
        return await _repo.GetAsync(workspaceId, appointmentId, cancellationToken);
    }

    public async Task<ClinicAppointmentDto> CreateAsync(
        CreateClinicAppointmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerId == Guid.Empty)
            throw new InvalidOperationException("CustomerId không hợp lệ.");
        if (request.DurationMinutes < 5 || request.DurationMinutes > 480)
            throw new InvalidOperationException("Thời lượng lịch hẹn không hợp lệ.");
        if (!string.IsNullOrWhiteSpace(request.EncounterModality)
            && !ClinicEncounterModalities.IsKnown(request.EncounterModality))
            throw new InvalidOperationException("Hình thức khám không hợp lệ.");
        if (string.Equals(
                ClinicEncounterModalities.Normalize(request.EncounterModality),
                ClinicEncounterModalities.RemoteVideo,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Khám video trong Novixa chưa bật (CL3-B). Dùng khám từ xa (gọi ngoài) hoặc tại phòng khám.");

        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Workspace clinic_crm chưa được provision.");

        var id = await _repo.CreateAsync(
            workspaceId,
            request with
            {
                EncounterModality = ClinicEncounterModalities.Normalize(request.EncounterModality),
            },
            cancellationToken);
        return (await _repo.GetAsync(workspaceId, id, cancellationToken))!;
    }

    public async Task<ClinicAppointmentDto?> UpdateStatusAsync(
        Guid appointmentId,
        string appointmentStatus,
        CancellationToken cancellationToken = default)
    {
        var to = appointmentStatus?.Trim()
            ?? throw new InvalidOperationException("Trạng thái không hợp lệ.");
        if (!AllowedStatuses.Contains(to))
            throw new InvalidOperationException("Trạng thái lịch hẹn không hợp lệ.");

        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken);
        var row = await _repo.GetAsync(workspaceId, appointmentId, cancellationToken);
        if (row is null) return null;

        if (!CanTransition(row.AppointmentStatus, to))
            throw new InvalidOperationException(
                $"Không chuyển được từ {row.AppointmentStatus} sang {to}.");

        var ok = await _repo.UpdateStatusAsync(
            workspaceId,
            appointmentId,
            row.AppointmentStatus,
            to,
            cancellationToken);
        if (!ok) return null;
        return await _repo.GetAsync(workspaceId, appointmentId, cancellationToken);
    }

    public async Task<ClinicVisitDto> CheckInAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var workspaceId = await ResolveClinicWorkspaceAsync(cancellationToken)
            ?? throw new InvalidOperationException("Workspace clinic_crm chưa được provision.");

        var appt = await _repo.GetAsync(workspaceId, appointmentId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy lịch hẹn.");

        if (string.Equals(appt.AppointmentStatus, ClinicAppointmentStatuses.Scheduled, StringComparison.OrdinalIgnoreCase))
        {
            var ok = await _repo.UpdateStatusAsync(
                workspaceId,
                appointmentId,
                ClinicAppointmentStatuses.Scheduled,
                ClinicAppointmentStatuses.CheckedIn,
                cancellationToken);
            if (!ok)
                throw new InvalidOperationException("Không check-in được lịch hẹn.");
        }
        else if (!string.Equals(appt.AppointmentStatus, ClinicAppointmentStatuses.CheckedIn, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Chỉ check-in lịch đang scheduled hoặc đã checked_in.");
        }

        var existing = await _visits.FindOpenByAppointmentAsync(workspaceId, appointmentId, cancellationToken);
        if (existing is not null)
            return existing;

        var visitId = await _visits.CreateAsync(
            workspaceId,
            new CreateClinicVisitRequest(
                appt.CustomerId,
                appointmentId,
                appt.ProviderId,
                appt.Reason,
                ClinicEncounterModalities.Normalize(appt.EncounterModality)),
            cancellationToken);

        await _sessions.EnsureRemoteStubAsync(
            workspaceId,
            visitId,
            appointmentId,
            ClinicEncounterModalities.Normalize(appt.EncounterModality),
            cancellationToken);

        return (await _visits.GetAsync(workspaceId, visitId, cancellationToken))!;
    }

    private static bool CanTransition(string from, string to)
    {
        from = from.Trim().ToLowerInvariant();
        to = to.Trim().ToLowerInvariant();
        if (from == to) return true;

        return from switch
        {
            ClinicAppointmentStatuses.Scheduled => to is
                ClinicAppointmentStatuses.CheckedIn or
                ClinicAppointmentStatuses.Cancelled or
                ClinicAppointmentStatuses.NoShow,
            ClinicAppointmentStatuses.CheckedIn => to is
                ClinicAppointmentStatuses.Completed or
                ClinicAppointmentStatuses.Cancelled or
                ClinicAppointmentStatuses.NoShow,
            _ => false,
        };
    }

    private Task<Guid?> ResolveClinicWorkspaceAsync(CancellationToken cancellationToken) =>
        _workspace.ResolveWorkspaceIdAsync(
            _tenant.TenantId,
            _tenant.WorkspaceId,
            ClinicPackDefinition.PackCode,
            cancellationToken);
}
