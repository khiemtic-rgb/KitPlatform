using KitPlatform.Packs.PersonalIntelligence;
using Npgsql;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

public sealed class SqlEvidenceLedger : IEvidenceLedger, IDecisionSupportLedger, IEvidenceAssessmentLedger
{
    private readonly string _connection;

    public SqlEvidenceLedger(string connection)
    {
        _connection = connection;
    }

    public EvidenceObservation Read(Guid tenantId, string epistemicKind)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(epistemicKind))
            throw new InvalidOperationException("Evidence review requires a tenant and a kind.");
        using var connection = new NpgsqlConnection(_connection);
        connection.Open();
        using var command = new NpgsqlCommand(
            """
            SELECT COUNT(*)::int, MIN(source_ref)
            FROM pack_personal_intelligence.pi_evidence
            WHERE tenant_id = @Tenant AND epistemic_kind = @Kind
            """,
            connection);
        command.Parameters.AddWithValue("Tenant", tenantId);
        command.Parameters.AddWithValue("Kind", epistemicKind);
        using var reader = command.ExecuteReader();
        reader.Read();
        var count = reader.GetInt32(0);
        var sample = reader.IsDBNull(1) ? null : reader.GetString(1);
        return new EvidenceObservation(epistemicKind, count, sample);
    }

    public DecisionSupportCoverage Read(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Decision support requires a tenant.");
        using var connection = new NpgsqlConnection(_connection);
        connection.Open();
        using var command = new NpgsqlCommand(
            """
            SELECT
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_decision d
                    WHERE d.tenant_id = @Tenant
                      AND EXISTS (
                        SELECT 1 FROM pack_personal_intelligence.pi_evidence e
                        WHERE e.tenant_id = d.tenant_id AND e.context_id = d.context_id)),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_evidence
                    WHERE tenant_id = @Tenant AND payload <> '{}'::jsonb)
            """,
            connection);
        command.Parameters.AddWithValue("Tenant", tenantId);
        using var reader = command.ExecuteReader();
        reader.Read();
        return new DecisionSupportCoverage(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
    }

    EvidenceQuality IEvidenceAssessmentLedger.Read(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Evidence validation requires a tenant.");
        using var connection = new NpgsqlConnection(_connection);
        connection.Open();
        using var command = new NpgsqlCommand(
            """
            SELECT
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Tenant AND context_id IS NULL),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_decision d
                    WHERE d.tenant_id = @Tenant
                      AND d.context_id IS NOT NULL
                      AND EXISTS (
                        SELECT 1 FROM pack_personal_intelligence.pi_evidence e
                        WHERE e.tenant_id = d.tenant_id AND e.context_id = d.context_id)),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_decision d
                    WHERE d.tenant_id = @Tenant
                      AND d.context_id IS NOT NULL
                      AND NOT EXISTS (
                        SELECT 1 FROM pack_personal_intelligence.pi_evidence e
                        WHERE e.tenant_id = d.tenant_id AND e.context_id = d.context_id)),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_evidence WHERE tenant_id = @Tenant),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_evidence
                    WHERE tenant_id = @Tenant AND payload = '{}'::jsonb),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_evidence e
                    JOIN pack_personal_intelligence.pi_context c
                      ON c.context_id = e.context_id AND c.tenant_id = e.tenant_id
                    WHERE e.tenant_id = @Tenant AND c.summary = @Observation),
                (SELECT COUNT(*)::int FROM pack_personal_intelligence.pi_evidence
                    WHERE tenant_id = @Tenant
                      AND jsonb_typeof(payload) = 'object'
                      AND jsonb_exists(payload, 'statement')
                      AND length(btrim(payload->>'statement')) > 0)
            """,
            connection);
        command.Parameters.AddWithValue("Tenant", tenantId);
        command.Parameters.AddWithValue("Observation", StoredTradeObservation.Statement);
        using var reader = command.ExecuteReader();
        reader.Read();
        return new EvidenceQuality(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetInt32(6),
            reader.GetInt32(7));
    }
}
