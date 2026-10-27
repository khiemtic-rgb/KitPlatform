using KitPlatform.Packs.PersonalIntelligence;
using Npgsql;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

public sealed class SqlEvidenceLedger : IEvidenceLedger
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
}
