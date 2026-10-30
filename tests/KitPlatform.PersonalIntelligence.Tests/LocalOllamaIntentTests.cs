using System.Net.Http.Headers;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intent;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class LocalOllamaIntentTests(ITestOutputHelper output)
{
    const string ConnectionString = "Host=127.0.0.1;Port=5433;Database=second_brain_agent_v1;Username=pi_validation;Application Name=local-ollama-intent";

    [LocalOllamaFact]
    public async Task Local_model_records_a_paper_task_or_stops_without_a_decision()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        Assert.Equal(5433, builder.Port);
        Assert.Equal("127.0.0.1", builder.Host);
        Assert.Equal("second_brain_agent_v1", builder.Database);

        await using var lookup = new NpgsqlConnection(ConnectionString);
        await lookup.OpenAsync();
        var ids = await Dapper.SqlMapper.QuerySingleAsync<(Guid TenantId, Guid UserId)>(lookup,
            """
            SELECT t.id AS TenantId, u.id AS UserId
            FROM public.tenants t
            JOIN public.users u ON u.tenant_id = t.id AND u.username = 'sb-operator'
            WHERE t.tenant_code = 'SECOND_BRAIN'
            """);
        var before = await Dapper.SqlMapper.ExecuteScalarAsync<int>(lookup,
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @TenantId",
            new { ids.TenantId });

        var probe = new LocalOnlyHandler();
        var options = new BusinessIntentModelOptions
        {
            Enabled = true,
            BaseUrl = "http://127.0.0.1:11434/v1",
            ApiKey = "ollama",
            Model = "qwen2.5:7b",
            MaxOutputTokens = 512,
            MaxCallsPerProcess = 1,
            RequestTimeoutSeconds = 45,
        };
        var http = new HttpClient(probe) { Timeout = TimeSpan.FromSeconds(45) };
        var session = new BusinessIntentSession(
            new CatalogGroundedIntentInterpreter(http, Options.Create(options)),
            [new PaperEvaluationHandler()],
            new PaperDecisionRecorder(new NpgsqlConnectionFactory(ConnectionString), new FixedTenant(ids.TenantId, ids.UserId), new TradingOperationalLoop(new NpgsqlConnectionFactory(ConnectionString), new FixedTenant(ids.TenantId, ids.UserId))),
            new PaperTaskStore(new NpgsqlConnectionFactory(ConnectionString), new FixedTenant(ids.TenantId, ids.UserId)));

        var evidence = await session.HandleAsync(
            "Đánh giá một lệnh giấy, không live. symbol BTCUSDT, side BUY, quantity 0.01, price 100, leverage 1, confidence 0.8, maxNotional 1000, maxLeverage 2, minConfidence 0.5, mode paper.",
            new BusinessCaller(ids.UserId, ids.TenantId, false, new HashSet<string>(["personal_intelligence.trading.run"], StringComparer.Ordinal)),
            CancellationToken.None);

        Assert.Equal(1, probe.Calls);
        output.WriteLine(evidence.Outcome);
        output.WriteLine(evidence.NextStep);
        Assert.DoesNotContain("ollama", evidence.NextStep, StringComparison.OrdinalIgnoreCase);
        var after = await Dapper.SqlMapper.ExecuteScalarAsync<int>(lookup,
            "SELECT COUNT(*) FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @TenantId",
            new { ids.TenantId });
        if (evidence.Outcome is "unreadable" or "unavailable" or "blocked" or "ran_incomplete")
        {
            Assert.Equal(before, after);
            return;
        }

        Assert.Equal("completed", evidence.Outcome);
        Assert.Equal(before + 1, after);
        var step = evidence.Execution.Single();
        using var json = System.Text.Json.JsonDocument.Parse(step.EvidenceJson);
        Assert.True(json.RootElement.GetProperty("decisionPersisted").GetBoolean());
        Assert.Equal("waiting_approval", json.RootElement.GetProperty("taskStatus").GetString());
    }

    sealed class LocalOnlyHandler : DelegatingHandler
    {
        public int Calls;

        public LocalOnlyHandler() : base(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) })
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not { Host: "127.0.0.1", Port: 11434 })
                throw new InvalidOperationException("The local model test refuses every endpoint except 127.0.0.1:11434.");
            Calls++;
            return base.SendAsync(request, cancellationToken);
        }
    }

    sealed class FixedTenant(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId { get; } = userId;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }

    sealed class LocalOllamaFactAttribute : FactAttribute
    {
        public LocalOllamaFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("SB_LOCAL_OLLAMA") != "1")
                Skip = "Local Ollama is not selected for this run.";
        }
    }
}
