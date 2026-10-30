using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class PaperLedgerSqlTests
{
    const string ConnectionString = "Host=127.0.0.1;Port=5433;Database=second_brain_agent_v1;Username=pi_validation;Application Name=paper-ledger-sql-test";
    static readonly Guid KmTenant = Guid.Parse("e1e1e1e1-e1e1-41e1-81e1-e1e1e1e1e1e1");
    static readonly Guid KmUser = Guid.Parse("e2e2e2e2-e2e2-42e2-82e2-e2e2e2e2e2e2");
    static readonly Guid KmPerson = Guid.Parse("e3e3e3e3-e3e3-43e3-83e3-e3e3e3e3e3e3");
    static readonly Guid KmExchange = Guid.Parse("e4e4e4e4-e4e4-44e4-84e4-e4e4e4e4e4e4");
    static readonly Guid KmAccount = Guid.Parse("e5e5e5e5-e5e5-45e5-85e5-e5e5e5e5e5e5");
    static readonly Guid ExtraExchange = Guid.Parse("f4f4f4f4-f4f4-44f4-84f4-f4f4f4f4f4f4");
    static readonly Guid ExtraAccount = Guid.Parse("f5f5f5f5-f5f5-45f5-85f5-f5f5f5f5f5f5");

    [Fact]
    public async Task Both_ledgers_record_a_task_and_a_mismatch_or_other_tenant_does_not()
    {
        RequireIsolatedAgentDatabase();
        var factory = new NpgsqlConnectionFactory(ConnectionString);
        var sb = await LoadSecondBrainAsync();
        await SeedKitMktAsync();

        var sbEvidence = await Session(factory, sb.TenantId, sb.UserId).HandleAsync("paper", Caller(sb.UserId, sb.TenantId, "personal_intelligence.trading.run"), CancellationToken.None);
        var kmEvidence = await Session(factory, KmTenant, KmUser).HandleAsync("paper", Caller(KmUser, KmTenant, "personal_intelligence.trading.run"), CancellationToken.None);
        var replay = await Session(factory, sb.TenantId, sb.UserId).HandleAsync("paper", Caller(sb.UserId, sb.TenantId, "personal_intelligence.trading.run"), CancellationToken.None);

        Assert.True(Flag(sbEvidence, "decisionPersisted"));
        Assert.True(Flag(kmEvidence, "decisionPersisted"));
        Assert.Equal(Text(sbEvidence, "decisionId"), Text(replay, "decisionId"));
        Assert.Equal(Text(sbEvidence, "taskId"), Text(replay, "taskId"));
        Assert.Equal("waiting_approval", Text(sbEvidence, "taskStatus"));
        var sbResult = await OperationResultAsync(sb.TenantId, Text(sbEvidence, "operationKey"));
        var kmResult = await OperationResultAsync(KmTenant, Text(kmEvidence, "operationKey"));
        Assert.Contains(sb.AccountId.ToString("D"), sbResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(KmAccount.ToString("D"), kmResult, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(KmAccount.ToString("D"), sbResult, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*) FROM pack_personal_intelligence.pi_task WHERE tenant_id = @Id AND operation_key = @Key", new { Id = sb.TenantId, Key = Text(sbEvidence, "operationKey") }));
        Assert.True(await ScalarAsync<int>("SELECT COUNT(*) FROM pack_personal_intelligence.pi_events WHERE tenant_id = @Id AND correlation_id = @DecisionId::uuid AND event_type = 'DecisionMade'", new { Id = sb.TenantId, DecisionId = Text(sbEvidence, "decisionId") }) >= 1);

        var decisions = await ScalarAsync<int>("SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Id", new { Id = sb.TenantId });
        var denied = await Session(factory, sb.TenantId, sb.UserId).HandleAsync("paper", Caller(sb.UserId, sb.TenantId, "personal_intelligence.research.read"), CancellationToken.None);
        Assert.Equal("permission_denied", denied.Plan.Single().Disposition);
        Assert.Empty(denied.Execution);
        var quiet = await Session(factory, sb.TenantId, sb.UserId, failModel: true).HandleAsync("paper", Caller(sb.UserId, sb.TenantId, "personal_intelligence.trading.run"), CancellationToken.None);
        Assert.Equal("unavailable", quiet.Outcome);
        Assert.Equal(decisions, await ScalarAsync<int>("SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Id", new { Id = sb.TenantId }));

        await using (var conn = await OpenAsync())
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_personal_intelligence.exchange (exchange_id, tenant_id, exchange_code, exchange_name)
                VALUES (@ExtraExchange, @TenantId, 'PAPER_LEDGER', 'Wrong ledger')
                ON CONFLICT (exchange_id) DO NOTHING;
                INSERT INTO pack_personal_intelligence.trading_account
                    (trading_account_id, person_id, tenant_id, exchange_id, external_account_id, account_name)
                VALUES (@ExtraAccount, @PersonId, @TenantId, @ExtraExchange, 'KIT_MKT-PAPER-LEDGER', 'Wrong account')
                ON CONFLICT (trading_account_id) DO NOTHING;
                """,
                new { ExtraExchange, ExtraAccount, sb.TenantId, sb.PersonId });
        }
        try
        {
            var mismatched = await Session(factory, sb.TenantId, sb.UserId, price: "101").HandleAsync("paper", Caller(sb.UserId, sb.TenantId, "personal_intelligence.trading.run"), CancellationToken.None);
            Assert.Contains("không khớp", mismatched.NextStep, StringComparison.OrdinalIgnoreCase);
            Assert.False(Flag(mismatched, "decisionPersisted"));
            Assert.Equal(decisions, await ScalarAsync<int>("SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @Id", new { Id = sb.TenantId }));
        }
        finally
        {
            await using var conn = await OpenAsync();
            await conn.ExecuteAsync(
                """
                DELETE FROM pack_personal_intelligence.trading_account WHERE trading_account_id = @ExtraAccount;
                DELETE FROM pack_personal_intelligence.exchange WHERE exchange_id = @ExtraExchange;
                """,
                new { ExtraAccount, ExtraExchange });
        }
    }

    static void RequireIsolatedAgentDatabase()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        if (builder.Port == 5432 || builder.Host is not ("127.0.0.1" or "::1"))
            throw new InvalidOperationException("Paper ledger SQL test refuses every database except the loopback agent test database.");
        if (!string.Equals(builder.Database, "second_brain_agent_v1", StringComparison.Ordinal))
            throw new InvalidOperationException("Paper ledger SQL test refuses a database other than second_brain_agent_v1.");
    }

    static async Task<(Guid TenantId, Guid UserId, Guid PersonId, Guid AccountId)> LoadSecondBrainAsync()
    {
        await using var conn = await OpenAsync();
        var row = await conn.QuerySingleAsync<(Guid TenantId, Guid UserId, Guid PersonId, Guid AccountId)>(
            """
            SELECT t.id AS TenantId, u.id AS UserId, p.person_id AS PersonId, a.trading_account_id AS AccountId
            FROM public.tenants t
            JOIN public.users u ON u.tenant_id = t.id AND u.username = 'sb-operator'
            JOIN pack_personal_intelligence.person p ON p.user_id = u.id AND p.tenant_id = t.id AND p.deleted_at IS NULL
            JOIN pack_personal_intelligence.trading_account a ON a.person_id = p.person_id AND a.tenant_id = t.id AND a.deleted_at IS NULL
            JOIN pack_personal_intelligence.exchange e ON e.exchange_id = a.exchange_id AND e.tenant_id = t.id
            WHERE t.tenant_code = 'SECOND_BRAIN'
              AND e.exchange_code = 'PAPER'
              AND a.external_account_id = 'paper-fixture'
            """);
        return row;
    }

    static async Task SeedKitMktAsync()
    {
        await using var conn = await OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO public.tenants (id, tenant_code, tenant_name)
            VALUES (@KmTenant, 'KIT_MKT', 'Kit mkt ledger test')
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO public.users (id, tenant_id, username, email, password_hash)
            VALUES (@KmUser, @KmTenant, 'km-ledger-test', 'km-ledger-test@validation.local', 'not-a-secret')
            ON CONFLICT (id) DO NOTHING;
            INSERT INTO pack_personal_intelligence.person (person_id, user_id, tenant_id, display_name)
            VALUES (@KmPerson, @KmUser, @KmTenant, 'KM')
            ON CONFLICT (person_id) DO NOTHING;
            INSERT INTO pack_personal_intelligence.exchange (exchange_id, tenant_id, exchange_code, exchange_name)
            VALUES (@KmExchange, @KmTenant, 'PAPER_LEDGER', 'Paper ledger')
            ON CONFLICT (exchange_id) DO NOTHING;
            INSERT INTO pack_personal_intelligence.trading_account
                (trading_account_id, person_id, tenant_id, exchange_id, external_account_id, account_name)
            VALUES (@KmAccount, @KmPerson, @KmTenant, @KmExchange, 'KIT_MKT-PAPER-LEDGER', 'Kit mkt paper')
            ON CONFLICT (trading_account_id) DO NOTHING;
            """,
            new { KmTenant, KmUser, KmPerson, KmExchange, KmAccount });
    }

    static BusinessIntentSession Session(NpgsqlConnectionFactory factory, Guid tenantId, Guid userId, bool failModel = false, string price = "100")
    {
        var tenant = new LedgerTenant(tenantId, userId);
        return new BusinessIntentSession(
            failModel ? new QuietInterpreter() : new ReadyPaperInterpreter(price),
            [new PaperEvaluationHandler()],
            new PaperDecisionRecorder(factory, tenant, new TradingOperationalLoop(factory, tenant)),
            new PaperTaskStore(factory, tenant));
    }

    static async Task<T> ScalarAsync<T>(string sql, object args)
    {
        await using var conn = await OpenAsync();
        return await conn.ExecuteScalarAsync<T>(sql, args) ?? throw new InvalidOperationException("The ledger query returned no value.");
    }

    static async Task<string> OperationResultAsync(Guid tenantId, string operationKey)
    {
        await using var conn = await OpenAsync();
        return await conn.ExecuteScalarAsync<string>(
            """
            SELECT result::text FROM pack_personal_intelligence.pi_operation
            WHERE tenant_id = @TenantId AND operation_key = @OperationKey
            """,
            new { TenantId = tenantId, OperationKey = operationKey }) ?? "";
    }

    static async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    static bool Flag(BusinessIntentEvidence evidence, string name)
    {
        var step = evidence.Execution.SingleOrDefault();
        if (step is null)
            return false;
        using var json = System.Text.Json.JsonDocument.Parse(step.EvidenceJson);
        return json.RootElement.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.True;
    }

    static string Text(BusinessIntentEvidence evidence, string name)
    {
        using var json = System.Text.Json.JsonDocument.Parse(evidence.Execution.Single().EvidenceJson);
        return json.RootElement.GetProperty(name).GetString() ?? "";
    }

    static BusinessCaller Caller(Guid userId, Guid tenantId, string permission) =>
        new(userId, tenantId, false, new HashSet<string>([permission], StringComparer.Ordinal));

    sealed class LedgerTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }

    sealed class ReadyPaperInterpreter(string price) : IBusinessIntentInterpreter
    {
        public Task<InterpretationOutcome> InterpretAsync(string utterance, IReadOnlyList<CapabilityDescriptor> catalog, InterpretationContext context, CancellationToken cancellationToken) =>
            Task.FromResult<InterpretationOutcome>(new InterpretationOutcome.Ready(new IntentInterpretation(
                "paper",
                ["recorded"],
                [],
                [],
                [],
                [new IntentBinding(PlatformCapabilityCatalog.TradingPaperEvaluate, new Dictionary<string, string>
                {
                    ["symbol"] = "ZZSBTEST",
                    ["side"] = "BUY",
                    ["quantity"] = "0.01",
                    ["price"] = price,
                    ["mode"] = "paper",
                    ["maxNotional"] = "1000",
                    ["maxLeverage"] = "2",
                    ["minConfidence"] = "0.5",
                    ["leverage"] = "1",
                    ["confidence"] = "0.8",
                }, "evaluate")],
                false, null, null, null, null)));
    }

    sealed class QuietInterpreter : IBusinessIntentInterpreter
    {
        public Task<InterpretationOutcome> InterpretAsync(string utterance, IReadOnlyList<CapabilityDescriptor> catalog, InterpretationContext context, CancellationToken cancellationToken) =>
            Task.FromResult<InterpretationOutcome>(new InterpretationOutcome.Unavailable("not configured"));
    }
}
