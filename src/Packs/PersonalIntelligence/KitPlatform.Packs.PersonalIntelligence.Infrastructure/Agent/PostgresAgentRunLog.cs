using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using Npgsql;
using NpgsqlTypes;

namespace KitPlatform.Packs.PersonalIntelligence.Agent;

/// <summary>
/// Task log on pack_personal_intelligence.pi_agent_task.
/// One instance claims one run and must be disposed so the session lock is released.
/// </summary>
public sealed class PostgresAgentRunLog : IAgentRunLog, IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _connectionString;
    private readonly Guid _tenantId;
    private NpgsqlConnection? _connection;
    private bool _locked;
    private long _lockKey;
    private int _revision;

    public PostgresAgentRunLog(string connectionString, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("A task log requires a connection string.");
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("A task log requires a tenant.");
        _connectionString = connectionString;
        _tenantId = tenantId;
    }

    public AgentTaskReceipt? Find(string taskKey)
    {
        using var connection = Open();
        return Read(connection, taskKey);
    }

    public AgentTaskReceipt? Claim(AgentTaskReceipt receipt)
    {
        if (_connection is not null)
            throw new InvalidOperationException("This log is already holding a task.");
        RequireSameTenant(receipt.TenantId);
        _connection = Open();
        try
        {
            AcquireLock(LockKey(receipt.TenantId, receipt.TaskKey));
            var existing = Read(_connection, receipt.TaskKey);
            if (existing is not null)
            {
                Release();
                Reject(existing, receipt);
                return existing;
            }

            try
            {
                Insert(_connection, receipt, 1);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                existing = Read(_connection, receipt.TaskKey);
                Release();
                if (existing is null)
                    throw;
                Reject(existing, receipt);
                return existing;
            }

            _revision = 1;
            if (ReleasesLock(receipt.Status))
                Release();
            return null;
        }
        catch
        {
            Release();
            throw;
        }
    }

    public AgentTaskReceipt? Adopt(AgentTaskReceipt receipt)
    {
        if (_connection is not null)
            throw new InvalidOperationException("This log is already holding a task.");
        RequireSameTenant(receipt.TenantId);
        _connection = Open();
        try
        {
            AcquireLock(LockKey(receipt.TenantId, receipt.TaskKey));
            var existing = Read(_connection, receipt.TaskKey);
            if (existing is null)
            {
                Release();
                return null;
            }

            Reject(existing, receipt);
            if (existing.Status is not (AgentTaskStatus.Running or AgentTaskStatus.Verified))
            {
                Release();
                return existing;
            }

            _revision = Revision(_connection, receipt.TaskKey);
            return existing;
        }
        catch
        {
            Release();
            throw;
        }
    }

    public void Remember(AgentTaskReceipt receipt)
    {
        RequireSameTenant(receipt.TenantId);
        if (_connection is null || !_locked)
            throw new InvalidOperationException("A task result can be saved only by the log that claimed it.");
        var next = _revision + 1;
        var rows = Update(_connection, receipt, _revision, next);
        if (rows != 1)
            throw new InvalidOperationException("The stored task changed before this result was saved.");
        _revision = next;
        if (ReleasesLock(receipt.Status))
            Release();
    }

    public void Dispose() => Release();

    private void RequireSameTenant(Guid tenantId)
    {
        if (tenantId != _tenantId)
            throw new AgentTenantMismatchException();
    }

    private static void Reject(AgentTaskReceipt existing, AgentTaskReceipt incoming)
    {
        if (existing.TenantId != incoming.TenantId)
            throw new AgentTenantMismatchException();
        if (!string.Equals(existing.PayloadHash, incoming.PayloadHash, StringComparison.Ordinal))
            throw new AgentRunConflictException();
    }

    private static bool ReleasesLock(AgentTaskStatus status)
        => status is AgentTaskStatus.Completed
            or AgentTaskStatus.Failed
            or AgentTaskStatus.AwaitingApproval
            or AgentTaskStatus.Cancelled
            or AgentTaskStatus.ReconciliationRequired;

    private NpgsqlConnection Open()
    {
        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        if (builder.Port == 5432 || IsForbidden(builder.Database))
            throw new InvalidOperationException("Refusing this database for the agent task log.");
        var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Open();
        try
        {
            using var check = new NpgsqlCommand("SELECT current_database(), inet_server_port()", connection);
            using var reader = check.ExecuteReader();
            reader.Read();
            var database = reader.GetString(0);
            var port = reader.GetInt32(1);
            if (port == 5432 || port != builder.Port || !string.Equals(database, builder.Database, StringComparison.Ordinal) || IsForbidden(database))
                throw new InvalidOperationException("Connected database does not match the agent task log.");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static bool IsForbidden(string? database)
        => database is "trading_agent" or "novixa_prod" or "trading_research" or "kitplatform" or "kitplatform_test" or "pharmacore" or "postgres" or "kit_pi_validation";

    private AgentTaskReceipt? Read(NpgsqlConnection connection, string taskKey)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT payload_hash, status, receipt::text
            FROM pack_personal_intelligence.pi_agent_task
            WHERE tenant_id = @TenantId AND task_key = @TaskKey
            """,
            connection);
        command.Parameters.AddWithValue("TenantId", _tenantId);
        command.Parameters.AddWithValue("TaskKey", taskKey);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;
        var status = reader.GetString(1);
        var receipt = JsonSerializer.Deserialize<AgentTaskReceipt>(reader.GetString(2), Json)
            ?? throw new InvalidOperationException("The stored task receipt is empty.");
        if (receipt.TenantId != _tenantId || !string.Equals(receipt.Status.ToString(), status, StringComparison.Ordinal))
            throw new InvalidOperationException("The stored task receipt does not match its row.");
        return receipt;
    }

    private static void Insert(NpgsqlConnection connection, AgentTaskReceipt receipt, int revision)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO pack_personal_intelligence.pi_agent_task
                (tenant_id, task_key, payload_hash, status, receipt, revision)
            VALUES
                (@TenantId, @TaskKey, @PayloadHash, @Status, @Receipt, @Revision)
            """,
            connection);
        Bind(command, receipt, revision);
        command.ExecuteNonQuery();
    }

    private static int Update(NpgsqlConnection connection, AgentTaskReceipt receipt, int expectedRevision, int nextRevision)
    {
        using var command = new NpgsqlCommand(
            """
            UPDATE pack_personal_intelligence.pi_agent_task
            SET status = @Status,
                receipt = @Receipt,
                revision = @Revision,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND task_key = @TaskKey
              AND payload_hash = @PayloadHash
              AND revision = @ExpectedRevision
            """,
            connection);
        Bind(command, receipt, nextRevision);
        command.Parameters.AddWithValue("ExpectedRevision", expectedRevision);
        return command.ExecuteNonQuery();
    }

    private static void Bind(NpgsqlCommand command, AgentTaskReceipt receipt, int revision)
    {
        command.Parameters.AddWithValue("TenantId", receipt.TenantId);
        command.Parameters.AddWithValue("TaskKey", receipt.TaskKey);
        command.Parameters.AddWithValue("PayloadHash", receipt.PayloadHash);
        command.Parameters.AddWithValue("Status", receipt.Status.ToString());
        var json = new NpgsqlParameter("Receipt", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(receipt, Json),
        };
        command.Parameters.Add(json);
        command.Parameters.AddWithValue("Revision", revision);
    }

    private void AcquireLock(long key)
    {
        _lockKey = key;
        using (var timeout = new NpgsqlCommand("SET lock_timeout = '15s'", _connection))
            timeout.ExecuteNonQuery();
        try
        {
            using var lockCommand = new NpgsqlCommand("SELECT pg_advisory_lock(@Key)", _connection);
            lockCommand.Parameters.AddWithValue("Key", key);
            lockCommand.ExecuteNonQuery();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.LockNotAvailable)
        {
            throw new AgentTaskBusyException();
        }

        _locked = true;
    }

    private int Revision(NpgsqlConnection connection, string taskKey)
    {
        using var command = new NpgsqlCommand(
            """
            SELECT revision
            FROM pack_personal_intelligence.pi_agent_task
            WHERE tenant_id = @TenantId AND task_key = @TaskKey
            """,
            connection);
        command.Parameters.AddWithValue("TenantId", _tenantId);
        command.Parameters.AddWithValue("TaskKey", taskKey);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static long LockKey(Guid tenantId, string taskKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(tenantId.ToString("N") + "|" + taskKey));
        return BitConverter.ToInt64(bytes, 0);
    }

    private void Release()
    {
        var connection = _connection;
        _connection = null;
        if (connection is null)
            return;
        try
        {
            if (_locked)
            {
                using var unlock = new NpgsqlCommand("SELECT pg_advisory_unlock(@Key)", connection);
                unlock.Parameters.AddWithValue("Key", _lockKey);
                unlock.ExecuteNonQuery();
            }
        }
        finally
        {
            _locked = false;
            connection.Dispose();
        }
    }
}
