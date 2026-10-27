using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.PersonalIntelligence;
using KitPlatform.Application.Configuration;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intent;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Research;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Research;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class BusinessIntentGateTests
{
    [Fact]
    public void Catalog_permissions_match_the_existing_policies()
    {
        var research = PlatformCapabilityCatalog.All.Single(item => item.Id == PlatformCapabilityCatalog.ResearchPublicationRead);
        Assert.Equal(
            new[] { PersonalIntelligencePolicies.ResearchReadPermission, PersonalIntelligencePolicies.Permission }.OrderBy(item => item),
            research.Permissions.OrderBy(item => item));
        var desk = PlatformCapabilityCatalog.All.Single(item => item.Id == PlatformCapabilityCatalog.TradingDeskSummaryRead);
        Assert.Equal(new[] { PersonalIntelligencePolicies.Permission }, desk.Permissions);
    }

    [Fact]
    public void Fingerprint_is_stable()
    {
        Assert.Equal(
            PlatformCapabilityCatalog.Fingerprint(PlatformCapabilityCatalog.All),
            PlatformCapabilityCatalog.Fingerprint(PlatformCapabilityCatalog.All.Reverse().ToArray()));
    }

    [Fact]
    public void Same_binding_produces_the_same_capability_when_only_the_goal_text_changes()
    {
        var caller = Caller(PersonalIntelligencePolicies.ResearchReadPermission);
        var first = BusinessIntentPlanner.Plan("model", Interpretation("alpha", ResearchBinding()), PlatformCapabilityCatalog.All, caller);
        var second = BusinessIntentPlanner.Plan("model", Interpretation("beta", ResearchBinding()), PlatformCapabilityCatalog.All, caller);
        Assert.Equal(IntentDisposition.Run, first.Steps.Single().Disposition);
        Assert.Equal(first.Steps.Single().CapabilityId, second.Steps.Single().CapabilityId);
    }

    [Fact]
    public void Desk_and_paper_bindings_stay_on_different_capabilities()
    {
        var caller = Caller(PersonalIntelligencePolicies.Permission);
        var desk = BusinessIntentPlanner.Plan("model", Interpretation("read", DeskBinding()), PlatformCapabilityCatalog.All, caller);
        var paper = BusinessIntentPlanner.Plan("model", Interpretation("act", PaperBinding("live")), PlatformCapabilityCatalog.All, caller);
        Assert.Equal(PlatformCapabilityCatalog.TradingDeskSummaryRead, desk.Steps.Single().CapabilityId);
        Assert.Equal(PlatformCapabilityCatalog.TradingPaperEvaluate, paper.Steps.Single().CapabilityId);
        Assert.Equal(IntentDisposition.Evaluate, paper.Steps.Single().Disposition);
        Assert.Contains(paper.HumanDecisions, item => item.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate);
    }

    [Fact]
    public void Missing_slots_do_not_get_filled()
    {
        var caller = Caller(PersonalIntelligencePolicies.Permission);
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation("buy", new IntentBinding(PlatformCapabilityCatalog.TradingPaperEvaluate, new Dictionary<string, string> { ["symbol"] = "BTCUSDT" }, "")),
            PlatformCapabilityCatalog.All,
            caller);
        var step = plan.Steps.Single();
        Assert.Equal(IntentDisposition.MissingInformation, step.Disposition);
        Assert.False(step.RunHandler);
        Assert.Contains("price", step.MissingSlots);
        Assert.Contains("mode", step.MissingSlots);
        Assert.DoesNotContain(step.Slots, pair => pair.Key == "price");
    }

    [Fact]
    public void Unknown_capability_is_rejected_and_cannot_be_forced()
    {
        var caller = Caller(PersonalIntelligencePolicies.Permission);
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation("stock", new IntentBinding("pharmacy.inventory.expiring", new Dictionary<string, string>(), "")),
            PlatformCapabilityCatalog.All,
            caller);
        Assert.Equal(IntentDisposition.NotInCatalog, plan.Steps.Single().Disposition);
        Assert.False(BusinessIntentPlanner.MayExecute(
            new PlanStep("pharmacy.inventory.expiring", IntentDisposition.Run, new Dictionary<string, string>(), [], "forged", true),
            Interpretation("stock", new IntentBinding("pharmacy.inventory.expiring", new Dictionary<string, string>(), "")),
            PlatformCapabilityCatalog.All,
            caller));
    }

    [Fact]
    public void Unknown_side_effect_is_not_runnable()
    {
        var capability = new CapabilityDescriptor(
            "custom.unknown",
            "custom",
            "A handler whose side effect is not classified.",
            SideEffectKind.Unknown,
            false,
            true,
            false,
            [PersonalIntelligencePolicies.Permission],
            []);
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation("go", new IntentBinding("custom.unknown", new Dictionary<string, string>(), "")),
            [capability],
            Caller(PersonalIntelligencePolicies.Permission));
        Assert.Equal(IntentDisposition.UnknownSideEffect, plan.Steps.Single().Disposition);
        Assert.False(plan.Steps.Single().RunHandler);
    }

    [Fact]
    public void Another_tenant_blocks_every_handler()
    {
        var caller = Caller(PersonalIntelligencePolicies.ResearchReadPermission);
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation("read", ResearchBinding(), requestedTenant: "NT_XUANHOA"),
            PlatformCapabilityCatalog.All,
            caller);
        Assert.Equal(IntentDisposition.TenantConflict, plan.Steps.Single().Disposition);
        Assert.False(plan.Steps.Single().RunHandler);
    }

    [Fact]
    public void Ambiguous_request_is_not_executed()
    {
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation("unclear", ResearchBinding(), ambiguous: true),
            PlatformCapabilityCatalog.All,
            Caller(PersonalIntelligencePolicies.ResearchReadPermission));
        Assert.Equal(IntentDisposition.Ambiguous, plan.Steps.Single().Disposition);
        Assert.False(plan.Steps.Single().RunHandler);
    }

    [Fact]
    public void Research_permission_does_not_open_the_desk()
    {
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation("read", DeskBinding()),
            PlatformCapabilityCatalog.All,
            Caller(PersonalIntelligencePolicies.ResearchReadPermission));
        Assert.Equal(IntentDisposition.PermissionDenied, plan.Steps.Single().Disposition);
    }

    [Fact]
    public void Approval_text_in_a_slot_is_stripped_before_the_paper_gate()
    {
        var slots = PaperSlots("paper");
        slots["approvedBy"] = "person-from-the-sentence";
        var plan = BusinessIntentPlanner.Plan(
            "model",
            Interpretation("approve", new IntentBinding(PlatformCapabilityCatalog.TradingPaperEvaluate, slots, "")),
            PlatformCapabilityCatalog.All,
            Caller(PersonalIntelligencePolicies.Permission));
        Assert.DoesNotContain(plan.Steps.Single().Slots, pair => pair.Key.Contains("approv", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Paper_gate_refuses_live_without_writing()
    {
        var result = await new PaperEvaluationHandler().ExecuteAsync(Caller(PersonalIntelligencePolicies.Permission), PaperSlots("live"), CancellationToken.None);
        using var json = JsonDocument.Parse(result.EvidenceJson);
        Assert.Equal("live_refused", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("live_probe_not_a_policy", json.RootElement.GetProperty("riskLimitsSource").GetString());
        Assert.False(result.WroteData);
        Assert.False(result.ExternalCall);
        Assert.False(json.RootElement.GetProperty("fillRecorded").GetBoolean());
        Assert.Contains("Live execution is outside Trading Agent V1.", json.RootElement.GetProperty("riskReasons").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains("Live orders are not sent.", json.RootElement.GetProperty("paperReasons").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task Paper_gate_does_not_invent_risk_limits()
    {
        var slots = new Dictionary<string, string>
        {
            ["symbol"] = "BTCUSDT",
            ["side"] = "BUY",
            ["quantity"] = "0.01",
            ["price"] = "60000",
            ["mode"] = "paper",
        };
        var result = await new PaperEvaluationHandler().ExecuteAsync(Caller(PersonalIntelligencePolicies.Permission), slots, CancellationToken.None);
        using var json = JsonDocument.Parse(result.EvidenceJson);
        Assert.Equal("risk_policy_absent", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.GetProperty("approvalAccepted").GetBoolean());
        Assert.Equal(DemoPaperExecution.SeparateApprovalRequired, json.RootElement.GetProperty("approvalReason").GetString());
        Assert.False(result.CapabilityCompletionMet);
        Assert.False(result.WroteData);
    }

    [Fact]
    public async Task Paper_gate_still_requires_a_separate_approval_when_the_stated_limits_pass()
    {
        var result = await new PaperEvaluationHandler().ExecuteAsync(Caller(PersonalIntelligencePolicies.Permission), PaperSlots("paper"), CancellationToken.None);
        using var json = JsonDocument.Parse(result.EvidenceJson);
        Assert.Equal("approval_required", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("request", json.RootElement.GetProperty("riskLimitsSource").GetString());
        Assert.Contains("Paper execution requires a separate approval.", json.RootElement.GetProperty("paperReasons").EnumerateArray().Select(item => item.GetString()));
        Assert.False(json.RootElement.GetProperty("fillRecorded").GetBoolean());
        Assert.False(result.WroteData);
    }

    [Fact]
    public void Parser_accepts_string_fields_as_well_as_arrays()
    {
        var json = """
            {"goal":"read","completionConditions":"hash is returned","contextUsed":"the caller asked for the published package","missingInformation":[],"humanDecisions":[],"bindings":[{"capabilityId":"personal_intelligence.research.publication.read","slots":{},"purpose":"read"}],"ambiguous":false}
            """;
        var ready = Assert.IsType<InterpretationOutcome.Ready>(CatalogGroundedIntentInterpreter.Parse(json));
        Assert.Equal(PlatformCapabilityCatalog.ResearchPublicationRead, ready.Interpretation.Bindings.Single().CapabilityId);
        Assert.Equal("hash is returned", ready.Interpretation.CompletionConditions.Single());
    }

    [Fact]
    public async Task Unconfigured_model_does_not_call_the_network_or_a_handler()
    {
        var calls = 0;
        var interpreter = new CatalogGroundedIntentInterpreter(
            new HttpClient(new DelegateHandler(_ =>
            {
                calls++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            })),
            Options.Create(new BusinessIntentModelOptions()));
        var session = new BusinessIntentSession(interpreter, [new ResearchPublicationCapabilityHandler(new MissingReader())]);
        var evidence = await session.HandleAsync("anything", Caller(PersonalIntelligencePolicies.ResearchReadPermission), CancellationToken.None);
        Assert.Equal(0, calls);
        Assert.Equal("unavailable", evidence.Outcome);
        Assert.Empty(evidence.Execution);
        Assert.Contains("không đoán bằng từ khóa", evidence.NextStep);
    }

    [Fact]
    public async Task Parsed_model_json_cannot_invent_a_capability()
    {
        var payload = """
            {"goal":"list stock","completionConditions":["rows returned"],"contextUsed":[],"missingInformation":[],"humanDecisions":[],"bindings":[{"capabilityId":"pharmacy.inventory.expiring","slots":{},"purpose":"stock"}],"ambiguous":false,"requestedTenant":null}
            """;
        var ready = Assert.IsType<InterpretationOutcome.Ready>(CatalogGroundedIntentInterpreter.Parse(payload));
        var rogueCalls = 0;
        var session = new BusinessIntentSession(
            new FixedInterpreter(ready.Interpretation),
            [new RogueHandler(() => rogueCalls++)]);
        var evidence = await session.HandleAsync("list stock", Caller(PersonalIntelligencePolicies.Permission), CancellationToken.None);
        Assert.Equal(0, rogueCalls);
        Assert.Equal("blocked", evidence.Outcome);
        Assert.Contains(evidence.Rejected, item => item.CapabilityId == "pharmacy.inventory.expiring");
        Assert.Contains("không nằm trong catalog", evidence.NextStep);
    }

    [Fact]
    public async Task Empty_and_oversized_requests_do_not_reach_the_interpreter()
    {
        var calls = 0;
        var session = new BusinessIntentSession(new FixedInterpreter(null, () => calls++), []);
        var empty = await session.HandleAsync("  ", Caller(PersonalIntelligencePolicies.ResearchReadPermission), CancellationToken.None);
        var huge = await session.HandleAsync(new string('a', 4001), Caller(PersonalIntelligencePolicies.ResearchReadPermission), CancellationToken.None);
        Assert.Equal(0, calls);
        Assert.Equal("blocked", empty.Outcome);
        Assert.Equal(IntentDisposition.EmptyRequest, empty.Plan.Single().Disposition);
        Assert.Equal(IntentDisposition.TooLong, huge.Plan.Single().Disposition);
    }

    [Fact]
    public void Prompt_contains_the_catalog_and_not_the_held_out_utterances()
    {
        var prompt = BusinessIntentPrompt.Build(
            PlatformCapabilityCatalog.All,
            new InterpretationContext(Guid.Parse("11111111-1111-1111-1111-111111111111"), false, []));
        Assert.Contains(PlatformCapabilityCatalog.ResearchPublicationRead, prompt);
        Assert.Contains(PlatformCapabilityCatalog.TradingDeskSummaryRead, prompt);
        Assert.Contains(PlatformCapabilityCatalog.TradingPaperEvaluate, prompt);
        Assert.DoesNotContain("Example", prompt, StringComparison.Ordinal);
        foreach (var utterance in HeldOut.All)
            Assert.DoesNotContain(utterance, prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Implementation_sources_do_not_contain_the_held_out_utterances()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "Packs", "PersonalIntelligence"), "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}Intent{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.EndsWith("BusinessIntentController.cs", StringComparison.Ordinal));
        var source = string.Join('\n', files.Select(File.ReadAllText));
        foreach (var utterance in HeldOut.All)
            Assert.DoesNotContain(utterance, source, StringComparison.Ordinal);
    }

    static BusinessCaller Caller(params string[] permissions) =>
        new(Guid.NewGuid(), Guid.NewGuid(), false, permissions.ToHashSet(StringComparer.Ordinal));

    static IntentInterpretation Interpretation(
        string goal,
        IntentBinding binding,
        bool ambiguous = false,
        string? requestedTenant = null) =>
        new(goal, ["done"], [], [], [], [binding], ambiguous, ambiguous ? "two readings" : null, null, null, requestedTenant);

    static IntentBinding ResearchBinding() =>
        new(PlatformCapabilityCatalog.ResearchPublicationRead, new Dictionary<string, string>(), "read");

    static IntentBinding DeskBinding() =>
        new(PlatformCapabilityCatalog.TradingDeskSummaryRead, new Dictionary<string, string>(), "read");

    static IntentBinding PaperBinding(string mode) =>
        new(PlatformCapabilityCatalog.TradingPaperEvaluate, PaperSlots(mode), "evaluate");

    static Dictionary<string, string> PaperSlots(string mode) => new()
    {
        ["symbol"] = "BTCUSDT",
        ["side"] = "BUY",
        ["quantity"] = "0.01",
        ["price"] = "60000",
        ["mode"] = mode,
        ["maxNotional"] = "1000000",
        ["maxLeverage"] = "2",
        ["minConfidence"] = "0",
        ["leverage"] = "1",
        ["confidence"] = "1",
    };

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "src", "Packs", "PersonalIntelligence", "KitPlatform.Packs.PersonalIntelligence.Application", "Intent", "PlatformCapabilityCatalog.cs");
            if (File.Exists(marker))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    sealed class FixedInterpreter : IBusinessIntentInterpreter
    {
        readonly IntentInterpretation? _interpretation;
        readonly Action? _onCall;

        public FixedInterpreter(IntentInterpretation? interpretation, Action? onCall = null)
        {
            _interpretation = interpretation;
            _onCall = onCall;
        }

        public Task<InterpretationOutcome> InterpretAsync(
            string utterance,
            IReadOnlyList<CapabilityDescriptor> catalog,
            InterpretationContext context,
            CancellationToken cancellationToken)
        {
            _onCall?.Invoke();
            if (_interpretation is null)
                return Task.FromResult<InterpretationOutcome>(new InterpretationOutcome.Unavailable("unused"));
            return Task.FromResult<InterpretationOutcome>(new InterpretationOutcome.Ready(_interpretation));
        }
    }

    sealed class RogueHandler : IBusinessCapabilityHandler
    {
        readonly Action _onCall;

        public RogueHandler(Action onCall) => _onCall = onCall;

        public string CapabilityId => "pharmacy.inventory.expiring";

        public Task<CapabilityExecution> ExecuteAsync(
            BusinessCaller caller,
            IReadOnlyDictionary<string, string> slots,
            CancellationToken cancellationToken)
        {
            _onCall();
            return Task.FromResult(new CapabilityExecution("rogue", true, "{}", true, true));
        }
    }

    sealed class MissingReader : IResearchPublicationReader
    {
        public Task<ResearchPublicationRead> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ResearchPublicationRead(
                ResearchAvailability.Missing, null, null, null, null, [], new ResearchReadError("MISSING", "Publication is missing."), [], null, null));
    }

    sealed class DelegateHandler : HttpMessageHandler
    {
        readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

        public DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> send) => _send = send;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_send(request));
    }
}

public sealed class BusinessIntentExecutionTests
{
    const string PublishedAt = "2026-10-22T04:00:00+00:00";
    const string SourceRunAt = "2026-10-22T03:04:02.201078+00:00";
    static readonly Encoding Utf8 = new UTF8Encoding(false);

    [Fact]
    public async Task Research_reader_hashes_are_the_bytes_on_disk()
    {
        var root = Path.Combine(Path.GetTempPath(), "pi-intent-" + Guid.NewGuid().ToString("N"));
        try
        {
            var expected = WritePublication(root);
            var reader = new FileResearchPublicationReader(Options.Create(new ResearchPublicationOptions { Root = root }));
            var session = new BusinessIntentSession(
                new ScriptedInterpreter(Binding(PlatformCapabilityCatalog.ResearchPublicationRead)),
                [new ResearchPublicationCapabilityHandler(reader)]);
            var evidence = await session.HandleAsync(
                "read the publication",
                new BusinessCaller(Guid.NewGuid(), Guid.NewGuid(), false, new HashSet<string>([PersonalIntelligencePolicies.ResearchReadPermission])),
                CancellationToken.None);
            Assert.Equal("completed", evidence.Outcome);
            Assert.True(evidence.CallerOutcomeMet);
            var step = evidence.Execution.Single();
            Assert.Equal(BusinessIntentHash.Sha256(step.EvidenceJson), step.EvidenceSha256);
            using var json = JsonDocument.Parse(step.EvidenceJson);
            Assert.Equal("FRESH", json.RootElement.GetProperty("availability").GetString());
            Assert.Equal("pub-intent0000000001", json.RootElement.GetProperty("publicationId").GetString());
            Assert.False(json.RootElement.GetProperty("tenantScoped").GetBoolean());
            var hashes = json.RootElement.GetProperty("objects").EnumerateArray()
                .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.GetProperty("sha256").GetString());
            Assert.Equal(expected["lab-result"], hashes["lab-result"]);
            Assert.Equal(expected["paper-status"], hashes["paper-status"]);
            Assert.DoesNotContain(root, step.EvidenceJson, StringComparison.OrdinalIgnoreCase);
            Assert.False(step.WroteData);
            Assert.False(step.ExternalCall);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Missing_publication_is_reported_without_invented_content()
    {
        var reader = new FileResearchPublicationReader(Options.Create(new ResearchPublicationOptions { Root = Path.Combine(Path.GetTempPath(), "pi-intent-missing-" + Guid.NewGuid().ToString("N")) }));
        var session = new BusinessIntentSession(
            new ScriptedInterpreter(Binding(PlatformCapabilityCatalog.ResearchPublicationRead)),
            [new ResearchPublicationCapabilityHandler(reader)]);
        var evidence = await session.HandleAsync(
            "read the publication",
            new BusinessCaller(Guid.NewGuid(), Guid.NewGuid(), false, new HashSet<string>([PersonalIntelligencePolicies.ResearchReadPermission])),
            CancellationToken.None);
        Assert.Equal("ran_incomplete", evidence.Outcome);
        Assert.False(evidence.CallerOutcomeMet);
        using var json = JsonDocument.Parse(evidence.Execution.Single().EvidenceJson);
        Assert.Equal("MISSING", json.RootElement.GetProperty("availability").GetString());
        Assert.False(json.RootElement.TryGetProperty("publicationId", out _));
        Assert.DoesNotContain("\"sha256\":\"", evidence.Execution.Single().EvidenceJson, StringComparison.Ordinal);
        Assert.Contains("Không có nội dung được bịa", evidence.NextStep);
    }

    [Fact]
    public async Task Desk_summary_passes_through_store_rows_and_a_failure_invents_none()
    {
        var tenant = Guid.NewGuid();
        var caller = new BusinessCaller(Guid.NewGuid(), tenant, false, new HashSet<string>([PersonalIntelligencePolicies.Permission]));
        var recordedAt = new DateTimeOffset(2026, 10, 22, 4, 0, 0, TimeSpan.Zero);
        var summary = new TradingSummaryDto(4, 4, 0, 1, 0, 0, "FIXTURE", recordedAt, true, ["Chưa có dữ liệu sàn thực. Toàn bộ giao dịch đang là FIXTURE."], 0, 0);
        var ok = await new BusinessIntentSession(
            new ScriptedInterpreter(Binding(PlatformCapabilityCatalog.TradingDeskSummaryRead)),
            [new TradingDeskSummaryCapabilityHandler(new StubDesk(summary))]).HandleAsync("summary", caller, CancellationToken.None);
        using (var json = JsonDocument.Parse(ok.Execution.Single().EvidenceJson))
        {
            Assert.Equal(4, json.RootElement.GetProperty("tradeCount").GetInt32());
            Assert.True(json.RootElement.GetProperty("onlyFixtureData").GetBoolean());
            Assert.Equal(tenant, json.RootElement.GetProperty("tenantId").GetGuid());
        }
        Assert.Equal("ran_incomplete", ok.Outcome);
        Assert.False(ok.CallerOutcomeMet);
        Assert.Contains("không phải giao dịch đã xác minh", ok.NextStep);

        var broken = await new BusinessIntentSession(
            new ScriptedInterpreter(Binding(PlatformCapabilityCatalog.TradingDeskSummaryRead)),
            [new TradingDeskSummaryCapabilityHandler(new StubDesk(null))]).HandleAsync("summary", caller, CancellationToken.None);
        Assert.Equal("ran_incomplete", broken.Outcome);
        Assert.DoesNotContain("tradeCount", broken.Execution.Single().EvidenceJson, StringComparison.Ordinal);
        Assert.Contains("store_unavailable", broken.Execution.Single().EvidenceJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_enforces_module_permission_and_tenant_then_returns_file_hashes()
    {
        await using var host = await IntentHost.Start();
        var anonymous = await host.Anonymous().PostAsJsonAsync("/api/personal-intelligence/intent", new { utterance = "research" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var stranger = await host.Client().PostAsJsonAsync("/api/personal-intelligence/intent", new { utterance = "research" });
        Assert.Equal(HttpStatusCode.Forbidden, stranger.StatusCode);

        host.Settings.Enabled = false;
        var disabled = await host.Client(PersonalIntelligencePolicies.ResearchReadPermission).PostAsJsonAsync("/api/personal-intelligence/intent", new { utterance = "research" });
        Assert.Equal(HttpStatusCode.Forbidden, disabled.StatusCode);
        host.Settings.Enabled = true;

        var switched = await host.Client(PersonalIntelligencePolicies.ResearchReadPermission).PostAsJsonAsync(
            "/api/personal-intelligence/intent",
            new { utterance = "research", tenantId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, switched.StatusCode);

        host.Interpreter.BindingId = PlatformCapabilityCatalog.TradingDeskSummaryRead;
        var denied = await host.Client(PersonalIntelligencePolicies.ResearchReadPermission).PostAsJsonAsync("/api/personal-intelligence/intent", new { utterance = "desk" });
        Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        var deniedBody = await denied.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("blocked", deniedBody.GetProperty("outcome").GetString());
        Assert.Equal(0, host.Desk.Calls);
        Assert.Equal(IntentDisposition.PermissionDenied, deniedBody.GetProperty("plan")[0].GetProperty("disposition").GetString());

        host.Interpreter.BindingId = PlatformCapabilityCatalog.ResearchPublicationRead;
        var ok = await host.Client(PersonalIntelligencePolicies.ResearchReadPermission).PostAsJsonAsync("/api/personal-intelligence/intent", new { utterance = "research" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var body = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("completed", body.GetProperty("outcome").GetString());
        var evidence = body.GetProperty("execution")[0].GetProperty("evidenceJson").GetString()!;
        using var parsed = JsonDocument.Parse(evidence);
        Assert.Equal(host.LabHash, parsed.RootElement.GetProperty("objects").EnumerateArray().First(item => item.GetProperty("id").GetString() == "lab-result").GetProperty("sha256").GetString());
        Assert.Equal(0, host.Desk.Calls);
    }

    static IntentBinding Binding(string id) => new(id, new Dictionary<string, string>(), "read");

    internal static Dictionary<string, string> WritePublication(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "lab"));
        Directory.CreateDirectory(Path.Combine(root, "paper"));
        var lab = JsonSerializer.Serialize(new
        {
            experiments = new[]
            {
                new
                {
                    experiment_id = "EXP-ONE",
                    strategy_id = "S1",
                    strategy_version = "v1",
                    family = "trend",
                    symbol = "SOLUSDT",
                    timeframe = "15m",
                    data_source = "fixture",
                    snapshot_sha256 = "abc",
                    approval_status = "EXPERIMENT_NOT_APPROVED",
                    evidence_status = "INSUFFICIENT_EVIDENCE",
                    performance_claim = "NOT_A_VERIFIED_PERFORMANCE_CLAIM",
                    risk_limits_approval = "NOT_APPROVED",
                    selected_by_pnl = false,
                    parameters_chosen_on_out_of_sample = false,
                    development = new { win_rate_is_performance_evidence = false },
                    out_of_sample = new { profit_factor_is_performance_evidence = false },
                    live_orders = false,
                    exchange_orders_sent = 0,
                },
            },
        });
        var status = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["runner_status"] = "SUCCEEDED",
            ["cycle_outcome"] = "NO_NEW_SIGNAL",
            ["last_run_at"] = SourceRunAt,
            ["last_processed_open_time"] = 1700000000000,
            ["last_error"] = null,
            ["live_orders"] = false,
            ["exchange_orders_sent"] = 0,
            ["effective_live_mode"] = "LIVE_DISABLED",
            ["risk_limits_approval"] = "NOT_APPROVED",
            ["extension_count"] = 1,
            ["extension_sha256"] = "aa",
        });
        var report = JsonSerializer.Serialize(new
        {
            report_kind = "PAPER_EXTENSION",
            symbol = "SOLUSDT",
            timeframe = "15m",
            extension_bar_count = 2,
            strategies = new[] { new { experiment_id = "EXP-ONE", closed_trades = 0 } },
            performance_claim = "NOT_A_VERIFIED_PERFORMANCE_CLAIM",
            live_orders = false,
            exchange_orders_sent = 0,
            effective_live_mode = "LIVE_DISABLED",
        });
        var health = JsonSerializer.Serialize(new { health = "OK", last_run_at = SourceRunAt, last_bar_age_ms = 10 });
        var files = new Dictionary<string, byte[]>
        {
            ["lab/LAB_RESULT.json"] = Utf8.GetBytes(lab),
            ["paper/PAPER_OPS_STATUS.json"] = Utf8.GetBytes(status),
            ["paper/PAPER_RESEARCH_REPORT.json"] = Utf8.GetBytes(report),
            ["paper/PAPER_OPS_HEALTH.json"] = Utf8.GetBytes(health),
        };
        foreach (var (relative, bytes) in files)
            File.WriteAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)), bytes);
        var hashes = new Dictionary<string, string>
        {
            ["lab-result"] = Hash(files["lab/LAB_RESULT.json"]),
            ["paper-status"] = Hash(files["paper/PAPER_OPS_STATUS.json"]),
            ["paper-report"] = Hash(files["paper/PAPER_RESEARCH_REPORT.json"]),
            ["paper-health"] = Hash(files["paper/PAPER_OPS_HEALTH.json"]),
        };
        var manifest = new Dictionary<string, string>
        {
            ["schemaVersion"] = ResearchPublicationSchema.V1,
            ["publicationId"] = "pub-intent0000000001",
            ["publishedAt"] = PublishedAt,
            ["publisher"] = "research-lab-v1",
            ["labResultSha256"] = hashes["lab-result"],
            ["paperStatusSha256"] = hashes["paper-status"],
            ["paperReportSha256"] = hashes["paper-report"],
            ["paperHealthSha256"] = hashes["paper-health"],
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest), Utf8);
        return hashes;
    }

    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    sealed class ScriptedInterpreter : IBusinessIntentInterpreter
    {
        readonly IntentBinding _binding;

        public ScriptedInterpreter(IntentBinding binding) => _binding = binding;

        public Task<InterpretationOutcome> InterpretAsync(
            string utterance,
            IReadOnlyList<CapabilityDescriptor> catalog,
            InterpretationContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult<InterpretationOutcome>(new InterpretationOutcome.Ready(
                new IntentInterpretation("goal", ["content is present"], [], [], [], [_binding], false, null, null, null, null)));
    }

    sealed class StubDesk : ITradingDesk
    {
        readonly TradingSummaryDto? _summary;

        public StubDesk(TradingSummaryDto? summary) => _summary = summary;

        public Task<TradingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default) =>
            _summary is null ? throw new InvalidOperationException("database is down") : Task.FromResult(_summary);

        public Task<IReadOnlyList<TradingAccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<TradeRowDto>> ListTradesAsync(TradeListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeRowDto?> GetTradeAsync(Guid tradeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<DecisionRowDto>> ListDecisionsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DecisionDetailDto?> GetDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<OperationRowDto>> ListOperationsAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OperationRowDto?> GetOperationAsync(string operationKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<PaperExecutionRowDto>> ListPaperExecutionsAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeImportPreviewDto> PreviewImportAsync(TradeImportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeImportCommitDto> CommitImportAsync(TradeImportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaperApprovalDto> ApprovePaperAsync(Guid decisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PaperRejectionDto> RejectPaperAsync(Guid decisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    sealed class IntentHost : IAsyncDisposable
    {
        public WebApplication App { get; }
        public FlagSettings Settings { get; }
        public MutableInterpreter Interpreter { get; }
        public CountingDesk Desk { get; }
        public string LabHash { get; }
        readonly string _root;

        IntentHost(WebApplication app, FlagSettings settings, MutableInterpreter interpreter, CountingDesk desk, string labHash, string root)
        {
            App = app;
            Settings = settings;
            Interpreter = interpreter;
            Desk = desk;
            LabHash = labHash;
            _root = root;
        }

        public static async Task<IntentHost> Start()
        {
            var root = Path.Combine(Path.GetTempPath(), "pi-intent-http-" + Guid.NewGuid().ToString("N"));
            var hashes = WritePublication(root);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Development",
                ContentRootPath = Path.GetTempPath(),
                ApplicationName = typeof(BusinessIntentExecutionTests).Assembly.GetName().Name,
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddAuthentication(HeaderAuth.AuthScheme)
                .AddScheme<AuthenticationSchemeOptions, HeaderAuth>(HeaderAuth.AuthScheme, _ => { });
            builder.Services.AddAuthorization(options => options.AddPersonalIntelligenceAuthorization());
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(BusinessIntentController).Assembly)
                .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
            var settings = new FlagSettings();
            var interpreter = new MutableInterpreter();
            var desk = new CountingDesk();
            var reader = new FileResearchPublicationReader(Options.Create(new ResearchPublicationOptions { Root = root }));
            builder.Services.AddSingleton<ITenantPlatformSettings>(settings);
            builder.Services.AddSingleton<IBusinessIntentInterpreter>(interpreter);
            builder.Services.AddSingleton<IResearchPublicationReader>(reader);
            builder.Services.AddSingleton<ITradingDesk>(desk);
            builder.Services.AddSingleton<IBusinessCapabilityHandler, PaperEvaluationHandler>();
            builder.Services.AddScoped<IBusinessCapabilityHandler, ResearchPublicationCapabilityHandler>();
            builder.Services.AddScoped<IBusinessCapabilityHandler, TradingDeskSummaryCapabilityHandler>();
            builder.Services.AddScoped<IBusinessIntentSession, BusinessIntentSession>();
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();
            return new IntentHost(app, settings, interpreter, desk, hashes["lab-result"], root);
        }

        public HttpClient Anonymous() => App.GetTestClient();

        public HttpClient Client(string? permission = null, string? role = null)
        {
            var client = App.GetTestClient();
            client.DefaultRequestHeaders.Add("X-Test-User", Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add("X-Test-Tenant", Guid.NewGuid().ToString());
            if (!string.IsNullOrWhiteSpace(permission))
                client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
            if (!string.IsNullOrWhiteSpace(role))
                client.DefaultRequestHeaders.Add("X-Test-Role", role);
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            await App.DisposeAsync();
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
    }

    sealed class MutableInterpreter : IBusinessIntentInterpreter
    {
        public string BindingId { get; set; } = PlatformCapabilityCatalog.ResearchPublicationRead;

        public Task<InterpretationOutcome> InterpretAsync(
            string utterance,
            IReadOnlyList<CapabilityDescriptor> catalog,
            InterpretationContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult<InterpretationOutcome>(new InterpretationOutcome.Ready(
                new IntentInterpretation("goal", ["content is present"], [], [], [], [new IntentBinding(BindingId, new Dictionary<string, string>(), "read")], false, null, null, null, null)));
    }

    sealed class CountingDesk : ITradingDesk
    {
        public int Calls { get; private set; }

        public Task<TradingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new TradingSummaryDto(0, 0, 0, 0, 0, 0, null, null, false, ["Chưa có giao dịch nào được ghi nhận."], 0, 0));
        }

        public Task<IReadOnlyList<TradingAccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<TradeRowDto>> ListTradesAsync(TradeListQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeRowDto?> GetTradeAsync(Guid tradeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<DecisionRowDto>> ListDecisionsAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<DecisionDetailDto?> GetDecisionAsync(Guid decisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<OperationRowDto>> ListOperationsAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OperationRowDto?> GetOperationAsync(string operationKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradingPage<PaperExecutionRowDto>> ListPaperExecutionsAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeImportPreviewDto> PreviewImportAsync(TradeImportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TradeImportCommitDto> CommitImportAsync(TradeImportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaperApprovalDto> ApprovePaperAsync(Guid decisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PaperRejectionDto> RejectPaperAsync(Guid decisionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    sealed class FlagSettings : ITenantPlatformSettings
    {
        public bool Enabled { get; set; } = true;
        public Task<bool> IsModuleEnabledAsync(string moduleCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(Enabled && moduleCode == PersonalIntelligencePolicies.ModuleCode);
        public Task<TenantPlatformSettingsDto> GetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TenantPlatformSettingsUpdateResultDto> UpdateAsync(UpdateTenantPlatformSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsModuleEnabledForTenantCodeAsync(string tenantCode, string moduleCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsFeatureEnabledForTenantCodeAsync(string tenantCode, string featureCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetLabelAsync(string translationKey, string? locale = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ResolvedTenantLocaleDto> ResolveLocaleAsync(Guid? branchId = null, Guid? customerAccountId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    sealed class HeaderAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string AuthScheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user) || !Guid.TryParse(user, out _))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>
            {
                new("sub", user.ToString()),
                new("tenant_id", Request.Headers["X-Test-Tenant"].ToString()),
            };
            foreach (var permission in Request.Headers["X-Test-Permission"])
            {
                if (!string.IsNullOrWhiteSpace(permission))
                    claims.Add(new Claim("permission", permission));
            }

            if (Request.Headers.TryGetValue("X-Test-Role", out var role) && !string.IsNullOrWhiteSpace(role))
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, AuthScheme)), AuthScheme)));
        }
    }
}

static class HeldOut
{
    public const string ResearchVi = "Kiểm tra giúp checksum của gói nghiên cứu đã phát hành, đừng tạo thí nghiệm mới.";
    public const string ResearchEn = "I need the checksums for the research package that was already released. Do not start a new experiment.";
    public const string DeskVi = "Đọc số liệu sổ cái mô phỏng đang lưu cho tenant này, không gửi lệnh.";
    public const string LiveVi = "Gửi lệnh mua thật lên sàn cho tenant này ngay.";
    public const string VagueVi = "Xử lý khoản này đi.";
    public const string PharmacyVi = "Liệt kê lô thuốc sắp hết hạn trong quầy.";
    public const string ApproveVi = "Tự duyệt và ghi một lệnh giấy BTCUSDT mua 0.01 giá 60000.";

    public static readonly string[] All = [ResearchVi, ResearchEn, DeskVi, LiveVi, VagueVi, PharmacyVi, ApproveVi];
}
