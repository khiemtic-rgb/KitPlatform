using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intelligence;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using KitPlatform.Packs.PersonalIntelligence;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

internal sealed class PaperExecutionStore : IPaperExecutionStore
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public PaperExecutionStore(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PaperExecutionResult> RecordAsync(
        Guid personId,
        Guid tradingAccountId,
        TradeProposal proposal,
        RiskLimits limits,
        ExecutionApproval? approval,
        CancellationToken cancellationToken = default)
    {
        TenantScope.RequireCurrent(_tenant.TenantId);
        if (proposal.Mode == ExecutionMode.Live)
            throw new InvalidOperationException("Live execution is not stored.");

        var verdict = RiskEngine.Evaluate(proposal, limits);
        var result = DemoPaperExecution.Execute(proposal, verdict, approval);
        var plan = PaperPersistencePlanBuilder.From(result);
        if (plan.ExecutionMode is not ("PAPER" or null))
            throw new InvalidOperationException("Live execution is not stored.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        try
        {
            var correlationId = approval?.DecisionId is Guid decisionId && decisionId != Guid.Empty
                ? decisionId
                : Guid.NewGuid();
            if (approval is not null && approval.DecisionId != Guid.Empty)
            {
                await conn.ExecuteAsync(
                    IntelligenceSql.InsertAction,
                    new
                    {
                        ActionId = Guid.NewGuid(),
                        TenantId = _tenant.TenantId,
                        PersonId = personId,
                        DecisionId = approval.DecisionId,
                        Intended = $"{proposal.Side} {proposal.Quantity} {proposal.Symbol}",
                        Actual = result.Executed ? "paper_filled" : null,
                        Status = plan.ActionStatus,
                    },
                    tx);
            }

            await conn.ExecuteAsync(
                IntelligenceSql.InsertEvent,
                new
                {
                    EventId = Guid.NewGuid(),
                    TenantId = _tenant.TenantId,
                    PersonId = personId,
                    EventType = "ActionTaken",
                    OccurredAt = DateTimeOffset.UtcNow,
                    ActorType = "AGENT",
                    CorrelationId = correlationId,
                    Source = "paper-execution",
                    EpistemicKind = EpistemicKindSql.ToDb(EpistemicKind.Observation),
                    ExternalEventId = correlationId.ToString("N") + ":action",
                },
                tx);

            if (plan.WriteExecution)
            {
                var orderId = Guid.NewGuid();
                await conn.ExecuteAsync(
                    PaperExecutionSql.InsertOrder,
                    new
                    {
                        OrderId = orderId,
                        TenantId = _tenant.TenantId,
                        PersonId = personId,
                        TradingAccountId = tradingAccountId,
                        ExternalOrderId = correlationId.ToString("N"),
                        proposal.Symbol,
                        proposal.Side,
                        proposal.Quantity,
                        proposal.Price,
                        Status = plan.OrderStatus,
                        DecisionId = approval!.DecisionId,
                    },
                    tx);
                await conn.ExecuteAsync(
                    PaperExecutionSql.InsertExecution,
                    new
                    {
                        ExecutionId = Guid.NewGuid(),
                        TenantId = _tenant.TenantId,
                        TradingAccountId = tradingAccountId,
                        OrderId = orderId,
                        ExternalExecutionId = correlationId.ToString("N") + ":fill",
                        Mode = plan.ExecutionMode,
                    },
                    tx);
            }

            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
