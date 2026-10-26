using System.Diagnostics;
using KitPlatform.Packs.PersonalIntelligence.Agent;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class AgentTaskDatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = "";
    public Guid TenantA { get; } = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    public Guid TenantB { get; } = Guid.Parse("b2222222-2222-2222-2222-222222222222");

    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("KIT_PI_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("KIT_PI_TEST_CONNECTION is required.");
        var builder = new NpgsqlConnectionStringBuilder(source);
        if (builder.Port != 5433 || builder.Host is not ("127.0.0.1" or "localhost"))
            throw new InvalidOperationException("Refusing a database that is not the isolated local cluster.");
        if (builder.Database is "kitplatform" or "kitplatform_test" or "pharmacore" or "trading_agent" or "trading_research" or "novixa_prod")
            throw new InvalidOperationException("Refusing a shared or operating database.");

        var admin = new NpgsqlConnectionStringBuilder(builder.ConnectionString) { Database = "postgres" };
        await using (var connection = new NpgsqlConnection(admin.ConnectionString))
        {
            await connection.OpenAsync();
            var current = (string)(await new NpgsqlCommand("SELECT current_database()", connection).ExecuteScalarAsync())!;
            var port = Convert.ToInt32(await new NpgsqlCommand("SELECT inet_server_port()", connection).ExecuteScalarAsync());
            if (current != "postgres" || port != 5433)
                throw new InvalidOperationException("Refusing to create a database on this connection.");
            var exists = await new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = 'kit_pi_agent_task'", connection).ExecuteScalarAsync();
            if (exists is null)
                await new NpgsqlCommand("CREATE DATABASE kit_pi_agent_task", connection).ExecuteNonQueryAsync();
        }

        builder.Database = "kit_pi_agent_task";
        ConnectionString = builder.ConnectionString;
        await using var database = new NpgsqlConnection(ConnectionString);
        await database.OpenAsync();
        var name = (string)(await new NpgsqlCommand("SELECT current_database()", database).ExecuteScalarAsync())!;
        var databasePort = Convert.ToInt32(await new NpgsqlCommand("SELECT inet_server_port()", database).ExecuteScalarAsync());
        if (name != "kit_pi_agent_task" || databasePort != 5433)
            throw new InvalidOperationException("The agent task database was not selected.");
        if (await new NpgsqlCommand("SELECT to_regclass('pack_personal_intelligence.pi_agent_task')::text", database).ExecuteScalarAsync() is null or DBNull)
        {
            await new NpgsqlCommand(BootstrapSql, database).ExecuteNonQueryAsync();
            var migration = await File.ReadAllTextAsync(Path.Combine(FindRepo(), "migrations", "399_pack_personal_intelligence_agent_task_v1.sql"));
            await new NpgsqlCommand(migration, database).ExecuteNonQueryAsync();
        }

        await InsertTenant(database, TenantA, "AGENT_A");
        await InsertTenant(database, TenantB, "AGENT_B");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    internal static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "migrations", "399_pack_personal_intelligence_agent_task_v1.sql")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("The agent task migration is not in this tree.");
    }

    private static async Task InsertTenant(NpgsqlConnection connection, Guid id, string code)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO public.tenants (id, tenant_code, tenant_name)
            VALUES (@Id, @Code, @Code)
            ON CONFLICT (id) DO NOTHING
            """,
            connection);
        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("Code", code);
        await command.ExecuteNonQueryAsync();
    }

    private const string BootstrapSql =
        """
        CREATE EXTENSION IF NOT EXISTS pgcrypto;
        CREATE OR REPLACE FUNCTION kit_uuid_v7()
        RETURNS uuid
        LANGUAGE plpgsql
        VOLATILE PARALLEL SAFE
        AS $$
        DECLARE
            unix_ts_ms bytea;
            rand_a     bytea;
            rand_b     bytea;
            uuid_bytes bytea;
        BEGIN
            unix_ts_ms := substring(int8send(floor(extract(epoch FROM clock_timestamp()) * 1000)::bigint) FROM 3);
            rand_a     := gen_random_bytes(2);
            rand_b     := gen_random_bytes(8);
            uuid_bytes := unix_ts_ms || rand_a || rand_b;
            uuid_bytes := set_byte(uuid_bytes, 6, (get_byte(uuid_bytes, 6) & 15) | 112);
            uuid_bytes := set_byte(uuid_bytes, 8, (get_byte(uuid_bytes, 8) & 63) | 128);
            RETURN encode(uuid_bytes, 'hex')::uuid;
        END;
        $$;
        CREATE TABLE IF NOT EXISTS public.tenants (
            id UUID PRIMARY KEY,
            tenant_code VARCHAR(50) NOT NULL UNIQUE,
            tenant_name VARCHAR(255) NOT NULL
        );
        """;
}

public sealed class PostgresAgentTaskTests : IClassFixture<AgentTaskDatabaseFixture>
{
    private readonly AgentTaskDatabaseFixture _database;

    public PostgresAgentTaskTests(AgentTaskDatabaseFixture database) => _database = database;

    [Fact]
    public void Agent_task_migration_is_not_on_a_deploy_manifest()
    {
        var root = AgentTaskDatabaseFixture.FindRepo();
        foreach (var name in new[]
        {
            "migration-files.prod.txt",
            "migration-files.personal-intelligence.txt",
            "migration-files.trading-research.txt",
            "migration-files.content.txt",
            "migration-files.family-os.txt",
            "migration-files.kit-sales.txt",
            "migration-files.local-os.txt",
        })
        {
            var text = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", name));
            Assert.DoesNotContain("399_pack_personal_intelligence_agent_task_v1.sql", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void New_log_reads_the_stored_receipt_from_postgres()
    {
        var calls = 0;
        var key = Key();
        var request = Request(key, _database.TenantA, [Step("one")]);
        AgentTaskReceipt first;
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
            first = new AgentRuntime(ReadRegistry(() => calls++)).Execute(request, log);

        AgentTaskReceipt second;
        var replayCalls = 0;
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
            second = new AgentRuntime(ReadRegistry(() => replayCalls++)).Execute(request, log);

        Assert.Equal(AgentTaskStatus.Completed, first.Status);
        Assert.Equal(1, calls);
        Assert.True(second.Replayed);
        Assert.Equal(first.Steps[0].Summary, second.Steps[0].Summary);
        Assert.Equal(first.Plan![0].StepId, second.Plan![0].StepId);
        Assert.Contains("step-one", second.EvidenceRefs);
        Assert.Equal(0, replayCalls);
    }

    [Fact]
    public void Awaiting_approval_survives_a_new_postgres_log()
    {
        var reads = 0;
        var writes = 0;
        var key = Key();
        var request = Request(
            key,
            _database.TenantA,
            [Step("one"), new AgentStepSpec("two", "paper", "one")],
            ["personal_intelligence.research.read", "personal_intelligence.trading.approve"]);
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            reads++;
            return ToolResponse.Succeeded("research stored", new Dictionary<string, string> { ["verified"] = "true", ["evidence"] = "session-1" });
        });
        registry.Register(new ToolContract("paper", "personal_intelligence.trading.approve", ToolEffect.Paper, true), _ =>
        {
            writes++;
            return ToolResponse.Succeeded("filled", new Dictionary<string, string> { ["verified"] = "true" });
        });
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
        {
            var first = new AgentRuntime(registry).Execute(request, log);
            Assert.Equal(AgentTaskStatus.AwaitingApproval, first.Status);
            Assert.False(first.ApprovalRecorded);
            Assert.Equal(ToolStepStatus.Blocked, first.Steps[1].Status);
        }

        var replayReads = 0;
        using var restarted = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var second = new AgentRuntime(ReadRegistry(() => replayReads++)).Execute(request, restarted);
        Assert.True(second.Replayed);
        Assert.Equal(AgentTaskStatus.AwaitingApproval, second.Status);
        Assert.False(second.ApprovalRecorded);
        Assert.Equal(1, reads);
        Assert.Equal(0, writes);
        Assert.Equal(0, replayReads);
    }

    [Fact]
    public void Completed_task_does_not_run_again_from_a_new_postgres_log()
    {
        var calls = 0;
        var request = Request(Key(), _database.TenantA, [Step("one")]);
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
            new AgentRuntime(ReadRegistry(() => calls++)).Execute(request, log);
        var replayCalls = 0;
        using var restarted = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var second = new AgentRuntime(ReadRegistry(() => replayCalls++)).Execute(request, restarted);

        Assert.Equal(AgentTaskStatus.Completed, second.Status);
        Assert.True(second.Replayed);
        Assert.False(second.LiveAccepted);
        Assert.Equal(1, calls);
        Assert.Equal(0, replayCalls);
    }

    [Fact]
    public void Failed_task_keeps_the_failed_step_in_postgres()
    {
        var later = 0;
        var key = Key();
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("first", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
            ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true", ["evidence"] = "step-one" }));
        registry.Register(new ToolContract("second", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
            throw new InvalidOperationException("boom"));
        registry.Register(new ToolContract("third", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            later++;
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var request = Request(
            key,
            _database.TenantA,
            [new AgentStepSpec("one", "first", null), new AgentStepSpec("two", "second", "one"), new AgentStepSpec("three", "third", "two")],
            ["personal_intelligence.research.read"]);
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
            new AgentRuntime(registry).Execute(request, log);

        using var restarted = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var stored = restarted.Find(key);
        Assert.NotNull(stored);
        Assert.Equal(AgentTaskStatus.Failed, stored.Status);
        Assert.Equal(ToolStepStatus.Failed, stored.Steps[1].Status);
        Assert.Contains("boom", stored.Error, StringComparison.Ordinal);
        Assert.Contains("step-one", stored.EvidenceRefs);
        Assert.Equal(0, later);
    }

    [Fact]
    public void Same_payload_does_not_call_the_tool_twice()
    {
        var calls = 0;
        var request = Request(Key(), _database.TenantA, [Step("one")]);
        var runtime = new AgentRuntime(ReadRegistry(() => calls++));
        using var first = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        using var second = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        runtime.Execute(request, first);
        var replay = runtime.Execute(request, second);

        Assert.True(replay.Replayed);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Different_payload_is_rejected_without_calling_the_tool()
    {
        var calls = 0;
        var request = Request(Key(), _database.TenantA, [Step("one")]);
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
            new AgentRuntime(ReadRegistry(() => calls++)).Execute(request, log);
        var replayCalls = 0;
        using var restarted = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var conflict = Assert.Throws<AgentRunConflictException>(() =>
            new AgentRuntime(ReadRegistry(() => replayCalls++)).Execute(request with { PayloadHash = "other-payload" }, restarted));

        Assert.Equal(1, calls);
        Assert.Equal(0, replayCalls);
        Assert.DoesNotContain("other-payload", conflict.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("step-one", conflict.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Another_tenant_cannot_read_or_replace_the_task()
    {
        var key = Key();
        var request = Request(key, _database.TenantA, [Step("one")]);
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
            new AgentRuntime(ReadRegistry(() => { }, "tenant-a-outcome")).Execute(request, log);

        using (var owner = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
        using (var other = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantB))
        {
            var stored = owner.Find(key);
            Assert.Null(other.Find(key));
            Assert.Throws<AgentTenantMismatchException>(() => other.Remember(stored!));
            Assert.Equal("tenant-a-outcome", owner.Find(key)!.Steps[0].Summary);
        }

        var otherCalls = 0;
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantB))
            new AgentRuntime(ReadRegistry(() => otherCalls++)).Execute(request with { TenantId = _database.TenantB, PayloadHash = "tenant-b" }, log);

        using var reread = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        Assert.Equal("tenant-a-outcome", reread.Find(key)!.Steps[0].Summary);
        Assert.Equal(1, otherCalls);
        Assert.Equal(0, Scalar(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_agent_task WHERE tenant_id = @Tenant AND task_key = @Key AND payload_hash = 'tenant-b'",
            _database.TenantA,
            key));
    }

    [Fact]
    public async Task Two_callers_do_not_create_two_rows_or_call_the_tool_twice()
    {
        var calls = 0;
        var request = Request(Key(), _database.TenantA, [Step("one")]);
        var registry = ReadRegistry(() =>
        {
            Interlocked.Increment(ref calls);
            Thread.Sleep(200);
        });
        var first = Task.Run(() =>
        {
            using var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
            return new AgentRuntime(registry).Execute(request, log);
        });
        var second = Task.Run(() =>
        {
            using var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
            return new AgentRuntime(registry).Execute(request, log);
        });
        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, calls);
        Assert.Equal(AgentTaskStatus.Completed, results[0].Status);
        Assert.Equal(AgentTaskStatus.Completed, results[1].Status);
        Assert.Single(results, row => row.Replayed);
        Assert.Equal(1, Scalar(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_agent_task WHERE tenant_id = @Tenant AND task_key = @Key",
            _database.TenantA,
            request.TaskKey));
    }

    [Fact]
    public void A_stale_save_does_not_mark_the_task_completed()
    {
        var calls = 0;
        var key = Key();
        var request = Request(key, _database.TenantA, [Step("one")]);
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            calls++;
            using var connection = new NpgsqlConnection(_database.ConnectionString);
            connection.Open();
            using var command = new NpgsqlCommand(
                """
                UPDATE pack_personal_intelligence.pi_agent_task
                SET revision = revision + 1
                WHERE tenant_id = @Tenant AND task_key = @Key
                """,
                connection);
            command.Parameters.AddWithValue("Tenant", _database.TenantA);
            command.Parameters.AddWithValue("Key", key);
            command.ExecuteNonQuery();
            return ToolResponse.Succeeded("ok", new Dictionary<string, string> { ["verified"] = "true" });
        });

        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
            Assert.Throws<InvalidOperationException>(() => new AgentRuntime(registry).Execute(request, log));

        using var restarted = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var stored = restarted.Find(key);
        Assert.Equal(1, calls);
        Assert.NotEqual(AgentTaskStatus.Completed, stored!.Status);
        var replayCalls = 0;
        var replay = new AgentRuntime(ReadRegistry(() => replayCalls++)).Execute(request, restarted);
        Assert.True(replay.Replayed);
        Assert.NotEqual(AgentTaskStatus.Completed, replay.Status);
        Assert.Equal(0, replayCalls);
    }

    [Fact]
    public void Crash_after_the_first_persisted_step_does_not_complete_the_task()
    {
        var calls = 0;
        var key = Key();
        var request = Request(key, _database.TenantA, [Step("one"), Step("two", "one")]);
        var crashed = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var log = new CrashAfterFirstStepLog(crashed);
        Assert.Throws<InvalidOperationException>(() => new AgentRuntime(ReadRegistry(() => calls++)).Execute(request, log));
        crashed.Dispose();

        using var restarted = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var stored = restarted.Find(key);
        Assert.Equal(1, calls);
        Assert.Equal(AgentTaskStatus.Running, stored!.Status);
        Assert.Single(stored.Steps);
        Assert.NotEqual(AgentTaskStatus.Completed, stored.Status);
        var replayCalls = 0;
        var replay = new AgentRuntime(ReadRegistry(() => replayCalls++)).Execute(request, restarted);
        Assert.True(replay.Replayed);
        Assert.Equal(AgentTaskStatus.Running, replay.Status);
        Assert.Equal(0, replayCalls);
    }

    [Fact]
    public async Task A_stopped_process_leaves_a_postgres_receipt_that_a_new_log_does_not_complete()
    {
        var key = "process-" + Guid.NewGuid().ToString("N");
        var repo = AgentTaskDatabaseFixture.FindRepo();
        var probe = Path.Combine(repo, "tests", "KitPlatform.PersonalIntelligence.AgentTaskProbe", "bin", "Debug", "net10.0", "KitPlatform.PersonalIntelligence.AgentTaskProbe.dll");
        var start = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "exec \"" + probe + "\"",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["KIT_AGENT_TASK_CONNECTION"] = _database.ConnectionString;
        start.Environment["KIT_AGENT_TASK_KEY"] = key;
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The child test process did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(90000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException("The child test process did not exit.");
        }

        var outputText = await output;
        var errorText = await error;
        Assert.True(process.ExitCode == 87, "Child exit " + process.ExitCode + Environment.NewLine + outputText + errorText);
        var replayCalls = 0;
        using var restarted = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var request = Request(key, _database.TenantA, [Step("one"), Step("two", "one")]);
        var stored = restarted.Find(key);
        var replay = new AgentRuntime(ReadRegistry(() => replayCalls++)).Execute(request, restarted);

        Assert.Equal(AgentTaskStatus.Running, stored!.Status);
        Assert.Single(stored.Steps);
        Assert.NotEqual(AgentTaskStatus.Completed, stored.Status);
        Assert.True(replay.Replayed);
        Assert.Equal(AgentTaskStatus.Running, replay.Status);
        Assert.Equal(0, replayCalls);
    }

    [Fact]
    public void Live_tool_is_rejected_and_the_postgres_receipt_stays_rejected()
    {
        var calls = 0;
        var key = Key();
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("live", "personal_intelligence.trading.approve", ToolEffect.Live, true), _ =>
        {
            calls++;
            return ToolResponse.Succeeded("sent", new Dictionary<string, string> { ["verified"] = "true" });
        });
        var request = Request(
            key,
            _database.TenantA,
            [new AgentStepSpec("one", "live", null)],
            ["personal_intelligence.trading.approve"],
            new ExecutionApproval(Guid.NewGuid(), "operator"));
        using (var log = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA))
        {
            var first = new AgentRuntime(registry).Execute(request, log);
            Assert.Equal(AgentTaskStatus.Failed, first.Status);
            Assert.Equal(ToolStepStatus.Rejected, first.Steps[0].Status);
            Assert.False(first.LiveAccepted);
            Assert.True(first.ApprovalRecorded);
        }

        using var restarted = new PostgresAgentRunLog(_database.ConnectionString, _database.TenantA);
        var stored = restarted.Find(key);
        Assert.Equal(0, calls);
        Assert.False(stored!.LiveAccepted);
        Assert.Equal(ToolStepStatus.Rejected, stored.Steps[0].Status);
        Assert.NotEqual(AgentTaskStatus.Completed, stored.Status);
    }

    private int Scalar(string sql, Guid tenant, string key)
    {
        using var connection = new NpgsqlConnection(_database.ConnectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("Tenant", tenant);
        command.Parameters.AddWithValue("Key", key);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string Key() => "task-" + Guid.NewGuid().ToString("N");

    private static AgentStepSpec Step(string id, string? dependsOn = null)
        => new(id, "read", dependsOn);

    private AgentRequest Request(
        string key,
        Guid tenant,
        IReadOnlyList<AgentStepSpec> plan,
        IReadOnlyList<string>? permissions = null,
        ExecutionApproval? approval = null)
        => new(
            key,
            "payload-" + key,
            tenant,
            "Record a paper recommendation.",
            "The receipt names the outcome.",
            permissions ?? ["personal_intelligence.research.read"],
            plan,
            approval);

    private static ToolRegistry ReadRegistry(Action call, string summary = "ok")
    {
        var registry = new ToolRegistry();
        registry.Register(new ToolContract("read", "personal_intelligence.research.read", ToolEffect.Read, false), _ =>
        {
            call();
            return ToolResponse.Succeeded(summary, new Dictionary<string, string> { ["verified"] = "true", ["evidence"] = "step-one" });
        });
        return registry;
    }

    private sealed class CrashAfterFirstStepLog(PostgresAgentRunLog inner) : IAgentRunLog
    {
        public AgentTaskReceipt? Find(string taskKey) => inner.Find(taskKey);

        public AgentTaskReceipt? Claim(AgentTaskReceipt receipt) => inner.Claim(receipt);

        public void Remember(AgentTaskReceipt receipt)
        {
            inner.Remember(receipt);
            if (receipt.Status == AgentTaskStatus.Running && receipt.Steps.Count >= 1)
                throw new InvalidOperationException("crash after the persisted step");
        }
    }
}
