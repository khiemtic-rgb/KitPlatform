using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Packs.Clinic.Infrastructure;

internal sealed class ClinicDaySummaryService : IClinicDaySummaryService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public ClinicDaySummaryService(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ClinicDaySummaryDto> GetAsync(
        DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        const string sql = """
            SELECT
                (SELECT COUNT(*)::int FROM pack_clinic.clinic_appointment a
                 WHERE a.tenant_id = @TenantId AND a.deleted_at IS NULL
                   AND a.appointment_at >= @From AND a.appointment_at < @To) AS AppointmentsToday,
                (SELECT COUNT(*)::int FROM pack_clinic.clinic_appointment a
                 WHERE a.tenant_id = @TenantId AND a.deleted_at IS NULL
                   AND a.appointment_at >= @From AND a.appointment_at < @To
                   AND a.encounter_modality IN ('remote_async', 'remote_video')) AS AppointmentsRemoteToday,
                (SELECT COUNT(*)::int FROM pack_clinic.clinic_appointment a
                 WHERE a.tenant_id = @TenantId AND a.deleted_at IS NULL
                   AND a.appointment_at >= @From AND a.appointment_at < @To
                   AND a.appointment_status = 'checked_in') AS AppointmentsCheckedIn,
                (SELECT COUNT(*)::int FROM pack_clinic.clinic_appointment a
                 WHERE a.tenant_id = @TenantId AND a.deleted_at IS NULL
                   AND a.appointment_at >= @From AND a.appointment_at < @To
                   AND a.appointment_status = 'no_show') AS AppointmentsNoShow,
                (SELECT COUNT(*)::int FROM pack_clinic.clinic_visit v
                 WHERE v.tenant_id = @TenantId AND v.deleted_at IS NULL
                   AND v.started_at >= @From AND v.started_at < @To
                   AND v.visit_status = 'open') AS VisitsOpen,
                (SELECT COUNT(*)::int FROM pack_clinic.clinic_visit v
                 WHERE v.tenant_id = @TenantId AND v.deleted_at IS NULL
                   AND v.started_at >= @From AND v.started_at < @To
                   AND v.visit_status = 'closed') AS VisitsClosed,
                (SELECT COUNT(*)::int FROM pack_clinic.clinic_prescription r
                 WHERE r.tenant_id = @TenantId AND r.deleted_at IS NULL
                   AND r.created_at >= @From AND r.created_at < @To
                   AND r.prescription_status = 'draft') AS PrescriptionsDraft,
                (SELECT COUNT(*)::int FROM pack_clinic.clinic_prescription r
                 WHERE r.tenant_id = @TenantId AND r.deleted_at IS NULL
                   AND COALESCE(r.finalized_at, r.created_at) >= @From
                   AND COALESCE(r.finalized_at, r.created_at) < @To
                   AND r.prescription_status = 'finalized') AS PrescriptionsFinalized,
                (SELECT COUNT(*)::int FROM pack_connect.rx_handoffs h
                 WHERE h.clinic_tenant_id = @TenantId
                   AND h.created_at >= @From AND h.created_at < @To) AS PrescriptionsSentToPharmacy
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<SummaryRow>(sql, new
        {
            TenantId = _tenant.TenantId,
            From = from,
            To = to,
        });

        return new ClinicDaySummaryDto(
            day,
            row.AppointmentsToday,
            row.AppointmentsRemoteToday,
            row.AppointmentsCheckedIn,
            row.AppointmentsNoShow,
            row.VisitsOpen,
            row.VisitsClosed,
            row.PrescriptionsDraft,
            row.PrescriptionsFinalized,
            row.PrescriptionsSentToPharmacy);
    }

    private sealed class SummaryRow
    {
        public int AppointmentsToday { get; init; }
        public int AppointmentsRemoteToday { get; init; }
        public int AppointmentsCheckedIn { get; init; }
        public int AppointmentsNoShow { get; init; }
        public int VisitsOpen { get; init; }
        public int VisitsClosed { get; init; }
        public int PrescriptionsDraft { get; init; }
        public int PrescriptionsFinalized { get; init; }
        public int PrescriptionsSentToPharmacy { get; init; }
    }
}
