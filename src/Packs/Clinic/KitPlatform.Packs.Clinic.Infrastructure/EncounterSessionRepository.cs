using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Packs.Clinic.Infrastructure;

internal sealed class EncounterSessionRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public EncounterSessionRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    /// <summary>CL3-A: stub session for remote visits (status=none). Idempotent per visit.</summary>
    public async Task EnsureRemoteStubAsync(
        Guid? workspaceId,
        Guid visitId,
        Guid? appointmentId,
        string encounterModality,
        CancellationToken cancellationToken)
    {
        if (!ClinicEncounterModalities.IsRemote(encounterModality))
            return;

        const string sql = """
            INSERT INTO pack_clinic.encounter_session (
                tenant_id, workspace_id, visit_id, appointment_id,
                session_status, media_provider
            )
            VALUES (
                @TenantId, @WorkspaceId, @VisitId, @AppointmentId,
                @Status, NULL
            )
            ON CONFLICT (visit_id) DO NOTHING
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            WorkspaceId = workspaceId,
            VisitId = visitId,
            AppointmentId = appointmentId,
            Status = EncounterSessionStatuses.None,
        });
    }

    public async Task<EncounterSessionDto?> GetByVisitAsync(
        Guid visitId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id AS Id,
                visit_id AS VisitId,
                appointment_id AS AppointmentId,
                session_status AS SessionStatus,
                media_provider AS MediaProvider,
                provider_session_id AS ProviderSessionId,
                join_url_patient AS JoinUrlPatient,
                join_url_clinician AS JoinUrlClinician,
                started_at AS StartedAt,
                ended_at AS EndedAt
            FROM pack_clinic.encounter_session
            WHERE tenant_id = @TenantId
              AND visit_id = @VisitId
              AND deleted_at IS NULL
            LIMIT 1
            """;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<EncounterSessionDto>(sql, new
        {
            TenantId = _tenant.TenantId,
            VisitId = visitId,
        });
    }
}

/// <summary>CL3-A no-op media — B registers a real WebRTC provider.</summary>
internal sealed class NullEncounterMediaProvider : IEncounterMediaProvider
{
    public string? ProviderCode => null;

    public Task<EncounterMediaStartResult> StartAsync(
        Guid visitId,
        string encounterModality,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new EncounterMediaStartResult(
            EncounterSessionStatuses.None,
            MediaProvider: null,
            ProviderSessionId: null,
            JoinUrlPatient: null,
            JoinUrlClinician: null));

    public Task EndAsync(Guid visitId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
