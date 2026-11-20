using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intelligence;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Npgsql;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class PostgresIntegrationTests : IClassFixture<PiValidationDatabase>
{
    private readonly PiValidationDatabase _db;

    public PostgresIntegrationTests(PiValidationDatabase db) => _db = db;

    [Fact]
    public async Task Journal_persists_context_evidence_decision_and_pi_events()
    {
        var journal = Journal(_db.TenantA);
        var context = new PiContext(Guid.NewGuid(), _db.TenantA, _db.PersonA, "Fixture context", EpistemicKind.Observation, "validation");
        var evidence = new PiEvidence(Guid.NewGuid(), _db.TenantA, _db.PersonA, context.ContextId, "fixture-evidence", EpistemicKind.Observation);
        var decision = IntelligenceLoop.Decide(_db.TenantA, _db.PersonA, context.ContextId, "Wait for a pullback", 0.6m, "Smaller drawdown", "Break of the low", EpistemicKind.Hypothesis);

        await journal.SaveContextAsync(context);
        await journal.SaveEvidenceAsync(evidence);
        await journal.SaveDecisionAsync(decision);

        var stored = await journal.GetDecisionAsync(decision.DecisionId);
        var events = await journal.ListEventsAsync(_db.PersonA);

        Assert.NotNull(stored);
        Assert.Equal(decision.Thesis, stored.Thesis);
        Assert.Equal(_db.TenantA, stored.TenantId);
        Assert.Equal(EpistemicKind.Hypothesis, stored.Kind);
        Assert.Contains(events, row => row.EventType == "DecisionMade" && row.CorrelationId == decision.DecisionId && row.Sink == IntelligenceLoop.EventSink);

        await using var conn = await _db.OpenAsync();
        var eventTable = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pack_personal_intelligence.pi_events
            WHERE tenant_id = @TenantId AND correlation_id = @DecisionId AND event_type = 'DecisionMade'
            """,
            new { TenantId = _db.TenantA, decision.DecisionId });
        var platformEvents = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'platform_events'");
        Assert.Equal(1, eventTable);
        Assert.Equal(0, platformEvents);
    }

    [Fact]
    public async Task Tenant_b_cannot_read_tenant_a_through_the_repository()
    {
        var decision = IntelligenceLoop.Decide(_db.TenantA, _db.PersonA, null, "Tenant A only", 0.7m, null, null, EpistemicKind.Hypothesis);
        await Journal(_db.TenantA).SaveDecisionAsync(decision);

        var hidden = await Journal(_db.TenantB).GetDecisionAsync(decision.DecisionId);
        var wrongTenantWrite = IntelligenceLoop.Decide(_db.TenantA, _db.PersonA, null, "Cross tenant write", 0.7m, null, null, EpistemicKind.Hypothesis);

        Assert.Null(hidden);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Journal(_db.TenantB).SaveDecisionAsync(wrongTenantWrite));

        await using var conn = await _db.OpenAsync();
        var visibleToA = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE decision_id = @Id AND tenant_id = @TenantId",
            new { Id = decision.DecisionId, TenantId = _db.TenantA });
        var visibleToB = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE decision_id = @Id AND tenant_id = @TenantId",
            new { Id = decision.DecisionId, TenantId = _db.TenantB });
        Assert.Equal(1, visibleToA);
        Assert.Equal(0, visibleToB);
    }

    [Fact]
    public async Task Failed_event_write_rolls_back_the_decision()
    {
        var decision = IntelligenceLoop.Decide(_db.TenantA, _db.PersonA, null, "This must roll back", 0.5m, null, null, EpistemicKind.Hypothesis);
        await Assert.ThrowsAnyAsync<PostgresException>(() => Journal(_db.TenantA).ProbeSecondWriteFailureAsync(decision));

        await using var conn = await _db.OpenAsync();
        var decisions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE decision_id = @Id",
            new { Id = decision.DecisionId });
        var events = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_events WHERE correlation_id = @Id",
            new { Id = decision.DecisionId });
        Assert.Equal(0, decisions);
        Assert.Equal(0, events);
    }

    [Fact]
    public async Task Repeating_a_trade_does_not_insert_or_overwrite()
    {
        var repo = new TradeRepository(_db.Factory, new FixedTenant(_db.TenantA));
        var first = await repo.CreateAsync(
            _db.PersonA, _db.TenantA, _db.AccountA, "EXT-1", "BTCUSDT", "BUY", 0.1m, 10m, 1m, null, null,
            DateTimeOffset.Parse("2026-10-01T00:00:00Z"), "fixture", "IMPORT", true, null, null, null, default);
        var second = await repo.CreateAsync(
            _db.PersonA, _db.TenantA, _db.AccountA, "EXT-1", "BTCUSDT", "BUY", 0.1m, 99m, 9.9m, null, null,
            DateTimeOffset.Parse("2026-10-02T00:00:00Z"), "fixture-replay", "IMPORT", true, null, null, null, default);

        await using var conn = await _db.OpenAsync();
        var row = await conn.QuerySingleAsync<(int Count, decimal Price, string Source)>(
            """
            SELECT COUNT(*)::int AS Count, MAX(price) AS Price, MAX(source) AS Source
            FROM pack_personal_intelligence.trade
            WHERE trading_account_id = @AccountId AND external_trade_id = 'EXT-1' AND tenant_id = @TenantId
            """,
            new { AccountId = _db.AccountA, TenantId = _db.TenantA });
        var uniqueIndex = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM pg_indexes
            WHERE schemaname = 'pack_personal_intelligence'
              AND indexname = 'uq_trade_account_external'
            """);

        Assert.Equal(first, second);
        Assert.Equal(1, row.Count);
        Assert.Equal(10m, row.Price);
        Assert.Equal("fixture", row.Source);
        Assert.Equal(1, uniqueIndex);
    }

    [Fact]
    public async Task Pi_events_reject_update_and_delete()
    {
        var decision = IntelligenceLoop.Decide(_db.TenantA, _db.PersonA, null, "Append only", 0.6m, null, null, EpistemicKind.Hypothesis);
        await Journal(_db.TenantA).SaveDecisionAsync(decision);
        await using var conn = await _db.OpenAsync();
        var eventId = await conn.ExecuteScalarAsync<Guid>(
            "SELECT event_id FROM pack_personal_intelligence.pi_events WHERE correlation_id = @Id",
            new { Id = decision.DecisionId });

        await Assert.ThrowsAnyAsync<PostgresException>(() => conn.ExecuteAsync(
            "UPDATE pack_personal_intelligence.pi_events SET source = 'mutated' WHERE event_id = @Id",
            new { Id = eventId }));
        await Assert.ThrowsAnyAsync<PostgresException>(() => conn.ExecuteAsync(
            "DELETE FROM pack_personal_intelligence.pi_events WHERE event_id = @Id",
            new { Id = eventId }));

        var source = await conn.ExecuteScalarAsync<string>(
            "SELECT source FROM pack_personal_intelligence.pi_events WHERE event_id = @Id",
            new { Id = eventId });
        Assert.Equal("journal", source);
    }

    [Fact]
    public async Task Paper_fill_is_stored_and_veto_or_live_leave_no_execution()
    {
        var decision = IntelligenceLoop.Decide(_db.TenantA, _db.PersonA, null, "Paper only", 0.8m, null, null, EpistemicKind.Hypothesis);
        await Journal(_db.TenantA).SaveDecisionAsync(decision);
        var limits = new RiskLimits(1_000m, 2m, 0.55m);
        var proposal = new TradeProposal("BTCUSDT", "BUY", 0.1m, 100m, 1m, 0.8m, ExecutionMode.Paper);
        var store = new PaperExecutionStore(_db.Factory, new FixedTenant(_db.TenantA));

        var filled = await store.RecordAsync(_db.PersonA, _db.AccountA, proposal, limits, new ExecutionApproval(decision.DecisionId, "owner"));
        var veto = await store.RecordAsync(
            _db.PersonA,
            _db.AccountA,
            new TradeProposal("BTCUSDT", "BUY", 10m, 500m, 1m, 0.9m, ExecutionMode.Paper),
            limits,
            new ExecutionApproval(decision.DecisionId, "owner"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.RecordAsync(
            _db.PersonA,
            _db.AccountA,
            proposal with { Mode = ExecutionMode.Live },
            limits,
            new ExecutionApproval(decision.DecisionId, "owner")));

        await using var conn = await _db.OpenAsync();
        var modes = (await conn.QueryAsync<string>(
            """
            SELECT e.mode
            FROM pack_personal_intelligence.trading_execution e
            JOIN pack_personal_intelligence.trading_order o
              ON o.order_id = e.order_id AND o.tenant_id = e.tenant_id
            WHERE o.decision_id = @DecisionId AND o.tenant_id = @TenantId
            """,
            new { DecisionId = decision.DecisionId, TenantId = _db.TenantA })).ToArray();

        Assert.True(filled.Executed);
        Assert.False(veto.Executed);
        Assert.Equal(["PAPER"], modes);
    }

    [Fact]
    public async Task Reflection_cannot_point_at_another_tenants_outcome()
    {
        var decision = IntelligenceLoop.Decide(_db.TenantA, _db.PersonA, null, "Outcome owner", 0.6m, null, null, EpistemicKind.Hypothesis);
        await Journal(_db.TenantA).SaveDecisionAsync(decision);
        var outcome = new PiOutcome(Guid.NewGuid(), _db.TenantA, decision.DecisionId, "Observed later", EpistemicKind.Observation);
        await Journal(_db.TenantA).SaveOutcomeAsync(outcome, _db.PersonA);

        await using var conn = await _db.OpenAsync();
        await Assert.ThrowsAnyAsync<PostgresException>(() => conn.ExecuteAsync(
            """
            INSERT INTO pack_personal_intelligence.pi_reflection (
                tenant_id, person_id, outcome_id, note, epistemic_kind)
            VALUES (@TenantId, @PersonId, @OutcomeId, 'cross tenant', 'INFERENCE')
            """,
            new { TenantId = _db.TenantB, PersonId = _db.PersonB, OutcomeId = outcome.OutcomeId }));
    }

    [Fact]
    public async Task Personal_intelligence_tables_do_not_enable_rls()
    {
        await using var conn = await _db.OpenAsync();
        var secured = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'pack_personal_intelligence'
              AND c.relkind = 'r'
              AND c.relrowsecurity
            """);
        Assert.Equal(0, secured);
    }

    private IntelligenceJournal Journal(Guid tenantId) => new(_db.Factory, new FixedTenant(tenantId));
}

public sealed class PiValidationDatabase : IAsyncLifetime
{
    public const string ExpectedDatabase = "kit_pi_validation";
    public Guid TenantA { get; } = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    public Guid TenantB { get; } = Guid.Parse("b2222222-2222-2222-2222-222222222222");
    public Guid UserA { get; } = Guid.Parse("a3333333-3333-3333-3333-333333333333");
    public Guid UserB { get; } = Guid.Parse("b4444444-4444-4444-4444-444444444444");
    public Guid PersonA { get; } = Guid.Parse("a5555555-5555-5555-5555-555555555555");
    public Guid PersonB { get; } = Guid.Parse("b6666666-6666-6666-6666-666666666666");
    public Guid ExchangeA { get; } = Guid.Parse("a7777777-7777-7777-7777-777777777777");
    public Guid AccountA { get; } = Guid.Parse("a8888888-8888-8888-8888-888888888888");
    public NpgsqlConnectionFactory Factory { get; private set; } = null!;
    public string ConnectionString { get; private set; } = "";
    private string _connectionString = "";

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("KIT_PI_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("KIT_PI_TEST_CONNECTION is required. PostgreSQL integration is NOT VERIFIED without the isolated database.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Database is "kitplatform" or "kitplatform_test" or "pharmacore" or "postgres")
            throw new InvalidOperationException("Refusing a shared database.");
        if (builder.Port == 5432)
            throw new InvalidOperationException("Refusing the shared PostgreSQL port.");
        if (!string.Equals(builder.Database, ExpectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing an unexpected database name.");

        _connectionString = connectionString;
        ConnectionString = connectionString;
        Factory = new NpgsqlConnectionFactory(connectionString);
        await using var conn = await OpenAsync();
        var current = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
        if (!string.Equals(current, ExpectedDatabase, StringComparison.Ordinal))
            throw new InvalidOperationException("Connected database does not match the isolated validation database.");

        await conn.ExecuteAsync(
            """
            INSERT INTO public.tenants (id, tenant_code, tenant_name)
            VALUES (@TenantA, 'PI_A', 'Validation A'), (@TenantB, 'PI_B', 'Validation B')
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO public.users (id, tenant_id, username, email, password_hash)
            VALUES
                (@UserA, @TenantA, 'pi-a', 'pi-a@validation.local', 'not-a-secret'),
                (@UserB, @TenantB, 'pi-b', 'pi-b@validation.local', 'not-a-secret')
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO pack_personal_intelligence.person (person_id, user_id, tenant_id, display_name)
            VALUES
                (@PersonA, @UserA, @TenantA, 'A'),
                (@PersonB, @UserB, @TenantB, 'B')
            ON CONFLICT (person_id) DO NOTHING;

            INSERT INTO pack_personal_intelligence.exchange (exchange_id, tenant_id, exchange_code, exchange_name)
            VALUES (@ExchangeA, @TenantA, 'fixture', 'Fixture')
            ON CONFLICT (exchange_id) DO NOTHING;

            INSERT INTO pack_personal_intelligence.trading_account (
                trading_account_id, person_id, tenant_id, exchange_id, external_account_id)
            VALUES (@AccountA, @PersonA, @TenantA, @ExchangeA, 'fixture-account')
            ON CONFLICT (trading_account_id) DO NOTHING;
            """,
            this);

        await conn.ExecuteAsync(
            """
            UPDATE public.tenants
            SET settings = settings || '{"platform":{"enabled_modules":["personal_intelligence"]}}'::jsonb
            WHERE id = @TenantA;

            INSERT INTO public.permissions (id, permission_code, permission_name, module_name)
            VALUES
                ('c1111111-1111-1111-1111-111111111111', 'personal_intelligence.trading.run', 'Run trading loop', 'personal_intelligence'),
                ('c4444444-4444-4444-4444-444444444444', 'personal_intelligence.trading.approve', 'Approve paper execution', 'personal_intelligence')
            ON CONFLICT (permission_code) DO NOTHING;

            INSERT INTO public.roles (id, tenant_id, role_code, role_name)
            VALUES
                ('c2222222-2222-2222-2222-222222222222', @TenantA, 'PI_TRADING', 'PI trading'),
                ('c3333333-3333-3333-3333-333333333333', @TenantB, 'PI_TRADING', 'PI trading'),
                ('c5555555-5555-5555-5555-555555555555', @TenantA, 'PI_APPROVE', 'PI paper approval'),
                ('c6666666-6666-6666-6666-666666666666', @TenantB, 'PI_APPROVE', 'PI paper approval')
            ON CONFLICT (tenant_id, role_code) DO NOTHING;

            INSERT INTO public.role_permissions (role_id, permission_id)
            SELECT r.id, p.id
            FROM public.roles r
            JOIN public.permissions p ON p.permission_code = 'personal_intelligence.trading.run'
            WHERE r.role_code = 'PI_TRADING'
            ON CONFLICT DO NOTHING;

            INSERT INTO public.role_permissions (role_id, permission_id)
            SELECT r.id, p.id
            FROM public.roles r
            JOIN public.permissions p ON p.permission_code = 'personal_intelligence.trading.approve'
            WHERE r.role_code = 'PI_APPROVE'
            ON CONFLICT DO NOTHING;

            INSERT INTO public.user_roles (user_id, role_id)
            SELECT u.id, r.id
            FROM public.users u
            JOIN public.roles r ON r.tenant_id = u.tenant_id AND r.role_code IN ('PI_TRADING', 'PI_APPROVE')
            WHERE u.id IN (@UserA, @UserB)
            ON CONFLICT DO NOTHING;
            """,
            new { TenantA, TenantB, UserA, UserB });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}

file sealed class FixedTenant(Guid tenantId) : ITenantContext
{
    public Guid TenantId { get; } = tenantId;
    public Guid UserId => Guid.Empty;
    public bool IsAuthenticated => true;
    public Guid? WorkspaceId => null;
}
