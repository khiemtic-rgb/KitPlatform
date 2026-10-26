namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed record PaperChainFill(
    string OperationKey,
    string LinkStatus,
    string? SignalKey,
    string? StrategyId,
    string? StrategyVersion,
    string? SignalStatus,
    bool? RiskAllowed,
    string PositionEffect);

public sealed record PaperChainResult(
    IReadOnlyList<PaperChainFill> Fills,
    decimal? RealizedPnl,
    string PnlStatus,
    string DrawdownStatus,
    bool FullyClosed);

/// <summary>
/// Links a paper fill to the signal and risk decision that produced it.
/// The link is the signal key. A missing key stays unlinked. An awaiting signal is not a fill.
/// </summary>
public static class PaperIntegrity
{
    public const string Unidentified = "unidentified";
    public const string Unlinked = "unlinked";
    public const string Linked = "linked";
    public const string Awaiting = "awaiting_not_a_fill";

    public static string StrategyLabel(string? strategyId, string? strategyVersion)
        => string.IsNullOrWhiteSpace(strategyId) || string.IsNullOrWhiteSpace(strategyVersion)
            ? Unidentified
            : strategyId;

    public static StoredSignal Promote(StoredSignal existing, StoredSignal incoming)
    {
        if (!string.Equals(existing.SignalKey, incoming.SignalKey, StringComparison.Ordinal))
            return existing;
        if (existing.Status != ResearchStrategies.AwaitingNextOpen || incoming.Status != ResearchStrategies.Ready)
            return existing;

        return incoming with
        {
            StrategyId = existing.StrategyId,
            StrategyVersion = existing.StrategyVersion,
            Timeframe = existing.Timeframe,
            CandleOpenTime = existing.CandleOpenTime,
        };
    }

    public static PaperChainResult Evaluate(
        IReadOnlyList<StoredSignal> signals,
        IReadOnlyList<ResearchRiskDecision> decisions,
        IReadOnlyList<StoredFill> fills)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(fills);
        var bySignal = signals.ToDictionary(signal => signal.SignalKey, StringComparer.Ordinal);
        var byDecision = decisions.ToDictionary(decision => decision.OperationKey, StringComparer.Ordinal);
        var rows = new List<PaperChainFill>();
        var quantity = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var cost = new Dictionary<string, decimal>(StringComparer.Ordinal);
        decimal realized = 0m;
        var reduced = false;
        var incomplete = false;

        foreach (var fill in fills)
        {
            bySignal.TryGetValue(fill.OperationKey, out var signal);
            byDecision.TryGetValue(fill.OperationKey, out var decision);
            if (!LinkedTo(fill, signal, decision))
            {
                rows.Add(Row(fill, Unlinked, signal, decision, "rejected"));
                continue;
            }

            if (signal!.Status == ResearchStrategies.AwaitingNextOpen)
            {
                rows.Add(Row(fill, Awaiting, signal, decision, "not_a_fill"));
                continue;
            }

            var symbol = signal.Symbol;
            quantity.TryGetValue(symbol, out var openQuantity);
            cost.TryGetValue(symbol, out var openCost);
            if (fill.Side == "BUY")
            {
                var opened = openQuantity == 0m;
                openQuantity += fill.Quantity;
                openCost += -fill.NetCash;
                quantity[symbol] = openQuantity;
                cost[symbol] = openCost;
                rows.Add(Row(fill, Linked, signal, decision, opened ? "opened" : "increased"));
                continue;
            }

            if (fill.Side != "SELL" || openQuantity <= 0m || fill.Quantity > openQuantity)
            {
                incomplete = true;
                rows.Add(Row(fill, Linked, signal, decision, "unmatched_exit"));
                continue;
            }

            var average = openCost / openQuantity;
            var proceeds = (fill.ExecutionPrice * fill.Quantity) - fill.Fee;
            realized += proceeds - (average * fill.Quantity);
            openCost -= average * fill.Quantity;
            openQuantity -= fill.Quantity;
            reduced = true;
            quantity[symbol] = openQuantity;
            cost[symbol] = openCost;
            rows.Add(Row(fill, Linked, signal, decision, openQuantity == 0m ? "closed" : "partial"));
        }

        var flat = quantity.Values.All(value => value == 0m);
        var fullyClosed = reduced && flat && !incomplete;
        var pnlStatus = incomplete
            ? PaperSession.PnlUnavailable
            : fullyClosed
                ? PaperSession.Realized
                : reduced
                    ? PaperSession.PartialClose
                    : PaperSession.NoClosedPosition;
        return new PaperChainResult(
            rows,
            fullyClosed ? realized : null,
            pnlStatus,
            fullyClosed ? ResearchSession.CashDrawdown : ResearchSession.DrawdownUnavailable,
            fullyClosed);
    }

    private static bool LinkedTo(StoredFill fill, StoredSignal? signal, ResearchRiskDecision? decision)
        => signal is not null
            && decision is not null
            && !string.IsNullOrWhiteSpace(fill.SignalKey)
            && !string.IsNullOrWhiteSpace(decision.SignalKey)
            && string.Equals(fill.SignalKey, signal.SignalKey, StringComparison.Ordinal)
            && string.Equals(fill.OperationKey, signal.SignalKey, StringComparison.Ordinal)
            && string.Equals(decision.SignalKey, signal.SignalKey, StringComparison.Ordinal)
            && string.Equals(decision.OperationKey, signal.SignalKey, StringComparison.Ordinal);

    private static PaperChainFill Row(StoredFill fill, string linkStatus, StoredSignal? signal, ResearchRiskDecision? decision, string effect)
        => new(
            fill.OperationKey,
            linkStatus,
            signal?.SignalKey,
            signal is null ? null : StrategyLabel(signal.StrategyId, signal.StrategyVersion),
            signal?.StrategyVersion,
            signal?.Status,
            decision?.Allowed,
            effect);
}
