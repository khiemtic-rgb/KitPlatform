namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record PaperPersistencePlan(bool WriteExecution, string OrderStatus, string? ExecutionMode, string ActionStatus);

public static class PaperPersistencePlanBuilder
{
    public static PaperPersistencePlan From(PaperExecutionResult result)
    {
        if (result.Executed)
        {
            if (result.Fill is null || result.Fill.Mode != "PAPER")
                throw new InvalidOperationException("A stored fill must be a paper fill.");
            return new PaperPersistencePlan(true, "paper", "PAPER", "paper_filled");
        }

        return new PaperPersistencePlan(false, "rejected", null, "refused");
    }
}

public interface IPaperExecutionStore
{
    Task<PaperExecutionResult> RecordAsync(
        Guid personId,
        Guid tradingAccountId,
        TradeProposal proposal,
        RiskLimits limits,
        ExecutionApproval? approval,
        CancellationToken cancellationToken = default);
}
