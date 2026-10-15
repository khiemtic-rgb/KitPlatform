using KitPlatform.Application.Core.Engines;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Infrastructure.Core.Engines;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Packs.Pharmacy.Knowledge;
using Xunit;

namespace KitPlatform.Application.Tests;

public class DrugKnowledgeRulesTests
{
    [Fact]
    public void TryAnswer_paracetamol_before_meal_returns_high_confidence()
    {
        var ok = DrugKnowledgeRules.TryAnswer(
            "paracetamol uống trước ăn được không?",
            "Paracetamol 500mg",
            "Paracetamol",
            out var answer,
            out var confidence);

        Assert.True(ok);
        Assert.Equal("high", confidence);
        Assert.Contains("Paracetamol", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void TryAnswer_unknown_drug_returns_false()
    {
        var ok = DrugKnowledgeRules.TryAnswer(
            "uống trước ăn?",
            "Thuốc XYZ",
            null,
            out _,
            out _);

        Assert.False(ok);
    }
}

public class PricingEngineTests
{
    [Fact]
    public void PriceOrder_prices_an_undiscounted_line()
    {
        var lines = new List<(CreateSaleLineRequest Request, decimal UnitPrice, decimal ConversionFactor)>
        {
            (new CreateSaleLineRequest(Guid.NewGuid(), Guid.NewGuid(), 2m, null, null), 10000m, 1m),
        };

        var priced = SalesPricing.PriceOrder(lines, null);

        Assert.Equal(20000m, priced.SubtotalGross);
        Assert.Equal(20000m, priced.TotalAmount);
    }

    [Fact]
    public void ValidateDiscounts_rejects_when_policy_disallows()
    {
        var lines = new List<(CreateSaleLineRequest Request, decimal UnitPrice, decimal ConversionFactor)>
        {
            (new CreateSaleLineRequest(Guid.NewGuid(), Guid.NewGuid(), 1m, SalesDiscountTypes.Percent, 10m), 10000m, 1m),
        };
        var pricing = SalesPricing.PriceOrder(lines, null);
        var policy = new SalesDiscountPolicy(CanApply: false, Unlimited: false, MaxPercent: 0m);

        Assert.Throws<InvalidOperationException>(() => SalesPricing.ValidateDiscounts(pricing, policy));
    }
}

public class InventoryEngineFefoTests
{
    [Fact]
    public void AllocateFromBatches_uses_earliest_expiry_first()
    {
        var lotA = Guid.NewGuid();
        var lotB = Guid.NewGuid();
        var batches = new List<BatchAvailabilityDto>
        {
            new(lotA, "LOT-A", new DateOnly(2026, 6, 1), 5m, 900m),
            new(lotB, "LOT-B", new DateOnly(2026, 12, 1), 5m, 1000m),
        };

        var engine = new BatchResolver(null!, null!);
        var allocations = engine.AllocateFromBatches(batches, 3m);

        Assert.Single(allocations);
        Assert.Equal(lotA, allocations[0].BatchId);
    }

    [Fact]
    public void AllocateFromBatches_throws_when_insufficient_stock()
    {
        var batches = new List<BatchAvailabilityDto>
        {
            new(Guid.NewGuid(), "LOT-A", new DateOnly(2026, 6, 1), 1m, 900m),
        };

        var engine = new BatchResolver(null!, null!);
        Assert.Throws<InvalidOperationException>(() => engine.AllocateFromBatches(batches, 2m));
    }
}

public class AiOrchestratorTests
{
    private sealed class StubAiConversations : IAiConversationRepository
    {
        public Task<Guid> GetOrCreateConversationAsync(
            Guid tenantId,
            Guid customerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid());

        public Task<Guid?> GetAgentIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(Guid.NewGuid());

        public Task PersistExchangeAsync(
            Guid tenantId,
            Guid conversationId,
            Guid? agentId,
            AiHealthAskRequest request,
            AiHealthAskResponse response,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class StubCopilot : ICustomerAiHealthService
    {
        public Task<AiHealthAskResponse> AskAsync(
            Guid tenantId,
            Guid customerId,
            AiHealthAskRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AiHealthAskResponse("ok", "high", false, "disclaimer"));
    }

    [Fact]
    public async Task AskAsync_rejects_short_question_BR_AI_001()
    {
        var orchestrator = new AiOrchestrator(new StubCopilot(), new StubAiConversations());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            orchestrator.AskAsync(Guid.NewGuid(), Guid.NewGuid(), new AiHealthAskRequest("ab"), default));
    }

    [Fact]
    public async Task AskAsync_truncates_question_over_500_chars()
    {
        AiHealthAskRequest? captured = null;
        var copilot = new CapturingCopilot(r => captured = r);
        var orchestrator = new AiOrchestrator(copilot, new StubAiConversations());
        var longQuestion = new string('x', 600);

        await orchestrator.AskAsync(Guid.NewGuid(), Guid.NewGuid(), new AiHealthAskRequest(longQuestion), default);

        Assert.NotNull(captured);
        Assert.Equal(500, captured!.Question.Length);
    }

    private sealed class CapturingCopilot : ICustomerAiHealthService
    {
        private readonly Action<AiHealthAskRequest> _capture;

        public CapturingCopilot(Action<AiHealthAskRequest> capture) => _capture = capture;

        public Task<AiHealthAskResponse> AskAsync(
            Guid tenantId,
            Guid customerId,
            AiHealthAskRequest request,
            CancellationToken cancellationToken = default)
        {
            _capture(request);
            return Task.FromResult(new AiHealthAskResponse("ok", "high", false, "d"));
        }
    }
}
