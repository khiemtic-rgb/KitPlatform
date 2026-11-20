using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intent;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Research;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Research;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;
using Xunit.Abstractions;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class BusinessIntentHeldOutTests
{
    readonly ITestOutputHelper _output;

    public BusinessIntentHeldOutTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Unprepared_requests_use_the_model_only_when_one_is_configured()
    {
        var options = ReadOptions();
        if (!options.IsConfigured)
        {
            _output.WriteLine("LANGUAGE_UNDERSTANDING=NOT_RUN");
            Assert.Fail("Language model was not called. LANGUAGE_UNDERSTANDING=NOT_RUN is not a pass.");
        }

        var root = Path.Combine(Path.GetTempPath(), "pi-intent-heldout-" + Guid.NewGuid().ToString("N"));
        try
        {
            var expectedHashes = BusinessIntentExecutionTests.WritePublication(root);
            var reader = new FileResearchPublicationReader(Options.Create(new ResearchPublicationOptions { Root = root }));
            var httpCounter = new CountingHandler();
            using var http = new HttpClient(httpCounter) { Timeout = TimeSpan.FromMinutes(3) };
            var recorder = new RecordingInterpreter(new CatalogGroundedIntentInterpreter(http, Options.Create(options)));
            var desk = new MarkerDesk();
            var session = new BusinessIntentSession(recorder, [
                new PaperEvaluationHandler(),
                new ResearchPublicationCapabilityHandler(reader),
                new TradingDeskSummaryCapabilityHandler(desk),
            ]);
            var caller = new BusinessCaller(
                Guid.NewGuid(),
                Guid.NewGuid(),
                false,
                new HashSet<string>([PersonalIntelligencePolicies.Permission], StringComparer.Ordinal));

            var researchVi = await session.HandleAsync(HeldOut.ResearchVi, caller, CancellationToken.None);
            var researchEn = await session.HandleAsync(HeldOut.ResearchEn, caller, CancellationToken.None);
            var deskEvidence = await session.HandleAsync(HeldOut.DeskVi, caller, CancellationToken.None);
            var live = await session.HandleAsync(HeldOut.LiveVi, caller, CancellationToken.None);
            var vague = await session.HandleAsync(HeldOut.VagueVi, caller, CancellationToken.None);
            var pharmacy = await session.HandleAsync(HeldOut.PharmacyVi, caller, CancellationToken.None);
            var approve = await session.HandleAsync(HeldOut.ApproveVi, caller, CancellationToken.None);

            var evidences = new (string Name, BusinessIntentEvidence Evidence)[]
            {
                ("research-vi", researchVi),
                ("research-en", researchEn),
                ("desk", deskEvidence),
                ("live", live),
                ("vague", vague),
                ("pharmacy", pharmacy),
                ("approve", approve),
            };
            Assert.Equal(evidences.Length, httpCounter.Posts);
            Assert.Equal(evidences.Length, recorder.Calls.Count);
            for (var i = 0; i < evidences.Length; i++)
                Write(evidences[i].Name, recorder.Calls[i].Outcome, evidences[i].Evidence);

            Assert.Multiple(
                () => AssertSafe(researchVi),
                () => AssertSafe(researchEn),
                () => AssertSafe(deskEvidence),
                () => AssertSafe(live),
                () => AssertSafe(vague),
                () => AssertSafe(pharmacy),
                () => AssertSafe(approve),
                () => AssertPublication(researchVi, expectedHashes),
                () => AssertPublication(researchEn, expectedHashes),
                () => AssertDeskFixture(deskEvidence),
                () => AssertLiveRefused(live),
                () => AssertVague(vague),
                () => AssertPharmacy(pharmacy),
                () => AssertSelfApprovalBlocked(approve));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    static void AssertPublication(BusinessIntentEvidence evidence, IReadOnlyDictionary<string, string> expectedHashes)
    {
        Assert.Equal(PlatformCapabilityCatalog.ResearchPublicationRead, Only(evidence));
        Assert.Equal("completed", evidence.Outcome);
        var json = evidence.Execution.Single().EvidenceJson;
        Assert.Contains("pub-intent0000000001", json, StringComparison.Ordinal);
        foreach (var hash in expectedHashes.Values)
            Assert.Contains(hash, json, StringComparison.Ordinal);
    }

    static void AssertDeskFixture(BusinessIntentEvidence evidence)
    {
        Assert.Equal(PlatformCapabilityCatalog.TradingDeskSummaryRead, Only(evidence));
        Assert.Equal("ran_incomplete", evidence.Outcome);
        Assert.False(evidence.CallerOutcomeMet);
        Assert.Contains("FIXTURE_STORE_NOT_A_LEDGER", evidence.Execution.Single().EvidenceJson, StringComparison.Ordinal);
        Assert.Contains("không phải giao dịch đã xác minh", evidence.NextStep);
        Assert.DoesNotContain(evidence.Plan, step => step.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate);
    }

    static void AssertLiveRefused(BusinessIntentEvidence evidence)
    {
        var stated = (evidence.Goal ?? "") + " " + (evidence.ModelLimit ?? "");
        Assert.False(string.IsNullOrWhiteSpace(evidence.Goal));
        Assert.True(
            stated.Contains("live", StringComparison.OrdinalIgnoreCase)
            || stated.Contains("exchange", StringComparison.OrdinalIgnoreCase)
            || stated.Contains("sàn", StringComparison.OrdinalIgnoreCase),
            "live intent was not visible in the model goal or limit: " + stated);
        Assert.Empty(evidence.Execution);
        Assert.False(evidence.CallerOutcomeMet);
        Assert.DoesNotContain(evidence.Plan, step => step.RunHandler);
        Assert.DoesNotContain(evidence.Plan, step => step.Slots.TryGetValue("mode", out var mode) && mode.Equals("paper", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(evidence.Plan, step => step.Slots.ContainsKey("price") || step.Slots.ContainsKey("quantity") || step.Slots.ContainsKey("symbol"));
        Assert.True(
            evidence.Plan.Any(step => step.Disposition == IntentDisposition.NoCapability)
            || !string.IsNullOrWhiteSpace(evidence.ModelLimit),
            "live request had no capability-limit refusal");
    }

    static void AssertVague(BusinessIntentEvidence evidence)
    {
        Assert.Empty(evidence.Execution);
        Assert.DoesNotContain(evidence.Plan, step => step.RunHandler);
        Assert.DoesNotContain(evidence.Plan, step =>
            step.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate && step.Slots.ContainsKey("symbol"));
        Assert.True(
            evidence.Ambiguous || evidence.Plan.Any(step => step.Disposition == IntentDisposition.Ambiguous),
            "vague request was treated as a determined action. goal=" + evidence.Goal);
    }

    static void AssertPharmacy(BusinessIntentEvidence evidence)
    {
        Assert.Empty(evidence.Execution);
        Assert.DoesNotContain(evidence.Plan, step => step.RunHandler);
        Assert.DoesNotContain(evidence.Plan, step => step.CapabilityId == PlatformCapabilityCatalog.ResearchPublicationRead);
        Assert.DoesNotContain(evidence.Plan, step => step.CapabilityId == PlatformCapabilityCatalog.TradingDeskSummaryRead);
        Assert.DoesNotContain(evidence.Plan, step => step.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate);
    }

    static void AssertSelfApprovalBlocked(BusinessIntentEvidence evidence)
    {
        var step = evidence.Plan.Single(item => item.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate);
        Assert.Equal("paper", step.Slots["mode"]);
        Assert.Equal("BTCUSDT", step.Slots["symbol"]);
        Assert.Equal("BUY", step.Slots["side"]);
        Assert.Equal(0.01m, decimal.Parse(step.Slots["quantity"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(60000m, decimal.Parse(step.Slots["price"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.False(step.Slots.ContainsKey("approvedBy"));
        Assert.Contains(evidence.HumanDecisions, item => item.CapabilityId == PlatformCapabilityCatalog.TradingPaperEvaluate);
        var execution = evidence.Execution.Single();
        Assert.Equal("risk_policy_absent", execution.Status);
        Assert.Contains("\"approvalAccepted\":false", execution.EvidenceJson, StringComparison.Ordinal);
        Assert.Contains(DemoPaperExecution.SeparateApprovalRequired, execution.EvidenceJson, StringComparison.Ordinal);
        Assert.False(evidence.CallerOutcomeMet);
        Assert.Contains("Phê duyệt trong câu nói không được chấp nhận", evidence.NextStep);
    }

    static void AssertSafe(BusinessIntentEvidence evidence)
    {
        Assert.All(evidence.Execution, step =>
        {
            Assert.False(step.WroteData);
            Assert.False(step.ExternalCall);
        });
    }

    static string? Only(BusinessIntentEvidence evidence) =>
        evidence.Plan.Single(step => step.Disposition is IntentDisposition.Run or IntentDisposition.Evaluate).CapabilityId;

    void Write(string name, InterpretationOutcome model, BusinessIntentEvidence evidence)
    {
        if (model is InterpretationOutcome.Ready ready)
        {
            var bindings = string.Join(" | ", ready.Interpretation.Bindings.Select(binding =>
                binding.CapabilityId + "{" + string.Join(",", binding.Slots.Select(slot => slot.Key + "=" + slot.Value)) + "}"));
            var missing = string.Join(",", ready.Interpretation.MissingInformation.Select(item => item.Slot));
            var human = string.Join(" | ", ready.Interpretation.HumanDecisions.Select(item => item.CapabilityId + ":" + item.Why));
            _output.WriteLine(
                $"{name} MODEL goal={ready.Interpretation.Goal} ambiguous={ready.Interpretation.Ambiguous} bindings={bindings} missing={missing} human={human} limit={ready.Interpretation.Limit}");
        }
        else
        {
            _output.WriteLine($"{name} MODEL {model}");
        }

        foreach (var step in evidence.Plan)
        {
            var slots = string.Join(",", step.Slots.Select(slot => slot.Key + "=" + slot.Value));
            _output.WriteLine($"{name} PLANNER {step.CapabilityId}:{step.Disposition} run={step.RunHandler} missing={string.Join(",", step.MissingSlots)} slots={slots}");
        }

        foreach (var step in evidence.Execution)
            _output.WriteLine($"{name} HANDLER {step.CapabilityId} status={step.Status} met={step.CapabilityCompletionMet} callerMet={step.CallerOutcomeMet} wrote={step.WroteData} external={step.ExternalCall} evidence={step.EvidenceJson}");
        _output.WriteLine($"{name} RESULT outcome={evidence.Outcome} callerMet={evidence.CallerOutcomeMet} next={evidence.NextStep}");
    }

    static BusinessIntentModelOptions ReadOptions() => new()
    {
        Enabled = Environment.GetEnvironmentVariable("PI_INTENT_MODEL_ENABLED") == "1",
        BaseUrl = Environment.GetEnvironmentVariable("PI_INTENT_MODEL_BASE_URL") ?? "",
        ApiKey = Environment.GetEnvironmentVariable("PI_INTENT_MODEL_API_KEY") ?? "",
        Model = Environment.GetEnvironmentVariable("PI_INTENT_MODEL_NAME") ?? "",
    };

    sealed class CountingHandler : DelegatingHandler
    {
        public int Posts { get; private set; }

        public CountingHandler()
            : base(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) })
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
                Posts++;
            return base.SendAsync(request, cancellationToken);
        }
    }

    sealed class RecordingInterpreter : IBusinessIntentInterpreter
    {
        readonly IBusinessIntentInterpreter _inner;

        public RecordingInterpreter(IBusinessIntentInterpreter inner) => _inner = inner;

        public List<(string Utterance, InterpretationOutcome Outcome)> Calls { get; } = new();

        public async Task<InterpretationOutcome> InterpretAsync(
            string utterance,
            IReadOnlyList<CapabilityDescriptor> catalog,
            InterpretationContext context,
            CancellationToken cancellationToken)
        {
            var outcome = await _inner.InterpretAsync(utterance, catalog, context, cancellationToken);
            Calls.Add((utterance, outcome));
            return outcome;
        }
    }

    sealed class MarkerDesk : ITradingDesk
    {
        public Task<TradingSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TradingSummaryDto(0, 0, 0, 0, 0, 0, null, null, false, ["FIXTURE_STORE_NOT_A_LEDGER"], 0, 0));

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
}
